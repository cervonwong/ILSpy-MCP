# Phase 7: Bulk Operations & Documentation - Research

**Researched:** 2026-04-08
**Domain:** Bulk decompilation (namespace listing, project export), README documentation
**Confidence:** HIGH

## Summary

Phase 7 adds two new MCP tools (`decompile_namespace` and `export_project`) and rewrites the README to document all 28 tools. The namespace tool is a summary listing built on existing `IDecompilerService.ListTypesAsync` and `GetTypeInfoAsync` APIs -- no new decompiler APIs needed. The project export tool wraps ICSharpCode.Decompiler's `WholeProjectDecompiler`, which handles all the complexity of writing .csproj + .cs files to disk. The README rewrite is a documentation task with specific formatting requirements (disclosure accordions, tool categories).

The existing codebase provides strong patterns for both tools. All 26 existing tools follow the same 4-layer architecture (Domain interface -> Infrastructure impl -> Application use case -> Transport tool). The namespace tool reuses existing service methods; the export tool requires a new service method wrapping `WholeProjectDecompiler`. Both tools follow established error handling, timeout, and concurrency patterns.

**Primary recommendation:** Build `decompile_namespace` first (simpler, reuses existing APIs), then `export_project` (requires WholeProjectDecompiler integration), then README last (needs final tool count and descriptions).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** `decompile_namespace` returns a summary listing, not concatenated source. Lists all types with full signatures (type name, kind, base type, member count, public method signatures). User calls `decompile_type` individually for full source.
- **D-02:** Include nested types in the listing, indented under their parent type.
- **D-03:** Types ordered by kind (interfaces, enums, structs, classes) then alphabetically within each group.
- **D-04:** Return NAMESPACE_NOT_FOUND error when the namespace doesn't exist, with suggestion to use `list_namespaces`. Consistent with TYPE_NOT_FOUND pattern.
- **D-05:** Timeout via existing TimeoutService + explicit `max_types` parameter to bound work.
- **D-06:** `export_project` uses `WholeProjectDecompiler` to write .csproj + .cs files to disk.
- **D-07:** Returns file listing + stats: output directory path, total file count, list of generated .cs file paths (relative to output dir), and .csproj path.
- **D-08:** `output_directory` is a required parameter. Tool auto-creates the directory if it doesn't exist.
- **D-09:** Fail if output directory is non-empty -- return an error. User must specify an empty or non-existent directory.
- **D-10:** Optional `namespace` parameter to limit export scope to a specific namespace. Full assembly export by default.
- **D-11:** Partial failure handling: continue exporting when individual types fail, include list of failed types in response. Matches Phase 6 skip-with-warning pattern.
- **D-12:** Timeout via existing TimeoutService + explicit `max_types` parameter.
- **D-13:** README focuses on installation, configuration, and tool reference only. No architecture section.
- **D-14:** Tools grouped by category: Decompilation, IL Disassembly, Type Analysis, Cross-References, Assembly Inspection, Search, Bulk Operations.
- **D-15:** Every tool gets a usage example showing JSON input + trimmed output snippet.
- **D-16:** Examples wrapped in HTML `<details>` disclosure accordions.

### Claude's Discretion
- Exact `max_types` default value for both tools
- WholeProjectDecompiler configuration and namespace filtering implementation
- Domain model types for namespace listing results and project export results
- README category groupings for the 28 tools (suggested grouping above, Claude finalizes)
- How nested types are visually distinguished in namespace listing output

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| BULK-01 | User can decompile all types in a namespace at once | Existing `ListTypesAsync(namespaceFilter)` provides type enumeration. `GetTypeInfoAsync` provides full signatures. New `DecompileNamespaceUseCase` formats summary listing with kind ordering and nested type indentation. |
| BULK-02 | User can export a complete .csproj with all decompiled source files to disk | `WholeProjectDecompiler.DecompileProject(MetadataFile, targetDirectory, CancellationToken)` handles full export. Requires `UniversalAssemblyResolver` and `PEFile` for input. New service method + use case + tool. |
| DOC-01 | README.md updated with all new tools, usage examples, and current feature list | 28 tools total (26 existing + 2 new). Existing README has tool table but no JSON examples. Rewrite with category grouping, disclosure accordions, JSON input/output examples. |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ICSharpCode.Decompiler | 10.0.0.8330 | WholeProjectDecompiler for project export | Already installed. Provides `WholeProjectDecompiler`, `UniversalAssemblyResolver`, `PEFile` |
| System.Reflection.Metadata | (transitive) | MetadataReader for namespace validation | Already available transitively |

