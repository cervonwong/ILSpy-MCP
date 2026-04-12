using ILSpy.Mcp.Application.Pagination;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

public sealed class SearchMembersByNameUseCase
{
    private readonly IDecompilerService _decompiler;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<SearchMembersByNameUseCase> _logger;

    public SearchMembersByNameUseCase(
        IDecompilerService decompiler,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<SearchMembersByNameUseCase> logger)
    {
        _decompiler = decompiler;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string searchTerm,
        string? memberKind,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);

            _logger.LogInformation("Searching members in {Assembly} for '{SearchTerm}' (kind: {Kind})",
                assemblyPath, searchTerm, memberKind ?? "any");

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var results = await _decompiler.SearchMembersAsync(assembly, searchTerm, memberKind, timeout.Token);

                var total = results.Count;
                var page = results.Skip(offset).Take(maxResults).ToList();
                var returned = page.Count;

                var result = new System.Text.StringBuilder();
                result.AppendLine($"Search results for '{searchTerm}' in {assembly.FileName}");
                result.AppendLine();
                if (total == 0)
                {
                    result.AppendLine("Found 0 matching members.");
                }
                else
                {
                    var rangeStart = offset + 1;
                    var rangeEnd = offset + returned;
                    result.AppendLine($"Found {total} matching members (showing {rangeStart}-{rangeEnd}):");
                }
                result.AppendLine();

                var grouped = page.GroupBy(m => m.TypeFullName);
                foreach (var group in grouped)
                {
                    result.AppendLine($"In type: {group.Key}");
                    foreach (var member in group)
                    {
                        result.AppendLine($"  [{member.Kind}] {member.Signature}");
                    }
                    result.AppendLine();
                }

                PaginationEnvelope.AppendFooter(result, total, returned, offset);
                return result.ToString();
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for searching members in {Assembly}", assemblyPath);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for searching members in {Assembly}", assemblyPath);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error searching members in {Assembly}", assemblyPath);
            throw;
        }
    }
}
