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
    [Description("Search for numeric integer constants in assembly IL bytecode. Finds all ldc.i4 and ldc.i8 instructions loading the specified value. Returns matches with containing method context.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Exact numeric value to search for (integer)")] long value,
        [Description("Maximum number of results to return (default: 100)")] int maxResults = 100,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0,
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
