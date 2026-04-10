using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler that dispatches to the appropriate cross-reference analysis tool.
/// </summary>
[McpServerToolType]
public sealed class AnalyzeReferencesTool
{
    private readonly FindUsagesUseCase _usagesUseCase;
    private readonly FindImplementorsUseCase _implementorsUseCase;
    private readonly FindDependenciesUseCase _dependenciesUseCase;
    private readonly FindInstantiationsUseCase _instantiationsUseCase;
    private readonly ILogger<AnalyzeReferencesTool> _logger;

    public AnalyzeReferencesTool(
        FindUsagesUseCase usagesUseCase,
        FindImplementorsUseCase implementorsUseCase,
        FindDependenciesUseCase dependenciesUseCase,
        FindInstantiationsUseCase instantiationsUseCase,
        ILogger<AnalyzeReferencesTool> logger)
    {
        _usagesUseCase = usagesUseCase;
        _implementorsUseCase = implementorsUseCase;
        _dependenciesUseCase = dependenciesUseCase;
        _instantiationsUseCase = instantiationsUseCase;
        _logger = logger;
    }

    [McpServerTool(Name = "analyze_references")]
    [Description("Unified cross-reference analysis tool. Routes to the appropriate analysis based on analysis_type parameter. Use this for exploratory analysis when you're not sure which specific tool to call.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Type of analysis: 'usages', 'implementors', 'dependencies', or 'instantiations'")] string analysisType,
        [Description("Full name of the type to analyze")] string typeName,
        [Description("Member name (required for 'usages', optional for 'dependencies')")] string? memberName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return analysisType.ToLowerInvariant() switch
            {
                "usages" => memberName is null
                    ? throw new McpToolException("INVALID_PARAMETER", "member_name is required for usages analysis")
                    : await _usagesUseCase.ExecuteAsync(assemblyPath, typeName, memberName, cancellationToken: cancellationToken),
                "implementors" => await _implementorsUseCase.ExecuteAsync(assemblyPath, typeName, cancellationToken: cancellationToken),
                "dependencies" => await _dependenciesUseCase.ExecuteAsync(assemblyPath, typeName, memberName, cancellationToken),
                "instantiations" => await _instantiationsUseCase.ExecuteAsync(assemblyPath, typeName, cancellationToken),
                _ => throw new McpToolException("INVALID_ANALYSIS_TYPE",
                    $"Unknown analysis type: {analysisType}. Valid types: usages, implementors, dependencies, instantiations")
            };
        }
        catch (McpToolException)
        {
            throw;
        }
        catch (TypeNotFoundException ex)
        {
            _logger.LogWarning("Type not found: {TypeName} in {Assembly}", ex.TypeName, ex.AssemblyPath);
            throw new McpToolException("TYPE_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (MethodNotFoundException ex)
        {
            _logger.LogWarning("Member not found: {MemberName} in {TypeName}", ex.MethodName, ex.TypeName);
            throw new McpToolException("MEMBER_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in analyze_references tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in analyze_references tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in analyze_references tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while analyzing references.");
        }
    }
}
