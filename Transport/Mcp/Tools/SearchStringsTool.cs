using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for searching string literals in assembly IL bytecode.
/// </summary>
[McpServerToolType]
public sealed class SearchStringsTool
{
    private readonly SearchStringsUseCase _useCase;
    private readonly ILogger<SearchStringsTool> _logger;

    public SearchStringsTool(
        SearchStringsUseCase useCase,
        ILogger<SearchStringsTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "search_strings")]
    [Description("Search for string literals in assembly IL bytecode matching a regex pattern. Scans all ldstr instructions and returns matches with containing method context.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Regex pattern to match against string literals (e.g., 'https?://', 'password', 'Error.*')")] string pattern,
        [Description("Maximum number of results to return (default: 100)")] int maxResults = 100,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, pattern, maxResults, offset, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid regex pattern: {Pattern}", pattern);
            throw new McpToolException("INVALID_PATTERN", $"Invalid regex pattern: {ex.Message}");
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in search_strings tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in search_strings tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in search_strings tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while searching strings.");
        }
    }
}
