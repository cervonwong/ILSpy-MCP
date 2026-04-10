using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for retrieving assembly-level custom attributes.
/// </summary>
[McpServerToolType]
public sealed class GetAssemblyAttributesTool
{
    private readonly GetAssemblyAttributesUseCase _useCase;
    private readonly ILogger<GetAssemblyAttributesTool> _logger;

    public GetAssemblyAttributesTool(
        GetAssemblyAttributesUseCase useCase,
        ILogger<GetAssemblyAttributesTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "get_assembly_attributes")]
    [Description("Lists all custom attributes declared at assembly level with their constructor arguments and named properties. Use this when investigating assembly-wide configuration like InternalsVisibleTo, CLSCompliant, or custom build metadata baked into a compiled binary.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly (.dll/.exe)")] string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, cancellationToken);
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in get_assembly_attributes tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in get_assembly_attributes tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in get_assembly_attributes tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while retrieving assembly attributes.");
        }
    }
}
