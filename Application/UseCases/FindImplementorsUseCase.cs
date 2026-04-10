using System.Text;
using ILSpy.Mcp.Application.Pagination;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for finding all types that implement an interface or extend a base class.
/// </summary>
public sealed class FindImplementorsUseCase
{
    private readonly ICrossReferenceService _crossRef;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<FindImplementorsUseCase> _logger;

    public FindImplementorsUseCase(
        ICrossReferenceService crossRef,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<FindImplementorsUseCase> logger)
    {
        _crossRef = crossRef;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string typeName,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);
            var type = TypeName.Create(typeName);

            _logger.LogInformation("Finding implementors of {TypeName} from {Assembly}", typeName, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var results = await _crossRef.FindImplementorsAsync(assembly, type, timeout.Token);

                var sorted = results
                    .OrderByDescending(r => r.IsDirect)
                    .ThenBy(r => r.TypeFullName, StringComparer.Ordinal)
                    .ToList();
                var total = sorted.Count;
                var page = sorted.Skip(offset).Take(maxResults).ToList();
                return FormatResults(typeName, page, total, offset);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for finding implementors of {TypeName}", typeName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for finding implementors of {TypeName}", typeName);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error finding implementors of {TypeName}", typeName);
            throw;
        }
    }

    private static string FormatResults(string typeName, IReadOnlyList<ImplementorResult> page, int total, int offset)
    {
        var sb = new StringBuilder();
        var returned = page.Count;

        if (total == 0)
        {
            sb.AppendLine($"Implementors of {typeName}: 0 found");
        }
        else if (returned == 0)
        {
            sb.AppendLine($"Implementors of {typeName}: {total} found (offset {offset} is beyond last page)");
        }
        else
        {
            var rangeStart = offset + 1;
            var rangeEnd = offset + returned;
            sb.AppendLine($"Implementors of {typeName}: {total} found (showing {rangeStart}-{rangeEnd})");
        }

        sb.AppendLine();

        if (total == 0)
        {
            sb.AppendLine("No implementors found in the assembly.");
        }
        else
        {
            foreach (var result in page)
            {
                var marker = result.IsDirect ? "direct" : "transitive";
                sb.AppendLine($"  [{marker}] [{result.Kind}] {result.TypeFullName}");
            }
        }

        PaginationEnvelope.AppendFooter(sb, total, returned, offset);
        return sb.ToString();
    }
}
