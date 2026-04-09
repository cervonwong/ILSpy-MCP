using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for IL disassembly of methods.
/// </summary>
[McpServerToolType]
public sealed class DisassembleMethodTool
{
    private readonly DisassembleMethodUseCase _useCase;
    private readonly ILogger<DisassembleMethodTool> _logger;

    public DisassembleMethodTool(
        DisassembleMethodUseCase useCase,
        ILogger<DisassembleMethodTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "disassemble_method")]
    [Description("Get raw CIL/IL disassembly of a specific .NET method showing the complete IL instruction listing with .maxstack, labels, and opcodes. Use show_bytes for raw opcode bytes.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Full name of the type containing the method")] string typeName,
        [Description("Name of the method to disassemble")] string methodName,
        [Description("Show raw opcode byte sequences")] bool showBytes = false,
        [Description("Show metadata token numbers (e.g., /* 06000001 */)")] bool showTokens = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, typeName, methodName, showBytes, showTokens, cancellationToken);
        }
        catch (TypeNotFoundException ex)
        {
            _logger.LogWarning("Type not found: {TypeName} in {Assembly}", ex.TypeName, ex.AssemblyPath);
            throw new McpToolException("TYPE_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (MethodNotFoundException ex)
        {
            _logger.LogWarning("Method not found: {MethodName} in {TypeName}", ex.MethodName, ex.TypeName);
            throw new McpToolException("METHOD_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in disassemble_method tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in disassemble_method tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in disassemble_method tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while disassembling the method.");
        }
    }
}
