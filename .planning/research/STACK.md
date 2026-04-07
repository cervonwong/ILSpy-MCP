# Technology Stack

**Project:** ILSpy MCP -- Feature Parity Milestone
**Researched:** 2026-04-07

## Current Stack (Baseline)

| Technology | Current Version | Target Version | Breaking? |
|------------|----------------|----------------|-----------|
| ICSharpCode.Decompiler | 9.1.0.7988 | 10.0.0.8330 | YES |
| ModelContextProtocol | 0.4.0-preview.3 | 1.2.0 | YES |
| Microsoft.Extensions.Hosting | 8.0.0 | 10.0.0 | Minor |
| .NET Runtime | net9.0 | net9.0 (keep) | NO |
| xUnit | 2.9.2 | 2.9.3 (keep v2) | NO |
| FluentAssertions | 8.8.0 | 8.9.0 | NO |

## Recommended Stack

### Core Decompilation

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| ICSharpCode.Decompiler | 10.0.0.8330 | C# decompilation, IL disassembly, type system | Just released (2026-04-06). Still targets netstandard2.0, compatible with net9.0. Adds `IDecompilerTypeSystem` interface on `CSharpDecompiler` constructor for better testability, new `ExpandParamsArguments` and `AlwaysMoveInitializer` settings. |
| System.Reflection.Metadata | 9.0.0+ (transitive) | IL scanning, metadata reading, cross-reference analysis | Comes transitively via ICSharpCode.Decompiler. Provides `MetadataReader`, `MethodBodyBlock.GetILReader()`, `ILOpCode` enum, `BlobReader` for raw IL bytecode scanning. This is the engine for string search (`ldstr`), constant search (`ldc.*`), and cross-reference tracing. |

**Confidence: HIGH** -- Verified via NuGet, GitHub releases, and package dependency analysis.

### MCP Server

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| ModelContextProtocol | 1.2.0 | MCP server framework with tool registration | Stable release (GA since Feb 2025). The `[McpServerToolType]` / `[McpServerTool]` attribute pattern and `WithToolsFromAssembly()` builder survive from 0.4.0-preview -- the core pattern used in this project is preserved. |
| ModelContextProtocol.Core | 1.2.0 (transitive) | Core protocol types | Pulled in automatically by ModelContextProtocol package. |
| Microsoft.Extensions.Hosting | 10.0.0 | DI, configuration, lifecycle management | Required by MCP SDK 1.2.0 (depends on Microsoft.Extensions.Hosting.Abstractions >= 10.0.5). Must upgrade from 8.0.0. |
| Microsoft.Extensions.Logging.Console | 10.0.0 | Stderr logging for MCP transport | Keep aligned with Hosting version. |

**Confidence: HIGH** -- Verified via NuGet package page, official SDK docs, .NET blog release announcement.

### Testing

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| xUnit | 2.9.3 | Test framework | Stay on v2. xUnit v3 (3.2.2) is stable but requires project restructuring (test projects become executables, package renames to `xunit.v3`, attribute API changes). Not worth the migration churn during a feature milestone. Upgrade separately later. |
| FluentAssertions | 8.9.0 | Assertion library | Minor patch update. No breaking changes from 8.8.0. |
| Microsoft.NET.Test.Sdk | 17.12.0 | Test runner infrastructure | Keep current. Compatible with xUnit 2.x on net9.0. |
| coverlet.collector | 6.0.2 | Code coverage | Keep current. Works with xUnit 2.x. |

**Confidence: HIGH** -- xUnit v2 is still supported. v3 migration is real but orthogonal to this milestone.

### Infrastructure (No New Dependencies)

| Technology | Version | Purpose | Why NOT add new packages |
|------------|---------|---------|--------------------------|
| System.Reflection.Metadata | (transitive) | IL scanning for cross-refs, string search, constant search | Already a dependency of ICSharpCode.Decompiler. Provides everything needed: `MetadataReader`, `MethodBodyBlock`, `BlobReader`, `ILOpCode`. No wrapper library needed. |
| ICSharpCode.Decompiler.Disassembler | (built-in namespace) | IL/CIL output for types and methods | Part of ICSharpCode.Decompiler package. `ReflectionDisassembler` class with `DisassembleType()` and `DisassembleMethod()`. No additional package. |

**Confidence: HIGH** -- These are established APIs in the existing dependency tree.

## Breaking Changes: Migration Guide

### ICSharpCode.Decompiler 9.1 to 10.0

**Risk: MODERATE** -- Most changes are in type system internals, not in the decompilation surface API.

