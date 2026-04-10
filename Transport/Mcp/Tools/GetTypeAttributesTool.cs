using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for retrieving type-level custom attributes.
/// </summary>
[McpServerToolType]
public sealed class GetTypeAttributesTool
{
    private readonly GetTypeAttributesUseCase _useCase;
    private readonly ILogger<GetTypeAttributesTool> _logger;

    public GetTypeAttributesTool(
        GetTypeAttributesUseCase useCase,
        ILogger<GetTypeAttributesTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "get_type_attributes")]
    [Description("Lists all custom attributes on a type with their constructor arguments and named properties. Use this when checking serialization settings, ORM mappings, validation rules, or other metadata-driven behavior declared on a class, struct, or interface.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly (.dll/.exe)")] string assemblyPath,
        [Description("Full type name (e.g., 'MyApp.Models.Order')")] string typeName,
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
            _logger.LogWarning("Timeout in get_type_attributes tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in get_type_attributes tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in get_type_attributes tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while retrieving type attributes.");
        }
    }
}
