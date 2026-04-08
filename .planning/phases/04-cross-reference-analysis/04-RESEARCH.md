# Phase 4: Cross-Reference Analysis - Research

**Researched:** 2026-04-08
**Domain:** IL scanning, type system metadata, cross-reference tracing via System.Reflection.Metadata
**Confidence:** HIGH

## Summary

Phase 4 adds cross-reference analysis to the MCP server -- the ability to trace execution flow by finding usages, implementors, dependencies, and instantiation sites within an assembly. This requires two distinct technical approaches: (1) IL bytecode scanning via `System.Reflection.Metadata` for usage/dependency/instantiation analysis (XREF-01, XREF-03, XREF-04), and (2) type system metadata traversal via ICSharpCode.Decompiler's `ITypeDefinition` for implementor discovery (XREF-02).

The existing codebase already has all the building blocks: `CSharpDecompiler` creates type systems with `MainModule.TypeDefinitions` for type hierarchy traversal, and `MetadataFile` provides access to `MetadataReader` for raw IL scanning. The disassembly service (Phase 3) already demonstrates both access patterns. The new `ICrossReferenceService` follows the exact same domain interface pattern as `IDecompilerService` and `IDisassemblyService`.

**Primary recommendation:** Build `ILSpyCrossReferenceService` with two internal strategies -- a type-system walker for `FindImplementors` and an IL scanner for the other three methods. Use `MetadataReader` + `MethodBodyBlock.GetILReader()` + `ILOpCode` enum for IL scanning. Keep IL scanning helpers private to the infrastructure class; Phase 6 can extract shared utilities later.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** 4 separate dedicated MCP tools: `find_usages`, `find_implementors`, `find_dependencies`, `find_instantiations` -- one tool per cross-reference concern.
- **D-02:** 1 unified `analyze_references` tool that acts as a thin dispatcher -- takes an `analysis_type` parameter and routes to the same use case.
- **D-03:** Total tool count goes from 10 to 15 (5 new tools).
- **D-04:** `find_usages` requires `member_name` (not optional). For type-level references, users should use `find_instantiations` or `find_implementors` instead.
- **D-05:** Parameters follow existing conventions: `assembly_path`, `type_name`, `member_name` (where applicable).
- **D-06:** Method context detail level -- each hit includes: containing type full name, containing method signature, IL offset, and the opcode used.
- **D-07:** Results grouped by containing type, ordered by IL offset within each method.
- **D-08:** `find_implementors` returns type names only with their relationship. Users call `get_type_members` on results for member details.
- **D-09:** New `ICrossReferenceService` domain interface with 4 methods. All four methods live on this single interface.
- **D-10:** Infrastructure implementation uses `System.Reflection.Metadata` APIs: `MetadataReader`, `MethodBodyBlock.GetILReader()`, `ILOpCode` enum for IL scanning; and `ITypeDefinition` hierarchy for implementor discovery.
- **D-11:** Shared IL scanning helper methods within the infrastructure layer -- private helpers. Don't over-abstract now.
- **D-12:** 4 dedicated use cases: `FindUsagesUseCase`, `FindImplementorsUseCase`, `FindDependenciesUseCase`, `FindInstantiationsUseCase`.
- **D-13:** Add focused cross-reference test types to TestTargets assembly -- ~5-8 new purpose-built types.
- **D-14:** One test class per tool: `FindUsagesToolTests`, `FindImplementorsToolTests`, `FindDependenciesToolTests`, `FindInstantiationsToolTests`, `AnalyzeReferencesToolTests`.
- **D-15:** Structural assertions against known cross-reference patterns in TestTargets.

