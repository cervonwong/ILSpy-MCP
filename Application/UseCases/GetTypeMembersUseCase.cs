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

                // Flatten all members into a single ordered list
                var allMembers = new List<(int CategoryOrder, string Name, bool IsInherited, string FormattedLine)>();

                // Category 0: Constructors
                foreach (var ctor in typeInfo.Constructors)
                {
                    var line = FormatConstructor(ctor);
                    allMembers.Add((0, ctor.Name, ctor.IsInherited, line));
                }

                // Category 1: Methods
                foreach (var method in typeInfo.Methods)
                {
                    var line = FormatMethod(method);
                    allMembers.Add((1, method.Name, method.IsInherited, line));
                }

                // Category 2: Properties
                foreach (var prop in typeInfo.Properties)
                {
                    var line = FormatProperty(prop);
                    allMembers.Add((2, prop.Name, prop.IsInherited, line));
                }

                // Category 3: Fields
                foreach (var field in typeInfo.Fields)
                {
                    var line = FormatField(field);
                    allMembers.Add((3, field.Name, field.IsInherited, line));
                }

                // Category 4: Events
                foreach (var evt in typeInfo.Events)
                {
                    var line = FormatEvent(evt);
                    allMembers.Add((4, evt.Name, evt.IsInherited, line));
                }

                // Sort: by category, then declared before inherited, then alphabetically
                var sorted = allMembers
                    .OrderBy(m => m.CategoryOrder)
                    .ThenBy(m => m.IsInherited)
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Paginate
                var total = sorted.Count;
                var page = sorted.Skip(offset).Take(maxResults).ToList();
                var returned = page.Count;

                // Format output
                var result = new System.Text.StringBuilder();
                result.AppendLine($"\u2554\u2550\u2550\u2550 Type Members: {typeInfo.FullName}");
                result.AppendLine($"\u2551 Assembly: {assembly.FileName}");
                result.AppendLine($"\u2551 Kind: {typeInfo.Kind}");
                result.AppendLine($"\u2551 Namespace: {typeInfo.Namespace ?? "(global)"}");
                result.AppendLine($"\u255a\u2550\u2550\u2550");
                result.AppendLine();

                // Group page items by category and emit section headers
                var categoryNames = new Dictionary<int, string>
                {
                    { 0, "Constructors:" },
                    { 1, "Methods:" },
                    { 2, "Properties:" },
                    { 3, "Fields:" },
                    { 4, "Events:" }
                };

                int? lastCategory = null;
                foreach (var item in page)
                {
                    if (lastCategory != item.CategoryOrder)
                    {
                        if (lastCategory != null) result.AppendLine();
                        result.AppendLine(categoryNames[item.CategoryOrder]);
                        lastCategory = item.CategoryOrder;
                    }
                    result.AppendLine(item.FormattedLine);
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

    private static string FormatConstructor(MethodInfo ctor)
    {
        var accessibility = ctor.Accessibility.ToString().ToLower();
        var modifiers = new List<string>();
        if (ctor.IsStatic) modifiers.Add("static");
        var parameters = string.Join(", ", ctor.Parameters.Select(p => $"{p.Type} {p.Name}"));
        var mods = modifiers.Count > 0 ? string.Join(" ", modifiers) + " " : "";
        var tags = FormatTags(ctor.IsInherited, ctor.Attributes);
        return $"  {accessibility} {mods}{ctor.Name}({parameters}){tags}";
    }

    private static string FormatMethod(MethodInfo method)
    {
        var accessibility = method.Accessibility.ToString().ToLower();
        var modifiers = new List<string>();
        if (method.IsStatic) modifiers.Add("static");
        if (method.IsSealed && method.IsOverride) modifiers.Add("sealed override");
        else if (method.IsOverride) modifiers.Add("override");
        else if (method.IsAbstract) modifiers.Add("abstract");
        else if (method.IsVirtual) modifiers.Add("virtual");
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
        var mods = modifiers.Count > 0 ? string.Join(" ", modifiers) + " " : "";
        var tags = FormatTags(method.IsInherited, method.Attributes);
        return $"  {accessibility} {mods}{method.ReturnType} {method.Name}({parameters}){tags}";
    }

    private static string FormatProperty(PropertyInfo prop)
    {
        var accessibility = prop.Accessibility.ToString().ToLower();
        var getter = prop.HasGetter ? "get;" : "";
        var setter = prop.HasSetter ? "set;" : "";
        var tags = FormatTags(prop.IsInherited, prop.Attributes);
        return $"  {accessibility} {prop.Type} {prop.Name} {{ {getter} {setter} }}{tags}";
    }

    private static string FormatField(FieldInfo field)
    {
        var accessibility = field.Accessibility.ToString().ToLower();
        var modifiers = field.IsStatic ? "static " : "";
        var tags = FormatTags(field.IsInherited, field.Attributes);
        return $"  {accessibility} {modifiers}{field.Type} {field.Name}{tags}";
    }

    private static string FormatEvent(EventInfo evt)
    {
        var accessibility = evt.Accessibility.ToString().ToLower();
        var tags = FormatTags(evt.IsInherited, evt.Attributes);
        return $"  {accessibility} event {evt.Type} {evt.Name}{tags}";
    }

    private static string FormatTags(bool isInherited, IReadOnlyList<string> attributes)
    {
        var tags = new List<string>();
        if (isInherited) tags.Add("[inherited]");
        if (attributes.Count > 0) tags.Add($"[{string.Join(", ", attributes)}]");
        return tags.Count > 0 ? "  " + string.Join(" ", tags) : "";
    }
}
