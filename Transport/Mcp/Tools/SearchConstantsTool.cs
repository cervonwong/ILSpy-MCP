using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for searching numeric integer constants in assembly IL bytecode.
/// </summary>
[McpServerToolType]
public sealed class SearchConstantsTool
{
    private readonly SearchConstantsUseCase _useCase;
    private readonly ILogger<SearchConstantsTool> _logger;

    public SearchConstantsTool(
        SearchConstantsUseCase useCase,
        ILogger<SearchConstantsTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "search_constants")]
    [Description("Searches for numeric integer constants in assembly IL bytecode. Use this to find magic numbers, status codes, buffer sizes, or cryptographic constants embedded in compiled IL when source is unavailable. Returns paginated matches with constant value, containing method FQN, and IL offset.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly (.dll/.exe)")] string assemblyPath,
        [Description("Exact integer value to search for")] long value,
        [Description("Maximum results to return (default 100)")] int maxResults = 100,
        [Description("Results to skip for pagination (default 0)")] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, value, maxResults, offset, cancellationToken);
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in search_constants tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in search_constants tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in search_constants tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while searching constants.");
        }
    }
}
