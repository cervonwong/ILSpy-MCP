namespace ILSpy.Mcp.Domain.Models;

/// <summary>
/// Information about an assembly reference (dependency).
/// </summary>
public sealed record AssemblyReferenceInfo
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Culture { get; init; }
    public string? PublicKeyToken { get; init; }
}
