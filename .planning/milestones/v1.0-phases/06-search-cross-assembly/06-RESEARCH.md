# Phase 6: Search & Cross-Assembly - Research

**Researched:** 2026-04-08
**Domain:** IL bytecode scanning for string/constant search, multi-assembly directory loading and type resolution
**Confidence:** HIGH

## Summary

Phase 6 adds 4 new MCP tools: `search_strings` (SRCH-01), `search_constants` (SRCH-02), `resolve_type` (XASM-01), and `load_assembly_directory` (XASM-02). The search tools scan IL bytecode for `ldstr` and `ldc.*` instructions using the exact same `BlobReader`/`ILOpCode` pattern already proven in `ILSpyCrossReferenceService`. The cross-assembly tools introduce a new concern -- loading multiple assemblies from a directory and resolving types across them.

The codebase has strong, well-established patterns from Phases 3-5. Every new tool follows the 4-layer architecture: Domain interface -> Infrastructure implementation -> Application use case -> Transport MCP tool. The IL scanning infrastructure in `ILSpyCrossReferenceService` provides a complete reference for `ReadILOpCode`, `SkipOperand`, and `GetOperandSize` helpers that the search service will reuse or extract.

**Primary recommendation:** Split into 2 plans: Plan 1 covers search (ISearchService + 2 tools), Plan 2 covers cross-assembly (ICrossAssemblyService + 2 tools). Search reuses the existing IL scanning pattern directly. Cross-assembly introduces directory scanning as a genuinely new capability.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- D-01: 4 dedicated tools: `search_strings`, `search_constants`, `resolve_type`, `load_assembly_directory`
- D-02: Total tool count goes from 22 to 26
- D-03: New `ISearchService` domain interface for search tools
- D-04: New `ICrossAssemblyService` domain interface for cross-assembly tools
- D-05: IL scanning reuses BlobReader/ILOpCode pattern from ILSpyCrossReferenceService
- D-06: `search_strings` accepts regex pattern, scans `ldstr` operands
- D-07: `search_constants` accepts exact numeric value, finds `ldc.*` instructions
- D-08: Result context: matched value, containing type full name, method signature, IL offset
- D-09: Default result limit with offset for pagination, `max_results` override, total count shown
- D-10: Directory scanning is recursive with configurable depth limit
- D-11: File filter: `.dll` and `.exe` only
- D-12: Unloadable assemblies skipped with warning, skipped files listed in results
- D-13: `resolve_type` returns all matching assemblies when multiple matches exist
- D-14: `resolve_type` supports partial type name matching

### Claude's Discretion
- Exact domain model types for results
- Default result limit value and default directory scan depth
- Whether to extract shared IL scanning helpers into a common utility
- Infrastructure service internal organization
- TestTargets extensions for search and cross-assembly tests
- Error handling for edge cases

### Deferred Ideas (OUT OF SCOPE)
None
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SRCH-01 | Search for string literals (ldstr operands) matching regex across an assembly | IL scanning pattern from ILSpyCrossReferenceService; `Ldstr` is 4-byte token opcode referencing UserString heap; `MetadataReader.GetUserString(handle)` to read value |
| SRCH-02 | Search for numeric and enum constants across an assembly | `Ldc_i4`, `Ldc_i4_s`, `Ldc_i4_0`-`Ldc_i4_8`, `Ldc_i4_m1`, `Ldc_i8`, `Ldc_r4`, `Ldc_r8` opcodes; inline operand values read directly from BlobReader |
| XASM-01 | Resolve which assembly in a directory defines a given type | `Directory.EnumerateFiles` with recursive search + `PEFile`/`CSharpDecompiler` for type system access; partial name matching via string contains |
| XASM-02 | Load all assemblies from a directory for cross-assembly analysis | Directory enumeration, `MetadataFileNotSupportedException` catch for native DLLs, return summary with loaded/skipped counts |
</phase_requirements>

## Standard Stack

No new packages. All functionality uses existing dependencies per CLAUDE.md constraints.

### Core (existing)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ICSharpCode.Decompiler | 10.0.0.8330 | Assembly loading, type system, CSharpDecompiler | Already in project, provides MetadataFile and TypeSystem |
| System.Reflection.Metadata | transitive | BlobReader, ILOpCode, MetadataReader, UserString heap | IL scanning for ldstr/ldc opcodes |

