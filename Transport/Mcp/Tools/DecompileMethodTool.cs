using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

[McpServerToolType]
public sealed class DecompileMethodTool
{
    private readonly DecompileMethodUseCase _useCase;
    private readonly ILogger<DecompileMethodTool> _logger;

    public DecompileMethodTool(
        DecompileMethodUseCase useCase,
        ILogger<DecompileMethodTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "decompile_method")]
    [Description("Decompiles a single method to reconstructed C# source. Use this when you need to understand a method's logic, parameters, and side effects in a compiled binary. For IL-level analysis (compiler tricks, async state machines), use disassemble_method instead. Returns C# source with truncation metadata.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly (.dll/.exe)")] string assemblyPath,
        [Description("Full name of the type containing the method")] string typeName,
        [Description("Method name to decompile")] string methodName,
        [Description("Focus area (e.g., 'algorithm logic', 'error handling', 'performance')")] string? query = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(assemblyPath, typeName, methodName, query, cancellationToken);
        }
        catch (TypeNotFoundException ex)
        {
            _logger.LogWarning("Type not found: {TypeName} in {Assembly}", ex.TypeName, ex.AssemblyPath);
            throw new McpToolException("TYPE_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (MethodNotFoundException ex)
        {
            _logger.LogWarning("Method not found: {MethodName} in {TypeName}", ex.MethodName, ex.TypeName);
            throw new McpToolException("METHOD_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in decompile_method tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in decompile_method tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in decompile_method tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while decompiling the method.");
        }
    }
}
