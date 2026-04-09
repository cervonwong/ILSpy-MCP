using System.Text;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for extracting embedded resource content with optional pagination.
/// </summary>
public sealed class ExtractResourceUseCase
{
    private readonly IAssemblyInspectionService _inspection;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<ExtractResourceUseCase> _logger;

    public ExtractResourceUseCase(
        IAssemblyInspectionService inspection,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<ExtractResourceUseCase> logger)
    {
        _inspection = inspection;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string resourceName,
        int? offset = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        if (offset.HasValue && offset.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset.Value, "Offset must be non-negative.");
        if (limit.HasValue && limit.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit.Value, "Limit must be greater than zero.");

        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);

            _logger.LogInformation("Extracting resource {ResourceName} from {Assembly}", resourceName, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var content = await _inspection.ExtractResourceAsync(assembly, resourceName, offset, limit, timeout.Token);
                return FormatContent(content);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for resource {ResourceName}", resourceName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for resource {ResourceName}", resourceName);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error extracting resource {ResourceName}", resourceName);
            throw;
        }
    }

    private static string FormatContent(ResourceContent content)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Resource: {content.Name}");
        sb.AppendLine();
        sb.AppendLine($"Content Type: {content.ContentType}");
        sb.AppendLine($"Total Size: {content.TotalSize} bytes");

        if (content.Offset.HasValue || content.Length.HasValue)
        {
            sb.AppendLine($"Offset: {content.Offset ?? 0}");
            sb.AppendLine($"Length: {content.Length ?? 0}");
        }

        sb.AppendLine();
        sb.AppendLine("## Content");
        sb.AppendLine();
        sb.AppendLine(content.Content);

        return sb.ToString();
    }
}
