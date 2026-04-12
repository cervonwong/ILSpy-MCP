using ILSpy.Mcp.Application.Pagination;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for searching numeric integer constants in assembly IL bytecode by exact value.
/// </summary>
public sealed class SearchConstantsUseCase
{
    private readonly ISearchService _searchService;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<SearchConstantsUseCase> _logger;

    public SearchConstantsUseCase(
        ISearchService searchService,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<SearchConstantsUseCase> logger)
    {
        _searchService = searchService;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        long value,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);

            _logger.LogInformation("Searching constants with value {Value} in {Assembly}",
                value, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var results = await _searchService.SearchConstantsAsync(
                    assembly, value, maxResults, offset, timeout.Token);

                return FormatResults(value, results);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for constant search {Value}", value);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for constant search {Value}", value);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error searching constants with value {Value}", value);
            throw;
        }
    }

    private static string FormatResults(long value, SearchResults<ConstantSearchResult> results)
    {
        var sb = new System.Text.StringBuilder();
        var total = results.TotalCount;
        var offset = results.Offset;
        var returned = results.Results.Count;

        int rangeEnd = Math.Min(offset + returned, total);
        if (total == 0)
        {
            sb.AppendLine($"Constant search for {value}: 0 total matches");
        }
        else
        {
            sb.AppendLine($"Constant search for {value}: {total} total matches (showing {offset + 1}-{rangeEnd})");
        }
        sb.AppendLine();

        if (returned == 0)
        {
            sb.AppendLine("No matching constants found in the assembly.");
        }
        else
        {
            foreach (var result in results.Results)
            {
                sb.AppendLine($"  {result.MatchedValue} ({result.ConstantType}) in {result.DeclaringType}.{result.MethodName} (IL_{result.ILOffset:X4})");
            }
        }

        PaginationEnvelope.AppendFooter(sb, total, returned, offset);
        return sb.ToString();
    }
}