### Do NOT Add
Per CLAUDE.md: No Mono.Cecil, no dnlib, no explicit System.Reflection.Metadata package.

## Architecture Patterns

### New Files for Phase 6

```
Domain/
  Models/
    SearchResult.cs              # StringSearchResult, ConstantSearchResult records
    CrossAssemblyResult.cs       # TypeResolutionResult, DirectoryLoadResult records
  Services/
    ISearchService.cs            # SearchStringsAsync, SearchConstantsAsync
    ICrossAssemblyService.cs     # ResolveTypeAsync, LoadDirectoryAsync

Infrastructure/
  Decompiler/
    ILSpySearchService.cs        # IL scanning for ldstr/ldc, regex matching
    ILSpyCrossAssemblyService.cs # Directory enumeration, multi-assembly loading

Application/
  UseCases/
    SearchStringsUseCase.cs
    SearchConstantsUseCase.cs
    ResolveTypeUseCase.cs
    LoadAssemblyDirectoryUseCase.cs

Transport/
  Mcp/
    Tools/
      SearchStringsTool.cs
      SearchConstantsTool.cs
      ResolveTypeTool.cs
      LoadAssemblyDirectoryTool.cs

TestTargets/
  Types/
    SearchTargets.cs             # Types with known string literals and constants

Tests/
  Tools/
    SearchStringsToolTests.cs
    SearchConstantsToolTests.cs
    ResolveTypeToolTests.cs
    LoadAssemblyDirectoryToolTests.cs
```

### Pattern 1: IL String Search (ldstr scanning)

**What:** Scan all method bodies for `ldstr` instructions, read the UserString from metadata, match against regex.

**When to use:** SRCH-01 implementation.

**Key API detail:** `ldstr` has a 4-byte inline token. The token encodes a UserString heap handle. Use `MetadataTokens.Handle(token)` to get the handle, then cast to `UserStringHandle` and call `reader.GetUserString(handle)` to read the actual string value.

```csharp
// Source: System.Reflection.Metadata API + existing ILSpyCrossReferenceService pattern
if (opCode == ILOpCode.Ldstr)
{
    int token = ilReader.ReadInt32();
    var handle = MetadataTokens.UserStringHandle(token & 0x00FFFFFF);
    string value = reader.GetUserString(handle);
    
    if (regex.IsMatch(value))
    {
        results.Add(new StringSearchResult
        {
            MatchedValue = value,
            DeclaringType = scanType.FullName,
            MethodName = method.Name,
            MethodSignature = FormatMethodSignature(method),
            ILOffset = offset
        });
    }
}
```

**Critical detail:** The `ldstr` token is a metadata token where the table bits (high byte) identify the UserString heap (0x70). Use `MetadataTokens.UserStringHandle(token & 0x00FFFFFF)` or simply `(UserStringHandle)MetadataTokens.Handle(token)` depending on how the token is structured. The token from IL is a raw int32 that encodes the heap offset.

### Pattern 2: Constant Search (ldc.* scanning)

**What:** Scan all method bodies for `ldc.*` instructions, read inline operand values, match against target value.

**When to use:** SRCH-02 implementation.

**Key IL opcodes for constants:**

| Opcode | Operand | Value |
|--------|---------|-------|
| `Ldc_i4_m1` | none | -1 |
| `Ldc_i4_0` through `Ldc_i4_8` | none | 0-8 |
| `Ldc_i4_s` | 1 byte (sbyte) | -128 to 127 |
| `Ldc_i4` | 4 bytes (int32) | full int range |
| `Ldc_i8` | 8 bytes (int64) | full long range |
| `Ldc_r4` | 4 bytes (float) | float |
| `Ldc_r8` | 8 bytes (double) | double |

```csharp
// Reading constant values from IL
long? constantValue = opCode switch
{
    ILOpCode.Ldc_i4_m1 => -1,
    ILOpCode.Ldc_i4_0 => 0,
    ILOpCode.Ldc_i4_1 => 1,
    ILOpCode.Ldc_i4_2 => 2,
    ILOpCode.Ldc_i4_3 => 3,
    ILOpCode.Ldc_i4_4 => 4,
    ILOpCode.Ldc_i4_5 => 5,
    ILOpCode.Ldc_i4_6 => 6,
    ILOpCode.Ldc_i4_7 => 7,
    ILOpCode.Ldc_i4_8 => 8,
    ILOpCode.Ldc_i4_s => ilReader.ReadSByte(),
    ILOpCode.Ldc_i4 => ilReader.ReadInt32(),
    ILOpCode.Ldc_i8 => ilReader.ReadInt64(),
    _ => null
};
```

