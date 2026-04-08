namespace ILSpy.Mcp.Domain.Models;

/// <summary>
/// Catalog entry for an embedded resource in an assembly.
/// Per D-07: type + size for catalog listing.
/// </summary>
public sealed record ResourceInfo
{
    public required string Name { get; init; }
    public required long Size { get; init; }
    public required string ResourceType { get; init; }  // "Embedded", "Linked"
    public bool IsPublic { get; init; }
}
