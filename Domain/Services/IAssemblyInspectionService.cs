using ILSpy.Mcp.Domain.Models;

namespace ILSpy.Mcp.Domain.Services;

/// <summary>
/// Port for assembly inspection operations. Abstracts metadata reading,
/// attribute inspection, resource extraction, and compiler-generated type discovery.
/// </summary>
public interface IAssemblyInspectionService
{
    /// <summary>
    /// Gets assembly metadata including PE header info and assembly references.
    /// META-01 + META-02 (per D-03: unified metadata + references).
    /// </summary>
    Task<AssemblyMetadata> GetAssemblyMetadataAsync(
        AssemblyPath assemblyPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all custom attributes declared on the assembly.
    /// META-03 (per D-06: assembly_path only).
    /// </summary>
    Task<IReadOnlyList<AttributeInfo>> GetAssemblyAttributesAsync(
        AssemblyPath assemblyPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all custom attributes declared on a specific type.
    /// META-04 (per D-06: assembly_path + type_name).
    /// </summary>
    Task<IReadOnlyList<AttributeInfo>> GetTypeAttributesAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all custom attributes declared on a specific member (method, field, property, event).
    /// META-04 (per D-06: assembly_path + type_name + member_name).
    /// </summary>
    Task<IReadOnlyList<AttributeInfo>> GetMemberAttributesAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        string memberName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all embedded resources in the assembly with type and size info.
    /// RES-01 (per D-07: catalog listing).
    /// </summary>
    Task<IReadOnlyList<ResourceInfo>> ListEmbeddedResourcesAsync(
        AssemblyPath assemblyPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts content of a specific embedded resource with optional offset/limit pagination.
    /// RES-02 (per D-08: content with offset/limit pagination).
    /// </summary>
    Task<ResourceContent> ExtractResourceAsync(
        AssemblyPath assemblyPath,
        string resourceName,
        int? offset = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all compiler-generated types (async state machines, display classes, closures, etc.)
    /// with parent method/type context.
    /// TYPE-01 + TYPE-02 (per D-09: dedicated tool, per D-10: parent context).
    /// </summary>
    Task<IReadOnlyList<CompilerGeneratedTypeInfo>> FindCompilerGeneratedTypesAsync(
        AssemblyPath assemblyPath,
        CancellationToken cancellationToken = default);
}
