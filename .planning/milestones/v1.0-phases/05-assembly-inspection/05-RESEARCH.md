# Phase 5: Assembly Inspection - Research

**Researched:** 2026-04-08
**Domain:** Assembly metadata, custom attributes, embedded resources, compiler-generated type detection
**Confidence:** HIGH

## Summary

Phase 5 adds 7 new MCP tools for assembly inspection: `get_assembly_metadata` (unified PE metadata + assembly references), three attribute tools (`get_assembly_attributes`, `get_type_attributes`, `get_member_attributes`), two resource tools (`list_embedded_resources`, `extract_resource`), and `find_compiler_generated_types`. All functionality is achievable using APIs already in the dependency tree -- `System.Reflection.Metadata` for low-level metadata/attributes/resources and ICSharpCode.Decompiler's type system for compiler-generated type detection with parent resolution.

The architecture follows the established pattern: new `IAssemblyInspectionService` domain interface, `ILSpyAssemblyInspectionService` infrastructure implementation, 7 use case classes, and 7 tool classes. The critical complexity areas are: (1) custom attribute value decoding (requires implementing `ICustomAttributeTypeProvider<string>` for `DecodeValue`), (2) embedded resource binary extraction with pagination, and (3) compiler-generated type parent method resolution via naming convention parsing.

**Primary recommendation:** Use ICSharpCode.Decompiler's `MetadataFile`/`PEFile` as the entry point (already used by existing services), access `PEHeaders` for PE metadata, `MetadataReader` for attributes/resources/references, and the decompiler's type system for compiler-generated type detection.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** 7 new tools total, organized by capability area
- **D-02:** Tool naming: verb_noun pattern (`get_assembly_metadata`, `find_compiler_generated_types`)
- **D-03:** Single unified `get_assembly_metadata` tool returns PE header info AND referenced assemblies in one call
- **D-04:** Three separate attribute tools by scope: `get_assembly_attributes`, `get_type_attributes`, `get_member_attributes`
- **D-05:** Declared attributes only -- no inherited attribute traversal
- **D-06:** Attribute tool parameters: assembly-level takes only path, type-level adds type_name, member-level adds member_name
- **D-07:** Two resource tools: `list_embedded_resources` (catalog) and `extract_resource` (content)
- **D-08:** `extract_resource` returns text inline, binary as base64, with `offset` and `limit` for pagination
- **D-09:** Dedicated `find_compiler_generated_types` tool separate from `list_assembly_types`
- **D-10:** Each compiler-generated type shows parent context when detectable
- **D-11:** New `IAssemblyInspectionService` domain interface -- separate from existing services
- **D-12:** Infrastructure uses S.R.Metadata APIs + ICSharpCode.Decompiler type system
- **D-13:** One test class per tool (7 new test classes)
- **D-14:** TestTargets extended with assembly-level attributes, attributed types, embedded resources, nested types, compiler-generated types
- **D-15:** Structural assertions for all tests

