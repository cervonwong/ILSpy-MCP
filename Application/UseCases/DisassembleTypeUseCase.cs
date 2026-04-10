using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

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

    public DisassembleTypeUseCase(
        IDisassemblyService disassembly,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<DisassembleTypeUseCase> logger)
    {
        _disassembly = disassembly;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
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
                return await _disassembly.DisassembleTypeAsync(assembly, type, showTokens, timeout.Token);
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