| Change | Impact on This Project | Action Required |
|--------|----------------------|-----------------|
| Removed `ITypeReference` and implementations | LOW -- Project uses `FullTypeName` and `ITypeDefinition`, not `ITypeReference` | Verify no transitive usage in type hierarchy code |
| `ResolvedUsingScope` renamed to `UsingScope` | NONE -- Project does not use using scope APIs | No action |
| Removed `UnresolvedUsingScope` | NONE -- Not used | No action |
| Removed `ToTypeReference` | LOW -- Check if `FindTypeHierarchyUseCase` or type resolution code uses this | Search codebase for `ToTypeReference` calls |
| `CSharpDecompiler` constructor accepts `IDecompilerTypeSystem` | POSITIVE -- Enables better testability with mock type systems | Opportunity for test improvement, not a break |
| `ILInstruction.Extract()` returns `ILVariable?` (nullable) | LOW -- Only relevant if building IL analysis on top of ILInstruction tree | Relevant for new cross-ref features; handle nulls |
| `MetadataFile` constructor accepts `MetadataStringDecoder` | NONE -- Optional parameter | No action |
| New settings: `ExpandParamsArguments`, `AlwaysMoveInitializer` | NONE -- Additive | Consider enabling for better decompilation output |

**Migration steps:**
1. Update package reference from `9.1.0.7988` to `10.0.0.8330`
2. Build and fix any compilation errors (expected: minimal)
3. Run existing tests to verify decompilation output unchanged
4. Grep for `ToTypeReference`, `ITypeReference`, `UnresolvedUsingScope` usage

### ModelContextProtocol 0.4.0-preview.3 to 1.2.0

**Risk: HIGH** -- Significant API churn across 8+ preview releases, but the core pattern (attributes + builder) is preserved.

| Change | Impact on This Project | Action Required |
|--------|----------------------|-----------------|
| Core builder pattern unchanged | POSITIVE -- `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()` still works | Verify compilation |
| `[McpServerToolType]` and `[McpServerTool]` attributes preserved | POSITIVE -- All 8 existing tools use this pattern | No structural change to tool classes |
| `RequestOptions` bag replaces individual parameters | LOW -- Project tools return `string`, not using `JsonSerializerOptions` directly | Check if any tool passes serialization options |
| `IOptions<McpServerHandlers>` removed (v0.9 change) | UNKNOWN -- Check if `Program.cs` configures handlers via DI | Verify server configuration code |
| Binary data types changed to `ReadOnlyMemory<byte>` (v0.9) | NONE -- Project returns text, not binary | No action |
| `Tool.Name` now required property | LOW -- All tools already set `Name` via attribute | No action |
| Legacy SSE endpoints disabled by default (v1.2) | NONE -- Project uses stdio transport | No action |
| Collection types changed `List<T>` to `IList<T>` | LOW -- May affect if code accesses protocol types directly | Check compilation |

**Migration steps:**
1. Update package reference from `0.4.0-preview.3` to `1.2.0`
2. Update `Microsoft.Extensions.Hosting` from `8.0.0` to `10.0.0` (required by MCP SDK dependency)
3. Update `Microsoft.Extensions.Logging.Console` from `8.0.0` to `10.0.0`
4. Build and fix compilation errors
5. Test stdio transport still works end-to-end
6. Update test project's MCP SDK reference to `1.2.0` and Hosting to `10.0.0`

### Microsoft.Extensions.Hosting 8.0.0 to 10.0.0

**Risk: LOW** -- Microsoft.Extensions packages maintain backward compatibility across major versions. The upgrade is forced by MCP SDK 1.2.0 requiring `>= 10.0.5` for Hosting.Abstractions.

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| Decompiler | ICSharpCode.Decompiler 10.0 | dnSpy/dnlib | dnSpy is abandoned (archived 2020). dnlib is lower-level -- no C# decompilation, only IL/metadata reading. ICSharpCode.Decompiler provides both. |
| Decompiler | ICSharpCode.Decompiler 10.0 | ICSharpCode.Decompiler 9.1 (stay) | 10.0 is netstandard2.0 compatible, just released, and the project requirements call for the upgrade. Breaking changes are minimal for this codebase. |
| MCP SDK | ModelContextProtocol 1.2.0 | ModelContextProtocol 1.0.0 | 1.2.0 is latest stable with bug fixes. No reason to target an older stable when the upgrade path is the same from 0.4.0-preview. |
| IL scanning | System.Reflection.Metadata (transitive) | Mono.Cecil | Adding Cecil would duplicate functionality already available via S.R.Metadata (which ICSharpCode.Decompiler already depends on). Cecil would add an unnecessary dependency with overlapping capabilities. |
| Testing | xUnit 2.9.x | xUnit 3.2.x | v3 migration requires structural changes (OutputType=Exe, package renames, attribute changes). Not worth it during a feature milestone. |
| Testing | xUnit 2.9.x | NUnit 4.x | Existing tests use xUnit. No reason to switch frameworks mid-project. |
| Assertions | FluentAssertions 8.9.0 | Shouldly | FluentAssertions already in use, well-maintained, wider adoption. |

## Key APIs for New Features

### IL Scanning (cross-refs, string search, constant search)

