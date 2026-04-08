namespace ILSpy.Mcp.Domain.Models;

/// <summary>
/// Information about a compiler-generated type with parent context.
/// Per D-09, D-10: dedicated discovery with parent method/type context.
/// </summary>
public sealed record CompilerGeneratedTypeInfo
{
    public required string FullName { get; init; }
    public required string ShortName { get; init; }
    public required string GeneratedKind { get; init; }  // "DisplayClass", "AsyncStateMachine", "Iterator", "Closure", "CompilerGenerated"
    public string? ParentMethod { get; init; }
    public string? ParentType { get; init; }
}
