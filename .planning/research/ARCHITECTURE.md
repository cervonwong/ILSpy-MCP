# Architecture Patterns

**Domain:** .NET decompilation MCP server — IL scanning, cross-reference analysis, resource extraction, bulk decompilation
**Researched:** 2026-04-07

## Existing Architecture (Baseline)

The current codebase follows a clean four-layer architecture:

```
Transport (MCP Tools)  -->  Application (Use Cases)  -->  Domain (Interfaces + Models)
                                                                   ^
                                                     Infrastructure (ILSpy impl)
```

- **Domain**: `IDecompilerService` interface, value objects (`AssemblyPath`, `TypeName`), domain models (`TypeInfo`, `AssemblyInfo`), domain exceptions
- **Infrastructure**: `ILSpyDecompilerService` — single class implementing all of `IDecompilerService`, creates a new `CSharpDecompiler` per call (stateless)
- **Application**: One use case class per tool, each injecting `IDecompilerService` + `ITimeoutService`
- **Transport**: One MCP tool class per use case, maps exceptions to `McpToolException`

Key observation: `ILSpyDecompilerService` is a God-class growing toward ~350 lines. Every new capability adds methods here. The new features (IL scanning, cross-refs, resources, bulk decompile) would push it past 1000 lines and mix unrelated concerns.

## Recommended Architecture

Split the single `IDecompilerService` into focused domain service interfaces. Add `IILScannerService` as a new domain port for all IL-scanning operations, and `IAssemblyMetadataService` for metadata/resource operations. Keep `IDecompilerService` for decompilation-only operations.

### Component Boundaries

```
Transport Layer (MCP Tools)
  |
  v
Application Layer (Use Cases)
  |
  +---> IDecompilerService        (decompile type, method, namespace, project)
  +---> IILScannerService         (find usages, find implementors, string search, constant search)
  +---> IAssemblyMetadataService  (assembly metadata, references, resources, attributes)
  +---> IAssemblyResolverService  (load directory, resolve type across assemblies)
  +---> ITimeoutService           (unchanged)
```

```
Infrastructure Layer
  |
  +---> ILSpyDecompilerService    implements IDecompilerService
  +---> ILScannerService          implements IILScannerService (uses System.Reflection.Metadata)
  +---> AssemblyMetadataService   implements IAssemblyMetadataService (uses PEReader + MetadataReader)
  +---> AssemblyResolverService   implements IAssemblyResolverService (directory scanning + resolver)
```

### Component Details

| Component | Responsibility | Communicates With | Layer |
|-----------|---------------|-------------------|-------|
| `IDecompilerService` | Decompile types, methods, namespaces; IL disassembly output; export project | Application use cases | Domain (interface) |
| `IILScannerService` | Scan IL instructions across method bodies; find references to types/methods/fields; search string literals (`ldstr`); search constants (`ldc.*`) | Application use cases | Domain (interface) |
| `IAssemblyMetadataService` | Read PE metadata (target framework, bitness, strong name, entry point); list assembly references; list/extract embedded resources; read custom attributes | Application use cases | Domain (interface) |
| `IAssemblyResolverService` | Enumerate assemblies in a directory; resolve a type name across multiple assemblies; provide assembly paths for cross-assembly analysis | Application use cases, other services | Domain (interface) |
| `ILSpyDecompilerService` | Wraps `CSharpDecompiler` for C# output; wraps `CSharpDecompiler` with `DisassemblerSettings` for IL output; wraps `WholeProjectDecompiler` for bulk export | `IDecompilerService` | Infrastructure |
| `ILScannerService` | Opens `PEReader` + `MetadataReader`; iterates `MethodDefinition` handles; reads `MethodBodyBlock` via `GetILReader()`; decodes opcodes to find `MemberReference`/`TypeReference` targets | `IILScannerService` | Infrastructure |
| `AssemblyMetadataService` | Opens `PEReader` for PE headers; reads `MetadataReader` tables (AssemblyRef, CustomAttribute, ManifestResource) | `IAssemblyMetadataService` | Infrastructure |
| `AssemblyResolverService` | Scans directories for `.dll`/`.exe`; uses `MetadataReader` to check type definitions without full decompiler load | `IAssemblyResolverService` | Infrastructure |

## ILScanner Service: Core Design

This is the most architecturally significant new component. It must be reusable across four features: find_usages, find_implementors, string_search, and constant_search.

### Interface Design