### Supporting
No new dependencies needed. All functionality comes from existing packages.

## Architecture Patterns

### Recommended Project Structure (new files only)
```
Domain/
  Models/
    NamespaceTypeSummary.cs     # Result model for namespace listing
    ProjectExportResult.cs      # Result model for project export
Application/
  UseCases/
    DecompileNamespaceUseCase.cs
    ExportProjectUseCase.cs
Infrastructure/
  Decompiler/
    ILSpyDecompilerService.cs   # Add ExportProjectAsync method (or new IBulkDecompilerService)
Transport/
  Mcp/
    Tools/
      DecompileNamespaceTool.cs
      ExportProjectTool.cs
Tests/
  Tools/
    DecompileNamespaceToolTests.cs
    ExportProjectToolTests.cs
```

### Pattern 1: Namespace Summary Listing
**What:** `decompile_namespace` enumerates types in a namespace and returns a formatted summary with full signatures.
**When to use:** When the user wants an overview of what's in a namespace before drilling into specific types.
**Implementation approach:**

1. Use `IDecompilerService.ListTypesAsync(assemblyPath, namespaceFilter)` to get all types
2. **Critical:** The existing `ListTypesAsync` uses `Contains` matching, not exact matching. For namespace decompilation, we need exact namespace match. Either add a new method or filter results post-query.
3. Filter to exact namespace match (e.g., `ILSpy.Mcp.TestTargets.Shapes` should not match `ILSpy.Mcp.TestTargets.Shapes.Sub`)
4. Sort by kind order: interfaces -> enums -> structs -> classes (per D-03)
5. For each type, format: kind, full name, base type, member counts, public method signatures
6. Indent nested types under parent (per D-02)
7. Validate namespace exists -- if no types found, throw NAMESPACE_NOT_FOUND

```csharp
// Namespace listing output format (recommended)
// Namespace: ILSpy.Mcp.TestTargets.Shapes (5 types)
//
// Interfaces:
//   interface IShape
//     Methods: 1 | Properties: 0 | Fields: 0
//     double CalculateArea()
//
// Classes:
//   class Circle : Shape
//     Methods: 2 | Properties: 1 | Fields: 1
//     double CalculateArea()
//     string ToString()
//     Nested types:
//       class CircleComparer
```

### Pattern 2: WholeProjectDecompiler Integration
**What:** `export_project` wraps `WholeProjectDecompiler` to write a complete .csproj + .cs files to disk.
**When to use:** Full codebase recovery from a compiled assembly.
**Implementation approach:**

```csharp
// Source: ICSharpCode.Decompiler WholeProjectDecompiler API (GitHub)
// Key steps:
// 1. Create PEFile from assembly path
// 2. Create UniversalAssemblyResolver
// 3. Create WholeProjectDecompiler with resolver
// 4. Call DecompileProject(peFile, targetDirectory, cancellationToken)
// 5. Enumerate output directory for file listing

using var peFile = new PEFile(assemblyPath.Value);
var resolver = new UniversalAssemblyResolver(
    assemblyPath.Value,
    throwOnError: false,
    targetFramework: null);

var decompiler = new WholeProjectDecompiler(
    settings,        // DecompilerSettings
    resolver,        // IAssemblyResolver
    null,            // IProjectFileWriter (null = default)
    null,            // AssemblyReferenceClassifier (null = default)
    null);           // IDebugInfoProvider (null = default)

decompiler.Settings.ThrowOnAssemblyResolveErrors = false;
decompiler.DecompileProject(peFile, outputDirectory, cancellationToken);
```

### Pattern 3: README Documentation Structure
**What:** Tool reference with JSON examples in disclosure accordions.
**Format per tool:**

```markdown
### `tool_name`
Brief description.

<details>
<summary>Example</summary>

**Input:**
```json
{
  "assemblyPath": "C:\\path\\to\\assembly.dll",
  "typeName": "MyNamespace.MyType"
}
```

**Output (trimmed):**
```
[truncated output sample]
```

</details>
```

