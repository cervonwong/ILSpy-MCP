namespace ILSpy.Mcp.Domain.Models;

/// <summary>
/// Result of a project export operation.
/// </summary>
public sealed record ProjectExportResult
{
    /// <summary>
    /// The output directory where the project was exported.
    /// </summary>
    public string OutputDirectory { get; init; }

    /// <summary>
    /// Path to the .csproj file, relative to the output directory.
    /// </summary>
    public string CsprojPath { get; init; }

    /// <summary>
    /// Relative paths of all .cs source files in the exported project.
    /// </summary>
    public IReadOnlyList<string> SourceFiles { get; init; }

    /// <summary>
    /// Total number of files written to the output directory.
    /// </summary>
    public int TotalFileCount { get; init; }

    /// <summary>
    /// Warnings for partial failures (e.g., types that failed to decompile).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; }

    public ProjectExportResult(
        string outputDirectory,
        string csprojPath,
        IReadOnlyList<string> sourceFiles,
        int totalFileCount,
        IReadOnlyList<string> warnings)
    {
        OutputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
        CsprojPath = csprojPath ?? throw new ArgumentNullException(nameof(csprojPath));
        SourceFiles = sourceFiles ?? throw new ArgumentNullException(nameof(sourceFiles));
        TotalFileCount = totalFileCount;
        Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));
    }
}
