using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for finding all usage sites of a member across an assembly.
/// </summary>
[McpServerToolType]
public sealed class FindUsagesTool
{
    private readonly FindUsagesUseCase _useCase;
    private readonly ILogger<FindUsagesTool> _logger;

    public FindUsagesTool(
        FindUsagesUseCase useCase,
        ILogger<FindUsagesTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "find_usages")]
    [Description("Finds all call sites, field reads, and property accesses of a specific member across an assembly. Use this when tracing how a method or field propagates through a binary, assessing impact before patching, or mapping data flow. Returns paginated matches with declaring type, method signature, and IL offset.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly (.dll/.exe)")] string assemblyPath,
        [Description("Full name of the type containing the member (e.g., 'MyApp.Services.OrderProcessor')")] string typeName,
        [Description("Member to find usages of (method, field, or property name)")] string memberName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, typeName, memberName, cancellationToken);
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
            _logger.LogWarning("Timeout in find_usages tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in find_usages tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in find_usages tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while finding usages.");
        }
    }
}
