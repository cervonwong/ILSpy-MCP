using ILSpy.Mcp.Application.Pagination;
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
        int maxResults = 100,
        int offset = 0,
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

                var allMembers = new List<(string Section, string Line)>();
                foreach (var ctor in typeInfo.Constructors)
                {
                    var accessibility = ctor.Accessibility.ToString().ToLower();
                    var modifiers = ctor.IsStatic ? "static " : "";
                    var parameters = string.Join(", ", ctor.Parameters.Select(p => $"{p.Type} {p.Name}"));
                    allMembers.Add(("Constructors", $"  {accessibility} {modifiers}{ctor.Name}({parameters})"));
                }
                foreach (var method in typeInfo.Methods)
                {
                    var accessibility = method.Accessibility.ToString().ToLower();
                    var modifiers = new List<string>();
                    if (method.IsStatic) modifiers.Add("static");
                    if (method.IsAbstract) modifiers.Add("abstract");
                    if (method.IsVirtual) modifiers.Add("virtual");
                    var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
                    var mods = modifiers.Any() ? string.Join(" ", modifiers) + " " : "";
                    allMembers.Add(("Methods", $"  {accessibility} {mods}{method.ReturnType} {method.Name}({parameters})"));
                }
                foreach (var prop in typeInfo.Properties)
                {
                    var accessibility = prop.Accessibility.ToString().ToLower();
                    var getter = prop.HasGetter ? "get;" : "";
                    var setter = prop.HasSetter ? "set;" : "";
                    allMembers.Add(("Properties", $"  {accessibility} {prop.Type} {prop.Name} {{ {getter} {setter} }}"));
                }
                foreach (var field in typeInfo.Fields)
                {
                    var accessibility = field.Accessibility.ToString().ToLower();
                    var modifiers = field.IsStatic ? "static " : "";
                    allMembers.Add(("Fields", $"  {accessibility} {modifiers}{field.Type} {field.Name}"));
                }
                foreach (var evt in typeInfo.Events)
                {
                    var accessibility = evt.Accessibility.ToString().ToLower();
                    allMembers.Add(("Events", $"  {accessibility} event {evt.Type} {evt.Name}"));
                }

                var total = allMembers.Count;
                var page = allMembers.Skip(offset).Take(maxResults).ToList();
                var returned = page.Count;

                var result = new System.Text.StringBuilder();
                result.AppendLine($"╔═══ Type Members: {typeInfo.FullName}");
                result.AppendLine($"║ Assembly: {assembly.FileName}");
                result.AppendLine($"║ Kind: {typeInfo.Kind}");
                result.AppendLine($"║ Namespace: {typeInfo.Namespace ?? "(global)"}");
                if (total > 0)
                {
                    var rangeStart = offset + 1;
                    var rangeEnd = offset + returned;
                    result.AppendLine($"║ Members: {total} (showing {rangeStart}-{rangeEnd})");
                }
                result.AppendLine($"╚═══");
                result.AppendLine();

                string? currentSection = null;
                foreach (var (section, line) in page)
                {
                    if (section != currentSection)
                    {
                        if (currentSection != null) result.AppendLine();
                        result.AppendLine($"{section}:");
                        currentSection = section;
                    }
                    result.AppendLine(line);
                }

                PaginationEnvelope.AppendFooter(result, total, returned, offset);
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
