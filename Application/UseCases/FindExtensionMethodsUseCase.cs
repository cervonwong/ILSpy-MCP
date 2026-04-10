using System.Text;
using ILSpy.Mcp.Application.Pagination;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

public sealed class FindExtensionMethodsUseCase
{
    private readonly IDecompilerService _decompiler;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<FindExtensionMethodsUseCase> _logger;

    public FindExtensionMethodsUseCase(
        IDecompilerService decompiler,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<FindExtensionMethodsUseCase> logger)
    {
        _decompiler = decompiler;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string targetTypeName,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);
            var targetType = TypeName.Create(targetTypeName);

            _logger.LogInformation("Finding extension methods for {TypeName} in {Assembly}",
                targetTypeName, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var extensionMethods = await _decompiler.FindExtensionMethodsAsync(assembly, targetType, timeout.Token);

                // Stable sort: MethodInfo has no containing-type FQN field in the domain model,
                // so D-07's "(containing static class FQN asc, Name asc, signature asc)" adapts
                // to the fields actually available: (Name asc, ReturnType asc, params-string asc).
                // StringComparer.Ordinal for culture-invariant determinism.
                var sorted = extensionMethods
                    .OrderBy(m => m.Name, StringComparer.Ordinal)
                    .ThenBy(m => m.ReturnType, StringComparer.Ordinal)
                    .ThenBy(m => string.Join(",", m.Parameters.Select(p => p.Type + " " + p.Name)), StringComparer.Ordinal)
                    .ToList();
                var total = sorted.Count;
                var page = sorted.Skip(offset).Take(maxResults).ToList();

                var sb = new StringBuilder();
                var returned = page.Count;

                sb.AppendLine($"Extension methods for type: {targetTypeName}");
                sb.AppendLine($"Assembly: {assembly.FileName}");
                if (total == 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("No extension methods found for this type in the assembly.");
                    sb.AppendLine();
                    sb.AppendLine("Note: Extension methods are defined in static classes and marked with the 'this' keyword on their first parameter.");
                }
                else
                {
                    if (returned == 0)
                    {
                        sb.AppendLine($"{total} extension methods total (offset {offset} is beyond last page)");
                    }
                    else
                    {
                        sb.AppendLine($"{total} extension methods total (showing {offset + 1}-{offset + returned})");
                    }
                    sb.AppendLine();
                    foreach (var method in page)
                    {
                        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
                        sb.AppendLine($"  {method.ReturnType} {method.Name}({parameters})");
                    }
                }

                PaginationEnvelope.AppendFooter(sb, total, returned, offset);
                return sb.ToString();
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for finding extension methods for {TypeName}", targetTypeName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for finding extension methods for {TypeName}", targetTypeName);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error finding extension methods for {TypeName}", targetTypeName);
            throw;
        }
    }
}