### Anti-Patterns to Avoid
- **Concatenating full decompiled source for namespace:** D-01 explicitly says summary listing, not concatenated source. The architecture document originally suggested concatenation but the user decided against it.
- **Using `ListTypesAsync` Contains matching for exact namespace:** The existing method uses `Contains` which matches sub-namespaces. Must filter to exact match or add a new overload.
- **Inlining WholeProjectDecompiler in the tool class:** Keep the 4-layer separation. The Infrastructure service wraps WholeProjectDecompiler, the use case orchestrates, the tool handles MCP concerns.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Project export (.csproj + .cs files) | Custom file-by-file decompilation + project file generation | `WholeProjectDecompiler.DecompileProject()` | Handles SDK-style vs old-style .csproj, assembly references, resource copying, namespace-to-folder mapping, parallel decompilation. Hundreds of edge cases. |
| Assembly reference resolution during export | Manual assembly probing | `UniversalAssemblyResolver` | Handles .NET Core runtime packs, NuGet cache, GAC, framework reference assemblies. Complex resolution logic. |
| Type signature formatting | Custom reflection-based signature builder | Existing `TypeInfo` model from `MapToTypeInfo` | Already maps kind, accessibility, methods, properties, fields, events, base types, interfaces. |

## Common Pitfalls

### Pitfall 1: Namespace Contains vs Exact Match
**What goes wrong:** `ListTypesAsync` uses `namespace.Contains(filter)` which matches `ILSpy.Mcp.TestTargets` when filtering for `ILSpy.Mcp.TestTargets.Shapes` and vice versa.
**Why it happens:** The existing method was designed for broad search, not exact namespace matching.
**How to avoid:** Post-filter results to exact namespace match: `type.Namespace == targetNamespace`. Or add a new `ListTypesByNamespaceAsync` method with exact matching.
**Warning signs:** Types from parent or child namespaces appearing in results.

### Pitfall 2: WholeProjectDecompiler Constructor Requires IAssemblyResolver
**What goes wrong:** Attempting to create `WholeProjectDecompiler` without a resolver causes NullReferenceException during decompilation when referenced types can't be resolved.
**Why it happens:** The minimal constructor `WholeProjectDecompiler(IAssemblyResolver)` requires a non-null resolver.
**How to avoid:** Always create a `UniversalAssemblyResolver` with the assembly's path so it can find referenced assemblies in the same directory.
**Warning signs:** NullReferenceException or unresolved type errors during export.

### Pitfall 3: WholeProjectDecompiler Namespace Filtering Not Built-In
**What goes wrong:** D-10 requires optional namespace filtering for export, but `WholeProjectDecompiler` exports the entire assembly.
**Why it happens:** The API has an `IncludeTypeWhenDecompilingProject` virtual method but it's designed for internal filtering (XAML namespaces), not user-facing namespace filtering.
**How to avoid:** Two approaches: (a) Subclass `WholeProjectDecompiler` and override `IncludeTypeWhenDecompilingProject` to filter by namespace, or (b) Do a full export then delete files outside the namespace (wasteful). Subclassing is cleaner.
**Warning signs:** Exported project contains types from all namespaces when user requested a specific one.

### Pitfall 4: Non-Empty Output Directory
**What goes wrong:** D-09 requires failing on non-empty directories, but the directory might have hidden files or system files.
**Why it happens:** `Directory.GetFileSystemEntries()` includes hidden files on some platforms.
**How to avoid:** Use `Directory.EnumerateFileSystemEntries(dir).Any()` for the check. This is the correct behavior -- any existing content means non-empty.
**Warning signs:** Export succeeds when it should have failed, or fails on seemingly empty directories.

### Pitfall 5: PEFile Disposal
**What goes wrong:** `PEFile` implements `IDisposable` (holds file handles). Not disposing it causes file locks.
**Why it happens:** WholeProjectDecompiler uses the PEFile during decompilation but doesn't own its lifetime.
**How to avoid:** Wrap `PEFile` creation in a `using` statement. Ensure the PEFile outlives the `DecompileProject` call (it needs to remain open during the entire export).
**Warning signs:** File lock errors on subsequent operations, especially in tests.

