using ILSpy.Mcp.Application.Configuration;
using ILSpy.Mcp.Application.Pagination;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace ILSpy.Mcp.Application.UseCases;

public sealed class AnalyzeAssemblyUseCase
{
    private readonly IDecompilerService _decompiler;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<AnalyzeAssemblyUseCase> _logger;
    private readonly ILSpyOptions _options;

    public AnalyzeAssemblyUseCase(
        IDecompilerService decompiler,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<AnalyzeAssemblyUseCase> logger,
        IOptions<ILSpyOptions> options)
    {
        _decompiler = decompiler;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string? query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);

            _logger.LogInformation("Analyzing assembly {Assembly}", assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var assemblyInfo = await _decompiler.GetAssemblyInfoAsync(assembly, timeout.Token);

                // Build a summary of the assembly
                var result = new StringBuilder();
                result.AppendLine($"Assembly: {assemblyInfo.FileName}");
                result.AppendLine($"Total Types: {assemblyInfo.TotalTypeCount}");
                result.AppendLine($"Public Types: {assemblyInfo.PublicTypes.Count}");
                result.AppendLine();

                if (assemblyInfo.NamespaceCounts.Any())
                {
                    result.AppendLine("Namespaces:");
                    foreach (var ns in assemblyInfo.NamespaceCounts.OrderByDescending(kvp => kvp.Value))
                    {
                        result.AppendLine($"  {ns.Key}: {ns.Value} types");
                    }
                    result.AppendLine();
                }

                if (assemblyInfo.PublicTypes.Any())
                {
                    result.AppendLine("Key Public Types:");
                    foreach (var type in assemblyInfo.PublicTypes)
                    {
                        result.AppendLine($"  {type.Kind} {type.FullName}");
                    }
                }

                var raw = result.ToString();
                var totalBytes = raw.Length;
                var maxBytes = _options.MaxDecompilationSize;
                var truncated = totalBytes > maxBytes;
                var body = truncated ? raw[..maxBytes] : raw;
                var returnedBytes = body.Length;

                var sb = new StringBuilder(body);
                PaginationEnvelope.AppendFooter(sb, totalBytes, returnedBytes, offset: 0);
                return sb.ToString();
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
            _logger.LogError(ex, "Unexpected error analyzing assembly {Assembly}", assemblyPath);
            throw;
        }
    }
}
