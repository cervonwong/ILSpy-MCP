using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

public sealed class ListAssemblyTypesUseCase
{
    private readonly IDecompilerService _decompiler;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<ListAssemblyTypesUseCase> _logger;

    public ListAssemblyTypesUseCase(
        IDecompilerService decompiler,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<ListAssemblyTypesUseCase> logger)
    {
        _decompiler = decompiler;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string? namespaceFilter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);

            _logger.LogInformation("Listing types from {Assembly} with filter: {Filter}",
                assemblyPath, namespaceFilter ?? "none");

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var types = await _decompiler.ListTypesAsync(assembly, namespaceFilter, timeout.Token);

                var result = new System.Text.StringBuilder();
                result.AppendLine($"Assembly: {assembly.FileName}");
                result.AppendLine($"Types found: {types.Count}");
                result.AppendLine();

                foreach (var type in types)
                {
                    var kind = type.Kind.ToString().ToLower();
                    result.AppendLine($"  {kind,-10} {type.FullName}");
                }

                return result.ToString();
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for listing types from {Assembly}", assemblyPath);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for listing types from {Assembly}", assemblyPath);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error listing types from {Assembly}", assemblyPath);
            throw;
        }
    }
}
