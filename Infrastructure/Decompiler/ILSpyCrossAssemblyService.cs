using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Infrastructure.Decompiler;

/// <summary>
/// Adapter that implements ICrossAssemblyService using PEFile for directory scanning
/// and CSharpDecompiler type system for type resolution across assemblies.
/// </summary>
public sealed class ILSpyCrossAssemblyService : ICrossAssemblyService
{
    private readonly ILogger<ILSpyCrossAssemblyService> _logger;
    private readonly DecompilerSettings _settings;

    public ILSpyCrossAssemblyService(ILogger<ILSpyCrossAssemblyService> logger)
    {
        _logger = logger;
        _settings = new DecompilerSettings
        {
            ThrowOnAssemblyResolveErrors = false,
            ShowXmlDocumentation = false
        };
    }

    public async Task<DirectoryLoadResult> LoadAssemblyDirectoryAsync(
        DirectoryPath directoryPath,
        int maxDepth = 3,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var loaded = new List<AssemblyDirectoryEntry>();
            var skipped = new List<SkippedAssemblyEntry>();

            foreach (var file in EnumerateAssemblyFiles(directoryPath.Value, maxDepth))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var peFile = new PEFile(file);
                    var reader = peFile.Metadata;
                    var assemblyDef = reader.GetAssemblyDefinition();
                    var name = reader.GetString(assemblyDef.Name);
                    var version = assemblyDef.Version.ToString();

                    loaded.Add(new AssemblyDirectoryEntry
                    {
                        FilePath = file,
                        AssemblyName = name,
                        Version = version
                    });
                }
                catch (MetadataFileNotSupportedException)
                {
                    skipped.Add(new SkippedAssemblyEntry
                    {
                        FilePath = file,
                        Reason = "Not a .NET assembly"
                    });
                }
                catch (BadImageFormatException)
                {
                    skipped.Add(new SkippedAssemblyEntry
                    {
                        FilePath = file,
                        Reason = "Corrupt or invalid PE file"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load assembly: {File}", file);
                    skipped.Add(new SkippedAssemblyEntry
                    {
                        FilePath = file,
                        Reason = ex.Message
                    });
                }
            }

            return new DirectoryLoadResult
            {
                LoadedAssemblies = loaded,
                SkippedFiles = skipped,
                TotalFiles = loaded.Count + skipped.Count
            };
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<TypeResolutionResult>> ResolveTypeAsync(
        DirectoryPath directoryPath,
        string typeName,
        int maxDepth = 3,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var results = new List<TypeResolutionResult>();

            foreach (var file in EnumerateAssemblyFiles(directoryPath.Value, maxDepth))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var decompiler = new CSharpDecompiler(file, _settings);
                    var assemblyName = decompiler.TypeSystem.MainModule.AssemblyName;

                    foreach (var type in decompiler.TypeSystem.MainModule.TypeDefinitions)
                    {
                        if (type.FullName.Contains(typeName, StringComparison.OrdinalIgnoreCase) ||
                            type.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(new TypeResolutionResult
                            {
                                AssemblyPath = file,
                                AssemblyName = assemblyName,
                                TypeFullName = type.FullName,
                                TypeShortName = type.Name
                            });
                        }
                    }
                }
                catch (MetadataFileNotSupportedException)
                {
                    _logger.LogDebug("Skipping non-.NET assembly: {File}", file);
                }
                catch (BadImageFormatException)
                {
                    _logger.LogDebug("Skipping corrupt/invalid PE file: {File}", file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load assembly for type resolution: {File}", file);
                }
            }

            return results;
        }, cancellationToken);
    }

    /// <summary>
    /// Depth-limited enumeration of .dll and .exe files in a directory tree.
    /// Does NOT use SearchOption.AllDirectories to avoid scanning huge directory trees.
    /// </summary>
    private static IEnumerable<string> EnumerateAssemblyFiles(string root, int maxDepth)
    {
        foreach (var file in Directory.EnumerateFiles(root))
        {
            var ext = Path.GetExtension(file);
            if (ext.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                yield return file;
        }

        if (maxDepth > 0)
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                foreach (var file in EnumerateAssemblyFiles(dir, maxDepth - 1))
                    yield return file;
            }
        }
    }
}