### Claude's Discretion
- Exact domain model types for results (`UsageResult`, `ImplementorResult`, `DependencyResult`, `InstantiationResult`) -- field names and shapes
- Infrastructure service internal organization and IL scanning helper structure
- Specific TestTargets type names and relationship graph
- `analyze_references` dispatcher implementation pattern (switch statement vs dictionary dispatch)
- Error handling for edge cases (abstract methods with no body, extern methods, generic instantiations)
- Whether `find_dependencies` member_name parameter is optional (type-level vs method-level dependency analysis)

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| XREF-01 | Find all usages of a type member across an assembly via IL scanning | IL scanner iterates all method bodies, decodes call/callvirt/ldfld/ldsfld/stfld/stsfld opcodes, matches against target member token |
| XREF-02 | Find all types implementing a given interface or extending a given base class | Type system traversal via `ITypeDefinition.DirectBaseTypes` -- no IL scanning needed |
| XREF-03 | Find all outward dependencies of a method or type | IL scanner reads method body, collects all member references (call targets, field accesses, type refs) |
| XREF-04 | Find all instantiation sites (newobj) of a given type | IL scanner filters for `ILOpCode.Newobj` where constructor belongs to target type |
| TEST-02 | Cross-reference analysis tools have integration tests against real assemblies | Purpose-built TestTargets types with known, traceable relationships |
</phase_requirements>

## Standard Stack

### Core (Already in project -- no new dependencies)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ICSharpCode.Decompiler | 10.0.0.8330 | Type system access via `CSharpDecompiler`, `ITypeDefinition` hierarchy | Already upgraded in Phase 2. Provides `MainModule.TypeDefinitions` for implementor discovery. |
| System.Reflection.Metadata | (transitive via Decompiler) | IL bytecode scanning: `MetadataReader`, `MethodBodyBlock`, `ILOpCode` | Core engine for XREF-01, XREF-03, XREF-04. Provides `GetILReader()` returning `BlobReader` for opcode-by-opcode iteration. |

### Supporting (Already in project)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| ModelContextProtocol | 1.2.0 | MCP tool registration via `[McpServerToolType]`/`[McpServerTool]` | 5 new tool classes auto-discovered by `WithToolsFromAssembly()` |
| xUnit | 2.9.3 | Test framework for 5 new test classes | One test class per tool, `[Collection("ToolTests")]` shared fixture |
| FluentAssertions | 8.9.0 | Structural assertions on cross-reference results | `.Should().Contain()`, `.Should().HaveCount()`, etc. |

**Installation:** No new packages required. All dependencies are already in the project.

## Architecture Patterns

### Recommended Project Structure (new files only)
```
Domain/
  Services/
    ICrossReferenceService.cs          # 4 methods: FindUsages, FindImplementors, FindDependencies, FindInstantiations
  Models/
    CrossReferenceResults.cs           # UsageResult, ImplementorResult, DependencyResult, InstantiationResult records
Infrastructure/
  Decompiler/
    ILSpyCrossReferenceService.cs      # Implementation with IL scanner + type system walker
Application/
  UseCases/
    FindUsagesUseCase.cs
    FindImplementorsUseCase.cs
    FindDependenciesUseCase.cs
    FindInstantiationsUseCase.cs
Transport/
  Mcp/
    Tools/
      FindUsagesTool.cs
      FindImplementorsTool.cs
      FindDependenciesTool.cs
      FindInstantiationsTool.cs
      AnalyzeReferencesTool.cs          # Dispatcher tool
TestTargets/
  Types/
    CrossReferenceTypes.cs             # Purpose-built types for testing
Tests/
  Tools/
    FindUsagesToolTests.cs
    FindImplementorsToolTests.cs
    FindDependenciesToolTests.cs
    FindInstantiationsToolTests.cs
    AnalyzeReferencesToolTests.cs
```

### Pattern 1: IL Bytecode Scanning
**What:** Iterate every method body in an assembly, decode IL opcodes, match against target tokens.
**When to use:** XREF-01 (usages), XREF-03 (dependencies), XREF-04 (instantiations).
**How it works:**

