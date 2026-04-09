using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for finding all outward dependencies of a type or method.
/// </summary>
[McpServerToolType]
public sealed class FindDependenciesTool
{
    private readonly FindDependenciesUseCase _useCase;
    private readonly ILogger<FindDependenciesTool> _logger;

    public FindDependenciesTool(
        FindDependenciesUseCase useCase,
        ILogger<FindDependenciesTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "find_dependencies")]
    [Description("Find all outward dependencies (method calls, field accesses, type references) of a type or specific method. Shows what external members the code depends on.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Full name of the type to analyze (e.g., 'MyNamespace.MyClass')")] string typeName,
        [Description("Optional method name to narrow analysis to a specific method")] string? methodName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, typeName, methodName, cancellationToken);
        }
        catch (TypeNotFoundException ex)
        {
            _logger.LogWarning("Type not found: {TypeName} in {Assembly}", ex.TypeName, ex.AssemblyPath);
            throw new McpToolException("TYPE_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (MethodNotFoundException ex)
        {
            _logger.LogWarning("Method not found: {MethodName} in {TypeName}", ex.MethodName, ex.TypeName);
            throw new McpToolException("METHOD_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in find_dependencies tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in find_dependencies tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in find_dependencies tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while finding dependencies.");
        }
    }
}
