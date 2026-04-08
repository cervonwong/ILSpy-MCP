namespace ILSpy.Mcp.Domain.Models;

/// <summary>
/// Information about a custom attribute declared on an assembly, type, or member.
/// Per D-05: declared only, no inheritance traversal.
/// </summary>
public sealed record AttributeInfo
{
    public required string AttributeType { get; init; }
    public IReadOnlyList<string> ConstructorArguments { get; init; } = [];
    public IReadOnlyDictionary<string, string> NamedArguments { get; init; } = new Dictionary<string, string>();
}
