using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for finding all usage sites of a member (method, field, property) across an assembly.
/// </summary>
public sealed class FindUsagesUseCase
{
    private readonly ICrossReferenceService _crossRef;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<FindUsagesUseCase> _logger;

    public FindUsagesUseCase(
        ICrossReferenceService crossRef,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<FindUsagesUseCase> logger)
    {
        _crossRef = crossRef;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string typeName,
        string memberName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);
            var type = TypeName.Create(typeName);

            _logger.LogInformation("Finding usages of {MemberName} in {TypeName} from {Assembly}",
                memberName, typeName, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var results = await _crossRef.FindUsagesAsync(assembly, type, memberName, timeout.Token);

                return FormatResults(typeName, memberName, results);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for finding usages of {MemberName}", memberName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for finding usages of {MemberName}", memberName);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error finding usages of {MemberName}", memberName);
            throw;
        }
    }

    private static string FormatResults(string typeName, string memberName, IReadOnlyList<UsageResult> results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Usages of {typeName}.{memberName}: {results.Count} found");
        sb.AppendLine();

        if (results.Count == 0)
        {
            sb.AppendLine("No usages found in the assembly.");
            return sb.ToString();
        }

        foreach (var result in results)
        {
            sb.AppendLine($"  [{result.Kind}] {result.DeclaringType}.{result.MethodName} (IL_{result.ILOffset:X4})");
        }

        return sb.ToString();
    }
}
