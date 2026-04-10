using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for listing all types in a namespace with summary information.
/// </summary>
[McpServerToolType]
public sealed class DecompileNamespaceTool
{
    private readonly DecompileNamespaceUseCase _useCase;
    private readonly ILogger<DecompileNamespaceTool> _logger;

    public DecompileNamespaceTool(
        DecompileNamespaceUseCase useCase,
        ILogger<DecompileNamespaceTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "decompile_namespace")]
    [Description("Lists all types in a namespace with full signatures, member counts, and public method signatures. Returns a summary -- use decompile_type to get full source for individual types.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Full namespace name (e.g., 'System.Collections.Generic')")] string namespaceName,
        [Description("Maximum number of types to return (default 200)")] int maxTypes = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, namespaceName, maxTypes, cancellationToken);
        }
        catch (NamespaceNotFoundException ex)
        {
            _logger.LogWarning("Namespace not found: {Namespace} in {Assembly}", ex.NamespaceName, ex.AssemblyPath);
            throw new McpToolException("NAMESPACE_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in decompile_namespace tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in decompile_namespace tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in decompile_namespace tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while listing namespace types.");
        }
    }
}
