using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for resolving which assembly in a directory defines a given type name.
/// </summary>
public sealed class ResolveTypeUseCase
{
    private readonly ICrossAssemblyService _crossAssembly;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<ResolveTypeUseCase> _logger;

    public ResolveTypeUseCase(
        ICrossAssemblyService crossAssembly,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<ResolveTypeUseCase> logger)
    {
        _crossAssembly = crossAssembly;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string directoryPath,
        string typeName,
        int maxDepth = 3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = DirectoryPath.Create(directoryPath);

            _logger.LogInformation("Resolving type '{TypeName}' in directory {Directory}",
                typeName, directoryPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var results = await _crossAssembly.ResolveTypeAsync(
                    directory, typeName, maxDepth, timeout.Token);

                return FormatResults(typeName, results);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for resolving type '{TypeName}'", typeName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for resolving type '{TypeName}'", typeName);
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
            _logger.LogError(ex, "Unexpected error resolving type '{TypeName}'", typeName);
            throw;
        }
    }

    private static string FormatResults(string typeName, IReadOnlyList<TypeResolutionResult> results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Type resolution for '{typeName}': {results.Count} match(es) found");
        sb.AppendLine();

        if (results.Count == 0)
        {
            sb.AppendLine($"No assemblies in the directory define a type matching '{typeName}'.");
            return sb.ToString();
        }

        foreach (var result in results)
        {
            sb.AppendLine($"  {result.TypeFullName} in {result.AssemblyName} ({result.AssemblyPath})");
        }

        return sb.ToString();
    }
}