### Pitfall 6: README Tool Count
**What goes wrong:** Documenting wrong number of tools or missing newly added tools.
**Why it happens:** Tools are auto-discovered by `WithToolsFromAssembly()` but README is manually written.
**How to avoid:** Count tool files in `Transport/Mcp/Tools/` directory. Currently 26 files. After this phase: 28 files. Verify count matches README.
**Warning signs:** README says "28 tools" but lists 27, or has duplicate entries.

## Code Examples

### Creating WholeProjectDecompiler
```csharp
// Source: ICSharpCode.Decompiler GitHub + existing project patterns
public async Task<ProjectExportResult> ExportProjectAsync(
    AssemblyPath assemblyPath,
    string outputDirectory,
    string? namespaceFilter = null,
    CancellationToken cancellationToken = default)
{
    return await Task.Run(() =>
    {
        using var peFile = new PEFile(assemblyPath.Value);
        var resolver = new UniversalAssemblyResolver(
            assemblyPath.Value,
            throwOnError: false,
            targetFramework: null);

        var settings = new DecompilerSettings
        {
            ThrowOnAssemblyResolveErrors = false,
            ShowXmlDocumentation = false
        };

        var decompiler = namespaceFilter != null
            ? new NamespaceFilteredProjectDecompiler(namespaceFilter, resolver)
            : new WholeProjectDecompiler(resolver);

        decompiler.Settings.ThrowOnAssemblyResolveErrors = false;
        Directory.CreateDirectory(outputDirectory);
        decompiler.DecompileProject(peFile, outputDirectory, cancellationToken);

        // Enumerate output files
        var files = Directory.EnumerateFiles(outputDirectory, "*.*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(outputDirectory, f))
            .ToList();

        return new ProjectExportResult { /* ... */ };
    }, cancellationToken);
}
```

### Namespace Filtering via Subclass
```csharp
// Override IncludeTypeWhenDecompilingProject for namespace filtering
internal class NamespaceFilteredProjectDecompiler : WholeProjectDecompiler
{
    private readonly string _namespace;

    public NamespaceFilteredProjectDecompiler(string ns, IAssemblyResolver resolver)
        : base(resolver)
    {
        _namespace = ns;
    }

    // Note: verify this virtual method exists in v10.0 and its exact signature
    // It may need to be implemented differently
}
```

### Namespace Summary Formatting
```csharp
// Ordering by kind (D-03)
var kindOrder = new Dictionary<TypeKind, int>
{
    [TypeKind.Interface] = 0,
    [TypeKind.Enum] = 1,
    [TypeKind.Struct] = 2,
    [TypeKind.Class] = 3,
    [TypeKind.Delegate] = 4,
    [TypeKind.Unknown] = 5,
};

var ordered = types
    .OrderBy(t => kindOrder.GetValueOrDefault(t.Kind, 99))
    .ThenBy(t => t.FullName);
```

