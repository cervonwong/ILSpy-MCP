using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for loading and listing all .NET assemblies in a directory.
/// </summary>
[McpServerToolType]
public sealed class LoadAssemblyDirectoryTool
{
    private readonly LoadAssemblyDirectoryUseCase _useCase;
    private readonly ILogger<LoadAssemblyDirectoryTool> _logger;

    public LoadAssemblyDirectoryTool(
        LoadAssemblyDirectoryUseCase useCase,
        ILogger<LoadAssemblyDirectoryTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "load_assembly_directory")]
    [Description("Scans a directory for .NET assemblies and loads them for analysis. Use this when starting work on a multi-assembly project or application directory -- load the directory first, then use resolve_type to find which assembly defines a specific type. Reports loaded assemblies with name/version and skipped files with reasons.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to directory containing .NET assemblies")] string directoryPath,
        [Description("Maximum recursion depth for subdirectories (default 3)")] int maxDepth = 3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _useCase.ExecuteAsync(directoryPath, maxDepth, cancellationToken);
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogWarning("Directory not found: {Directory}", directoryPath);
            throw new McpToolException("DIRECTORY_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid argument: {Message}", ex.Message);
            throw new McpToolException("INVALID_ARGUMENT", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in load_assembly_directory tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT",
                "The operation timed out. The directory may contain too many files.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in load_assembly_directory tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in load_assembly_directory tool");
            throw new McpToolException("INTERNAL_ERROR",
                "An unexpected error occurred while loading assembly directory.");
        }
    }
}
