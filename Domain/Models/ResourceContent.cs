namespace ILSpy.Mcp.Domain.Models;

/// <summary>
/// Content of an embedded resource with optional pagination.
/// Per D-08: text inline, binary base64, with offset/limit pagination.
/// </summary>
public sealed record ResourceContent
{
    public required string Name { get; init; }
    public required string ContentType { get; init; }    // "text", "binary"
    public required string Content { get; init; }        // text or base64
    public long TotalSize { get; init; }
    public int? Offset { get; init; }
    public int? Length { get; init; }
}