### Directory Validation for Export
```csharp
// D-08: Auto-create if doesn't exist
// D-09: Fail if non-empty
if (Directory.Exists(outputDirectory))
{
    if (Directory.EnumerateFileSystemEntries(outputDirectory).Any())
        throw new McpToolException("DIRECTORY_NOT_EMPTY",
            $"Output directory is not empty: {outputDirectory}. Specify an empty or non-existent directory.");
}
else
{
    Directory.CreateDirectory(outputDirectory);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Concatenate all decompiled source | Summary listing with drill-down | D-01 (this phase) | Much better for AI workflows -- AI reads TOC then selects specific types |
| Custom project file generation | WholeProjectDecompiler (ICSharpCode.Decompiler) | Built-in since ILSpy 5+ | Handles all .csproj complexity automatically |

## Open Questions

1. **WholeProjectDecompiler `IncludeTypeWhenDecompilingProject` method signature in v10.0**
   - What we know: This method exists in the current master branch as a virtual method for filtering types during export
   - What's unclear: Exact signature and whether it's public/protected in 10.0.0.8330. The method may have changed between versions.
   - Recommendation: Verify at implementation time. If subclassing doesn't work, fall back to full export + post-filtering (delete unwanted files). Mark namespace filtering as best-effort.

2. **`max_types` default value**
   - What we know: User left this to Claude's discretion for both tools
   - Recommendation: Default to 200 for `decompile_namespace` (summary is compact) and 500 for `export_project` (file writes are fast). These can be adjusted based on testing.

3. **Error handling for WholeProjectDecompiler partial failures**
   - What we know: D-11 requires skip-with-warning for failed types. WholeProjectDecompiler has a `ProgressIndicator` property.
   - What's unclear: Whether WholeProjectDecompiler surfaces individual type failures or just throws on fatal errors.
   - Recommendation: Wrap in try/catch and report what was written. If the API doesn't support per-type error reporting, catch the overall exception and report partial results from whatever was written to disk.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + FluentAssertions 8.9.0 |
| Config file | Tests project references |
| Quick run command | `dotnet test --filter "FullyQualifiedName~DecompileNamespace"` |
| Full suite command | `dotnet test` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| BULK-01 | Namespace summary listing returns types with signatures | integration | `dotnet test --filter "FullyQualifiedName~DecompileNamespaceToolTests" -x` | Wave 0 |
| BULK-01 | Types ordered by kind then alphabetically | integration | `dotnet test --filter "FullyQualifiedName~DecompileNamespaceToolTests" -x` | Wave 0 |
| BULK-01 | Nested types indented under parent | integration | `dotnet test --filter "FullyQualifiedName~DecompileNamespaceToolTests" -x` | Wave 0 |
| BULK-01 | NAMESPACE_NOT_FOUND error for invalid namespace | integration | `dotnet test --filter "FullyQualifiedName~DecompileNamespaceToolTests" -x` | Wave 0 |
| BULK-02 | Exports .csproj + .cs files to disk | integration | `dotnet test --filter "FullyQualifiedName~ExportProjectToolTests" -x` | Wave 0 |
| BULK-02 | Returns file listing with relative paths | integration | `dotnet test --filter "FullyQualifiedName~ExportProjectToolTests" -x` | Wave 0 |
| BULK-02 | Fails on non-empty directory | integration | `dotnet test --filter "FullyQualifiedName~ExportProjectToolTests" -x` | Wave 0 |
| BULK-02 | Creates directory if not exists | integration | `dotnet test --filter "FullyQualifiedName~ExportProjectToolTests" -x` | Wave 0 |
| DOC-01 | README documents all 28 tools | manual-only | Visual review | N/A |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~DecompileNamespace or FullyQualifiedName~ExportProject"`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `Tests/Tools/DecompileNamespaceToolTests.cs` -- covers BULK-01
- [ ] `Tests/Tools/ExportProjectToolTests.cs` -- covers BULK-02 (needs temp directory cleanup in tests)
- [ ] Test fixture update: add new use cases and tools to `ToolTestFixture.cs` DI registration

## Sources

### Primary (HIGH confidence)
- ICSharpCode.Decompiler GitHub - WholeProjectDecompiler.cs source (constructor, DecompileProject API, IncludeTypeWhenDecompilingProject)
- ICSharpCode.Decompiler GitHub - UniversalAssemblyResolver.cs (constructor signature, usage pattern)
- Existing codebase - ILSpyDecompilerService.cs (CSharpDecompiler patterns, DecompilerSettings, PEFile usage)
- Existing codebase - All 26 existing tool/use case files (established 4-layer pattern)
- Existing codebase - ToolTestFixture.cs (DI registration pattern, test scope pattern)

### Secondary (MEDIUM confidence)
- CLAUDE.md technology stack section - WholeProjectDecompiler API details and breaking changes

### Tertiary (LOW confidence)
- WholeProjectDecompiler namespace filtering via subclass - needs verification at implementation time that `IncludeTypeWhenDecompilingProject` is overridable in v10.0.0.8330

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - No new dependencies, all existing packages
- Architecture: HIGH - Follows established 4-layer pattern exactly, 26 existing examples
- Namespace listing (BULK-01): HIGH - Built on existing `ListTypesAsync` + `GetTypeInfoAsync`
- Project export (BULK-02): MEDIUM - WholeProjectDecompiler API verified from GitHub source, but namespace filtering override needs implementation-time verification
- Documentation (DOC-01): HIGH - Straightforward README rewrite with known tool list
- Pitfalls: HIGH - Based on codebase analysis and API review

**Research date:** 2026-04-08
**Valid until:** 2026-05-08 (stable domain, no fast-moving dependencies)
