using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.Output;
using ICSharpCode.Decompiler.TypeSystem;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using AssemblyPath = ILSpy.Mcp.Domain.Models.AssemblyPath;

namespace ILSpy.Mcp.Infrastructure.Decompiler;

/// <summary>
/// Adapter that implements ISearchService using System.Reflection.Metadata IL scanning
/// for string literal and numeric constant search across assembly method bodies.
/// </summary>
public sealed class ILSpySearchService : ISearchService
{
    private readonly ILogger<ILSpySearchService> _logger;
    private readonly DecompilerSettings _settings;

    public ILSpySearchService(ILogger<ILSpySearchService> logger)
    {
        _logger = logger;
        _settings = new DecompilerSettings
        {
            ThrowOnAssemblyResolveErrors = false,
            ShowXmlDocumentation = false
        };
    }

    public async Task<SearchResults<StringSearchResult>> SearchStringsAsync(
        AssemblyPath assemblyPath,
        string regexPattern,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        // Validate regex before starting scan — throws ArgumentException on invalid pattern
        // Use a match timeout to prevent ReDoS (catastrophic backtracking)
        var regex = new Regex(regexPattern, RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
                var reader = metadataFile.Metadata;
                var allMatches = new List<StringSearchResult>();
                int totalCount = 0;
                int matchCap = offset + maxResults;

                foreach (var scanType in decompiler.TypeSystem.MainModule.TypeDefinitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (scanType.ParentModule != decompiler.TypeSystem.MainModule)
                        continue;

                    foreach (var method in scanType.Methods)
                    {
                        if (method.MetadataToken.IsNil)
                            continue;

                        var methodHandle = (MethodDefinitionHandle)method.MetadataToken;
                        var methodDef = reader.GetMethodDefinition(methodHandle);

                        if (methodDef.RelativeVirtualAddress == 0)
                            continue;

                        try
                        {
                            var body = metadataFile.GetMethodBody(methodDef.RelativeVirtualAddress);
                            if (body == null) continue;

                            var ilReader = body.GetILReader();
                            ScanILForStrings(ref ilReader, reader, regex, scanType, method, allMatches, ref totalCount, matchCap);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException and not RegexMatchTimeoutException)
                        {
                            _logger.LogDebug(ex, "Skipping method {Method} during string scan", method.FullName);
                        }
                    }
                }

                // Enrich matches with surrounding IL window per method (cached per method)
                var pagedResults = allMatches.Skip(offset).Take(maxResults).ToList();
                var enrichedResults = EnrichWithSurroundingIL(pagedResults, metadataFile, cancellationToken);

                return new SearchResults<StringSearchResult>
                {
                    TotalCount = totalCount,
                    Offset = offset,
                    Limit = maxResults,
                    Results = enrichedResults
                };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is not ArgumentException and not RegexMatchTimeoutException)
            {
                _logger.LogError(ex, "Failed to search strings in {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<SearchResults<ConstantSearchResult>> SearchConstantsAsync(
        AssemblyPath assemblyPath,
        long value,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
                var reader = metadataFile.Metadata;
                var allMatches = new List<ConstantSearchResult>();
                int totalCount = 0;
                int matchCap = offset + maxResults;

                foreach (var scanType in decompiler.TypeSystem.MainModule.TypeDefinitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (scanType.ParentModule != decompiler.TypeSystem.MainModule)
                        continue;

                    foreach (var method in scanType.Methods)
                    {
                        if (method.MetadataToken.IsNil)
                            continue;

                        var methodHandle = (MethodDefinitionHandle)method.MetadataToken;
                        var methodDef = reader.GetMethodDefinition(methodHandle);

                        if (methodDef.RelativeVirtualAddress == 0)
                            continue;

                        try
                        {
                            var body = metadataFile.GetMethodBody(methodDef.RelativeVirtualAddress);
                            if (body == null) continue;

                            var ilReader = body.GetILReader();
                            ScanILForConstants(ref ilReader, value, scanType, method, allMatches, ref totalCount, matchCap);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger.LogDebug(ex, "Skipping method {Method} during constant scan", method.FullName);
                        }
                    }
                }

                return new SearchResults<ConstantSearchResult>
                {
                    TotalCount = totalCount,
                    Offset = offset,
                    Limit = maxResults,
                    Results = allMatches.Skip(offset).Take(maxResults).ToList()
                };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search constants in {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    // ---- Private IL scanning methods ----

    private static void ScanILForStrings(
        ref BlobReader ilReader,
        MetadataReader reader,
        Regex regex,
        ITypeDefinition scanType,
        IMethod method,
        List<StringSearchResult> results,
        ref int totalCount,
        int matchCap)
    {
        while (ilReader.RemainingBytes > 0)
        {
            int offset = ilReader.Offset;
            var opCode = ILParsingHelper.ReadILOpCode(ref ilReader);

            if (opCode == ILOpCode.Ldstr)
            {
                int token = ilReader.ReadInt32();
                var handle = MetadataTokens.UserStringHandle(token & 0x00FFFFFF);
                var stringValue = reader.GetUserString(handle);

                if (regex.IsMatch(stringValue))
                {
                    totalCount++;
                    if (results.Count < matchCap)
                    {
                        results.Add(new StringSearchResult
                        {
                            MatchedValue = stringValue,
                            DeclaringType = scanType.FullName,
                            MethodName = method.Name,
                            MethodSignature = FormatMethodSignature(method),
                            ILOffset = offset
                        });
                    }
                }
            }
            else if (ILParsingHelper.IsTokenReferenceOpCode(opCode))
            {
                ilReader.ReadInt32(); // Skip token
            }
            else
            {
                ILParsingHelper.SkipOperand(ref ilReader, opCode);
            }
        }
    }

    private static void ScanILForConstants(
        ref BlobReader ilReader,
        long targetValue,
        ITypeDefinition scanType,
        IMethod method,
        List<ConstantSearchResult> results,
        ref int totalCount,
        int matchCap)
    {
        while (ilReader.RemainingBytes > 0)
        {
            int offset = ilReader.Offset;
            var opCode = ILParsingHelper.ReadILOpCode(ref ilReader);

            long? extractedValue = null;
            string constantType = "Int32";

            switch (opCode)
            {
                case ILOpCode.Ldc_i4_m1: extractedValue = -1; break;
                case ILOpCode.Ldc_i4_0: extractedValue = 0; break;
                case ILOpCode.Ldc_i4_1: extractedValue = 1; break;
                case ILOpCode.Ldc_i4_2: extractedValue = 2; break;
                case ILOpCode.Ldc_i4_3: extractedValue = 3; break;
                case ILOpCode.Ldc_i4_4: extractedValue = 4; break;
                case ILOpCode.Ldc_i4_5: extractedValue = 5; break;
                case ILOpCode.Ldc_i4_6: extractedValue = 6; break;
                case ILOpCode.Ldc_i4_7: extractedValue = 7; break;
                case ILOpCode.Ldc_i4_8: extractedValue = 8; break;
                case ILOpCode.Ldc_i4_s:
                    extractedValue = ilReader.ReadSByte();
                    break;
                case ILOpCode.Ldc_i4:
                    extractedValue = ilReader.ReadInt32();
                    break;
                case ILOpCode.Ldc_i8:
                    extractedValue = ilReader.ReadInt64();
                    constantType = "Int64";
                    break;
                default:
                    if (ILParsingHelper.IsTokenReferenceOpCode(opCode))
                        ilReader.ReadInt32();
                    else
                        ILParsingHelper.SkipOperand(ref ilReader, opCode);
                    continue;
            }

            if (extractedValue == targetValue)
            {
                totalCount++;
                if (results.Count < matchCap)
                {
                    results.Add(new ConstantSearchResult
                    {
                        MatchedValue = extractedValue.Value,
                        ConstantType = constantType,
                        DeclaringType = scanType.FullName,
                        MethodName = method.Name,
                        MethodSignature = FormatMethodSignature(method),
                        ILOffset = offset
                    });
                }
            }
        }
    }

    private static string FormatMethodSignature(IMethod method)
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => p.Type.FullName));
        return $"{method.DeclaringType.FullName}.{method.Name}({parameters})";
    }

    /// <summary>
    /// Enriches string search results with surrounding IL instructions using ReflectionDisassembler.
    /// Caches disassembly per method to avoid redundant work within the same scan.
    /// </summary>
    private static List<StringSearchResult> EnrichWithSurroundingIL(
        List<StringSearchResult> results,
        MetadataFile metadataFile,
        CancellationToken cancellationToken)
    {
        if (results.Count == 0) return results;

        // Group by method signature to cache disassembly per method
        var enriched = new List<StringSearchResult>(results.Count);
        var ilLinesCache = new Dictionary<string, List<(int offset, string line)>>();

        foreach (var result in results)
        {
            var cacheKey = $"{result.DeclaringType}.{result.MethodName}";
            if (!ilLinesCache.TryGetValue(cacheKey, out var ilLines))
            {
                ilLines = CaptureMethodILLines(metadataFile, result, cancellationToken);
                ilLinesCache[cacheKey] = ilLines;
            }

            if (ilLines.Count > 0)
            {
                // Find the IL line matching our offset
                int matchIdx = ilLines.FindIndex(l => l.offset == result.ILOffset);
                if (matchIdx >= 0)
                {
                    int windowStart = Math.Max(0, matchIdx - 3);
                    int windowEnd = Math.Min(ilLines.Count - 1, matchIdx + 3);
                    var window = ilLines.GetRange(windowStart, windowEnd - windowStart + 1);
                    int matchInWindow = matchIdx - windowStart;

                    enriched.Add(result with
                    {
                        SurroundingInstructions = window.Select(l => l.line).ToList(),
                        MatchInstructionIndex = matchInWindow
                    });
                    continue;
                }
            }

            enriched.Add(result);
        }

        return enriched;
    }

    /// <summary>
    /// Disassembles a method body and extracts IL instruction lines with their offsets.
    /// Uses ReflectionDisassembler for proper token resolution.
    /// </summary>
    private static List<(int offset, string line)> CaptureMethodILLines(
        MetadataFile metadataFile,
        StringSearchResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var reader = metadataFile.Metadata;
            // Find the method handle by scanning type definitions
            foreach (var typeHandle in reader.TypeDefinitions)
            {
                var typeDef = reader.GetTypeDefinition(typeHandle);
                var typeFullName = GetTypeFullName(reader, typeDef);
                if (typeFullName != result.DeclaringType) continue;

                foreach (var methodHandle in typeDef.GetMethods())
                {
                    var methodDef = reader.GetMethodDefinition(methodHandle);
                    var methodName = reader.GetString(methodDef.Name);
                    if (methodName != result.MethodName) continue;
                    if (methodDef.RelativeVirtualAddress == 0) continue;

                    using var writer = new StringWriter();
                    var output = new PlainTextOutput(writer);
                    var disassembler = new ReflectionDisassembler(output, cancellationToken)
                    {
                        DetectControlStructure = false,
                        ShowMetadataTokens = false
                    };
                    disassembler.DisassembleMethod(metadataFile, methodHandle);

                    var lines = writer.ToString().Split('\n');
                    var ilLinesList = new List<(int, string)>();
                    foreach (var line in lines)
                    {
                        var trimmed = line.TrimStart();
                        if (trimmed.StartsWith("IL_") && trimmed.Length >= 7)
                        {
                            if (int.TryParse(trimmed.Substring(3, 4),
                                System.Globalization.NumberStyles.HexNumber, null, out int ilOffset))
                            {
                                ilLinesList.Add((ilOffset, trimmed.TrimEnd()));
                            }
                        }
                    }
                    return ilLinesList;
                }
            }
        }
        catch
        {
            // If disassembly fails for any reason, return empty — the match still shows without IL window
        }

        return new List<(int, string)>();
    }

    private static string GetTypeFullName(MetadataReader reader, TypeDefinition typeDef)
    {
        var name = reader.GetString(typeDef.Name);
        var ns = reader.GetString(typeDef.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }
}
