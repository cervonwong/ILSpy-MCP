using System.Text;
using ILSpy.Mcp.Application.Configuration;
using ILSpy.Mcp.Application.Pagination;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for decompiling a type.
/// </summary>
public sealed class DecompileTypeUseCase
{
    private readonly IDecompilerService _decompiler;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<DecompileTypeUseCase> _logger;
    private readonly ILSpyOptions _options;

    public DecompileTypeUseCase(
        IDecompilerService decompiler,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<DecompileTypeUseCase> logger,
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
        string typeName,
        string? query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);
            var type = TypeName.Create(typeName);

            _logger.LogInformation("Decompiling type {TypeName} from {Assembly}", typeName, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var decompilation = await _decompiler.DecompileTypeAsync(assembly, type, timeout.Token);
                var result = decompilation.SourceCode;
                var (text, totalLines, returnedLines, wasTruncated) =
                    TruncationEnvelope.TruncateSource(result, _options.MaxDecompilationSize);
                var sb = new StringBuilder(text);
                TruncationEnvelope.AppendSourceFooter(sb, totalLines, returnedLines, wasTruncated);
                return sb.ToString();
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for type {TypeName}", typeName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for type {TypeName}", typeName);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error decompiling type {TypeName}", typeName);
            throw;
        }
    }
}
