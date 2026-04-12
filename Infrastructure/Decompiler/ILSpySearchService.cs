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

    // Window size: N=3 before/after (OUTPUT-06)
    private const int SurroundingILWindowSize = 3;

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
        // Phase 1: walk the method body once and render every instruction. Capture
        // (offset, rendered, isLdstrHit, matchedValue) so Phase 2 can slice a window
        // around each ldstr regex hit without re-reading the body.
        var instructions = new List<(int offset, string rendered, bool isLdstrHit, string? ldstrValue)>();

        while (ilReader.RemainingBytes > 0)
        {
            int offset = ilReader.Offset;
            var opCode = ILParsingHelper.ReadILOpCode(ref ilReader);
            string rendered = RenderInstruction(opCode, ref ilReader, reader, offset,
                out bool isLdstrHit, out string? ldstrValue);
            instructions.Add((offset, rendered, isLdstrHit, ldstrValue));
        }

        // Phase 2: locate each ldstr hit that matches the regex and build a window.
        for (int i = 0; i < instructions.Count; i++)
        {
            var instr = instructions[i];
            if (!instr.isLdstrHit)
                continue;

            string stringValue = instr.ldstrValue ?? string.Empty;
            if (!regex.IsMatch(stringValue))
                continue;

            totalCount++;
            if (results.Count >= matchCap)
                continue;

            int start = Math.Max(0, i - SurroundingILWindowSize);
            int end = Math.Min(instructions.Count - 1, i + SurroundingILWindowSize);
            var window = new List<string>(end - start + 1);
            for (int j = start; j <= end; j++)
            {
                window.Add(instructions[j].rendered);
            }

            results.Add(new StringSearchResult
            {
                MatchedValue = stringValue,
                DeclaringType = scanType.FullName,
                MethodName = method.Name,
                MethodSignature = FormatMethodSignature(method),
                ILOffset = instr.offset,
                SurroundingIL = window
            });
        }
    }

    /// <summary>
    /// Reads the operand for <paramref name="opCode"/> (advancing the reader) and
    /// returns a rendered "IL_XXXX: opcode [operand]" line. When the opcode is ldstr
    /// and the operand resolves to a user string, <paramref name="isLdstrHit"/> is set
    /// true and <paramref name="ldstrValue"/> carries the literal for regex matching.
    /// </summary>
    private static string RenderInstruction(
        ILOpCode opCode,
        ref BlobReader reader,
        MetadataReader metadataReader,
        int instructionOffset,
        out bool isLdstrHit,
        out string? ldstrValue)
    {
        isLdstrHit = false;
        ldstrValue = null;

        string opName = opCode.ToString().ToLowerInvariant().Replace('_', '.');
        string prefix = $"IL_{instructionOffset:X4}: ";

        switch (opCode)
        {
            case ILOpCode.Ldstr:
            {
                int token = reader.ReadInt32();
                var handle = MetadataTokens.UserStringHandle(token & 0x00FFFFFF);
                string value;
                try
                {
                    value = metadataReader.GetUserString(handle);
                }
                catch
                {
                    value = string.Empty;
                }
                isLdstrHit = true;
                ldstrValue = value;
                string display = value.Length > 64 ? value.Substring(0, 64) + "..." : value;
                return $"{prefix}ldstr \"{display}\"";
            }
            case ILOpCode.Ldc_i4:
                return $"{prefix}ldc.i4 {reader.ReadInt32()}";
            case ILOpCode.Ldc_i4_s:
                return $"{prefix}ldc.i4.s {reader.ReadSByte()}";
            case ILOpCode.Ldc_i8:
                return $"{prefix}ldc.i8 {reader.ReadInt64()}";
            case ILOpCode.Ldc_r4:
                return $"{prefix}ldc.r4 {reader.ReadSingle()}";
            case ILOpCode.Ldc_r8:
                return $"{prefix}ldc.r8 {reader.ReadDouble()}";
            // Short-form branches (1-byte signed offset)
            case ILOpCode.Br_s:
            case ILOpCode.Brfalse_s:
            case ILOpCode.Brtrue_s:
            case ILOpCode.Beq_s:
            case ILOpCode.Bge_s:
            case ILOpCode.Bgt_s:
            case ILOpCode.Ble_s:
            case ILOpCode.Blt_s:
            case ILOpCode.Bne_un_s:
            case ILOpCode.Bge_un_s:
            case ILOpCode.Bgt_un_s:
            case ILOpCode.Ble_un_s:
            case ILOpCode.Blt_un_s:
            case ILOpCode.Leave_s:
            {
                sbyte rel = reader.ReadSByte();
                int target = reader.Offset + rel;
                return $"{prefix}{opName} IL_{target:X4}";
            }
            // Long-form branches (4-byte signed offset)
            case ILOpCode.Br:
            case ILOpCode.Brfalse:
            case ILOpCode.Brtrue:
            case ILOpCode.Beq:
            case ILOpCode.Bge:
            case ILOpCode.Bgt:
            case ILOpCode.Ble:
            case ILOpCode.Blt:
            case ILOpCode.Bne_un:
            case ILOpCode.Bge_un:
            case ILOpCode.Bgt_un:
            case ILOpCode.Ble_un:
            case ILOpCode.Blt_un:
            case ILOpCode.Leave:
            {
                int rel = reader.ReadInt32();
                int target = reader.Offset + rel;
                return $"{prefix}{opName} IL_{target:X4}";
            }
            default:
                if (ILParsingHelper.IsTokenReferenceOpCode(opCode))
                {
                    int token = reader.ReadInt32();
                    return $"{prefix}{opName} token:0x{token:X8}";
                }
                // Render with any numeric operand hint based on size
                switch (ILParsingHelper.GetOperandSize(opCode))
                {
                    case 0:
                        return $"{prefix}{opName}";
                    case 1:
                        return $"{prefix}{opName} {reader.ReadByte()}";
                    case 2:
                        return $"{prefix}{opName} {reader.ReadInt16()}";
                    case 4:
                        return $"{prefix}{opName} {reader.ReadInt32()}";
                    case 8:
                        return $"{prefix}{opName} {reader.ReadInt64()}";
                    case -1: // switch
                    {
                        int count = reader.ReadInt32();
                        for (int i = 0; i < count; i++)
                            reader.ReadInt32();
                        return $"{prefix}{opName} (switch[{count}])";
                    }
                    default:
                        return $"{prefix}{opName}";
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
