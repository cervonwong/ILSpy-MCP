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
/// Use case for listing all types in a namespace with pagination support.
/// </summary>
public sealed class ListNamespaceTypesUseCase
{
    private readonly IDecompilerService _decompiler;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<ListNamespaceTypesUseCase> _logger;
    private readonly ILSpyOptions _options;

    private static readonly Dictionary<TypeKind, int> KindOrder = new()
    {
        [TypeKind.Interface] = 0,
        [TypeKind.Enum] = 1,
        [TypeKind.Struct] = 2,
        [TypeKind.Class] = 3,
        [TypeKind.Delegate] = 4,
        [TypeKind.Unknown] = 5,
    };

    public ListNamespaceTypesUseCase(
        IDecompilerService decompiler,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<ListNamespaceTypesUseCase> logger,
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
        string namespaceName,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);

            _logger.LogInformation("Listing types in namespace {Namespace} from {Assembly}",
                namespaceName, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);

                var allTypes = await _decompiler.ListTypesAsync(assembly, namespaceName, timeout.Token);

                // Post-filter to exact namespace match (ListTypesAsync uses Contains matching)
                var exactMatches = allTypes
                    .Where(t => t.Namespace == namespaceName)
                    .ToList();

                if (exactMatches.Count == 0)
                {
                    throw new NamespaceNotFoundException(namespaceName, assemblyPath);
                }

                // Separate top-level from nested types
                var nestedByParent = new Dictionary<string, List<TypeInfo>>();
                var topLevelTypes = new List<TypeInfo>();

                foreach (var type in exactMatches)
                {
                    if (type.DeclaringTypeFullName != null)
                    {
                        if (!nestedByParent.TryGetValue(type.DeclaringTypeFullName, out var nested))
                        {
                            nested = new List<TypeInfo>();
                            nestedByParent[type.DeclaringTypeFullName] = nested;
                        }
                        nested.Add(type);
                    }
                    else
                    {
                        topLevelTypes.Add(type);
                    }
                }