### Claude's Discretion
- Exact domain model types for results (metadata record shapes, attribute result format, resource info format)
- Infrastructure service internal organization and method signatures
- Specific TestTargets additions (type names, attribute choices, resource content)
- How `find_compiler_generated_types` detects parent method relationship (naming convention heuristics vs metadata analysis)
- Whether `extract_resource` offset/limit operates on bytes or base64 characters
- How attribute constructor arguments are formatted in output (positional vs named display)

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| META-01 | Retrieve assembly metadata (target framework, runtime version, PE bitness, strong name, entry point) | `PEHeaders.CorHeader` for CLR metadata, `PEHeaders.CoffHeader.Machine` for bitness, `MetadataReader.GetAssemblyDefinition()` for name/version, custom attributes for TargetFramework |
| META-02 | List all referenced assemblies with name, version, culture, public key token | `MetadataReader.AssemblyReferences` collection with `GetAssemblyReference()` for each handle |
| META-03 | Inspect assembly-level custom attributes with constructor arguments | `MetadataReader.GetAssemblyDefinition().GetCustomAttributes()` + `CustomAttribute.DecodeValue<string>()` with custom type provider |
| META-04 | Inspect custom attributes on types and members | `TypeDefinition.GetCustomAttributes()` and `MethodDefinition.GetCustomAttributes()` / `FieldDefinition.GetCustomAttributes()` etc. |
| RES-01 | List embedded resources with type and size | `MetadataReader.ManifestResources` + `ManifestResourceAttributes` check + resource size from PE data |
| RES-02 | Extract embedded resource content (text inline, binary as base64) | `PEReader.GetSectionData()` at CorHeader.ResourcesDirectory offset + ManifestResource.Offset, or ICSharpCode.Decompiler `Resource.TryOpenStream()` |
| TYPE-01 | List nested types within a type | ICSharpCode.Decompiler `ITypeDefinition.NestedTypes` property |
| TYPE-02 | Find compiler-generated types with CompilerGenerated attribute filter | Naming convention parsing (DisplayClass, state machine patterns) + `IsCompilerGenerated` metadata check + parent method resolution from `<MethodName>` prefix |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Tech stack**: C#, .NET, ICSharpCode.Decompiler, System.Reflection.Metadata, MCP SDK -- no new runtime dependencies
- **Architecture**: Follow existing layered pattern (Domain/Infrastructure/Application/Transport)
- **Testing**: Critical-path tests for P0 features; xUnit 2.9.x, FluentAssertions 8.9.0
- **Compatibility**: Must not break existing 15 tools during upgrades
- **Do NOT add**: Mono.Cecil, dnlib, ILSpyX, explicit System.Reflection.Metadata, Moq/NSubstitute

## Standard Stack

No new packages required. All APIs come from existing dependencies.

### Core APIs (Already in Dependency Tree)

| API | Source Package | Purpose |
|-----|---------------|---------|
| `MetadataReader` | System.Reflection.Metadata (transitive via ICSharpCode.Decompiler) | Read assembly references, custom attributes, manifest resources |
| `PEHeaders` / `CorHeader` | System.Reflection.PortableExecutable (transitive) | PE bitness, CLR version, entry point, resources directory |
| `AssemblyDefinition` | System.Reflection.Metadata | Assembly name, version, culture, public key |
| `CustomAttribute` + `DecodeValue<T>` | System.Reflection.Metadata | Parse attribute constructor args and named properties |
| `ManifestResource` | System.Reflection.Metadata | Resource name, offset, attributes (public/private) |
| `ITypeDefinition.NestedTypes` | ICSharpCode.Decompiler TypeSystem | Nested type enumeration |
| `KnownAttribute.CompilerGenerated` | ICSharpCode.Decompiler TypeSystem | Compiler-generated detection via `HasAttribute` |
| `MetadataFile` / `PEFile` | ICSharpCode.Decompiler.Metadata | Wraps PEReader; provides `GetMethodBody`, section data access |
| `Resource` / `EmbeddedResource` | ICSharpCode.Decompiler.Metadata | Higher-level resource access with `TryOpenStream()` |

## Architecture Patterns

### Recommended Project Structure (New Files)

```
Domain/
  Services/
    IAssemblyInspectionService.cs      # New domain interface
  Models/
    AssemblyMetadata.cs                # Assembly metadata result record
    AssemblyReference.cs               # Referenced assembly info
    AttributeInfo.cs                   # Custom attribute result record
    ResourceInfo.cs                    # Resource catalog entry
    ResourceContent.cs                 # Extracted resource content
    CompilerGeneratedTypeInfo.cs       # Compiler-generated type with parent context
Infrastructure/
  Decompiler/
    ILSpyAssemblyInspectionService.cs  # Implementation of IAssemblyInspectionService
Application/
  UseCases/
    GetAssemblyMetadataUseCase.cs
    GetAssemblyAttributesUseCase.cs
    GetTypeAttributesUseCase.cs
    GetMemberAttributesUseCase.cs
    ListEmbeddedResourcesUseCase.cs
    ExtractResourceUseCase.cs
    FindCompilerGeneratedTypesUseCase.cs
Transport/
  Mcp/
    Tools/
      GetAssemblyMetadataTool.cs
      GetAssemblyAttributesTool.cs
      GetTypeAttributesTool.cs
      GetMemberAttributesTool.cs
      ListEmbeddedResourcesTool.cs
      ExtractResourceTool.cs
      FindCompilerGeneratedTypesTool.cs
Tests/
  Tools/
    GetAssemblyMetadataToolTests.cs
    GetAssemblyAttributesToolTests.cs
    GetTypeAttributesToolTests.cs
    GetMemberAttributesToolTests.cs
    ListEmbeddedResourcesToolTests.cs
    ExtractResourceToolTests.cs
    FindCompilerGeneratedTypesToolTests.cs
TestTargets/
  Types/
    AssemblyAttributes.cs              # Assembly-level attributes
    ResourceTestTypes.cs               # Types for resource testing
    CompilerGeneratedTestTypes.cs      # Async methods, lambdas, closures
  Resources/
    sample.txt                         # Text embedded resource
    sample.bin                         # Binary embedded resource
```

