using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for extracting embedded resource content.
/// </summary>
[McpServerToolType]
public sealed class ExtractResourceTool
{
    private readonly ExtractResourceUseCase _useCase;
    private readonly ILogger<ExtractResourceTool> _logger;

    public ExtractResourceTool(
        ExtractResourceUseCase useCase,
        ILogger<ExtractResourceTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "extract_resource")]
    [Description("Extracts the content of a named embedded resource. Use this when you need to read configuration, localization tables, or other data embedded in a compiled assembly. Text resources return inline; binary resources return as base64. Supports offset/limit for paginated binary extraction.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly (.dll/.exe)")] string assemblyPath,
        [Description("Embedded resource name (e.g., 'MyApp.Resources.config.json')")] string resourceName,
        [Description("Byte offset for paginated binary extraction")] int? offset = null,
        [Description("Maximum bytes to return for paginated binary extraction")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, resourceName, offset, limit, cancellationToken);
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in extract_resource tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in extract_resource tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in extract_resource tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while extracting the resource.");
        }
    }
}
