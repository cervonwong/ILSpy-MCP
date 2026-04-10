using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for finding all types implementing an interface or extending a base class.
/// </summary>
[McpServerToolType]
public sealed class FindImplementorsTool
{
    private readonly FindImplementorsUseCase _useCase;
    private readonly ILogger<FindImplementorsTool> _logger;

    public FindImplementorsTool(
        FindImplementorsUseCase useCase,
        ILogger<FindImplementorsTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "find_implementors")]
    [Description("Finds all types implementing an interface or extending a base class within an assembly. Use this when mapping the concrete implementations behind an abstraction, identifying plugin points, or understanding a polymorphic dispatch target. Returns paginated matches with type name, assembly, and direct-vs-transitive relationship.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly (.dll/.exe)")] string assemblyPath,
        [Description("Full name of the interface or base class (e.g., 'System.IDisposable')")] string typeName,
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

            return await _useCase.ExecuteAsync(assemblyPath, typeName, maxResults, offset, cancellationToken);
        }
        catch (McpToolException)
        {
            throw;  // Rethrow our own INVALID_PARAMETER without mapping it again
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
            _logger.LogWarning("Timeout in find_implementors tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in find_implementors tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in find_implementors tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while finding implementors.");
        }
    }
}