                // Sort top-level types by kind then alphabetically
                var allSorted = topLevelTypes
                    .OrderBy(t => KindOrder.GetValueOrDefault(t.Kind, 5))
                    .ThenBy(t => t.FullName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var totalTopLevelTypes = allSorted.Count;
                var page = allSorted
                    .Skip(offset)
                    .Take(maxResults)
                    .ToList();

                var entries = page.Select(t => BuildEntry(t, nestedByParent)).ToList();

                var summary = new NamespaceTypeSummary
                {
                    Namespace = namespaceName,
                    Types = entries,
                    TotalTypeCount = totalTopLevelTypes,  // top-level types only, not exactMatches.Count
                };

                var result = FormatOutput(summary, totalTopLevelTypes, offset, maxResults);
                if (result.Length > _options.MaxDecompilationSize)
                {
                    result = result[.._options.MaxDecompilationSize]
                        + $"\n\n[Output truncated at {_options.MaxDecompilationSize} bytes. The full output is {result.Length} bytes.]";
                }
                return result;
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for namespace {Namespace}", namespaceName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for namespace {Namespace}", namespaceName);
            throw new TimeoutException(
                $"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error listing namespace {Namespace}", namespaceName);
            throw;
        }
    }

    private static TypeSummaryEntry BuildEntry(TypeInfo type, Dictionary<string, List<TypeInfo>> nestedByParent)
    {
        var publicMethods = type.Methods
            .Where(m => m.Accessibility == Accessibility.Public)
            .Select(m => FormatMethodSignature(m))
            .ToList();

        IReadOnlyList<TypeSummaryEntry>? nestedEntries = null;
        if (nestedByParent.TryGetValue(type.FullName, out var nestedTypes))
        {
            nestedEntries = nestedTypes
                .OrderBy(n => KindOrder.GetValueOrDefault(n.Kind, 5))
                .ThenBy(n => n.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(n => BuildEntry(n, nestedByParent))
                .ToList();
        }

        return new TypeSummaryEntry
        {
            FullName = type.FullName,
            ShortName = type.ShortName,
            Kind = type.Kind,
            BaseType = type.BaseTypes.FirstOrDefault(),
            MethodCount = type.Methods.Count + type.Constructors.Count,
            PropertyCount = type.Properties.Count,
            FieldCount = type.Fields.Count,
            PublicMethodSignatures = publicMethods,
            NestedTypes = nestedEntries,
        };
    }

    private static string FormatMethodSignature(MethodInfo method)
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
        return $"{method.ReturnType} {method.Name}({parameters})";
    }

    private static string FormatOutput(
        NamespaceTypeSummary summary,
        int totalTopLevelTypes,
        int offset,
        int maxResults)
    {
        var sb = new StringBuilder();

        // Header — three branches:
        // 1. Namespace exists but zero top-level types (rare — nested-only namespace)
        // 2. Namespace exists, types exist, but current page is empty (offset >= total)
        // 3. Normal case
        var returned = summary.Types.Count;
        if (totalTopLevelTypes == 0)
        {
            sb.AppendLine($"Namespace: {summary.Namespace} (0 top-level types)");
        }
        else if (returned == 0)
        {
            sb.AppendLine($"Namespace: {summary.Namespace} ({totalTopLevelTypes} top-level types, offset {offset} is beyond last page)");
        }
        else
        {
            var rangeStart = offset + 1;
            var rangeEnd = offset + returned;
            sb.AppendLine($"Namespace: {summary.Namespace} ({totalTopLevelTypes} top-level types, showing {rangeStart}-{rangeEnd})");
        }

        // Body — existing grouping by kind, preserved verbatim
        var groups = summary.Types
            .GroupBy(t => t.Kind)
            .OrderBy(g => KindOrder.GetValueOrDefault(g.Key, 5));
        foreach (var group in groups)
        {
            sb.AppendLine();
            sb.AppendLine($"{GetKindGroupName(group.Key)}:");
            foreach (var entry in group)
            {
                WriteEntry(sb, entry, indent: "  ");
            }
        }

        // Footer — the parseable contract. ALWAYS present.
        PaginationEnvelope.AppendFooter(sb, totalTopLevelTypes, returned, offset);

        return sb.ToString();
    }

    private static void WriteEntry(StringBuilder sb, TypeSummaryEntry entry, string indent)
    {
        var kindLabel = GetKindLabel(entry.Kind);
        var baseInfo = entry.BaseType != null ? $" : {entry.BaseType}" : "";
        sb.AppendLine($"{indent}{kindLabel} {entry.ShortName}{baseInfo}");
        sb.AppendLine($"{indent}  Methods: {entry.MethodCount} | Properties: {entry.PropertyCount} | Fields: {entry.FieldCount}");

        foreach (var sig in entry.PublicMethodSignatures)
        {
            sb.AppendLine($"{indent}  {sig}");
        }

        if (entry.NestedTypes is { Count: > 0 })
        {
            sb.AppendLine($"{indent}  Nested types:");
            foreach (var nested in entry.NestedTypes)
            {
                WriteEntry(sb, nested, indent + "    ");
            }
        }
    }

    private static string GetKindGroupName(TypeKind kind) => kind switch
    {
        TypeKind.Interface => "Interfaces",
        TypeKind.Enum => "Enums",
        TypeKind.Struct => "Structs",
        TypeKind.Class => "Classes",
        TypeKind.Delegate => "Delegates",
        _ => "Other",
    };

    private static string GetKindLabel(TypeKind kind) => kind switch
    {
        TypeKind.Interface => "interface",
        TypeKind.Enum => "enum",
        TypeKind.Struct => "struct",
        TypeKind.Class => "class",
        TypeKind.Delegate => "delegate",
        _ => "type",
    };
}
