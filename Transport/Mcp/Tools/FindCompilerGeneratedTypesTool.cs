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
    [Description("Find compiler-generated types (async state machines, display classes, closures, iterators) with their parent method and type context")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, cancellationToken);
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
