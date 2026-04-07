using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

public sealed class FindTypeHierarchyUseCase
{
    private readonly IDecompilerService _decompiler;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<FindTypeHierarchyUseCase> _logger;

    public FindTypeHierarchyUseCase(
        IDecompilerService decompiler,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<FindTypeHierarchyUseCase> logger)
    {
        _decompiler = decompiler;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string typeName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);
            var type = TypeName.Create(typeName);

            _logger.LogInformation("Finding hierarchy for type {TypeName} in {Assembly}", typeName, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var typeInfo = await _decompiler.GetTypeInfoAsync(assembly, type, timeout.Token);

                var result = new System.Text.StringBuilder();
                result.AppendLine($"Type Hierarchy: {typeInfo.FullName}");
                result.AppendLine($"Kind: {typeInfo.Kind}");
                result.AppendLine();

                result.AppendLine("Inherits from:");
                if (typeInfo.BaseTypes.Any())
                {
                    foreach (var baseType in typeInfo.BaseTypes)
                    {
                        result.AppendLine($"  \u2191 {baseType}");
                    }
                }
                else
                {
                    result.AppendLine("  (none, inherits from System.Object)");
                }
                result.AppendLine();

                result.AppendLine("Implements interfaces:");
                if (typeInfo.Interfaces.Any())
                {
                    foreach (var iface in typeInfo.Interfaces)
                    {
                        result.AppendLine($"  \u2022 {iface}");
                    }
                }
                else
                {
                    result.AppendLine("  (none)");
                }

                return result.ToString();
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for finding hierarchy of {TypeName}", typeName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for finding hierarchy of {TypeName}", typeName);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error finding hierarchy for {TypeName}", typeName);
            throw;
        }
    }
}
