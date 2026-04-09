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
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, targetTypeName, cancellationToken);
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
