using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for IL disassembly of types.
/// </summary>
[McpServerToolType]
public sealed class DisassembleTypeTool
{
    private readonly DisassembleTypeUseCase _useCase;
    private readonly ILogger<DisassembleTypeTool> _logger;

    public DisassembleTypeTool(
        DisassembleTypeUseCase useCase,
        ILogger<DisassembleTypeTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "disassemble_type")]
    [Description("Get raw CIL/IL disassembly of a .NET type showing method signatures, fields, properties, and events in IL format. Use disassemble_method to drill into specific method IL bodies.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Full name of the type (e.g., 'System.String')")] string typeName,
        [Description("Show metadata token numbers (e.g., /* 06000001 */)")] bool showTokens = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, typeName, showTokens, cancellationToken);
        }
        catch (TypeNotFoundException ex)
        {
            _logger.LogWarning("Type not found: {TypeName} in {Assembly}", ex.TypeName, ex.AssemblyPath);
            throw new McpToolException("TYPE_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in disassemble_type tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in disassemble_type tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in disassemble_type tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while disassembling the type.");
        }
    }
}