### Pattern 1: Domain Interface (follows IDecompilerService, IDisassemblyService, ICrossReferenceService)

```csharp
public interface IAssemblyInspectionService
{
    Task<AssemblyMetadata> GetAssemblyMetadataAsync(
        AssemblyPath assemblyPath,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttributeInfo>> GetAssemblyAttributesAsync(
        AssemblyPath assemblyPath,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttributeInfo>> GetTypeAttributesAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttributeInfo>> GetMemberAttributesAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        string memberName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResourceInfo>> ListEmbeddedResourcesAsync(
        AssemblyPath assemblyPath,
        CancellationToken cancellationToken = default);

    Task<ResourceContent> ExtractResourceAsync(
        AssemblyPath assemblyPath,
        string resourceName,
        int? offset = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompilerGeneratedTypeInfo>> FindCompilerGeneratedTypesAsync(
        AssemblyPath assemblyPath,
        CancellationToken cancellationToken = default);
}
```

### Pattern 2: Domain Models (follows existing record patterns)

```csharp
// Assembly metadata combining PE header info + assembly references
public sealed record AssemblyMetadata
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? TargetFramework { get; init; }
    public string? RuntimeVersion { get; init; }
    public required string PEKind { get; init; }        // "x86", "x64", "AnyCPU"
    public string? StrongName { get; init; }
    public string? EntryPoint { get; init; }
    public string? Culture { get; init; }
    public string? PublicKeyToken { get; init; }
    public required IReadOnlyList<AssemblyReferenceInfo> References { get; init; }
}

public sealed record AssemblyReferenceInfo
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Culture { get; init; }
    public string? PublicKeyToken { get; init; }
}

public sealed record AttributeInfo
{
    public required string AttributeType { get; init; }
    public IReadOnlyList<string> ConstructorArguments { get; init; } = [];
    public IReadOnlyDictionary<string, string> NamedArguments { get; init; } = new Dictionary<string, string>();
}

public sealed record ResourceInfo
{
    public required string Name { get; init; }
    public required long Size { get; init; }
    public required string ResourceType { get; init; }  // "Embedded", "Linked"
    public bool IsPublic { get; init; }
}

public sealed record ResourceContent
{
    public required string Name { get; init; }
    public required string ContentType { get; init; }    // "text", "binary"
    public required string Content { get; init; }        // text or base64
    public long TotalSize { get; init; }
    public int? Offset { get; init; }
    public int? Length { get; init; }
}

public sealed record CompilerGeneratedTypeInfo
{
    public required string FullName { get; init; }
    public required string ShortName { get; init; }
    public required string GeneratedKind { get; init; }  // "DisplayClass", "AsyncStateMachine", "Iterator", "Closure"
    public string? ParentMethod { get; init; }
    public string? ParentType { get; init; }
}
```

### Pattern 3: Infrastructure Implementation Core Logic

```csharp
// Access pattern for MetadataReader (already used in ILSpyCrossReferenceService)
var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
var reader = metadataFile.Metadata;   // This is the MetadataReader
var peHeaders = metadataFile.Reader.PEHeaders;  // PEHeaders for PE info

// Assembly metadata
var assemblyDef = reader.GetAssemblyDefinition();
var name = reader.GetString(assemblyDef.Name);
var version = assemblyDef.Version.ToString();

// PE bitness
var machine = peHeaders.CoffHeader.Machine;  // Machine.I386, Machine.Amd64, etc.
var corFlags = peHeaders.CorHeader.Flags;     // CorFlags.Requires32Bit, etc.

// Assembly references
foreach (var refHandle in reader.AssemblyReferences)
{
    var asmRef = reader.GetAssemblyReference(refHandle);
    // asmRef.Name, asmRef.Version, asmRef.Culture, asmRef.GetHashValue()
}
```

