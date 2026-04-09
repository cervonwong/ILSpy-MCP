using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for retrieving member-level custom attributes.
/// </summary>
[McpServerToolType]
public sealed class GetMemberAttributesTool
{
    private readonly GetMemberAttributesUseCase _useCase;
    private readonly ILogger<GetMemberAttributesTool> _logger;

    public GetMemberAttributesTool(
        GetMemberAttributesUseCase useCase,
        ILogger<GetMemberAttributesTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "get_member_attributes")]
    [Description("List all custom attributes on a type member (method, property, field, event) with their constructor arguments")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Full name of the type (e.g., 'MyNamespace.MyClass')")] string typeName,
        [Description("Name of the member (method, property, field, or event)")] string memberName,
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
            _logger.LogWarning("Timeout in get_member_attributes tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in get_member_attributes tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in get_member_attributes tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while retrieving member attributes.");
        }
    }
}