```csharp
// Access pattern established by ILSpyDisassemblyService
var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
var metadataReader = metadataFile.Metadata; // PEReader's MetadataReader

foreach (var typeDefHandle in metadataReader.TypeDefinitions)
{
    var typeDef = metadataReader.GetTypeDefinition(typeDefHandle);
    foreach (var methodDefHandle in typeDef.GetMethods())
    {
        var methodDef = metadataReader.GetMethodDefinition(methodDefHandle);
        
        // Skip abstract/extern methods (no body)
        if (methodDef.RelativeVirtualAddress == 0) continue;
        
        var methodBody = metadataFile.GetMethodBody(methodDef.RelativeVirtualAddress);
        var ilReader = methodBody.GetILReader();
        
        while (ilReader.RemainingBytes > 0)
        {
            var offset = ilReader.Offset;
            var opCode = ilReader.DecodeOpCode();
            
            // Check for member reference opcodes
            if (opCode == ILOpCode.Call || opCode == ILOpCode.Callvirt || 
                opCode == ILOpCode.Newobj || opCode == ILOpCode.Ldfld || 
                opCode == ILOpCode.Stfld || opCode == ILOpCode.Ldsfld ||
                opCode == ILOpCode.Stsfld || opCode == ILOpCode.Ldftn ||
                opCode == ILOpCode.Ldvirtftn)
            {
                var token = ilReader.ReadInt32();
                var memberRef = MetadataTokens.EntityHandle(token);
                // Resolve and match against target
            }
            else
            {
                SkipOperand(ref ilReader, opCode); // Must advance past operand bytes
            }
        }
    }
}
```

**Critical detail:** `BlobReader.DecodeOpCode()` reads 1 or 2 bytes (for 0xFE prefix). After decoding, you MUST consume the operand bytes based on the opcode. Failure to do so corrupts the read position and produces garbage results.

### Pattern 2: Type System Traversal for Implementors
**What:** Walk all type definitions, check `DirectBaseTypes` for interface/base class match.
**When to use:** XREF-02 (implementors).

```csharp
// Already proven pattern from ILSpyDecompilerService.MapToTypeInfo
var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
var mainModule = decompiler.TypeSystem.MainModule;

var implementors = new List<ImplementorResult>();
foreach (var type in mainModule.TypeDefinitions.Where(t => t.ParentModule == mainModule))
{
    // Check interfaces
    foreach (var baseType in type.DirectBaseTypes)
    {
        if (baseType.FullName == targetTypeName)
        {
            var relationship = baseType.Kind == TypeKind.Interface 
                ? "implements" 
                : "extends";
            implementors.Add(new ImplementorResult(type.FullName, relationship));
        }
    }
}
```

### Pattern 3: Use Case Pattern (established)
**What:** Each use case injects service + `ITimeoutService` + `IConcurrencyLimiter` + `ILogger`.
**Exact pattern from `DecompileMethodUseCase`:**
- Validate inputs with `AssemblyPath.Create()` and `TypeName.Create()`
- Wrap service call in `_limiter.ExecuteAsync(async () => { ... })`
- Use `_timeout.CreateTimeoutToken(cancellationToken)` inside limiter
- Catch `OperationCanceledException` with two branches (user-cancelled vs timeout)
- Re-throw `DomainException` subtypes directly

### Pattern 4: MCP Tool Pattern (established)
**What:** Tool class with `[McpServerToolType]`, method with `[McpServerTool(Name = "...")]` and `[Description]`.
**Exact pattern from `DecompileTypeTool`:**
- Constructor injects use case + `ILogger`
- `ExecuteAsync` delegates to use case
- Catch block maps domain exceptions to `McpToolException` with error codes
- Error codes: `TYPE_NOT_FOUND`, `ASSEMBLY_LOAD_FAILED`, `TIMEOUT`, `CANCELLED`, `INTERNAL_ERROR`
- Add `MEMBER_NOT_FOUND` error code for `find_usages` when member_name is invalid

### Pattern 5: Dispatcher Tool
**What:** `analyze_references` routes to the correct use case based on `analysis_type` parameter.

```csharp
[McpServerTool(Name = "analyze_references")]
[Description("Unified cross-reference analysis dispatcher...")]
public async Task<string> ExecuteAsync(
    [Description("Path to the .NET assembly file")] string assemblyPath,
    [Description("Type of analysis: usages, implementors, dependencies, instantiations")] string analysisType,
    [Description("Full name of the type")] string typeName,
    [Description("Member name (required for usages)")] string? memberName = null,
    CancellationToken cancellationToken = default)
{
    return analysisType.ToLowerInvariant() switch
    {
        "usages" => await _findUsagesUseCase.ExecuteAsync(assemblyPath, typeName, memberName!, cancellationToken),
        "implementors" => await _findImplementorsUseCase.ExecuteAsync(assemblyPath, typeName, cancellationToken),
        "dependencies" => await _findDependenciesUseCase.ExecuteAsync(assemblyPath, typeName, memberName, cancellationToken),
        "instantiations" => await _findInstantiationsUseCase.ExecuteAsync(assemblyPath, typeName, cancellationToken),
        _ => throw new McpToolException("INVALID_ANALYSIS_TYPE", $"Unknown analysis type: {analysisType}")
    };
}
```

