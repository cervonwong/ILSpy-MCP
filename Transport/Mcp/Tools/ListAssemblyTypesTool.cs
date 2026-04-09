using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

[McpServerToolType]
public sealed class ListAssemblyTypesTool
{
    private readonly ListAssemblyTypesUseCase _useCase;
    private readonly ILogger<ListAssemblyTypesTool> _logger;

    public ListAssemblyTypesTool(
        ListAssemblyTypesUseCase useCase,
        ILogger<ListAssemblyTypesTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "list_assembly_types")]
    [Description("Use this when you added a NuGet package but don't know what classes/types it provides. Lists all available types by namespace to help you discover what's available. Essential first step before using decompile_type to examine specific classes.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Optional: Filter types by namespace (case-insensitive)")] string? namespaceFilter = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, namespaceFilter, cancellationToken);
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in list_assembly_types tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in list_assembly_types tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in list_assembly_types tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while listing types.");
        }
    }
}
