using ILSpy.Mcp.Domain.Models;

namespace ILSpy.Mcp.Domain.Services;

/// <summary>
/// Port for cross-assembly analysis operations. Abstracts directory scanning,
/// assembly loading, and type resolution across multiple assemblies.
/// </summary>
public interface ICrossAssemblyService
{
    /// <summary>
    /// Resolves which assemblies in a directory define a given type name.
    /// Supports partial matching (short name or substring of full name).
    /// Returns all matching assemblies when multiple define the type.
    /// </summary>
    Task<IReadOnlyList<TypeResolutionResult>> ResolveTypeAsync(
        DirectoryPath directoryPath,
        string typeName,
        int maxDepth = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads and lists all .NET assemblies found in a directory, scanning
    /// .dll and .exe files recursively up to the specified depth.
    /// </summary>
    Task<DirectoryLoadResult> LoadAssemblyDirectoryAsync(
        DirectoryPath directoryPath,
        int maxDepth = 3,
        CancellationToken cancellationToken = default);
}