### Anti-Patterns to Avoid
- **Loading assembly multiple times per scan:** Create one `CSharpDecompiler` instance per service call, reuse across all method body iterations within that call.
- **Ignoring abstract/extern methods:** Methods with `RelativeVirtualAddress == 0` have no body. Attempting to read their body will throw. Always check before calling `GetMethodBody()`.
- **Not consuming IL operand bytes:** After `DecodeOpCode()`, the reader position is right after the opcode. You MUST read or skip the operand bytes (4 bytes for int32 token, variable for other opcodes) before decoding the next opcode.
- **Using ICSharpCode.Decompiler's analyzer classes directly:** ILSpy has built-in analyzer infrastructure (`Analyzers` namespace) but it's tightly coupled to the GUI model and not designed for library consumption. Use raw `System.Reflection.Metadata` instead per CLAUDE.md guidance.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| IL opcode decoding | Custom opcode parser | `BlobReader.DecodeOpCode()` + `ILOpCode` enum | .NET SDK handles 1-byte vs 2-byte opcode prefixes correctly |
| Method body access | Raw PE parsing | `MetadataFile.GetMethodBody(rva)` via ICSharpCode.Decompiler | Handles exception tables, local variables, fat vs tiny headers |
| Token resolution | Manual metadata table lookups | `MetadataReader.GetMemberReference()`, `GetMethodDefinition()` | Correctly resolves TypeRef, TypeSpec, MemberRef, MethodSpec tokens |
| Type hierarchy traversal | Manual base type chain walking | `ITypeDefinition.DirectBaseTypes` | ICSharpCode.Decompiler resolves generic substitutions and handles type forwarding |

