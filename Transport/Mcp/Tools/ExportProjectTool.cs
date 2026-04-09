using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for exporting a .NET assembly as a complete C# project.
/// </summary>
[McpServerToolType]
public sealed class ExportProjectTool
{
    private readonly ExportProjectUseCase _useCase;
    private readonly ILogger<ExportProjectTool> _logger;

    public ExportProjectTool(
        ExportProjectUseCase useCase,
        ILogger<ExportProjectTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "export_project")]
    [Description("Exports a .NET assembly as a complete C# project (.csproj + .cs files) to a target directory. The directory must be empty or non-existent.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Target directory for the exported project. Must be empty or non-existent.")] string outputDirectory,
        [Description("Optional namespace filter to limit export scope")] string? namespaceFilter = null,
        [Description("Maximum number of types to export (default 500)")] int maxTypes = 500,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(
                assemblyPath, outputDirectory, namespaceFilter, maxTypes, cancellationToken);
        }
        catch (McpToolException)
        {
            throw;
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in export_project tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT",
                "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in export_project tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in export_project tool");
            throw new McpToolException("INTERNAL_ERROR",
                "An unexpected error occurred while exporting the project.");
        }
    }
}
