namespace ILSpy.Mcp.Domain.Models;

/// <summary>
/// Assembly metadata including PE header info and referenced assemblies.
/// </summary>
public sealed record AssemblyMetadata
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? TargetFramework { get; init; }
    public string? RuntimeVersion { get; init; }
    public required string PEKind { get; init; }        // "x86", "x64", "AnyCPU", "ARM64"
    public string? StrongName { get; init; }
    public string? EntryPoint { get; init; }
    public string? Culture { get; init; }
    public string? PublicKeyToken { get; init; }
    public required IReadOnlyList<AssemblyReferenceInfo> References { get; init; }
}
