using System.Text;
using ILSpy.Mcp.Application.Pagination;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for finding compiler-generated types with parent context.
/// </summary>
public sealed class FindCompilerGeneratedTypesUseCase
{
    private readonly IAssemblyInspectionService _inspection;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<FindCompilerGeneratedTypesUseCase> _logger;

    public FindCompilerGeneratedTypesUseCase(
        IAssemblyInspectionService inspection,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<FindCompilerGeneratedTypesUseCase> logger)
    {
        _inspection = inspection;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);

            _logger.LogInformation("Finding compiler-generated types in {Assembly}", assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var types = await _inspection.FindCompilerGeneratedTypesAsync(assembly, timeout.Token);

                var sorted = types
                    .OrderBy(t => t.ParentType ?? t.FullName, StringComparer.Ordinal)
                    .ThenBy(t => t.FullName, StringComparer.Ordinal)
                    .ToList();
                var total = sorted.Count;
                var page = sorted.Skip(offset).Take(maxResults).ToList();
                return FormatCompilerGeneratedTypes(page, total, offset);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for assembly {Assembly}", assemblyPath);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for assembly {Assembly}", assemblyPath);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error finding compiler-generated types in {Assembly}", assemblyPath);
            throw;
        }
    }

    private static string FormatCompilerGeneratedTypes(IReadOnlyList<CompilerGeneratedTypeInfo> page, int total, int offset)
    {
        var sb = new StringBuilder();
        var returned = page.Count;

        if (total == 0)
        {
            sb.AppendLine($"# Compiler-Generated Types (0)");
            sb.AppendLine();
            sb.AppendLine("No compiler-generated types found.");
        }
        else
        {
            if (returned == 0)
            {
                sb.AppendLine($"# Compiler-Generated Types ({total} total, offset {offset} is beyond last page)");
            }
            else
            {
                sb.AppendLine($"# Compiler-Generated Types ({total} total, showing {offset + 1}-{offset + returned})");
            }
            sb.AppendLine();
            foreach (var type in page)
            {
                sb.AppendLine($"- {type.FullName}");
                sb.AppendLine($"  Kind: {type.GeneratedKind}");
                if (type.ParentType != null)
                    sb.AppendLine($"  Parent Type: {type.ParentType}");
                if (type.ParentMethod != null)
                    sb.AppendLine($"  Parent Method: {type.ParentMethod}");
            }
        }

        PaginationEnvelope.AppendFooter(sb, total, returned, offset);
        return sb.ToString();
    }
}
