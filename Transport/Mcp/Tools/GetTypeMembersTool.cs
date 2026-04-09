using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

[McpServerToolType]
public sealed class GetTypeMembersTool
{
    private readonly GetTypeMembersUseCase _useCase;
    private readonly ILogger<GetTypeMembersTool> _logger;

    public GetTypeMembersTool(
        GetTypeMembersUseCase useCase,
        ILogger<GetTypeMembersTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "get_type_members")]
    [Description("Use this when you know the class name but need to see what methods and properties you can call on it. Shows complete API surface (method signatures, properties, events) without implementation details. Faster than decompile_type when you just need to know 'what can I call?'")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Full name of the type to inspect")] string typeName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, typeName, cancellationToken);
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
            _logger.LogWarning("Timeout in get_type_members tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in get_type_members tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in get_type_members tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while getting type members.");
        }
    }
}
