using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for disassembling a specific method to IL (full body with instructions).
/// </summary>
public sealed class DisassembleMethodUseCase
{
    private readonly IDisassemblyService _disassembly;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<DisassembleMethodUseCase> _logger;

    public DisassembleMethodUseCase(
        IDisassemblyService disassembly,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<DisassembleMethodUseCase> logger)
    {
        _disassembly = disassembly;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string typeName,
        string methodName,
        bool showBytes,
        bool showTokens,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);
            var type = TypeName.Create(typeName);

            _logger.LogInformation("Disassembling method {MethodName} from {TypeName} in {Assembly}",
                methodName, typeName, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                return await _disassembly.DisassembleMethodAsync(
                    assembly, type, methodName, showBytes, showTokens, timeout.Token);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for method {MethodName}", methodName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for method {MethodName}", methodName);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error disassembling method {MethodName}", methodName);
            throw;
        }
    }
}
