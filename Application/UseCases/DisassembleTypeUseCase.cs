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
/// Use case for disassembling a type to IL (headers-only, no method bodies).
/// </summary>
public sealed class DisassembleTypeUseCase
{
    private readonly IDisassemblyService _disassembly;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<DisassembleTypeUseCase> _logger;
    private readonly ILSpyOptions _options;

    public DisassembleTypeUseCase(
        IDisassemblyService disassembly,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<DisassembleTypeUseCase> logger,
        IOptions<ILSpyOptions> options)
    {
        _disassembly = disassembly;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string typeName,
        bool showTokens,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);
            var type = TypeName.Create(typeName);

            _logger.LogInformation("Disassembling type {TypeName} from {Assembly}", typeName, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var raw = await _disassembly.DisassembleTypeAsync(assembly, type, showTokens, timeout.Token);
                var totalBytes = raw.Length;
                var maxBytes = _options.MaxDecompilationSize;
                var truncated = totalBytes > maxBytes;
                var body = truncated ? raw[..maxBytes] : raw;
                var returnedBytes = body.Length;

                var sb = new System.Text.StringBuilder(body);
                PaginationEnvelope.AppendFooter(sb, totalBytes, returnedBytes, offset: 0);
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
            _logger.LogError(ex, "Unexpected error disassembling type {TypeName}", typeName);
            throw;
        }
    }
}
