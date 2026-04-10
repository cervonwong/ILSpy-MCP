namespace ILSpy.Mcp.Domain.Models;

/// <summary>
/// Paginated search results wrapper.
/// </summary>
public sealed record SearchResults<T>
{
    /// <summary>The result items for the current page.</summary>
    public required IReadOnlyList<T> Results { get; init; }

    /// <summary>Total number of matches across all pages.</summary>
    public int TotalCount { get; init; }

    /// <summary>Number of results skipped (pagination offset).</summary>
    public int Offset { get; init; }

    /// <summary>Maximum number of results per page.</summary>
    public int Limit { get; init; }
}

/// <summary>
/// A string literal found via ldstr IL instruction scanning.
/// </summary>
public sealed record StringSearchResult
{
    /// <summary>The matched string literal value.</summary>
    public required string MatchedValue { get; init; }

    /// <summary>Full name of the type containing the method with this string.</summary>
    public required string DeclaringType { get; init; }

    /// <summary>Name of the method containing the ldstr instruction.</summary>
    public required string MethodName { get; init; }

    /// <summary>Full signature of the containing method.</summary>
    public string? MethodSignature { get; init; }

    /// <summary>IL offset of the ldstr instruction.</summary>
    public int ILOffset { get; init; }
}

/// <summary>
/// A numeric constant found via ldc.i4/ldc.i8 IL instruction scanning.
/// </summary>
public sealed record ConstantSearchResult
{
    /// <summary>The matched constant value.</summary>
    public required long MatchedValue { get; init; }

    /// <summary>The IL type of the constant ("Int32" or "Int64").</summary>
    public required string ConstantType { get; init; }

    /// <summary>Full name of the type containing the method with this constant.</summary>
    public required string DeclaringType { get; init; }

    /// <summary>Name of the method containing the ldc instruction.</summary>
    public required string MethodName { get; init; }

    /// <summary>Full signature of the containing method.</summary>
    public string? MethodSignature { get; init; }

    /// <summary>IL offset of the ldc instruction.</summary>
    public int ILOffset { get; init; }
}
