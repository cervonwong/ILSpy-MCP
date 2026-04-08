namespace ILSpy.Mcp.Domain.Models;

/// <summary>
/// Result of resolving which assembly defines a given type name.
/// </summary>
public sealed record TypeResolutionResult
{
    /// <summary>Full file path to the assembly containing the type.</summary>
    public required string AssemblyPath { get; init; }

    /// <summary>Assembly name (e.g., "System.Runtime").</summary>
    public required string AssemblyName { get; init; }

    /// <summary>Fully qualified type name (e.g., "System.String").</summary>
    public required string TypeFullName { get; init; }

    /// <summary>Short type name (e.g., "String").</summary>
    public required string TypeShortName { get; init; }
}

/// <summary>
/// Result of loading and listing all assemblies in a directory.
/// </summary>
public sealed record DirectoryLoadResult
{
    /// <summary>Assemblies that were successfully loaded.</summary>
    public required IReadOnlyList<AssemblyDirectoryEntry> LoadedAssemblies { get; init; }

    /// <summary>Files that could not be loaded (native DLLs, corrupt files, etc.).</summary>
    public required IReadOnlyList<SkippedAssemblyEntry> SkippedFiles { get; init; }

    /// <summary>Total number of .dll/.exe files found.</summary>
    public int TotalFiles { get; init; }
}

/// <summary>
/// Information about a successfully loaded assembly in a directory scan.
/// </summary>
public sealed record AssemblyDirectoryEntry
{
    /// <summary>Full file path to the assembly.</summary>
    public required string FilePath { get; init; }

    /// <summary>Assembly name.</summary>
    public required string AssemblyName { get; init; }

    /// <summary>Assembly version string.</summary>
    public required string Version { get; init; }
}

/// <summary>
/// Information about a file that was skipped during directory scanning.
/// </summary>
public sealed record SkippedAssemblyEntry
{
    /// <summary>Full file path to the skipped file.</summary>
    public required string FilePath { get; init; }

    /// <summary>Reason the file was skipped.</summary>
    public required string Reason { get; init; }
}