### Pattern 4: Custom Attribute Value Decoding

```csharp
// Decoding attribute constructor args requires ICustomAttributeTypeProvider<string>
// This is a simplified string-based provider that formats values for display
public class StringAttributeTypeProvider : ICustomAttributeTypeProvider<string>
{
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        => reader.GetString(reader.GetTypeDefinition(handle).Name);
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        => reader.GetString(reader.GetTypeReference(handle).Name);
    public string GetTypeFromSerializedName(string name) => name;
    public string GetSZArrayType(string elementType) => $"{elementType}[]";
    public string GetSystemType() => "System.Type";
    public bool IsSystemType(string type) => type == "System.Type" || type == "Type";
    public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32;
}

// Usage:
var provider = new StringAttributeTypeProvider();
foreach (var attrHandle in typeDef.GetCustomAttributes())
{
    var attr = reader.GetCustomAttribute(attrHandle);
    var decoded = attr.DecodeValue(provider);
    // decoded.FixedArguments = positional constructor args
    // decoded.NamedArguments = named properties/fields
}
```

### Pattern 5: Embedded Resource Reading

```csharp
// Approach 1: Via ICSharpCode.Decompiler Resource abstraction
var peFile = new PEFile(assemblyPath.Value);
foreach (var resource in peFile.Resources)
{
    if (resource.ResourceType == ResourceType.Embedded)
    {
        var stream = resource.TryOpenStream();
        // Read bytes from stream
    }
}

// Approach 2: Direct via S.R.Metadata (lower level, needed for size/offset)
var corHeader = peHeaders.CorHeader;
var resourcesRva = corHeader.ResourcesDirectory.RelativeVirtualAddress;
foreach (var resHandle in reader.ManifestResources)
{
    var resource = reader.GetManifestResource(resHandle);
    var name = reader.GetString(resource.Name);
    var isEmbedded = resource.Implementation.IsNil;  // Nil = embedded in this assembly
    if (isEmbedded)
    {
        // Resource data is at: resources section base + resource.Offset
        // First 4 bytes = length (int32), then actual data
        var sectionData = metadataFile.Reader.GetSectionData(resourcesRva);
        var blobReader = sectionData.GetReader(resource.Offset, sectionData.Length - resource.Offset);
        var length = blobReader.ReadInt32();
        var data = blobReader.ReadBytes(length);
    }
}
```

### Pattern 6: Compiler-Generated Type Detection

```csharp
// Strategy: combine naming conventions + CompilerGenerated attribute check
// ICSharpCode.Decompiler's type system provides HasAttribute for KnownAttribute
foreach (var type in mainModule.TypeDefinitions)
{
    if (type.DeclaringType == null) continue;  // Only nested types
    
    var name = type.Name;
    string? kind = null;
    string? parentMethod = null;
    
    // DisplayClass (closures/lambdas)
    if (name.Contains("DisplayClass") || name.Contains("AnonStorey") || name.Contains("Closure$"))
    {
        kind = "DisplayClass";
        parentMethod = ExtractParentMethodFromName(name);  // Parse <>c__DisplayClass0 pattern
    }
    // Async state machines
    else if (name.Contains("d__"))
    {
        kind = "AsyncStateMachine";
        parentMethod = ExtractParentMethodFromName(name);  // Parse <MethodName>d__0
    }
    // Iterator state machines
    else if (name.Contains("d__") && /* implements IEnumerator */)
    {
        kind = "Iterator";
        parentMethod = ExtractParentMethodFromName(name);
    }
    // General compiler-generated check via attribute
    else if (type.GetAttributes().Any(a => a.AttributeType.FullName == 
        "System.Runtime.CompilerServices.CompilerGeneratedAttribute"))
    {
        kind = "CompilerGenerated";
    }
}

// Parent method name extraction from compiler naming convention:
// Pattern: <MethodName>d__N or <>c__DisplayClassN_M
// The text between < and > is the parent method name
static string? ExtractParentMethodFromName(string typeName)
{
    var start = typeName.IndexOf('<');
    var end = typeName.IndexOf('>');
    if (start >= 0 && end > start + 1)
    {
        var methodName = typeName.Substring(start + 1, end - start - 1);
        return string.IsNullOrEmpty(methodName) ? null : methodName;
    }
    return null;
}
```

