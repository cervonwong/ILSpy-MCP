using ILSpy.Mcp.Domain.Models;

namespace ILSpy.Mcp.Domain.Services;

/// <summary>
/// Port for IL disassembly operations. Abstracts the disassembler implementation.
/// </summary>
public interface IDisassemblyService
{
    /// <summary>
    /// Disassembles a type showing structure and method signatures only (no IL bodies).
    /// Per D-02: headers-only view for type-level output.
    /// </summary>
    Task<string> DisassembleTypeAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        bool showTokens = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disassembles a specific method with complete IL body.
    /// Per D-03: full IL with .maxstack, IL_xxxx labels, resolved names.
    /// </summary>
    Task<string> DisassembleMethodAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        string methodName,
        bool showBytes = false,
        bool showTokens = false,
        CancellationToken cancellationToken = default);
}