**Note on floating point:** `Ldc_r4` and `Ldc_r8` load float/double values. For SRCH-02 which specifies "numeric and enum constants" with exact match, comparing floating point requires care. Since D-07 specifies exact numeric value matching, the tool parameter should accept a `long` for integer constants. Floating point search could be deferred or handled with a separate parameter.

### Pattern 3: Cross-Assembly Directory Loading

**What:** Enumerate .dll/.exe files in a directory, attempt to load each as a managed assembly, report loaded vs skipped.

**When to use:** XASM-02 implementation.

```csharp
// Directory scanning with native DLL guard
var searchOption = depth == 0 ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
var files = Directory.EnumerateFiles(directoryPath, "*.*", searchOption)
    .Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) 
             || f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

var loaded = new List<AssemblyDirectoryEntry>();
var skipped = new List<SkippedAssemblyEntry>();

foreach (var file in files)
{
    try
    {
        // Use PEFile to check if it's a managed assembly
        using var peFile = new PEFile(file);
        var reader = peFile.Metadata;
        var assemblyDef = reader.GetAssemblyDefinition();
        var name = reader.GetString(assemblyDef.Name);
        var version = assemblyDef.Version;
        
        loaded.Add(new AssemblyDirectoryEntry { ... });
    }
    catch (MetadataFileNotSupportedException)
    {
        skipped.Add(new SkippedAssemblyEntry 
        { 
            FilePath = file, 
            Reason = "Not a .NET assembly" 
        });
    }
    catch (Exception ex)
    {
        skipped.Add(new SkippedAssemblyEntry 
        { 
            FilePath = file, 
            Reason = ex.Message 
        });
    }
}
```

**Critical detail on depth:** `SearchOption.AllDirectories` does not support depth limiting natively. For configurable depth, use a custom recursive enumeration or filter results by path depth relative to the root.

### Pattern 4: Type Resolution Across Assemblies

**What:** For each managed assembly in a directory, check if it defines a type matching the search (partial name match).

**When to use:** XASM-01 implementation.

```csharp
// Partial type name matching
var decompiler = new CSharpDecompiler(filePath, settings);
foreach (var type in decompiler.TypeSystem.MainModule.TypeDefinitions)
{
    if (type.FullName.Contains(searchName, StringComparison.OrdinalIgnoreCase)
        || type.Name.Equals(searchName, StringComparison.OrdinalIgnoreCase))
    {
        matches.Add(new TypeResolutionResult
        {
            AssemblyPath = filePath,
            AssemblyName = assemblyName,
            TypeFullName = type.FullName,
            TypeShortName = type.Name
        });
    }
}
```

### Anti-Patterns to Avoid
- **Loading full CSharpDecompiler for directory scanning:** For XASM-02 (load directory), use `PEFile` directly -- much lighter than constructing a full `CSharpDecompiler` per assembly. Only use `CSharpDecompiler` for XASM-01 (resolve type) where the type system is needed.
- **Unbounded directory recursion:** Always limit depth. Default to 3 levels to avoid scanning entire drive trees.
- **Regex compilation per match:** Compile the regex once before scanning, pass `RegexOptions.Compiled` for patterns used across many methods.
- **Not handling SearchOption depth:** `SearchOption.AllDirectories` has no depth control. Implement custom depth-limited enumeration.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| IL opcode parsing | Custom IL parser | Existing `ReadILOpCode`/`SkipOperand`/`GetOperandSize` from `ILSpyCrossReferenceService` | Complete opcode table already built and tested in Phase 4 |
| Regex matching | Custom string pattern matcher | `System.Text.RegularExpressions.Regex` | Standard library, handles all regex syntax |
| Assembly managed-check | Custom PE header parser | `PEFile` constructor + catch `MetadataFileNotSupportedException` | Established pattern in codebase (Phase 5 native DLL guard) |
| UserString reading | Manual heap offset calculation | `MetadataReader.GetUserString(UserStringHandle)` | Standard API, handles encoding correctly |

