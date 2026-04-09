using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for listing embedded resources in an assembly.
/// </summary>
[McpServerToolType]
public sealed class ListEmbeddedResourcesTool
{
    private readonly ListEmbeddedResourcesUseCase _useCase;
    private readonly ILogger<ListEmbeddedResourcesTool> _logger;

    public ListEmbeddedResourcesTool(
        ListEmbeddedResourcesUseCase useCase,
        ILogger<ListEmbeddedResourcesTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "list_embedded_resources")]
    [Description("List all embedded resources in an assembly with name, type, size, and visibility")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
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
            _logger.LogWarning("Timeout in list_embedded_resources tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in list_embedded_resources tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in list_embedded_resources tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while listing embedded resources.");
        }
    }
}