```csharp
namespace ILSpy.Mcp.Domain.Services;

/// <summary>
/// Port for IL-level scanning operations. Scans method bodies at the IL instruction level
/// using System.Reflection.Metadata, without full decompilation.
/// </summary>
public interface IILScannerService
{
    /// <summary>
    /// Find all locations where a given type/method/field is referenced in IL.
    /// Scans call, callvirt, newobj, ldfld, stfld, ldsfld, stsfld, castclass, isinst, etc.
    /// </summary>
    Task<IReadOnlyList<ILReference>> FindUsagesAsync(
        AssemblyPath assemblyPath,
        string targetFullName,
        ReferenceKind kind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find all types that implement a given interface or override a virtual method.
    /// Scans TypeDef table for interface implementations and method overrides.
    /// </summary>
    Task<IReadOnlyList<ImplementorInfo>> FindImplementorsAsync(
        AssemblyPath assemblyPath,
        string interfaceOrBaseFullName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for string literals in IL (ldstr operands).
    /// </summary>
    Task<IReadOnlyList<StringReference>> SearchStringsAsync(
        AssemblyPath assemblyPath,
        string searchPattern,
        bool isRegex = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for numeric constants and enum values (ldc.i4, ldc.i8, ldc.r4, ldc.r8
    /// and FieldDefinition.HasDefault).
    /// </summary>
    Task<IReadOnlyList<ConstantReference>> SearchConstantsAsync(
        AssemblyPath assemblyPath,
        string searchValue,
        CancellationToken cancellationToken = default);
}
```

### Internal Scanning Architecture

The `ILScannerService` implementation should use a **two-phase scan** pattern:

```
Phase 1: Open assembly (PEReader + MetadataReader)
         Build lookup tables (MemberRef -> resolved name, TypeRef -> resolved name)

Phase 2: Iterate all MethodDefinitions
         For each: get MethodBodyBlock, get BlobReader via GetILReader()
         Decode opcodes, match against target
         Collect results with location context (type + method where reference found)
```