```csharp
// Core pattern: iterate method bodies via System.Reflection.Metadata
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

// Get method body IL
MetadataReader reader = peFile.Metadata;
MethodBodyBlock body = peFile.Reader.GetMethodBody(methodDef.RelativeVirtualAddress);
BlobReader ilReader = body.GetILReader();

// Scan IL opcodes
while (ilReader.RemainingBytes > 0) {
    ILOpCode opCode = ilReader.DecodeOpCode();
    switch (opCode) {
        case ILOpCode.Ldstr:        // String literals
            var token = ilReader.ReadInt32();
            string value = reader.GetUserString(MetadataTokens.UserStringHandle(token));
            break;
        case ILOpCode.Call:          // Cross-references
        case ILOpCode.Callvirt:
        case ILOpCode.Newobj:
            var memberToken = ilReader.ReadInt32();
            // Resolve to MemberReference or MethodDefinition
            break;
        case ILOpCode.Ldc_I4:       // Integer constants
        case ILOpCode.Ldc_I8:
        case ILOpCode.Ldc_R4:
        case ILOpCode.Ldc_R8:
            // Read constant value
            break;
    }
}
```

### IL/CIL Disassembly Output

```csharp
// Use ICSharpCode.Decompiler's built-in disassembler
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;

var peFile = new PEFile(assemblyPath);
var output = new PlainTextOutput();
var disassembler = new ReflectionDisassembler(output, cancellationToken);
disassembler.DisassembleType(peFile, typeDefHandle);
string ilText = output.ToString();
```

### Assembly Metadata

```csharp
// System.Reflection.Metadata for PE/assembly metadata
MetadataReader reader = peFile.Metadata;
AssemblyDefinition asmDef = reader.GetAssemblyDefinition();
// Name, version, culture, public key token
// PE headers for bitness, entry point
PEHeaders headers = peFile.Reader.PEHeaders;
// CorHeader for target runtime, strong name
```

### Embedded Resources

```csharp
// Via MetadataReader
foreach (var resHandle in reader.ManifestResources) {
    var resource = reader.GetManifestResource(resHandle);
    string name = reader.GetString(resource.Name);
    // resource.Implementation, resource.Offset, resource.Attributes
}
```

## Installation

```bash
# Core project -- update existing references
dotnet add package ICSharpCode.Decompiler --version 10.0.0.8330
dotnet add package ModelContextProtocol --version 1.2.0
dotnet add package Microsoft.Extensions.Hosting --version 10.0.0
dotnet add package Microsoft.Extensions.Logging.Console --version 10.0.0

# Test project -- update existing references
dotnet add Tests/ package ModelContextProtocol --version 1.2.0
dotnet add Tests/ package Microsoft.Extensions.Hosting --version 10.0.0
dotnet add Tests/ package FluentAssertions --version 8.9.0
```

## Do NOT Add

| Package | Why Not |
|---------|---------|
| Mono.Cecil | Overlaps with System.Reflection.Metadata already in dependency tree |
| dnlib | Lower-level than needed; ICSharpCode.Decompiler wraps S.R.Metadata already |
| ILSpyX | Contains non-UI analyzers but tightly coupled to ILSpy app model; not designed for library consumption |
| System.Reflection.Metadata (explicit) | Already a transitive dependency via ICSharpCode.Decompiler. Adding explicitly risks version conflicts. |
| Moq / NSubstitute | Not needed for this project's test approach (integration tests against real assemblies). The new `IDecompilerTypeSystem` interface in Decompiler 10.0 enables constructor-based testing without mocking frameworks. |

## Target Framework Decision

**Stay on net9.0.** ICSharpCode.Decompiler 10.0 targets netstandard2.0 (runs on net9.0). MCP SDK 1.2.0 targets net8.0+ (runs on net9.0). .NET 10 is in preview (releases Nov 2026). No reason to upgrade runtime for this milestone.

## Sources

- [NuGet: ICSharpCode.Decompiler 10.0.0.8330](https://www.nuget.org/packages/ICSharpCode.Decompiler/10.0.0.8330) -- package metadata, dependencies, target framework
- [NuGet: ModelContextProtocol 1.2.0](https://www.nuget.org/packages/ModelContextProtocol/1.2.0) -- package metadata, dependencies
- [GitHub: ILSpy Releases](https://github.com/icsharpcode/ILSpy/releases) -- breaking changes across 9.1 to 10.0
- [GitHub: MCP C# SDK Releases](https://github.com/modelcontextprotocol/csharp-sdk/releases) -- breaking changes across 0.4.0 to 1.2.0
- [.NET Blog: MCP C# SDK v1.0](https://devblogs.microsoft.com/dotnet/release-v10-of-the-official-mcp-csharp-sdk/) -- v1.0 announcement and API patterns
- [MCP C# SDK Docs](https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html) -- getting started, builder pattern
- [Microsoft Learn: MethodBodyBlock.GetILReader](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata.methodbodyblock.getilreader) -- IL scanning API
- [Microsoft Learn: ILOpCode](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata.ilopcode) -- IL opcode enumeration
- [xUnit v3 Migration Guide](https://xunit.net/docs/getting-started/v3/migration) -- v2 to v3 changes (deferred)
- [NuGet: FluentAssertions 8.9.0](https://www.nuget.org/packages/fluentassertions/) -- latest version
- [NuGet: xUnit v3 3.2.2](https://www.nuget.org/packages/xunit.v3) -- available but deferred