### Anti-Patterns to Avoid

- **Loading assembly twice:** Don't create both `CSharpDecompiler` and separate `PEFile` -- the decompiler's `MetadataFile` already wraps PEFile and provides `Metadata` (MetadataReader) and `Reader` (PEReader) access
- **Using reflection (Assembly.LoadFrom):** Never load the assembly into the running process -- use `PEReader`/`MetadataReader` for read-only metadata access
- **Hand-rolling attribute blob parsing:** Use `CustomAttribute.DecodeValue<T>()` with a type provider rather than manually parsing the ECMA-335 blob format
- **Ignoring ManifestResourceAttributes:** Always check `resource.Implementation.IsNil` to distinguish embedded vs linked resources

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Attribute blob decoding | Custom ECMA-335 blob parser | `CustomAttribute.DecodeValue<T>(ICustomAttributeTypeProvider<T>)` | Blob format is complex with variable-length encodings for types, enums, arrays |
| PE bitness detection | Manual PE header byte parsing | `PEHeaders.CoffHeader.Machine` + `CorHeader.Flags` | Platform handles PE32/PE32+ differences |
| Resource data extraction | Manual RVA calculation | ICSharpCode.Decompiler `Resource.TryOpenStream()` for listing; `PEReader.GetSectionData()` for paginated access | RVA arithmetic is error-prone, off-by-one risks |
| Compiler-generated detection | Only attribute checking | Naming convention heuristics (DisplayClass, d__, c__) from ILSpy source | Not all compiler-generated types have the attribute; naming convention is the primary signal |
| Target framework parsing | String scanning of attributes | Read `TargetFrameworkAttribute` from assembly attributes, parse its constructor argument | Standard pattern across all .NET assemblies |

## Common Pitfalls

### Pitfall 1: Custom Attribute DecodeValue Requires Type Provider
**What goes wrong:** Calling `CustomAttribute.DecodeValue` without providing a proper `ICustomAttributeTypeProvider<T>` implementation causes runtime errors or incomplete decoding.
**Why it happens:** The S.R.Metadata API requires a type provider to resolve type references in attribute constructor arguments (enum types, System.Type values, arrays).
**How to avoid:** Implement a simple `StringAttributeTypeProvider` that returns string representations. For enum values, `GetUnderlyingEnumType` can default to `Int32` since we only need display values.
**Warning signs:** Attributes with enum or Type constructor arguments showing raw bytes instead of decoded values.

### Pitfall 2: Embedded vs Linked Resources
**What goes wrong:** Attempting to read resource data for linked/external resources that don't have data in the PE file.
**Why it happens:** `ManifestResource.Implementation` being non-nil means the resource is in another file (AssemblyFileHandle) or assembly (AssemblyReferenceHandle).
**How to avoid:** Always check `resource.Implementation.IsNil` before trying to read embedded data. Only truly embedded resources have their data in the PE image.
**Warning signs:** `BadImageFormatException` or reading garbage data.

### Pitfall 3: Resource Size Prefix
**What goes wrong:** Resource content includes 4 extra bytes or is offset by 4 bytes.
**Why it happens:** Embedded resources are stored with a 4-byte length prefix (Int32) before the actual data. Forgetting to skip this prefix or double-counting it corrupts the output.
**How to avoid:** Read the Int32 length first, then read exactly that many bytes for the content.
**Warning signs:** Base64 output that doesn't decode properly, or text resources with leading garbage characters.

### Pitfall 4: Compiler-Generated Type Naming Varies by Compiler Version
**What goes wrong:** Missing some compiler-generated types or misidentifying parent methods.
**Why it happens:** Roslyn's naming conventions have evolved: `<>c__DisplayClass` (closures), `<MethodName>d__N` (async/iterators), `<>c` (static lambda caches), `<<MethodName>g__LocalFuncName|N>d` (local functions). Older compilers used `AnonStorey`.
**How to avoid:** Use multiple heuristics: check for `CompilerGeneratedAttribute` AND naming patterns. Extract parent method from `<>` delimiters but handle empty names (anonymous methods use `<>`).
**Warning signs:** `find_compiler_generated_types` returns empty for assemblies that obviously have async methods or lambdas.

