using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

[McpServerToolType]
public sealed class AnalyzeAssemblyTool
{
    private readonly AnalyzeAssemblyUseCase _useCase;
    private readonly ILogger<AnalyzeAssemblyTool> _logger;

    public AnalyzeAssemblyTool(
        AnalyzeAssemblyUseCase useCase,
        ILogger<AnalyzeAssemblyTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "analyze_assembly")]
    [Description("Use this when you just installed a NuGet library and need to understand its overall architecture and entry points before writing code. Provides high-level overview of main namespaces, key public types, and design patterns. Start here for unfamiliar libraries.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("What aspects to analyze? (e.g., 'architecture overview', 'public API surface', 'design patterns')")] string? query = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, query, cancellationToken);
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in analyze_assembly tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in analyze_assembly tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in analyze_assembly tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while analyzing the assembly.");
        }
    }
}
