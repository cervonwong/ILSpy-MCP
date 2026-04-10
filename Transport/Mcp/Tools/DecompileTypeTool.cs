using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for decompiling types.
/// </summary>
[McpServerToolType]
public sealed class DecompileTypeTool
{
    private readonly DecompileTypeUseCase _useCase;
    private readonly ILogger<DecompileTypeTool> _logger;

    public DecompileTypeTool(
        DecompileTypeUseCase useCase,
        ILogger<DecompileTypeTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "decompile_type")]
    [Description("Decompiles a type to reconstructed C# source showing full implementation details. Use this when you need to read method bodies, understand control flow, or see how a class actually works inside a compiled binary. For a quick structural overview without source (cheaper), use get_type_members instead; for IL-level representation, use disassemble_type. Returns C# source with truncation metadata.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly (.dll/.exe)")] string assemblyPath,
        [Description("Full type name (e.g., 'System.String', 'MyApp.Services.OrderProcessor')")] string typeName,
        [Description("Focus area (e.g., 'error handling', 'state mutations', 'dependency usage')")] string? query = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, typeName, query, cancellationToken);
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
            _logger.LogWarning("Timeout in decompile_type tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in decompile_type tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in decompile_type tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while decompiling the type.");
        }
    }
}
