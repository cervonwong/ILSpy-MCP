using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for resolving which assembly in a directory defines a given type.
/// </summary>
[McpServerToolType]
public sealed class ResolveTypeTool
{
    private readonly ResolveTypeUseCase _useCase;
    private readonly ILogger<ResolveTypeTool> _logger;

    public ResolveTypeTool(
        ResolveTypeUseCase useCase,
        ILogger<ResolveTypeTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "resolve_type")]
    [Description("Resolve which assembly in a directory defines a given type. Supports partial type name matching -- 'HttpClient' matches 'System.Net.Http.HttpClient'. Returns all matching assemblies when multiple define the type.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the directory containing .NET assemblies")] string directoryPath,
        [Description("Type name to search for (partial match supported, e.g., 'HttpClient' or 'System.Net.Http.HttpClient')")] string typeName,
        [Description("Maximum directory recursion depth (default: 3)")] int maxDepth = 3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(directoryPath, typeName, maxDepth, cancellationToken);
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogWarning("Directory not found: {Directory}", directoryPath);
            throw new McpToolException("DIRECTORY_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid argument: {Message}", ex.Message);
            throw new McpToolException("INVALID_ARGUMENT", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in resolve_type tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT",
                "The operation timed out. The directory may contain too many assemblies.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in resolve_type tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in resolve_type tool");
            throw new McpToolException("INTERNAL_ERROR",
                "An unexpected error occurred while resolving type.");
        }
    }
}
