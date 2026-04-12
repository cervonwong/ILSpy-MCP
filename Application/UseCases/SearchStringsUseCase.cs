using ILSpy.Mcp.Application.Pagination;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for searching string literals in assembly IL bytecode by regex pattern.
/// </summary>
public sealed class SearchStringsUseCase
{
    private readonly ISearchService _searchService;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<SearchStringsUseCase> _logger;

    public SearchStringsUseCase(
        ISearchService searchService,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<SearchStringsUseCase> logger)
    {
        _searchService = searchService;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string regexPattern,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);

            _logger.LogInformation("Searching strings matching '{Pattern}' in {Assembly}",
                regexPattern, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var results = await _searchService.SearchStringsAsync(
                    assembly, regexPattern, maxResults, offset, timeout.Token);

                return FormatResults(regexPattern, results);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for string search '{Pattern}'", regexPattern);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for string search '{Pattern}'", regexPattern);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (RegexMatchTimeoutException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error searching strings with pattern '{Pattern}'", regexPattern);
            throw;
        }
    }

    private static string FormatResults(string pattern, SearchResults<StringSearchResult> results)
    {
        var sb = new System.Text.StringBuilder();
        var total = results.TotalCount;
        var offset = results.Offset;
        var returned = results.Results.Count;

        int rangeEnd = Math.Min(offset + returned, total);
        if (total == 0)
        {
            sb.AppendLine($"String search for '{pattern}': 0 total matches");
        }
        else
        {
            sb.AppendLine($"String search for '{pattern}': {total} total matches (showing {offset + 1}-{rangeEnd})");
        }
        sb.AppendLine();

        if (returned == 0)
        {
            sb.AppendLine("No matching strings found in the assembly.");
        }
        else
        {
            foreach (var result in results.Results)
            {
                sb.AppendLine($"  \"{result.MatchedValue}\" in {result.DeclaringType}.{result.MethodName} (IL_{result.ILOffset:X4})");
            }
        }

        PaginationEnvelope.AppendFooter(sb, total, returned, offset);
        return sb.ToString();
    }
}
