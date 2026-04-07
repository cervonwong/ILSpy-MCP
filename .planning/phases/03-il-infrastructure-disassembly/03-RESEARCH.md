# Phase 3: IL Infrastructure & Disassembly - Research

**Researched:** 2026-04-07
**Domain:** CIL disassembly via ICSharpCode.Decompiler's ReflectionDisassembler
**Confidence:** HIGH

## Summary

This phase adds two new MCP tools (`disassemble_type`, `disassemble_method`) using `ReflectionDisassembler` from ICSharpCode.Decompiler. The API is well-documented, stable, and already available as part of the existing ICSharpCode.Decompiler 10.0 dependency -- no new packages needed.

The main implementation challenge is that `ReflectionDisassembler.DisassembleType()` always outputs full method bodies, but decision D-02 requires type-level disassembly to show structure and signatures only (no IL bodies). This means the implementation cannot simply call `DisassembleType()` -- it must manually iterate type members using `DisassembleMethodHeader()`, `DisassembleFieldHeader()`, etc. to produce a headers-only view. Method-level disassembly via `DisassembleMethod()` is straightforward.

**Primary recommendation:** Create a new `IDisassemblyService` interface (parallel to `IDecompilerService`) with `DisassembleTypeAsync` and `DisassembleMethodAsync` methods, implemented using `ReflectionDisassembler` with `PlainTextOutput` for string capture. Follow the exact 4-layer pattern established by the decompile tools.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Use `ReflectionDisassembler` resolved IL output (annotated with resolved type/method names, not raw tokens). Add a summary header above the IL with type metadata for orientation.
- **D-02:** Type-level disassembly shows structure and method signatures only (no IL bodies). Users drill down to specific methods via `disassemble_method` for full IL instruction listings. This keeps type-level output manageable for large types.
- **D-03:** Method-level disassembly returns the complete IL body with resolved names, `.maxstack`, `IL_xxxx` labels, and full instruction listings.
- **D-04:** Two separate tools: `disassemble_type` and `disassemble_method` -- follows the existing `decompile_type`/`decompile_method` pattern. Tool count goes from 8 to 10.
- **D-05:** `disassemble_` prefix -- distinct from `decompile_` (C# output), mirrors ILSpy's own terminology.
- **D-06:** Same base parameters as decompile counterparts (`assembly_path`, `type_name`, `method_name`) plus optional IL-specific flags (`show_bytes` for raw opcode byte sequences, `show_tokens` for metadata token numbers). Consistent parameter naming and validation.
- **D-07:** Method overload disambiguation matches existing `decompile_method` behavior -- if ambiguous, return error listing available overloads so user can specify parameter types.
- **D-08:** Validate IL structural correctness via IL structure markers: assert output contains `.method` directives, `.maxstack`, `IL_xxxx` labels, known opcodes (`ldarg`, `call`, `ret`, etc.). Test against TestTargets methods with known signatures.
- **D-09:** Dedicated tests for each optional flag -- verify `show_bytes` adds hex byte sequences, `show_tokens` adds metadata token numbers to output.
- **D-10:** Comprehensive error coverage: standard errors (invalid assembly path, type not found, method not found) matching existing `decompile_` tool error codes, plus IL-specific edge cases (abstract methods with no IL body, extern methods, types with no methods).

### Claude's Discretion
- Summary header content and formatting for type-level disassembly (Claude picks what ReflectionDisassembler naturally produces)
- Exact implementation of `show_bytes` and `show_tokens` flags (how they map to ReflectionDisassembler options)
- Domain model types for IL output (whether to reuse `DecompilationResult` or create new types)
- Infrastructure service method signatures and internal organization
- Whether to add new methods to `IDecompilerService` or create a separate `IDisassemblyService` interface

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| IL-01 | User can get raw CIL/MSIL disassembly output for a type | `ReflectionDisassembler` with manual member iteration using `DisassembleMethodHeader()` for headers-only output |
| IL-02 | User can get raw CIL/MSIL disassembly output for a specific method | `ReflectionDisassembler.DisassembleMethod()` with `PlainTextOutput` captures full IL body |
| TEST-03 | IL disassembly output (IL-01, IL-02) has integration tests verifying structural correctness | TestTargets assembly provides deterministic types; assert `.method`, `.maxstack`, `IL_xxxx`, known opcodes |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ICSharpCode.Decompiler | 10.0.0.8330 | ReflectionDisassembler for IL output | Already installed (Phase 2 upgrade). Provides `ReflectionDisassembler`, `PlainTextOutput`, `MethodBodyDisassembler` |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| ICSharpCode.Decompiler.Disassembler (namespace) | (built-in) | `ReflectionDisassembler`, `MethodBodyDisassembler` | All disassembly operations |
| ICSharpCode.Decompiler.Output (namespace) | (built-in) | `PlainTextOutput`, `ITextOutput` | Capturing disassembly as string |
| System.Reflection.Metadata | (transitive) | `TypeDefinitionHandle`, `MethodDefinitionHandle` | Handle casting for disassembler API |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Separate `IDisassemblyService` | Add methods to `IDecompilerService` | Separate interface is cleaner -- disassembly and decompilation are distinct concerns. `IDecompilerService` already has 7 methods. |
| Custom IL output builder | `ReflectionDisassembler` | Custom builder would be massive hand-roll effort. ReflectionDisassembler handles all IL formatting, token resolution, exception handler blocks. |
| New `DisassemblyResult` model | Reuse `DecompilationResult` | New model is recommended -- IL output has different semantics (no "SourceCode", has optional flags). Keep it simple: `string` return from service, like `DecompileMethodAsync`. |

**Installation:**
```bash
# No new packages needed -- all APIs are in ICSharpCode.Decompiler 10.0 already installed
```

## Architecture Patterns

### Recommended Project Structure
```
Domain/
  Services/
    IDisassemblyService.cs        # NEW: domain port for disassembly
Infrastructure/
  Decompiler/
    ILSpyDisassemblyService.cs    # NEW: ReflectionDisassembler adapter
Application/
  UseCases/
    DisassembleTypeUseCase.cs     # NEW: type-level use case
    DisassembleMethodUseCase.cs   # NEW: method-level use case
Transport/
  Mcp/
    Tools/
      DisassembleTypeTool.cs      # NEW: MCP tool handler
      DisassembleMethodTool.cs    # NEW: MCP tool handler
Tests/
  Tools/
    DisassembleTypeToolTests.cs   # NEW: integration tests
    DisassembleMethodToolTests.cs # NEW: integration tests
Program.cs                        # MODIFY: add DI registrations
```

### Pattern 1: ReflectionDisassembler Instantiation
**What:** Create a `ReflectionDisassembler` with `PlainTextOutput` for string capture
**When to use:** Every disassembly operation
**Example:**
```csharp
// Source: ICSharpCode.Decompiler ReflectionDisassembler.cs + PlainTextOutput.cs
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Output;

var writer = new StringWriter();
var output = new PlainTextOutput(writer);
var disassembler = new ReflectionDisassembler(output, cancellationToken)
{
    ShowMetadataTokens = showTokens,      // maps to D-06 show_tokens flag
    ShowRawRVAOffsetAndBytes = showBytes,  // maps to D-06 show_bytes flag
    DetectControlStructure = true
};
```

### Pattern 2: Getting MetadataFile and Handles from Type System
**What:** Bridge from CSharpDecompiler's type system to ReflectionDisassembler's handle-based API
**When to use:** To resolve type/method names to the handles that ReflectionDisassembler expects
**Example:**
```csharp
// Source: CSharpDecompiler.cs, MetadataModule.cs
var decompiler = new CSharpDecompiler(assemblyPath, settings);
var type = decompiler.TypeSystem.MainModule.GetTypeDefinition(new FullTypeName(typeName));
var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;

// Cast MetadataToken to typed handle
var typeHandle = (TypeDefinitionHandle)type.MetadataToken;
var methodHandle = (MethodDefinitionHandle)method.MetadataToken;

// Pass to ReflectionDisassembler
disassembler.DisassembleType(metadataFile, typeHandle);     // full type with bodies
disassembler.DisassembleMethod(metadataFile, methodHandle); // single method with body
```

### Pattern 3: Type-Level Headers-Only Output (D-02 Compliance)
**What:** DisassembleType includes full method bodies, but D-02 requires headers only
**When to use:** `disassemble_type` tool implementation
**Example:**
```csharp
// CANNOT use DisassembleType directly -- it always includes method bodies
// Instead, manually iterate members and use header-only methods:

// Option A: Use DisassembleMethodHeader for each method
foreach (var method in type.Methods)
{
    var methodHandle = (MethodDefinitionHandle)method.MetadataToken;
    disassembler.DisassembleMethodHeader(metadataFile, methodHandle);
    // This outputs .method directive with signature but NO IL body
}

// Option B: Use DisassembleFieldHeader for each field
foreach (var field in type.Fields)
{
    var fieldHandle = (FieldDefinitionHandle)field.MetadataToken;
    disassembler.DisassembleFieldHeader(metadataFile, fieldHandle);
}

// Properties and events use their full methods (DisassembleProperty, DisassembleEvent)
// which output .property/.event directives
```

### Pattern 4: Follow Existing Tool Pattern Exactly
**What:** 4-layer architecture for each new tool
**When to use:** Both disassemble tools
**Example:**
```
1. Domain:    IDisassemblyService.DisassembleTypeAsync(AssemblyPath, TypeName, bool showTokens, bool showBytes, CancellationToken)
2. Infra:     ILSpyDisassemblyService implements using ReflectionDisassembler
3. UseCase:   DisassembleTypeUseCase wraps with timeout/cancellation (copy DecompileTypeUseCase pattern)
4. Transport: DisassembleTypeTool with [McpServerToolType]/[McpServerTool] attributes, error mapping
5. DI:        Register in Program.cs RegisterServices method
6. Tests:     DisassembleTypeToolTests using ToolTestFixture and TestTargets assembly
```

### Anti-Patterns to Avoid
- **Calling DisassembleType() for type-level output:** It includes full method bodies which violates D-02. Must use header methods instead.
- **Creating PEFile separately from CSharpDecompiler:** Would duplicate file loading. Instead, use `decompiler.TypeSystem.MainModule.MetadataFile` to get the already-loaded file.
- **Adding disassembly methods to IDecompilerService:** This interface already has 7 methods. Disassembly is a separate concern -- use a new `IDisassemblyService`.
- **Returning raw ReflectionDisassembler output without header:** D-01 requires a summary header with type metadata for orientation.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| IL instruction formatting | Custom opcode printer | `ReflectionDisassembler` / `MethodBodyDisassembler` | Handles all 200+ opcodes, metadata token resolution, exception handler blocks, sequence points |
| IL text output capture | Manual StringBuilder with IL formatting | `PlainTextOutput` with `StringWriter` | Handles indentation, line tracking, reference formatting |
| Type definition IL structure | Manual `.class` directive builder | `ReflectionDisassembler` header methods | Handles attributes, flags, generic params, constraints, extends/implements |
| Method signature IL format | Manual `.method` directive builder | `DisassembleMethodHeader()` | Handles calling conventions, marshal info, generic params, custom attributes |

**Key insight:** `ReflectionDisassembler` is ILSpy's production disassembler -- it handles every edge case (PInvoke methods, generic constraints, security attributes, exception handler blocks). Building custom IL formatting would be a massive effort with guaranteed edge-case bugs.

## Common Pitfalls

### Pitfall 1: DisassembleType Includes Method Bodies
**What goes wrong:** Calling `DisassembleType()` produces output with full IL bodies, which violates D-02's requirement for headers-only type view.
**Why it happens:** `DisassembleType()` internally calls `DisassembleMethod()` (not `DisassembleMethodHeader()`) for each method.
**How to avoid:** For type-level disassembly, manually iterate members and call `DisassembleMethodHeader()`, `DisassembleFieldHeader()`, `DisassembleProperty()`, `DisassembleEvent()` individually.
**Warning signs:** Type-level output contains `.maxstack`, `IL_xxxx` labels, or opcode mnemonics.

### Pitfall 2: Abstract/Extern Methods Have No IL Body
**What goes wrong:** `DisassembleMethod()` for abstract or extern methods may produce unexpected output (no `.maxstack`, no IL instructions).
**Why it happens:** Abstract methods have no body -- they are signature-only. Extern methods delegate to native code.
**How to avoid:** Test against `Shape.Area()` (abstract) in TestTargets. The output should still be valid IL (`.method` directive with no body block). Handle gracefully -- this is valid output, not an error.
**Warning signs:** Tests expecting `.maxstack` fail for abstract methods.

### Pitfall 3: MetadataToken Cast Safety
**What goes wrong:** Casting `type.MetadataToken` to `(TypeDefinitionHandle)` fails if the entity is not from the main module.
**Why it happens:** Type forwards and referenced types have different handle types.
**How to avoid:** Always resolve types via `GetTypeDefinition()` first (which returns null for type forwards), then check for null before casting. The existing `DecompileTypeAsync` pattern already does this.
**Warning signs:** `InvalidCastException` on `MetadataToken`.

### Pitfall 4: PlainTextOutput Requires StringWriter for String Capture
**What goes wrong:** Using the parameterless `PlainTextOutput()` constructor and then trying to get the output string.
**Why it happens:** The parameterless constructor creates an internal `StringWriter` but it's accessible via `ToString()`.
**How to avoid:** Either use `new PlainTextOutput(writer)` with an explicit `StringWriter` and call `writer.ToString()`, or use the parameterless constructor and call `output.ToString()`. Both work, but explicit writer is clearer.
**Warning signs:** Empty or null output string.

### Pitfall 5: DI Registration Order
**What goes wrong:** New use cases or tools fail to resolve from DI container.
**Why it happens:** Missing registration in `Program.cs` `RegisterServices()` method, or missing registration in `ToolTestFixture`.
**How to avoid:** Add registrations to BOTH `Program.cs` and `Tests/Fixtures/ToolTestFixture.cs`. Follow the exact pattern of existing registrations.
**Warning signs:** `InvalidOperationException` about unresolved service type.

## Code Examples

### Complete Infrastructure Method: DisassembleMethod
```csharp
// Source: ReflectionDisassembler.cs, PlainTextOutput.cs, CSharpDecompiler.cs
public async Task<string> DisassembleMethodAsync(
    AssemblyPath assemblyPath,
    TypeName typeName,
    string methodName,
    bool showBytes = false,
    bool showTokens = false,
    CancellationToken cancellationToken = default)
{
    return await Task.Run(() =>
    {
        var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
        var type = decompiler.TypeSystem.MainModule.GetTypeDefinition(
            new FullTypeName(typeName.FullName));

        if (type == null)
            throw new TypeNotFoundException(typeName.FullName, assemblyPath.Value);

        var methods = type.Methods.Where(m => m.Name == methodName).ToList();
        if (!methods.Any())
            throw new MethodNotFoundException(methodName, typeName.FullName);

        var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
        var writer = new StringWriter();
        var output = new PlainTextOutput(writer);
        var disassembler = new ReflectionDisassembler(output, cancellationToken)
        {
            ShowMetadataTokens = showTokens,
            ShowRawRVAOffsetAndBytes = showBytes,
            DetectControlStructure = true
        };

        foreach (var method in methods)
        {
            var handle = (MethodDefinitionHandle)method.MetadataToken;
            disassembler.DisassembleMethod(metadataFile, handle);
            output.WriteLine();
        }

        return writer.ToString();
    }, cancellationToken);
}
```

### Complete Infrastructure Method: DisassembleType (Headers-Only)
```csharp
// Source: ReflectionDisassembler.cs (adapted for D-02 headers-only)
public async Task<string> DisassembleTypeAsync(
    AssemblyPath assemblyPath,
    TypeName typeName,
    bool showTokens = false,
    CancellationToken cancellationToken = default)
{
    return await Task.Run(() =>
    {
        var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
        var type = decompiler.TypeSystem.MainModule.GetTypeDefinition(
            new FullTypeName(typeName.FullName));

        if (type == null)
            throw new TypeNotFoundException(typeName.FullName, assemblyPath.Value);

        var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
        var writer = new StringWriter();
        var output = new PlainTextOutput(writer);
        var disassembler = new ReflectionDisassembler(output, cancellationToken)
        {
            ShowMetadataTokens = showTokens,
            DetectControlStructure = true
        };

        // Build summary header (D-01)
        output.WriteLine($"// Type: {type.FullName}");
        output.WriteLine($"// Assembly: {assemblyPath.FileName}");
        output.WriteLine($"// Methods: {type.Methods.Count}");
        output.WriteLine();

        // Method signatures only (D-02) -- use header methods
        foreach (var method in type.Methods)
        {
            var handle = (MethodDefinitionHandle)method.MetadataToken;
            disassembler.DisassembleMethodHeader(metadataFile, handle);
            output.WriteLine();
        }

        // Fields
        foreach (var field in type.Fields)
        {
            var handle = (FieldDefinitionHandle)field.MetadataToken;
            disassembler.DisassembleFieldHeader(metadataFile, handle);
            output.WriteLine();
        }

        // Properties and events
        foreach (var prop in type.Properties)
        {
            var handle = (PropertyDefinitionHandle)prop.MetadataToken;
            disassembler.DisassembleProperty(metadataFile, handle);
            output.WriteLine();
        }

        foreach (var evt in type.Events)
        {
            var handle = (EventDefinitionHandle)evt.MetadataToken;
            disassembler.DisassembleEvent(metadataFile, handle);
            output.WriteLine();
        }

        return writer.ToString();
    }, cancellationToken);
}
```

### MCP Tool Pattern
```csharp
// Source: existing DecompileTypeTool.cs pattern
[McpServerToolType]
public sealed class DisassembleTypeTool
{
    [McpServerTool(Name = "disassemble_type")]
    [Description("Get raw CIL/IL disassembly of a .NET type showing method signatures, fields, properties, and events in IL format. Use disassemble_method to drill into specific method IL bodies.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Full name of the type (e.g., 'System.String')")] string typeName,
        [Description("Show metadata token numbers (e.g., /* 06000001 */)")] bool showTokens = false,
        CancellationToken cancellationToken = default) { ... }
}
```

## ReflectionDisassembler API Reference

### Key Classes
| Class | Namespace | Purpose |
|-------|-----------|---------|
| `ReflectionDisassembler` | `ICSharpCode.Decompiler.Disassembler` | Main disassembly engine |
| `MethodBodyDisassembler` | `ICSharpCode.Decompiler.Disassembler` | IL body disassembly (used internally) |
| `PlainTextOutput` | `ICSharpCode.Decompiler.Output` | String output capture |
| `ITextOutput` | `ICSharpCode.Decompiler.Output` | Output abstraction interface |

### ReflectionDisassembler Properties
| Property | Type | Default | Maps To |
|----------|------|---------|---------|
| `ShowMetadataTokens` | bool | false | `show_tokens` parameter (D-06) |
| `ShowRawRVAOffsetAndBytes` | bool | false | `show_bytes` parameter (D-06) |
| `DetectControlStructure` | bool | true | Always enable for readable output |
| `ShowSequencePoints` | bool | false | Not needed (no PDB) |
| `ExpandMemberDefinitions` | bool | false | UI folding only, irrelevant for text output |
| `ShowMetadataTokensInBase10` | bool | false | Keep hex (standard IL convention) |

### Method Signatures
| Method | Parameters | Output |
|--------|-----------|--------|
| `DisassembleType(MetadataFile, TypeDefinitionHandle)` | Module + type handle | Full type with all method bodies |
| `DisassembleMethod(MetadataFile, MethodDefinitionHandle)` | Module + method handle | Full method with IL body |
| `DisassembleMethodHeader(MetadataFile, MethodDefinitionHandle)` | Module + method handle | Method signature only (no body) |
| `DisassembleFieldHeader(MetadataFile, FieldDefinitionHandle)` | Module + field handle | Field declaration only |
| `DisassembleProperty(MetadataFile, PropertyDefinitionHandle)` | Module + property handle | Property with accessors |
| `DisassembleEvent(MetadataFile, EventDefinitionHandle)` | Module + event handle | Event with add/remove |

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + FluentAssertions 8.9.0 |
| Config file | `Tests/ILSpy.Mcp.Tests.csproj` |
| Quick run command | `dotnet test Tests/ --filter "DisassembleType\|DisassembleMethod" --no-build` |
| Full suite command | `dotnet test Tests/` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| IL-01 | Type disassembly returns IL structure (method headers, fields) | integration | `dotnet test Tests/ --filter "DisassembleTypeToolTests" -x` | Wave 0 |
| IL-02 | Method disassembly returns full IL body | integration | `dotnet test Tests/ --filter "DisassembleMethodToolTests" -x` | Wave 0 |
| TEST-03 | IL output structurally correct (`.method`, `.maxstack`, `IL_xxxx`, opcodes) | integration | `dotnet test Tests/ --filter "DisassembleMethodToolTests" -x` | Wave 0 |
| TEST-03 | `show_bytes` flag adds hex byte sequences | integration | `dotnet test Tests/ --filter "ShowBytes" -x` | Wave 0 |
| TEST-03 | `show_tokens` flag adds metadata token numbers | integration | `dotnet test Tests/ --filter "ShowTokens" -x` | Wave 0 |
| TEST-03 | Error handling: type not found, method not found, invalid assembly | integration | `dotnet test Tests/ --filter "DisassembleType\|DisassembleMethod" -x` | Wave 0 |
| TEST-03 | Edge case: abstract method (no IL body) | integration | `dotnet test Tests/ --filter "AbstractMethod" -x` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test Tests/ --filter "DisassembleType\|DisassembleMethod" --no-build`
- **Per wave merge:** `dotnet test Tests/`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `Tests/Tools/DisassembleTypeToolTests.cs` -- covers IL-01, TEST-03 (type-level)
- [ ] `Tests/Tools/DisassembleMethodToolTests.cs` -- covers IL-02, TEST-03 (method-level)
- [ ] Test fixture update: `Tests/Fixtures/ToolTestFixture.cs` needs new service/tool registrations

### TestTargets Available for IL Testing
| Type | Good For Testing | Key IL Characteristics |
|------|-----------------|----------------------|
| `SimpleClass` | Basic method IL, constructors | `ldstr`, `call`, `ret`, `newobj`, string interpolation |
| `Shape` (abstract) | Abstract method edge case | `.method abstract` with no body |
| `Circle` | Override methods | `call` to `Math.PI`, arithmetic opcodes |
| `IAnimal` (interface) | Interface IL structure | `.method abstract virtual` |
| `Dog`/`Cat` | Interface implementation | `ldstr` with known string constants |

## Sources

### Primary (HIGH confidence)
- [ReflectionDisassembler.cs source](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.Decompiler/Disassembler/ReflectionDisassembler.cs) - Full API, method signatures, property definitions
- [PlainTextOutput.cs source](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.Decompiler/Output/PlainTextOutput.cs) - ITextOutput implementation for string capture
- [MethodBodyDisassembler.cs source](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.Decompiler/Disassembler/MethodBodyDisassembler.cs) - ShowRawRVAOffsetAndBytes, ShowMetadataTokens properties
- [SharpLab ILDecompiler.cs](https://github.com/ashmind/SharpLab/blob/main/source/Server/Decompilation/ILDecompiler.cs) - Production usage example of ReflectionDisassembler
- [MetadataModule.cs source](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.Decompiler/TypeSystem/MetadataModule.cs) - MetadataFile property access pattern
- Existing codebase: `ILSpyDecompilerService.cs`, `DecompileTypeTool.cs`, `DecompileMethodTool.cs` - Established patterns

### Secondary (MEDIUM confidence)
- [ICSharpCode.Decompiler wiki](https://github.com/icsharpcode/ILSpy/wiki/Getting-Started-With-ICSharpCode.Decompiler) - Getting started guide

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All APIs are in ICSharpCode.Decompiler 10.0 already installed. Verified via source code.
- Architecture: HIGH - Follows exact existing 4-layer pattern with known DI registration approach.
- Pitfalls: HIGH - DisassembleType body inclusion verified via source code. Handle casting pattern confirmed.
- API surface: HIGH - ReflectionDisassembler constructor, properties, and methods verified against current master source.

**Research date:** 2026-04-07
**Valid until:** 2026-05-07 (stable API, no expected changes)