**Key insight:** The IL scanning loop is straightforward -- the complexity is in operand skipping (must match each opcode's operand size) and token resolution (must handle MemberRef vs MethodDef vs MethodSpec). Both are well-handled by the existing S.R.Metadata APIs.

## Common Pitfalls

### Pitfall 1: Operand Size Mismatch in IL Scanning
**What goes wrong:** After `DecodeOpCode()`, the IL reader is positioned after the opcode bytes but before the operand. Each opcode has a specific operand size (0, 1, 2, 4, or 8 bytes, or variable for `switch`). If you skip the wrong number of bytes, every subsequent opcode read is corrupted.
**Why it happens:** There are ~220 IL opcodes with varying operand sizes. Easy to miss one.
**How to avoid:** Use `ILOpCode.GetOperandType()` (returns `OperandType` enum) or build a switch on the operand type. The key operand types are: `InlineNone` (0 bytes), `ShortInlineBrTarget`/`ShortInlineI`/`ShortInlineVar` (1 byte), `InlineVar` (2 bytes), `InlineBrTarget`/`InlineField`/`InlineI`/`InlineMethod`/`InlineSig`/`InlineString`/`InlineTok`/`InlineType` (4 bytes), `InlineI8`/`InlineR` (8 bytes), `InlineSwitch` (4 + 4*n bytes).
**Warning signs:** Test results show impossible IL offsets or unrecognized opcodes.

### Pitfall 2: Generic Method/Type Token Resolution
**What goes wrong:** When code calls a generic method like `List<string>.Add(string)`, the IL contains a `MethodSpec` token (not a `MethodDef` or `MemberRef`). If you only handle `MethodDef` and `MemberRef`, you miss generic instantiation call sites.
**Why it happens:** The metadata token in IL can be one of several handle kinds: `MethodDefinitionHandle`, `MemberReferenceHandle`, or `MethodSpecificationHandle`.
**How to avoid:** Check `EntityHandle.Kind` and resolve through the chain: `MethodSpec` -> `MethodSpecification.Method` -> underlying `MethodDef` or `MemberRef`. Same for `TypeSpec` -> `TypeSpecification.Signature`.
**Warning signs:** Missing cross-references when generic types are involved.

### Pitfall 3: Property/Event Accessor Methods
**What goes wrong:** Properties and events compile to accessor methods (`get_PropertyName`, `set_PropertyName`, `add_EventName`, `remove_EventName`). If the user searches for a property by name, IL scanning must look for calls to these accessor methods, not the property itself (properties don't exist in IL).
**Why it happens:** IL has no property concept -- only methods. The property metadata is a separate table that associates accessor methods.
**How to avoid:** When `find_usages` targets a property, resolve it to its getter/setter method tokens and scan for calls to those methods. When `find_dependencies` outputs results, map accessor methods back to their property/event names for user-friendly output.
**Warning signs:** `find_usages` for a property returns zero results even though the property is clearly used.

### Pitfall 4: Constructors Have Special Names
**What goes wrong:** Instance constructors are named `.ctor` and static constructors `.cctor` in IL. If member_name matching uses the C# name, these won't match.
**Why it happens:** The member_name parameter might come as `.ctor` (from get_type_members which already uses this convention per Phase 2 SDK-05 decision).
**How to avoid:** Accept `.ctor`/`.cctor` as valid member names. The existing codebase already lists constructors with their IL names.
**Warning signs:** `find_usages` for constructors returns empty results.

### Pitfall 5: External Type References in Dependencies
**What goes wrong:** `find_dependencies` for a method shows calls to BCL types like `System.Console.WriteLine` as `MemberRef` tokens, not `MethodDef` tokens. The resolution path is different.
**Why it happens:** References to types in other assemblies are stored as `MemberRef` (cross-assembly reference) not `MethodDef` (same-assembly definition).
**How to avoid:** Handle both `MemberReferenceHandle` and `MethodDefinitionHandle` in the token resolution. For `MemberRef`, use `MetadataReader.GetMemberReference()` to get the declaring type and member name.
**Warning signs:** Dependencies only show intra-assembly calls, missing all BCL/library references.

## Code Examples

### IL Operand Skipping Helper
```csharp
// Source: System.Reflection.Metadata ILOpCode documentation
private static void SkipOperand(ref BlobReader reader, ILOpCode opCode)
{
    switch (opCode.GetOperandType())
    {
        case OperandType.InlineNone:
            break;
        case OperandType.ShortInlineBrTarget:
        case OperandType.ShortInlineI:
        case OperandType.ShortInlineVar:
            reader.ReadByte();
            break;
        case OperandType.InlineVar:
            reader.ReadInt16();
            break;
        case OperandType.InlineBrTarget:
        case OperandType.InlineField:
        case OperandType.InlineI:
        case OperandType.InlineMethod:
        case OperandType.InlineSig:
        case OperandType.InlineString:
        case OperandType.InlineTok:
        case OperandType.InlineType:
            reader.ReadInt32();
            break;
        case OperandType.InlineI8:
        case OperandType.InlineR:
            reader.ReadInt64();
            break;
        case OperandType.InlineSwitch:
            int count = reader.ReadInt32();
            reader.Offset += count * 4;
            break;
        case OperandType.ShortInlineR:
            reader.ReadSingle();
            break;
    }
}
```

### Token Resolution Helper
```csharp
// Resolve a metadata token to declaring type name and member name
private static (string DeclaringType, string MemberName)? ResolveToken(
    MetadataReader reader, EntityHandle handle)
{
    switch (handle.Kind)
    {
        case HandleKind.MethodDefinition:
            var methodDef = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
            var declaringTypeDef = reader.GetTypeDefinition(methodDef.GetDeclaringType());
            return (GetFullTypeName(reader, declaringTypeDef), 
                    reader.GetString(methodDef.Name));
        
        case HandleKind.MemberReference:
            var memberRef = reader.GetMemberReference((MemberReferenceHandle)handle);
            var parentName = ResolveParentName(reader, memberRef.Parent);
            return (parentName, reader.GetString(memberRef.Name));
        
        case HandleKind.MethodSpecification:
            var methodSpec = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
            return ResolveToken(reader, methodSpec.Method); // Recurse to underlying method
        
        default:
            return null;
    }
}
```

### Domain Model Recommendation
```csharp
// Domain/Models/CrossReferenceResults.cs
public sealed record UsageResult
{
    public required string ContainingType { get; init; }
    public required string ContainingMethod { get; init; }
    public required int ILOffset { get; init; }
    public required string OpCode { get; init; }  // "call", "callvirt", "ldfld", etc.
}

public sealed record ImplementorResult
{
    public required string TypeFullName { get; init; }
    public required string Relationship { get; init; }  // "implements" or "extends"
}

public sealed record DependencyResult
{
    public required string TargetType { get; init; }
    public required string TargetMember { get; init; }
    public required int ILOffset { get; init; }
    public required string OpCode { get; init; }
}

public sealed record InstantiationResult
{
    public required string ContainingType { get; init; }
    public required string ContainingMethod { get; init; }
    public required int ILOffset { get; init; }
}
```

### TestTargets Cross-Reference Type Graph Recommendation
```csharp
// TestTargets/Types/CrossReferenceTypes.cs
namespace ILSpy.Mcp.TestTargets.CrossRef;

// Interface with known implementors (XREF-02 test target)
public interface ICrossRefTarget
{
    void DoWork();
    string Name { get; }
}

// Known implementor #1
public class TargetImpl1 : ICrossRefTarget
{
    public string Name => "Impl1";
    public void DoWork() { }
}

// Known implementor #2  
public class TargetImpl2 : ICrossRefTarget
{
    public string Name => "Impl2";
    public void DoWork() { }
}

// Base class with known derived types (XREF-02 test target)
public class CrossRefBase
{
    public virtual void BaseMethod() { }
}

public class DerivedA : CrossRefBase
{
    public override void BaseMethod() { }
}

// Class that calls known methods (XREF-01 test: find_usages of DoWork)
// Also: XREF-03 test: find_dependencies of CallSite.InvokeAll
public class CallSite
{
    private ICrossRefTarget _target;
    
    public void InvokeAll()
    {
        var impl1 = new TargetImpl1();     // newobj (XREF-04 test)
        impl1.DoWork();                     // callvirt (XREF-01 test)
        var name = impl1.Name;             // property access (XREF-01 test)
    }
}

// Class with field access patterns (XREF-01 test: find_usages of a field)
public class FieldAccessor
{
    public int Value;
    
    public void SetValue(int v) { Value = v; }    // stfld
    public int GetValue() { return Value; }         // ldfld
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| ILSpy GUI Analyzers (tightly coupled to UI) | Raw S.R.Metadata IL scanning | N/A (this is library usage) | Full control over scan behavior without GUI dependencies |
| Mono.Cecil for IL inspection | S.R.Metadata (already in dependency tree) | N/A | Zero additional dependencies |

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | Tests project references, `[Collection("ToolTests")]` shared fixture |
| Quick run command | `dotnet test --filter "FullyQualifiedName~FindUsages"` |
| Full suite command | `dotnet test` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| XREF-01 | Find usages of method/field/property | integration | `dotnet test --filter "FullyQualifiedName~FindUsagesToolTests"` | Wave 0 |
| XREF-02 | Find implementors of interface/base class | integration | `dotnet test --filter "FullyQualifiedName~FindImplementorsToolTests"` | Wave 0 |
| XREF-03 | Find outward dependencies | integration | `dotnet test --filter "FullyQualifiedName~FindDependenciesToolTests"` | Wave 0 |
| XREF-04 | Find instantiation sites | integration | `dotnet test --filter "FullyQualifiedName~FindInstantiationsToolTests"` | Wave 0 |
| TEST-02 | All XREF tools have integration tests | integration | `dotnet test` (all 5 test classes) | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~<relevant_test_class>"`
- **Per wave merge:** `dotnet test` (full suite)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `TestTargets/Types/CrossReferenceTypes.cs` -- purpose-built cross-reference type graph
- [ ] `Tests/Tools/FindUsagesToolTests.cs` -- covers XREF-01
- [ ] `Tests/Tools/FindImplementorsToolTests.cs` -- covers XREF-02
- [ ] `Tests/Tools/FindDependenciesToolTests.cs` -- covers XREF-03
- [ ] `Tests/Tools/FindInstantiationsToolTests.cs` -- covers XREF-04
- [ ] `Tests/Tools/AnalyzeReferencesToolTests.cs` -- covers dispatcher routing

## DI Registration Pattern

New registrations needed in `Program.cs` `RegisterServices()` and `ToolTestFixture`:

```csharp
// Domain services
services.AddScoped<ICrossReferenceService, ILSpyCrossReferenceService>();

// Use cases
services.AddScoped<FindUsagesUseCase>();
services.AddScoped<FindImplementorsUseCase>();
services.AddScoped<FindDependenciesUseCase>();
services.AddScoped<FindInstantiationsUseCase>();

// MCP tool handlers
services.AddScoped<FindUsagesTool>();
services.AddScoped<FindImplementorsTool>();
services.AddScoped<FindDependenciesTool>();
services.AddScoped<FindInstantiationsTool>();
services.AddScoped<AnalyzeReferencesTool>();
```

## Project Constraints (from CLAUDE.md)

- **Tech stack**: C#, .NET, ICSharpCode.Decompiler, System.Reflection.Metadata, MCP SDK -- no new runtime dependencies
- **Architecture**: Follow existing layered pattern (Domain/Infrastructure/Application/Transport)
- **Testing**: Critical-path tests for P0 features and all bug fixes
- **Compatibility**: Must not break existing 10 tools during this phase
- **Do NOT add**: Mono.Cecil, dnlib, ILSpyX, explicit System.Reflection.Metadata package, Moq/NSubstitute
- **Target framework**: net10.0 (established in Phase 1)
- **Convention**: Using-alias for `TypeName` disambiguation (established in Phase 3)

## Open Questions

1. **`find_dependencies` member_name optionality**
   - What we know: D-05 says parameters follow existing conventions. Existing tools make member_name optional when type-level operation is valid.
   - What's unclear: Should `find_dependencies` support both type-level (all methods' dependencies) and method-level (single method's dependencies)?
   - Recommendation: Make `member_name` optional. If omitted, scan all methods in the type. If provided, scan only that method. This is more useful and follows existing optional-parameter patterns.

2. **Method signature format for `ContainingMethod` field**
   - What we know: D-06 says "containing method signature" in results.
   - What's unclear: Exact format -- `void DoWork()` vs `DoWork(String)` vs full qualified.
   - Recommendation: Use `ReturnType MethodName(ParamType1, ParamType2)` format matching the short parameter signatures already used in `SearchMembersAsync`. Build from `MetadataReader` method definition.

3. **Result string formatting**
   - What we know: Tools return `string` (established pattern). D-07 says grouped by containing type, ordered by IL offset.
   - Recommendation: Build a formatted string output similar to how existing tools format results. Use a `StringBuilder` with clear section headers per containing type.

## Sources

### Primary (HIGH confidence)
- Codebase analysis: `ILSpyDecompilerService.cs`, `ILSpyDisassemblyService.cs` -- established patterns for `CSharpDecompiler` usage, `MetadataFile` access, type system traversal
- Codebase analysis: `DecompileMethodUseCase.cs`, `DecompileTypeTool.cs` -- established use case and tool patterns
- Codebase analysis: `ToolTestFixture.cs`, `DisassembleTypeToolTests.cs` -- test infrastructure patterns
- CLAUDE.md: Technology stack, key APIs documentation, migration notes

### Secondary (MEDIUM confidence)
- Microsoft Learn: `MethodBodyBlock.GetILReader()` -- returns `BlobReader` for IL scanning
- Microsoft Learn: `ILOpCode` enum -- all .NET IL opcodes with `GetOperandType()` method
- Microsoft Learn: `BlobReader.DecodeOpCode()` -- handles 1-byte and 2-byte opcode prefixes

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new dependencies, all APIs already in project
- Architecture: HIGH - follows exact patterns established in Phases 1-3, verified by reading source
- Pitfalls: HIGH - IL scanning pitfalls are well-documented and the operand size table is definitive
- Test strategy: HIGH - follows established one-class-per-tool pattern with shared fixture

**Research date:** 2026-04-08
**Valid until:** 2026-05-08 (stable -- no external dependency changes expected)
