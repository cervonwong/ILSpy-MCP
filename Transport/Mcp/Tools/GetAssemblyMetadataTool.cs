using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for retrieving assembly metadata.
/// </summary>
[McpServerToolType]
public sealed class GetAssemblyMetadataTool
{
    private readonly GetAssemblyMetadataUseCase _useCase;
    private readonly ILogger<GetAssemblyMetadataTool> _logger;

    public GetAssemblyMetadataTool(
        GetAssemblyMetadataUseCase useCase,
        ILogger<GetAssemblyMetadataTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "get_assembly_metadata")]
    [Description("Reads assembly-level metadata: target framework, runtime version, PE bitness, strong name, entry point, and referenced assemblies. Use this when you need to determine an assembly's runtime requirements, verify strong-name signing, or map its dependency graph before deeper analysis.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly (.dll/.exe)")] string assemblyPath,
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
            _logger.LogWarning("Timeout in get_assembly_metadata tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in get_assembly_metadata tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in get_assembly_metadata tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while retrieving assembly metadata.");
        }
    }
}