### Pitfall 5: Member Attribute Lookup Ambiguity
**What goes wrong:** `get_member_attributes` for a member name that has overloads returns attributes from wrong overload, or misses attributes on properties vs methods vs fields with same name.
**Why it happens:** Members can share names across different member kinds (property `Name` vs method `Name`).
**How to avoid:** Search across all member kinds (methods, properties, fields, events) and return attributes from ALL matching members, clearly labeled with the member kind and signature.
**Warning signs:** User asks for attributes on a property but gets nothing because code only searched methods.

### Pitfall 6: Assembly-Level Attributes Include Internal Framework Attributes
**What goes wrong:** `get_assembly_attributes` returns dozens of framework-internal attributes that clutter the output.
**Why it happens:** .NET assemblies have many assembly-level attributes from the build system (`AssemblyVersionAttribute`, `AssemblyFileVersionAttribute`, `TargetFrameworkAttribute`, `AssemblyConfigurationAttribute`, `InternalsVisibleToAttribute`, etc.) plus compiler-generated ones.
**How to avoid:** Return all attributes but format them clearly with full type names. Don't filter -- let the AI assistant decide what's relevant.
**Warning signs:** Output is overwhelming with 20+ attributes for simple assemblies.

## Code Examples

### Assembly Metadata Extraction (Verified Pattern)

```csharp
// Source: Existing ILSpyDecompilerService + S.R.Metadata official docs
var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
var reader = metadataFile.Metadata;
var peHeaders = metadataFile.Reader.PEHeaders;

// Assembly identity
var asmDef = reader.GetAssemblyDefinition();
var name = reader.GetString(asmDef.Name);
var version = asmDef.Version;
var culture = reader.GetString(asmDef.Culture);
var publicKey = reader.GetBlobBytes(asmDef.PublicKey);

// PE characteristics
var machine = peHeaders.CoffHeader.Machine;
var peKind = machine switch
{
    Machine.I386 => (peHeaders.CorHeader.Flags & CorFlags.Requires32Bit) != 0 ? "x86" : "AnyCPU",
    Machine.Amd64 => "x64",
    Machine.Arm64 => "ARM64",
    _ => machine.ToString()
};

// Target framework from assembly attributes
var tfm = GetTargetFrameworkFromAttributes(reader);

// Entry point
string? entryPoint = null;
if (peHeaders.CorHeader.EntryPointTokenOrRelativeVirtualAddress != 0)
{
    var token = MetadataTokens.EntityHandle(peHeaders.CorHeader.EntryPointTokenOrRelativeVirtualAddress);
    if (token.Kind == HandleKind.MethodDefinition)
    {
        var method = reader.GetMethodDefinition((MethodDefinitionHandle)token);
        var declaringType = reader.GetTypeDefinition(method.GetDeclaringType());
        entryPoint = $"{reader.GetString(declaringType.Namespace)}.{reader.GetString(declaringType.Name)}.{reader.GetString(method.Name)}";
    }
}
```

### Assembly References (Verified from S.R.Metadata docs)

```csharp
// Source: Microsoft Learn - MetadataReader.AssemblyReferences
var references = new List<AssemblyReferenceInfo>();
foreach (var refHandle in reader.AssemblyReferences)
{
    var asmRef = reader.GetAssemblyReference(refHandle);
    var pkt = asmRef.GetHashValue().IsNil ? null : FormatPublicKeyToken(reader.GetBlobBytes(asmRef.PublicKeyOrToken));
    references.Add(new AssemblyReferenceInfo
    {
        Name = reader.GetString(asmRef.Name),
        Version = asmRef.Version.ToString(),
        Culture = reader.GetString(asmRef.Culture),
        PublicKeyToken = pkt
    });
}
```

### TestTargets Extensions Required

