using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Output;
using ICSharpCode.Decompiler.TypeSystem;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Reflection.Metadata;
using AssemblyPath = ILSpy.Mcp.Domain.Models.AssemblyPath;
using TypeName = ILSpy.Mcp.Domain.Models.TypeName;

namespace ILSpy.Mcp.Infrastructure.Decompiler;

/// <summary>
/// Adapter that wraps ICSharpCode.Decompiler's ReflectionDisassembler to implement IDisassemblyService.
/// </summary>
public sealed class ILSpyDisassemblyService : IDisassemblyService
{
    private readonly ILogger<ILSpyDisassemblyService> _logger;
    private readonly DecompilerSettings _settings;

    public ILSpyDisassemblyService(ILogger<ILSpyDisassemblyService> logger)
    {
        _logger = logger;
        _settings = new DecompilerSettings
        {
            ThrowOnAssemblyResolveErrors = false,
            ShowXmlDocumentation = false
        };
    }

    public async Task<string> DisassembleTypeAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        bool showTokens = false,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var type = decompiler.TypeSystem.MainModule.GetTypeDefinition(
                    new FullTypeName(typeName.FullName));

                if (type == null)
                    throw new TypeNotFoundException(typeName.FullName, assemblyPath.Value);

                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;

                var writer = new StringWriter();
                var output = new PlainTextOutput(writer);
                var disassembler = new ReflectionDisassembler(output, cancellationToken)
                {
                    ShowMetadataTokens = showTokens,
                    DetectControlStructure = true
                };

                // D-01: Summary header with type metadata for orientation
                output.WriteLine($"// Type: {type.FullName}");
                output.WriteLine($"// Assembly: {assemblyPath.FileName}");
                output.WriteLine($"// Methods: {type.Methods.Count()}");
                output.WriteLine();

                // D-02: Headers-only iteration — do NOT call DisassembleType which includes bodies
                foreach (var field in type.Fields)
                {
                    disassembler.DisassembleFieldHeader(metadataFile, (FieldDefinitionHandle)field.MetadataToken);
                    output.WriteLine();
                }

                foreach (var method in type.Methods)
                {
                    disassembler.DisassembleMethodHeader(metadataFile, (MethodDefinitionHandle)method.MetadataToken);
                    output.WriteLine();
                }

                foreach (var prop in type.Properties)
                {
                    disassembler.DisassembleProperty(metadataFile, (PropertyDefinitionHandle)prop.MetadataToken);
                    output.WriteLine();
                }

                foreach (var evt in type.Events)
                {
                    disassembler.DisassembleEvent(metadataFile, (EventDefinitionHandle)evt.MetadataToken);
                    output.WriteLine();
                }

                return writer.ToString();
            }
            catch (TypeNotFoundException)
            {
                throw;
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Assembly file not found: {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disassemble type {TypeName} from {Assembly}",
                    typeName.FullName, assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<string> DisassembleMethodAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        string methodName,
        bool showBytes = false,
        bool showTokens = false,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var type = decompiler.TypeSystem.MainModule.GetTypeDefinition(
                    new FullTypeName(typeName.FullName));

                if (type == null)
                    throw new TypeNotFoundException(typeName.FullName, assemblyPath.Value);

                var methods = type.Methods.Where(m => m.Name == methodName).ToList();

                if (methods.Count == 0)
                    throw new MethodNotFoundException(methodName, typeName.FullName);

                if (methods.Count > 1)
                {
                    var overloads = string.Join(", ",
                        methods.Select(m =>
                        {
                            var parameters = string.Join(", ",
                                m.Parameters.Select(p => $"{p.Type.Name} {p.Name}"));
                            return $"{methodName}({parameters})";
                        }));
                    throw new MethodNotFoundException(methodName, typeName.FullName);
                }

                var method = methods[0];
                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;

                var writer = new StringWriter();
                var output = new PlainTextOutput(writer);
                var disassembler = new ReflectionDisassembler(output, cancellationToken)
                {
                    ShowMetadataTokens = showTokens,
                    ShowRawRVAOffsetAndBytes = showBytes,
                    DetectControlStructure = true
                };

                // D-03: Full IL body with .maxstack, IL_xxxx labels, resolved names
                disassembler.DisassembleMethod(metadataFile, (MethodDefinitionHandle)method.MetadataToken);

                return writer.ToString();
            }
            catch (TypeNotFoundException)
            {
                throw;
            }
            catch (MethodNotFoundException)
            {
                throw;
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Assembly file not found: {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disassemble method {MethodName} from {TypeName} in {Assembly}",
                    methodName, typeName.FullName, assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }
}