## Common Pitfalls

### Pitfall 1: UserString Handle Encoding
**What goes wrong:** `ldstr` token is not a standard metadata table token -- it references the UserString heap (#US), not the String heap (#Strings). Using `MetadataTokens.EntityHandle(token)` will fail because UserString handles are not entity handles.
**Why it happens:** The token from IL bytes has table bits 0x70 for UserString. Unlike method/field tokens, this needs special handling.
**How to avoid:** Extract the heap offset from the token using `(token & 0x00FFFFFF)` and construct a `UserStringHandle` via `MetadataTokens.UserStringHandle(offset)`. Or use the overload that handles the full token.
**Warning signs:** `InvalidCastException` or `ArgumentOutOfRangeException` when trying to cast to `EntityHandle`.

### Pitfall 2: Ldc_i4_s Signedness
**What goes wrong:** `Ldc_i4_s` takes a signed byte (sbyte), not an unsigned byte. Reading it as `ReadByte()` instead of `ReadSByte()` gives wrong values for negative constants.
**Why it happens:** Easy to assume all single-byte operands are unsigned.
**How to avoid:** Use `ilReader.ReadSByte()` for `Ldc_i4_s`.
**Warning signs:** Constants like -1 (when encoded as `Ldc_i4_s 0xFF`) read as 255 instead.

### Pitfall 3: Directory Depth Without Built-in Support
**What goes wrong:** `Directory.EnumerateFiles` with `SearchOption.AllDirectories` recurses infinitely. No built-in depth limit.
**Why it happens:** .NET API doesn't expose depth parameter on `EnumerateFiles`.
**How to avoid:** Implement depth-limited enumeration using `Directory.EnumerateDirectories` recursively with a counter, or filter results by counting path separators relative to root.
**Warning signs:** Extremely slow tool execution when pointed at a root directory.

### Pitfall 4: Large Assembly Search Performance
**What goes wrong:** Scanning every method body in a large assembly (thousands of methods) can be slow if not careful.
**Why it happens:** Each method requires reading the method body from PE file and iterating IL bytes.
**How to avoid:** The existing pattern in `ILSpyCrossReferenceService` already handles this efficiently with `metadataFile.GetMethodBody(rva)`. Early-exit on `RelativeVirtualAddress == 0`. The same pattern applies here.
**Warning signs:** Timeouts on large framework assemblies.

### Pitfall 5: PEFile vs CSharpDecompiler Weight for Directory Scanning
**What goes wrong:** Creating a `CSharpDecompiler` for every file in a directory is extremely slow.
**Why it happens:** `CSharpDecompiler` constructor loads and resolves the full type system.
**How to avoid:** For XASM-02 (load directory), use `PEFile` directly -- it only reads PE headers and metadata tables, much lighter. Only use `CSharpDecompiler` for XASM-01 (resolve type) where type definitions need iteration.
**Warning signs:** 10+ second response times for directories with 20+ assemblies.

### Pitfall 6: AssemblyPath Validation for Directories
**What goes wrong:** The existing `AssemblyPath.Create()` validates a single file exists with .dll/.exe extension. Cross-assembly tools need a directory path, not a file path.
**Why it happens:** `AssemblyPath` is designed for single-assembly operations.
**How to avoid:** Create a new value object (e.g., `DirectoryPath`) for the cross-assembly tools, or accept the directory path as a plain string with validation in the service/use case layer.
**Warning signs:** `FileNotFoundException` when passing a directory to existing validation.

## Code Examples

### Example 1: ISearchService Interface
```csharp
// Domain/Services/ISearchService.cs
using ILSpy.Mcp.Domain.Models;

namespace ILSpy.Mcp.Domain.Services;

public interface ISearchService
{
    Task<SearchResults<StringSearchResult>> SearchStringsAsync(
        AssemblyPath assemblyPath,
        string regexPattern,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default);

    Task<SearchResults<ConstantSearchResult>> SearchConstantsAsync(
        AssemblyPath assemblyPath,
        long value,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default);
}
```

### Example 2: ICrossAssemblyService Interface
```csharp
// Domain/Services/ICrossAssemblyService.cs
using ILSpy.Mcp.Domain.Models;

namespace ILSpy.Mcp.Domain.Services;

public interface ICrossAssemblyService
{
    Task<IReadOnlyList<TypeResolutionResult>> ResolveTypeAsync(
        string directoryPath,
        string typeName,
        int maxDepth = 3,
        CancellationToken cancellationToken = default);

    Task<DirectoryLoadResult> LoadAssemblyDirectoryAsync(
        string directoryPath,
        int maxDepth = 3,
        CancellationToken cancellationToken = default);
}
```

### Example 3: Domain Models
```csharp
// Domain/Models/SearchResult.cs
namespace ILSpy.Mcp.Domain.Models;

public sealed record SearchResults<T>
{
    public required IReadOnlyList<T> Results { get; init; }
    public int TotalCount { get; init; }
    public int Offset { get; init; }
    public int Limit { get; init; }
}

public sealed record StringSearchResult
{
    public required string MatchedValue { get; init; }
    public required string DeclaringType { get; init; }
    public required string MethodName { get; init; }
    public string? MethodSignature { get; init; }
    public int ILOffset { get; init; }
}

public sealed record ConstantSearchResult
{
    public required long MatchedValue { get; init; }
    public required string ConstantType { get; init; } // "Int32", "Int64", "Float", "Double"
    public required string DeclaringType { get; init; }
    public required string MethodName { get; init; }
    public string? MethodSignature { get; init; }
    public int ILOffset { get; init; }
}
```

### Example 4: TestTargets for Search
```csharp
// TestTargets/Types/SearchTargets.cs
namespace ILSpy.Mcp.TestTargets.Search;

public class StringContainer
{
    public string GetGreeting() => "Hello, World!";
    public string GetUrl() => "https://example.com/api";
    public string GetEmpty() => "";
    public void LogMessage() { var msg = "Error: connection failed"; _ = msg; }
}

public class ConstantContainer
{
    public int GetMagicNumber() => 42;
    public int GetNegative() => -1;
    public long GetBigNumber() => 1234567890L;
    public void UseConstants()
    {
        int x = 255;
        int y = 0;
        _ = x + y;
    }
}
```

### Example 5: Shared IL Scanning Helpers (if extracted)
```csharp
// Infrastructure/Decompiler/ILScannerHelpers.cs (optional extraction)
// The existing helpers in ILSpyCrossReferenceService are:
// - ReadILOpCode(ref BlobReader) -> ILOpCode
// - SkipOperand(ref BlobReader, ILOpCode) -> void
// - GetOperandSize(ILOpCode) -> int
// - IsTokenReferenceOpCode(ILOpCode) -> bool
// - FormatMethodSignature(IMethod) -> string
// - GetTypeFullName(MetadataReader, TypeDefinitionHandle) -> string
// 
// These could be extracted to a static helper class shared between
// ILSpyCrossReferenceService and ILSpySearchService. Or the search
// service can duplicate them (they're small). Claude's discretion.
```

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + FluentAssertions 8.9.0 |
| Config file | Tests project references main project and TestTargets |
| Quick run command | `dotnet test --filter "ClassName~Search\|ClassName~ResolveType\|ClassName~LoadAssemblyDirectory"` |
| Full suite command | `dotnet test` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SRCH-01 | Search string literals by regex | integration | `dotnet test --filter ClassName~SearchStringsToolTests -x` | Wave 0 |
| SRCH-02 | Search numeric constants | integration | `dotnet test --filter ClassName~SearchConstantsToolTests -x` | Wave 0 |
| XASM-01 | Resolve type across directory | integration | `dotnet test --filter ClassName~ResolveTypeToolTests -x` | Wave 0 |
| XASM-02 | Load all assemblies from directory | integration | `dotnet test --filter ClassName~LoadAssemblyDirectoryToolTests -x` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "ClassName~Search|ClassName~ResolveType|ClassName~LoadAssemblyDirectory"`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before verification

### Wave 0 Gaps
- [ ] `TestTargets/Types/SearchTargets.cs` -- types with known strings and constants for SRCH-01, SRCH-02
- [ ] `Tests/Tools/SearchStringsToolTests.cs` -- covers SRCH-01
- [ ] `Tests/Tools/SearchConstantsToolTests.cs` -- covers SRCH-02
- [ ] `Tests/Tools/ResolveTypeToolTests.cs` -- covers XASM-01
- [ ] `Tests/Tools/LoadAssemblyDirectoryToolTests.cs` -- covers XASM-02

### Test Considerations for Cross-Assembly
- XASM-01/XASM-02 tests need a directory containing multiple assemblies. The test output directory (`AppContext.BaseDirectory`) already contains `ILSpy.Mcp.TestTargets.dll` plus framework assemblies. Use the test output directory itself as the test directory.
- Alternatively, create a temp directory with copies of the test assembly for isolated tests.
- The test output directory also contains the main `ILSpy.Mcp.dll`, providing a multi-assembly scenario naturally.

## Recommendations (Claude's Discretion Items)

### Default Values
- **Default max_results:** 100 (reasonable for AI assistant context windows, matches common API pagination)
- **Default directory scan depth:** 3 (covers typical `bin/Debug/net10.0/` structures without going wild)

### IL Scanning Helper Extraction
**Recommendation: Duplicate rather than extract.** The shared helpers (`ReadILOpCode`, `SkipOperand`, `GetOperandSize`, `FormatMethodSignature`, `GetTypeFullName`) are small static methods. Extracting them to a shared utility creates coupling between `ILSpyCrossReferenceService` and `ILSpySearchService`. The duplication cost is minimal (~120 lines of well-tested static code). If a third consumer appears in future, extraction becomes worthwhile.

However, if the implementer prefers extraction, a `static class ILScannerHelpers` in `Infrastructure/Decompiler/` would be the right location.

### DirectoryPath Value Object
**Recommendation: Create a `DirectoryPath` value object** similar to `AssemblyPath` but validating directory existence instead of file existence. This keeps the domain model consistent.

```csharp
public sealed record DirectoryPath
{
    public string Value { get; }
    private DirectoryPath(string value) => Value = value;
    
    public static DirectoryPath Create(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Directory path cannot be null or empty.");
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        return new DirectoryPath(Path.GetFullPath(path));
    }
}
```

### Depth-Limited Directory Enumeration
**Recommendation:** Use a simple recursive helper rather than `SearchOption.AllDirectories` with post-filtering.

```csharp
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
```

## Open Questions

1. **Floating-point constant search**
   - What we know: D-07 says "exact numeric value" and "ldc.*" instructions include `Ldc_r4` (float) and `Ldc_r8` (double)
   - What's unclear: Should `search_constants` support float/double matching? Exact float comparison is unreliable.
   - Recommendation: Accept the value as `long` for integer matching only. Add a note in the tool description that float/double constants are not currently searchable. This can be extended later.

2. **Enum constant representation**
   - What we know: Enums compile to integer `ldc.i4` instructions. The IL has no metadata linking a constant back to its enum type.
   - What's unclear: How to identify enum values vs plain integer constants in search results.
   - Recommendation: Report all matching integer constants. The AI assistant can cross-reference with type information to determine if a value corresponds to an enum member. The `ConstantType` field in results should report the IL type (Int32, Int64) not the source type.

## Sources

### Primary (HIGH confidence)
- Codebase: `Infrastructure/Decompiler/ILSpyCrossReferenceService.cs` -- complete IL scanning reference implementation
- Codebase: `Domain/Services/ICrossReferenceService.cs` -- domain interface pattern
- Codebase: `Domain/Models/CrossReferenceResult.cs` -- result model pattern
- Codebase: `Application/UseCases/FindUsagesUseCase.cs` -- use case pattern
- Codebase: `Transport/Mcp/Tools/FindUsagesTool.cs` -- MCP tool pattern
- Codebase: `Program.cs` -- DI registration pattern
- Codebase: `Tests/Fixtures/ToolTestFixture.cs` -- test infrastructure pattern
- CLAUDE.md -- technology stack, API references, breaking changes guide

### Secondary (MEDIUM confidence)
- Microsoft Learn: `MetadataReader.GetUserString` -- UserString heap access for ldstr operands
- Microsoft Learn: `ILOpCode` enum -- complete opcode listing with operand sizes
- ECMA-335 specification -- IL opcode encoding rules for ldc.* family

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new dependencies, all patterns established
- Architecture: HIGH -- direct extension of Phase 4 IL scanning, Phase 5 inspection patterns
- Pitfalls: HIGH -- based on actual codebase analysis and IL encoding rules

**Research date:** 2026-04-08
**Valid until:** 2026-05-08 (stable -- no external dependency changes expected)