```csharp
// AssemblyAttributes.cs - placed at TestTargets root
using System.Runtime.CompilerServices;

[assembly: System.Reflection.AssemblyDescription("Test assembly for ILSpy MCP")]
[assembly: System.Reflection.AssemblyCompany("TestCompany")]
[assembly: InternalsVisibleTo("ILSpy.Mcp.Tests")]

// CompilerGeneratedTestTypes.cs
namespace ILSpy.Mcp.TestTargets;

public class AsyncExample
{
    // This generates a state machine type: <DoWorkAsync>d__0
    public async Task<int> DoWorkAsync()
    {
        await Task.Delay(1);
        return 42;
    }
}

public class LambdaExample
{
    // This generates a DisplayClass: <>c__DisplayClass0_0
    public Func<int, int> CreateAdder(int x)
    {
        return y => x + y;  // Captures 'x' in a DisplayClass
    }
}

// Embedded resources: add to TestTargets.csproj
// <ItemGroup>
//   <EmbeddedResource Include="Resources\sample.txt" />
//   <EmbeddedResource Include="Resources\sample.bin" />
// </ItemGroup>
```

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + FluentAssertions 8.9.0 |
| Config file | Tests/ILSpy.Mcp.Tests.csproj |
| Quick run command | `dotnet test Tests/ILSpy.Mcp.Tests.csproj --filter "FullyQualifiedName~AssemblyMetadata"` |
| Full suite command | `dotnet test Tests/ILSpy.Mcp.Tests.csproj` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| META-01 | Assembly metadata (framework, version, PE kind, strong name, entry point) | integration | `dotnet test Tests --filter "FullyQualifiedName~GetAssemblyMetadataToolTests" -x` | Wave 0 |
| META-02 | Assembly references list with name/version/culture/PKT | integration | `dotnet test Tests --filter "FullyQualifiedName~GetAssemblyMetadataToolTests" -x` | Wave 0 |
| META-03 | Assembly-level custom attributes with arguments | integration | `dotnet test Tests --filter "FullyQualifiedName~GetAssemblyAttributesToolTests" -x` | Wave 0 |
| META-04 | Type/member custom attributes with arguments | integration | `dotnet test Tests --filter "FullyQualifiedName~GetTypeAttributesToolTests\|GetMemberAttributesToolTests" -x` | Wave 0 |
| RES-01 | List embedded resources with type and size | integration | `dotnet test Tests --filter "FullyQualifiedName~ListEmbeddedResourcesToolTests" -x` | Wave 0 |
| RES-02 | Extract resource content (text inline, binary base64, pagination) | integration | `dotnet test Tests --filter "FullyQualifiedName~ExtractResourceToolTests" -x` | Wave 0 |
| TYPE-01 | Nested types listing | integration | `dotnet test Tests --filter "FullyQualifiedName~FindCompilerGeneratedTypesToolTests" -x` | Wave 0 |
| TYPE-02 | Compiler-generated types with parent context | integration | `dotnet test Tests --filter "FullyQualifiedName~FindCompilerGeneratedTypesToolTests" -x` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test Tests/ILSpy.Mcp.Tests.csproj --filter "FullyQualifiedName~Phase5" -x`
- **Per wave merge:** `dotnet test Tests/ILSpy.Mcp.Tests.csproj`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `Tests/Tools/GetAssemblyMetadataToolTests.cs` -- covers META-01, META-02
- [ ] `Tests/Tools/GetAssemblyAttributesToolTests.cs` -- covers META-03
- [ ] `Tests/Tools/GetTypeAttributesToolTests.cs` -- covers META-04 (type level)
- [ ] `Tests/Tools/GetMemberAttributesToolTests.cs` -- covers META-04 (member level)
- [ ] `Tests/Tools/ListEmbeddedResourcesToolTests.cs` -- covers RES-01
- [ ] `Tests/Tools/ExtractResourceToolTests.cs` -- covers RES-02
- [ ] `Tests/Tools/FindCompilerGeneratedTypesToolTests.cs` -- covers TYPE-01, TYPE-02
- [ ] `TestTargets/Resources/sample.txt` -- text embedded resource for testing
- [ ] `TestTargets/Resources/sample.bin` -- binary embedded resource for testing
- [ ] `TestTargets/Types/CompilerGeneratedTestTypes.cs` -- async + lambda types
- [ ] `TestTargets/Types/AssemblyAttributes.cs` -- assembly-level attributes (or add to existing)
- [ ] `TestTargets/ILSpy.Mcp.TestTargets.csproj` -- EmbeddedResource items added

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `Assembly.LoadFrom` for metadata | `PEReader` + `MetadataReader` for read-only access | .NET Core 1.0+ | No process pollution, no file locks, no dependency resolution needed |
| Manual blob parsing for attributes | `CustomAttribute.DecodeValue<T>()` | .NET Core 2.0 | Type-safe attribute decoding with provider pattern |
| ILSpy Analyzers for compiler-gen detection | Naming convention + `CompilerGeneratedAttribute` check | Stable across ILSpy versions | ILSpy analyzers are app-coupled; use the same heuristics directly |

## Open Questions

1. **Offset/limit semantics for extract_resource**
   - What we know: D-08 specifies offset and limit parameters for paginated binary extraction
   - What's unclear: Should offset/limit operate on raw bytes (before base64 encoding) or on base64 characters?
   - Recommendation: Operate on raw bytes -- offset and limit in bytes, then base64-encode the slice. This is more intuitive for callers and matches how resource data is naturally chunked. Include `totalSize` in response so caller knows how many bytes remain.

2. **ICustomAttributeTypeProvider enum handling**
   - What we know: `GetUnderlyingEnumType` must return a PrimitiveTypeCode for enum types referenced in attributes
   - What's unclear: Without loading the referenced assembly, we can't know the actual underlying type of enums
   - Recommendation: Default to `PrimitiveTypeCode.Int32` (most common) and fall back gracefully. Display the numeric value with the enum type name. This handles 95%+ of real-world attributes.

3. **Nested types in find_compiler_generated_types vs TYPE-01**
   - What we know: TYPE-01 is about listing nested types, TYPE-02 is about compiler-generated types
   - What's unclear: Should `find_compiler_generated_types` also include nested types that are NOT compiler-generated? Context says dedicated tool separate from list_assembly_types.
   - Recommendation: `find_compiler_generated_types` shows ONLY compiler-generated types. Nested type listing is implicitly available through `list_assembly_types` which already shows nested types via the type system. Add a note in the tool description that non-generated nested types appear in `list_assembly_types`.

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn: CustomAttribute struct](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata.customattribute?view=net-9.0) - DecodeValue API, Constructor/Parent/Value properties, code examples
- [Microsoft Learn: ManifestResource struct](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata.manifestresource?view=net-8.0) - Resource properties (Name, Offset, Implementation, Attributes)
- [Microsoft Learn: PEHeaders.CorHeader](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.portableexecutable.peheaders.corheader?view=net-7.0) - CLR header access
- [Microsoft Learn: AssemblyDefinition.GetCustomAttributes](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata.assemblydefinition.getcustomattributes?view=net-7.0) - Assembly-level attributes
- [GitHub: ILSpy CSharpDecompiler.cs](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs) - IsClosureType, MemberIsHidden compiler-generated detection logic
- [GitHub: ILSpy TypeSystemExtensions.cs](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.Decompiler/TypeSystem/TypeSystemExtensions.cs) - GetAttributes, HasAttribute extension methods

### Secondary (MEDIUM confidence)
- [GitHub: dotnet/runtime#23234](https://github.com/dotnet/runtime/issues/23234) - Embedded resource reading pattern via CorHeader.ResourcesDirectory + offset
- [Gist: AssemblyAnalyzer](https://gist.github.com/jbe2277/f91ef12df682f3bfb6293aabcb47be2a) - StringParameterValueTypeProvider for attribute decoding
- [GitHub: ILSpy WholeProjectDecompiler.cs](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.Decompiler/CSharp/ProjectDecompiler/WholeProjectDecompiler.cs) - Resource.TryOpenStream() usage pattern

### Tertiary (LOW confidence)
- None -- all findings verified with primary or secondary sources

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - all APIs are from existing dependencies, verified via official docs
- Architecture: HIGH - follows established project patterns exactly (4 prior phases as reference)
- Pitfalls: HIGH - attribute decoding and resource reading pitfalls documented in official issues and ILSpy source
- Compiler-generated detection: MEDIUM - naming conventions verified from ILSpy source but may vary across Roslyn versions

**Research date:** 2026-04-08
**Valid until:** 2026-05-08 (stable APIs, no expected breaking changes)
