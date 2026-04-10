using System.Text;
using ILSpy.Mcp.Application.Pagination;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for finding all outward dependencies of a method or type.
/// </summary>
public sealed class FindDependenciesUseCase
{
    private readonly ICrossReferenceService _crossRef;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<FindDependenciesUseCase> _logger;

    public FindDependenciesUseCase(
        ICrossReferenceService crossRef,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<FindDependenciesUseCase> logger)
    {
        _crossRef = crossRef;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string typeName,
        string? methodName = null,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);
            var type = TypeName.Create(typeName);

            var target = methodName != null ? $"{typeName}.{methodName}" : typeName;
            _logger.LogInformation("Finding dependencies of {Target} from {Assembly}", target, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var results = await _crossRef.FindDependenciesAsync(assembly, type, methodName, timeout.Token);

                var sorted = results
                    .OrderBy(r => (int)r.Kind)  // enum order: MethodCall=0, FieldAccess=1, TypeReference=2, VirtualCall=3
                    .ThenBy(r => r.TargetMember, StringComparer.Ordinal)
                    .ToList();
                var total = sorted.Count;
                var page = sorted.Skip(offset).Take(maxResults).ToList();
                return FormatResults(target, page, total, offset);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for finding dependencies of {TypeName}", typeName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for finding dependencies of {TypeName}", typeName);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error finding dependencies of {TypeName}", typeName);
            throw;
        }
    }

    private static string FormatResults(string target, IReadOnlyList<DependencyResult> page, int total, int offset)
    {
        var sb = new StringBuilder();

        // Header — three branches
        if (total == 0)
        {
            sb.AppendLine($"Dependencies of {target}: 0 found");
        }
        else if (page.Count == 0)
        {
            sb.AppendLine($"Dependencies of {target}: {total} found (offset {offset} is beyond last page)");
        }
        else
        {
            sb.AppendLine($"Dependencies of {target}: {total} found (showing {offset + 1}-{offset + page.Count})");
        }

        // Body — flat sorted list, NO section headers. One line per match.
        foreach (var dep in page)
        {
            if (dep.ResolutionNote != null)
            {
                sb.AppendLine($"  [{dep.Kind}] {dep.TargetMember} [{dep.DefiningAssembly}] ({dep.ResolutionNote})");
            }
            else
            {
                sb.AppendLine($"  [{dep.Kind}] {dep.TargetMember} [{dep.DefiningAssembly}]");
            }
        }

        // Footer — the parseable contract. ALWAYS present.
        PaginationEnvelope.AppendFooter(sb, total, page.Count, offset);

        return sb.ToString();
    }
}
