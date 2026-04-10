using System.Text;
using ILSpy.Mcp.Application.Pagination;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for finding all instantiation sites (newobj) of a given type.
/// </summary>
public sealed class FindInstantiationsUseCase
{
    private readonly ICrossReferenceService _crossRef;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<FindInstantiationsUseCase> _logger;

    public FindInstantiationsUseCase(
        ICrossReferenceService crossRef,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<FindInstantiationsUseCase> logger)
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

            _logger.LogInformation("Finding instantiations of {TypeName} from {Assembly}", typeName, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var results = await _crossRef.FindInstantiationsAsync(assembly, type, timeout.Token);

                var sorted = results
                    .OrderBy(r => r.DeclaringType, StringComparer.Ordinal)
                    .ThenBy(r => r.ILOffset)
                    .ToList();
                var total = sorted.Count;
                var page = sorted.Skip(offset).Take(maxResults).ToList();
                return FormatResults(typeName, page, total, offset);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for finding instantiations of {TypeName}", typeName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for finding instantiations of {TypeName}", typeName);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error finding instantiations of {TypeName}", typeName);
            throw;
        }
    }

    private static string FormatResults(string typeName, IReadOnlyList<InstantiationResult> page, int total, int offset)
    {
        var sb = new StringBuilder();
        var returned = page.Count;

        // Header — three branches
        if (total == 0)
        {
            sb.AppendLine($"Instantiations of {typeName}: 0 found");
        }
        else if (returned == 0)
        {
            sb.AppendLine($"Instantiations of {typeName}: {total} found (offset {offset} is beyond last page)");
        }
        else
        {
            sb.AppendLine($"Instantiations of {typeName}: {total} found (showing {offset + 1}-{offset + returned})");
        }
        sb.AppendLine();

        // Body — one line per match
        if (total == 0)
        {
            sb.AppendLine("No instantiation sites found in the assembly.");
        }
        else
        {
            foreach (var result in page)
            {
                var sig = result.MethodSignature is not null ? " " + result.MethodSignature : "";
                sb.AppendLine($"  {result.DeclaringType}.{result.MethodName} (IL_{result.ILOffset:X4}){sig}");
            }
        }

        // Footer — the parseable contract. ALWAYS present.
        PaginationEnvelope.AppendFooter(sb, total, returned, offset);

        return sb.ToString();
    }
}
