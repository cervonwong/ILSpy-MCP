using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for finding compiler-generated types.
/// </summary>
[McpServerToolType]
public sealed class FindCompilerGeneratedTypesTool
{
    private readonly FindCompilerGeneratedTypesUseCase _useCase;
    private readonly ILogger<FindCompilerGeneratedTypesTool> _logger;

    public FindCompilerGeneratedTypesTool(
        FindCompilerGeneratedTypesUseCase useCase,
        ILogger<FindCompilerGeneratedTypesTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "find_compiler_generated_types")]
    [Description("Finds compiler-generated types (async state machines, display classes, closures, iterators) and maps them back to their originating user code. Use this when an assembly contains mysterious '<>c__DisplayClass' or '<Process>d__1' types and you need to understand which lambdas, async methods, or iterators produced them. Returns paginated matches with parent method and type context.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly (.dll/.exe)")] string assemblyPath,
        [Description("Maximum results to return (default 100)")] int maxResults = 100,
        [Description("Results to skip for pagination (default 0)")] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Phase 9 pagination contract: hard ceiling + positive minimum.
            if (maxResults > 500)
            {
                throw new McpToolException("INVALID_PARAMETER",
                    "maxResults cannot exceed 500. Use offset to paginate.");
            }
            if (maxResults <= 0)
            {
                throw new McpToolException("INVALID_PARAMETER",
                    "maxResults must be >= 1.");
            }

            return await _useCase.ExecuteAsync(assemblyPath, maxResults, offset, cancellationToken);
        }
        catch (McpToolException)
        {
            throw;  // Rethrow our own INVALID_PARAMETER without mapping it again
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in find_compiler_generated_types tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in find_compiler_generated_types tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in find_compiler_generated_types tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while finding compiler-generated types.");
        }
    }
}
