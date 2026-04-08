using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for loading and listing all .NET assemblies found in a directory.
/// </summary>
public sealed class LoadAssemblyDirectoryUseCase
{
    private readonly ICrossAssemblyService _crossAssembly;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<LoadAssemblyDirectoryUseCase> _logger;

    public LoadAssemblyDirectoryUseCase(
        ICrossAssemblyService crossAssembly,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<LoadAssemblyDirectoryUseCase> logger)
    {
        _crossAssembly = crossAssembly;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string directoryPath,
        int maxDepth = 3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = DirectoryPath.Create(directoryPath);

            _logger.LogInformation("Loading assembly directory: {Directory}", directoryPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var result = await _crossAssembly.LoadAssemblyDirectoryAsync(
                    directory, maxDepth, timeout.Token);

                return FormatResult(result);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for loading directory '{Directory}'", directoryPath);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for loading directory '{Directory}'", directoryPath);
            throw new TimeoutException(
                $"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DirectoryNotFoundException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading directory '{Directory}'", directoryPath);
            throw;
        }
    }

    private static string FormatResult(DirectoryLoadResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Directory scan: {result.LoadedAssemblies.Count} assemblies loaded, " +
                       $"{result.SkippedFiles.Count} files skipped ({result.TotalFiles} total)");
        sb.AppendLine();

        if (result.LoadedAssemblies.Count > 0)
        {
            sb.AppendLine("Loaded assemblies:");
            foreach (var entry in result.LoadedAssemblies)
            {
                sb.AppendLine($"  {entry.AssemblyName} v{entry.Version} ({entry.FilePath})");
            }
        }

        if (result.SkippedFiles.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Skipped files:");
            foreach (var entry in result.SkippedFiles)
            {
                sb.AppendLine($"  {entry.FilePath} -- {entry.Reason}");
            }
        }

        return sb.ToString();
    }
}
