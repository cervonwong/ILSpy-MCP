using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

[McpServerToolType]
public sealed class FindExtensionMethodsTool
{
    private readonly FindExtensionMethodsUseCase _useCase;
    private readonly ILogger<FindExtensionMethodsTool> _logger;

    public FindExtensionMethodsTool(
        FindExtensionMethodsUseCase useCase,
        ILogger<FindExtensionMethodsTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "find_extension_methods")]
    [Description("Use this when you want to know what extension methods are available for a specific type. Extension methods add functionality to existing types and are often discovered through IntelliSense, but this tool helps you find them when exploring a library. Shows you additional methods you can call on instances of the type.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Full name of the type to find extensions for (e.g., 'System.String')")] string targetTypeName,
        [Description("Maximum number of results to return (default: 100)")] int maxResults = 100,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0,
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

            return await _useCase.ExecuteAsync(assemblyPath, targetTypeName, maxResults, offset, cancellationToken);
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
            _logger.LogWarning("Timeout in find_extension_methods tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in find_extension_methods tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in find_extension_methods tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while finding extension methods.");
        }
    }
}