Key implementation detail: `BlobReader` from `MethodBodyBlock.GetILReader()` is a value type. The extension methods in `ILParser` (from ICSharpCode.Decompiler's Metadata layer) handle opcode decoding. The operand for `call`/`callvirt`/`newobj` is a metadata token that resolves to `MemberReferenceHandle` or `MethodDefinitionHandle` via `MetadataReader`.

```csharp
// Pseudocode for the core scanning loop
private IEnumerable<ILReference> ScanMethodBody(
    MetadataReader reader,
    MethodDefinitionHandle methodHandle,
    Func<EntityHandle, bool> matchPredicate)
{
    var methodDef = reader.GetMethodDefinition(methodHandle);
    if (methodDef.RelativeVirtualAddress == 0) yield break; // abstract/extern

    var body = peReader.GetMethodBody(methodDef.RelativeVirtualAddress);
    var ilReader = body.GetILReader();

    while (ilReader.RemainingBytes > 0)
    {
        var opCode = DecodeOpCode(ref ilReader);
        var operand = DecodeOperand(ref ilReader, opCode);

        if (operand is EntityHandle handle && matchPredicate(handle))
        {
            yield return new ILReference
            {
                ContainingType = GetTypeName(reader, methodHandle),
                ContainingMethod = reader.GetString(methodDef.Name),
                OpCode = opCode.ToString(),
                // offset info for context
            };
        }
    }
}
```

### Why System.Reflection.Metadata, Not CSharpDecompiler

- **Performance**: SRM provides zero-copy, direct access to metadata tables. Creating a `CSharpDecompiler` involves building a full type system, which is expensive for scanning.
- **Memory**: SRM reads bytes in-place without object allocation. Scanning a 10MB assembly for string references should not allocate proportional memory.
- **Precision**: IL-level scanning finds ALL references, including those in compiler-generated code, lambda captures, and async state machines that decompilation may hide.
- **Already available**: The project already depends on `ICSharpCode.Decompiler` which itself depends on `System.Reflection.Metadata`. The `ILOpCodes.cs`, `MetadataExtensions.cs`, and `MetadataTokenHelpers.cs` utilities in the decompiler's Metadata directory provide ready-made helpers.

### Domain Models for IL Scanning

```csharp
public sealed record ILReference
{
    public required string ContainingType { get; init; }
    public required string ContainingMethod { get; init; }
    public required string OpCode { get; init; }
    public required string TargetFullName { get; init; }
    public int ILOffset { get; init; }
}

public enum ReferenceKind { Type, Method, Field }

public sealed record ImplementorInfo
{
    public required string TypeFullName { get; init; }
    public required string Kind { get; init; } // "implements" or "overrides"
    public IReadOnlyList<string> ImplementedMembers { get; init; } = [];
}

public sealed record StringReference
{
    public required string ContainingType { get; init; }
    public required string ContainingMethod { get; init; }
    public required string Value { get; init; }
    public int ILOffset { get; init; }
}

public sealed record ConstantReference
{
    public required string ContainingType { get; init; }
    public required string MemberName { get; init; }
    public required string Value { get; init; }
    public required string ConstantType { get; init; } // "int32", "int64", "float32", "float64", "enum"
}
```

## Cross-Assembly Resolution: Stateless Design

The project explicitly defers multi-assembly session management (marked out of scope in PROJECT.md). Cross-assembly features must work without persistent state.

### Design: Caller-Provides-Context Pattern

```
MCP Tool receives: directory_path + type_name
  |
  v
AssemblyResolverService:
  1. Enumerate *.dll, *.exe in directory_path
  2. For each file: open PEReader, read MetadataReader, scan TypeDefinitions
  3. Return matching AssemblyPath(s)
  |
  v
Use Case composes: resolver result -> decompiler/scanner call
```

```csharp
public interface IAssemblyResolverService
{
    /// <summary>
    /// List all loadable assemblies in a directory (non-recursive by default).
    /// Uses PEReader to validate each file is a valid .NET assembly.
    /// </summary>
    Task<IReadOnlyList<AssemblySummary>> ListAssembliesAsync(
        string directoryPath,
        bool recursive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find which assembly(ies) in a directory define a given type.
    /// Scans TypeDef tables without full decompiler initialization.
    /// </summary>
    Task<IReadOnlyList<TypeLocation>> ResolveTypeAsync(
        string directoryPath,
        string typeFullName,
        CancellationToken cancellationToken = default);
}

public sealed record AssemblySummary
{
    public required string FilePath { get; init; }
    public required string AssemblyName { get; init; }
    public required string Version { get; init; }
    public int TypeCount { get; init; }
}

public sealed record TypeLocation
{
    public required AssemblyPath AssemblyPath { get; init; }
    public required string TypeFullName { get; init; }
    public required string Namespace { get; init; }
}
```

### Why Stateless

Each MCP tool invocation is independent. The AI assistant passes the directory path each time. This avoids:
- Memory leaks from holding assemblies open across calls
- Stale state when assemblies change on disk
- Complexity of session lifecycle management
- The explicit out-of-scope decision in PROJECT.md

**Tradeoff**: Repeated directory scans are slower than cached sessions. Mitigate with:
- `PEReader` is fast for metadata-only access (no decompilation)
- TypeDef table scan for type resolution is O(n) in type count, which is fast even for large assemblies
- If performance becomes an issue later, add an optional `IAssemblyCache` without changing the interface

## Data Flow: New Features

### find_usages / find_implementors / string_search / constant_search

```
MCP Tool (e.g., FindUsagesTool)
  |  assemblyPath, targetName, kind
  v
FindUsagesUseCase
  |  validates inputs, applies timeout
  v
IILScannerService.FindUsagesAsync(assemblyPath, targetName, kind)
  |
  v
ILScannerService (Infrastructure)
  |  1. Open PEReader from assemblyPath
  |  2. Get MetadataReader
  |  3. Build target handle resolution (MemberRef table lookup)
  |  4. Iterate all MethodDefinitions
  |  5. For each: scan IL via BlobReader
  |  6. Collect matching ILReferences
  v
IReadOnlyList<ILReference> -> formatted string -> MCP response
```

### load_directory / resolve_type_across_assemblies

```
MCP Tool (LoadDirectoryTool)
  |  directoryPath, optional filters
  v
LoadDirectoryUseCase
  |  validates path exists, applies timeout
  v
IAssemblyResolverService.ListAssembliesAsync(directoryPath)
  |
  v
AssemblyResolverService (Infrastructure)
  |  1. Enumerate *.dll, *.exe files
  |  2. For each: try open PEReader, read assembly name/version
  |  3. Skip non-.NET files (PEReader throws on native DLLs)
  |  4. Return summaries
  v
IReadOnlyList<AssemblySummary> -> formatted string -> MCP response
```

### Bulk Decompilation (decompile_namespace, export_project)

```
MCP Tool (DecompileNamespaceTool)
  |  assemblyPath, namespace
  v
DecompileNamespaceUseCase
  |  1. List types in namespace via IDecompilerService.ListTypesAsync
  |  2. For each type: IDecompilerService.DecompileTypeAsync
  |  3. Concatenate results (with size limits)
  v
String (concatenated source) -> MCP response

MCP Tool (ExportProjectTool)
  |  assemblyPath, outputDirectory
  v
ExportProjectUseCase
  |
  v
IDecompilerService.ExportProjectAsync(assemblyPath, outputDir)
  |
  v
ILSpyDecompilerService wraps WholeProjectDecompiler
  |  Writes .cs files + .csproj to outputDir
  v
ProjectExportResult (file count, path) -> MCP response
```

### Assembly Metadata / Resources / Attributes

```
MCP Tool (GetAssemblyMetadataTool)
  |  assemblyPath
  v
GetAssemblyMetadataUseCase
  |
  v
IAssemblyMetadataService.GetDetailedMetadataAsync(assemblyPath)
  |
  v
AssemblyMetadataService (Infrastructure)
  |  Opens PEReader -> reads PE headers (bitness, subsystem)
  |  Opens MetadataReader -> reads Assembly table (name, version, culture, public key)
  |  Reads AssemblyRef table for references
  |  Reads CustomAttribute table for assembly-level attributes
  |  Reads ManifestResource table for embedded resources
  v
DetailedAssemblyMetadata -> formatted string -> MCP response
```

## Patterns to Follow

### Pattern 1: Service-Per-Concern

**What:** Each domain interface covers one concern area (decompilation, IL scanning, metadata, resolution). Infrastructure implementations are 1:1 with interfaces.

**When:** Always for new features. Do not add methods to `IDecompilerService` unless they involve C# decompilation.

**Example:**
```csharp
// Good: separate concern
builder.Services.AddScoped<IILScannerService, ILScannerService>();
builder.Services.AddScoped<IAssemblyMetadataService, AssemblyMetadataService>();

// Bad: stuffing everything into one service
builder.Services.AddScoped<IDecompilerService, ILSpyDecompilerService>(); // 2000 lines
```

### Pattern 2: PEReader-First for Read-Only Operations

**What:** Use `PEReader` + `MetadataReader` for operations that only read metadata or IL, not `CSharpDecompiler`. Reserve `CSharpDecompiler` for actual C# output.

**When:** IL scanning, metadata extraction, resource listing, type resolution, assembly reference listing.

**Why:** `CSharpDecompiler` construction builds a full ITypeSystem. That is expensive and unnecessary when you only need to scan opcode bytes or read metadata tables.

```csharp
// Good: lightweight for scanning
using var stream = File.OpenRead(assemblyPath.Value);
using var peReader = new PEReader(stream);
var reader = peReader.GetMetadataReader();
// scan TypeDef table, MethodBody IL, etc.

// Reserve for decompilation output only
var decompiler = new CSharpDecompiler(assemblyPath.Value, settings);
var code = decompiler.DecompileTypeAsString(fullTypeName);
```

### Pattern 3: Streaming Results with Limits

**What:** IL scanning can produce large result sets. Apply configurable limits and return count information.

**When:** find_usages, string_search, constant_search.

```csharp
public sealed record ScanResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int TotalCount { get; init; }
    public bool IsTruncated { get; init; }
}
```

## Anti-Patterns to Avoid

### Anti-Pattern 1: Shared CSharpDecompiler Instances

**What:** Caching or sharing `CSharpDecompiler` instances across calls.

**Why bad:** `CSharpDecompiler` is explicitly documented as not thread-safe. The current code correctly creates a new instance per call. Do not optimize this away.

**Instead:** Create per-call. For scanning operations, use `PEReader`/`MetadataReader` instead, which are lighter weight.

### Anti-Pattern 2: Session State for Cross-Assembly

**What:** Keeping loaded assemblies in memory between MCP tool calls.

**Why bad:** MCP is request/response. No session guarantee. Memory leaks. Stale state. Explicitly deferred in PROJECT.md.

**Instead:** Caller provides directory path each time. Resolver scans on demand. Fast enough with PEReader for metadata-only access.

### Anti-Pattern 3: Mixed Abstraction Levels in Domain Interface

**What:** An interface that has both high-level methods (`DecompileType`) and low-level methods (`ScanILForOpcodes`).

**Why bad:** Different consumers need different abstraction levels. Mixing them creates a confusing contract.

**Instead:** Separate interfaces. `IDecompilerService` speaks in C# code strings. `IILScannerService` speaks in IL references. `IAssemblyMetadataService` speaks in metadata records.

### Anti-Pattern 4: Parsing IL Without Handling Edge Cases

**What:** Assuming all method bodies are straightforward IL.

**Why bad:** Methods may have: no body (abstract/extern, RVA=0), exception handling clauses, local variable signatures, fat vs thin headers. Compiler-generated methods (async state machines, iterator blocks, closures) have synthetic names.

**Instead:** Check `RelativeVirtualAddress != 0` before reading body. Use `MethodBodyBlock` which handles header parsing. Filter or flag compiler-generated types (`[CompilerGenerated]`, `<>c__DisplayClass`).

## Suggested Build Order

Build order is driven by dependency relationships between components:

```
Phase 1: Foundation (no dependencies on new code)
  1. IAssemblyMetadataService + implementation
     - Uses only PEReader/MetadataReader (well-understood)
     - Enables: get_assembly_metadata, list_assembly_references, get_attributes
     - No dependency on ILScanner or resolver

  2. IDecompilerService extensions (IL output, constructor inclusion)
     - Extends existing service interface
     - Enables: get_il_output, fixed get_type_members

Phase 2: IL Scanner (core reusable component)
  3. IILScannerService + ILScannerService implementation
     - Core scanning loop (PEReader + BlobReader iteration)
     - Enables: find_usages, find_implementors first
     - Then: string_search, constant_search (same scanning loop, different match predicates)

Phase 3: Cross-Assembly (depends on metadata reading from Phase 1)
  4. IAssemblyResolverService + implementation
     - Directory enumeration + PEReader validation
     - Enables: load_directory, resolve_type_across_assemblies

Phase 4: Bulk Operations (depends on decompiler from Phase 1)
  5. Bulk decompilation (decompile_namespace, export_project)
     - Composes existing decompiler operations
     - Wraps WholeProjectDecompiler for export

Phase 5: Resource Extraction (depends on metadata from Phase 1)
  6. Resource listing and extraction
     - ManifestResource table reading
     - Byte extraction for embedded resources
```

**Rationale for this order:**
- Phase 1 is lowest risk (extends existing patterns) and validates the service-splitting approach
- Phase 2 (ILScanner) is the highest-value, highest-complexity component; building it second means the architecture patterns are proven but it gets priority
- Phase 3 depends on being comfortable with PEReader patterns established in Phases 1-2
- Phases 4-5 compose existing capabilities and can be parallelized

## Scalability Considerations

| Concern | Small assembly (<1MB) | Medium assembly (1-20MB) | Large assembly (>20MB, e.g. Unity) |
|---------|----------------------|-------------------------|-------------------------------------|
| IL scan time | <100ms | 1-5 seconds | 5-30 seconds |
| Memory for scan | Minimal (PEReader streams) | Moderate | Need streaming, avoid loading full IL into memory |
| Cross-assembly resolve | <10 files, instant | 50-200 files, 1-2 seconds | 500+ files, need parallel PEReader opens |
| Bulk decompile | Fast, small output | May hit token limits | Must paginate or limit; WholeProjectDecompiler writes to disk |

**Mitigation for large assemblies:**
- Timeout service already exists; use it for all new operations
- Add `maxResults` parameter to scanning operations (default 100)
- For export_project, write to disk and return path rather than streaming all source through MCP
- For load_directory with many assemblies, use `Parallel.ForEachAsync` with bounded concurrency

## Sources

- [ILSpy GitHub Repository](https://github.com/icsharpcode/ILSpy)
- [System.Reflection.Metadata (SRM) Wiki](https://github.com/icsharpcode/ILSpy/wiki/srm) - BlobReader, MethodBodyBlock, ILParser patterns
- [ICSharpCode.Decompiler Metadata Directory](https://github.com/icsharpcode/ILSpy/tree/master/ICSharpCode.Decompiler/Metadata) - ILOpCodes.cs, MetadataExtensions.cs utilities
- [WholeProjectDecompiler](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.Decompiler/CSharp/ProjectDecompiler/WholeProjectDecompiler.cs) - Bulk export implementation
- [MetadataReader API](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata.metadatareader) - Core SRM API
- [ILSpy Analyzers](https://github.com/icsharpcode/ILSpy/tree/master/ILSpy/Analyzers) - Reference analysis pattern in ILSpy GUI
