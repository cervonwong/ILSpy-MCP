using System.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Metadata;
using ILSpy.Mcp.Application.Pagination;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for exporting a .NET assembly as a complete C# project.
/// </summary>
public sealed class ExportProjectUseCase
{
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<ExportProjectUseCase> _logger;

    public ExportProjectUseCase(
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<ExportProjectUseCase> logger)
    {
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string outputDirectory,
        string? namespaceFilter = null,
        int maxTypes = 500,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);

            // Parameter validation
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Output directory path must not be null or whitespace.", nameof(outputDirectory));
            }

            // Canonicalize the path to resolve any relative segments
            outputDirectory = Path.GetFullPath(outputDirectory);

            // Directory validation (D-08, D-09)
            // CreateDirectory is a no-op if it already exists, eliminating the
            // TOCTOU race between existence check and creation. This is acceptable
            // for a single-user MCP tool.
            Directory.CreateDirectory(outputDirectory);

            if (Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            {
                throw new OutputDirectoryNotEmptyException(outputDirectory);
            }

            _logger.LogInformation("Exporting project from {Assembly} to {OutputDirectory}",
                assemblyPath, outputDirectory);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var warnings = new List<string>();
                int totalTypeCount = 0;

                await Task.Run(() =>
                {
                    using var peFile = new PEFile(assembly.Value);
                    totalTypeCount = peFile.Metadata.TypeDefinitions.Count;
                    var resolver = new UniversalAssemblyResolver(
                        assembly.Value,
                        throwOnError: false,
                        targetFramework: null);

                    WholeProjectDecompiler decompiler;

                    if (namespaceFilter != null)
                    {
                        try
                        {
                            decompiler = new NamespaceFilteredProjectDecompiler(namespaceFilter, resolver);
                        }
                        catch (Exception)
                        {
                            // Fallback: namespace filtering not supported
                            warnings.Add($"Namespace filtering is not supported for project export. Exporting all types.");
                            decompiler = new WholeProjectDecompiler(resolver);
                        }
                    }
                    else
                    {
                        decompiler = new WholeProjectDecompiler(resolver);
                    }

                    decompiler.Settings.ThrowOnAssemblyResolveErrors = false;

                    try
                    {
                        decompiler.DecompileProject(peFile, outputDirectory, timeout.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Partial export failure: {ex.Message}");
                        _logger.LogWarning(ex, "Partial failure during project export");
                    }
                }, timeout.Token);

                // Enumerate output files
                var csprojFile = Directory.GetFiles(outputDirectory, "*.csproj", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();

                var csFiles = Directory.GetFiles(outputDirectory, "*.cs", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(outputDirectory, f))
                    .OrderBy(f => f)
                    .ToList();

                var allFiles = Directory.GetFiles(outputDirectory, "*.*", SearchOption.AllDirectories);
                var csprojRelative = csprojFile != null
                    ? Path.GetRelativePath(outputDirectory, csprojFile)
                    : "(no .csproj generated)";

                var result = new ProjectExportResult(
                    outputDirectory,
                    csprojRelative,
                    csFiles,
                    allFiles.Length,
                    warnings);

                return FormatOutput(result, totalTypeCount, maxTypes);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for project export");
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for project export");
            throw new TimeoutException(
                $"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error exporting project from {Assembly}", assemblyPath);
            throw;
        }
    }

    private static string FormatOutput(ProjectExportResult result, int totalTypeCount, int maxTypes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Project exported to: {result.OutputDirectory}");
        sb.AppendLine($"Project file: {result.CsprojPath}");
        sb.AppendLine($"Total files: {result.TotalFileCount}");
        sb.AppendLine();
        sb.AppendLine("Source files:");

        foreach (var file in result.SourceFiles)
        {
            sb.AppendLine($"  {file}");
        }

        if (result.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Warnings:");
            foreach (var warning in result.Warnings)
            {
                sb.AppendLine($"  - {warning}");
            }
        }

        var exportedTypes = result.SourceFiles.Count;
        var truncated = totalTypeCount > maxTypes;
        // TrimEnd before appending footer so there's no trailing whitespace before it
        var trimmed = sb.ToString().TrimEnd();
        var footerSb = new StringBuilder(trimmed);
        TruncationEnvelope.AppendExportFooter(footerSb, totalTypeCount, exportedTypes, truncated);
        return footerSb.ToString();
    }
}

/// <summary>
/// WholeProjectDecompiler subclass that filters types by namespace.
/// </summary>
internal class NamespaceFilteredProjectDecompiler : WholeProjectDecompiler
{
    private readonly string _namespace;

    public NamespaceFilteredProjectDecompiler(string ns, IAssemblyResolver resolver)
        : base(resolver)
    {
        _namespace = ns;
    }

    protected override bool IncludeTypeWhenDecompilingProject(
        MetadataFile module,
        System.Reflection.Metadata.TypeDefinitionHandle type)
    {
        var reader = module.Metadata;
        var typeDef = reader.GetTypeDefinition(type);
        var ns = reader.GetString(typeDef.Namespace);
        return ns == _namespace || ns.StartsWith(_namespace + ".", StringComparison.Ordinal);
    }
}
