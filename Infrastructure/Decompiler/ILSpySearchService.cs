using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
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

                return new SearchResults<StringSearchResult>
                {
                    TotalCount = totalCount,
                    Offset = offset,
                    Limit = maxResults,
                    Results = allMatches.Skip(offset).Take(maxResults).ToList()
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
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.Name} {p.Name}"));
        return $"{method.ReturnType.Name} {method.Name}({parameters})";
    }
}
