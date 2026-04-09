using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for decompiling types.
/// </summary>
[McpServerToolType]
public sealed class DecompileTypeTool
{
    private readonly DecompileTypeUseCase _useCase;
    private readonly ILogger<DecompileTypeTool> _logger;

    public DecompileTypeTool(
        DecompileTypeUseCase useCase,
        ILogger<DecompileTypeTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "decompile_type")]
    [Description("Use this when writing code that needs to interact with a NuGet library type but you don't know its structure. Decompiles a class/interface/struct to understand what methods, properties, and constructors are available. Returns AI-analyzed insights about usage patterns, not raw source code.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Full name of the type to decompile (e.g., 'System.String')")] string typeName,
        [Description("What specific information are you looking for? (e.g., 'method implementations', 'property definitions', 'overall structure')")] string? query = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, typeName, query, cancellationToken);
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
            _logger.LogWarning("Timeout in decompile_type tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in decompile_type tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in decompile_type tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while decompiling the type.");
        }
    }
}
