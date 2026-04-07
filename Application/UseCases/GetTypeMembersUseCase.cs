using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

public sealed class GetTypeMembersUseCase
{
    private readonly IDecompilerService _decompiler;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<GetTypeMembersUseCase> _logger;

    public GetTypeMembersUseCase(
        IDecompilerService decompiler,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<GetTypeMembersUseCase> logger)
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

            _logger.LogInformation("Getting members for type {TypeName} from {Assembly}", typeName, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var typeInfo = await _decompiler.GetTypeInfoAsync(assembly, type, timeout.Token);

                var result = new System.Text.StringBuilder();
            result.AppendLine($"╔═══ Type Members: {typeInfo.FullName}");
            result.AppendLine($"║ Assembly: {assembly.FileName}");
            result.AppendLine($"║ Kind: {typeInfo.Kind}");
            result.AppendLine($"║ Namespace: {typeInfo.Namespace ?? "(global)"}");
            result.AppendLine($"╚═══");
            result.AppendLine();

            if (typeInfo.Methods.Any())
            {
                result.AppendLine("Methods:");
                foreach (var method in typeInfo.Methods)
                {
                    var accessibility = method.Accessibility.ToString().ToLower();
                    var modifiers = new List<string>();
                    if (method.IsStatic) modifiers.Add("static");
                    if (method.IsAbstract) modifiers.Add("abstract");
                    if (method.IsVirtual) modifiers.Add("virtual");
                    
                    var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
                    var mods = modifiers.Any() ? string.Join(" ", modifiers) + " " : "";
                    result.AppendLine($"  {accessibility} {mods}{method.ReturnType} {method.Name}({parameters})");
                }
                result.AppendLine();
            }

            if (typeInfo.Properties.Any())
            {
                result.AppendLine("Properties:");
                foreach (var prop in typeInfo.Properties)
                {
                    var accessibility = prop.Accessibility.ToString().ToLower();
                    var getter = prop.HasGetter ? "get;" : "";
                    var setter = prop.HasSetter ? "set;" : "";
                    result.AppendLine($"  {accessibility} {prop.Type} {prop.Name} {{ {getter} {setter} }}");
                }
                result.AppendLine();
            }

            if (typeInfo.Fields.Any())
            {
                result.AppendLine("Fields:");
                foreach (var field in typeInfo.Fields)
                {
                    var accessibility = field.Accessibility.ToString().ToLower();
                    var modifiers = field.IsStatic ? "static " : "";
                    result.AppendLine($"  {accessibility} {modifiers}{field.Type} {field.Name}");
                }
                result.AppendLine();
            }

            if (typeInfo.Events.Any())
            {
                result.AppendLine("Events:");
                foreach (var evt in typeInfo.Events)
                {
                    var accessibility = evt.Accessibility.ToString().ToLower();
                    result.AppendLine($"  {accessibility} event {evt.Type} {evt.Name}");
                }
            }

                return result.ToString();
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for getting members of {TypeName}", typeName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for getting members of {TypeName}", typeName);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting type members for {TypeName}", typeName);
            throw;
        }
    }
}
