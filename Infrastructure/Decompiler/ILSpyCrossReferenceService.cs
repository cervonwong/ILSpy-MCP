using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using AssemblyPath = ILSpy.Mcp.Domain.Models.AssemblyPath;
using TypeName = ILSpy.Mcp.Domain.Models.TypeName;
using DomainTypeKind = ILSpy.Mcp.Domain.Models.TypeKind;

namespace ILSpy.Mcp.Infrastructure.Decompiler;

/// <summary>
/// Adapter that implements ICrossReferenceService using System.Reflection.Metadata IL scanning
/// and ICSharpCode.Decompiler type system for name resolution.
/// </summary>
public sealed class ILSpyCrossReferenceService : ICrossReferenceService
{
    private readonly ILogger<ILSpyCrossReferenceService> _logger;
    private readonly DecompilerSettings _settings;

    public ILSpyCrossReferenceService(ILogger<ILSpyCrossReferenceService> logger)
    {
        _logger = logger;
        _settings = new DecompilerSettings
        {
            ThrowOnAssemblyResolveErrors = false,
            ShowXmlDocumentation = false
        };
    }

    public async Task<IReadOnlyList<UsageResult>> FindUsagesAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        string memberName,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var type = decompiler.TypeSystem.MainModule.GetTypeDefinition(
                    new FullTypeName(typeName.FullName));

                if (type == null)
                    throw new TypeNotFoundException(typeName.FullName, assemblyPath.Value);

                // Find the target member's metadata tokens
                var targetTokens = GetMemberTokens(type, memberName, decompiler);
                if (targetTokens.Count == 0)
                    throw new MethodNotFoundException(memberName, typeName.FullName);

                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
                var reader = metadataFile.Metadata;
                var results = new List<UsageResult>();

                // Scan all method bodies in the assembly
                foreach (var scanType in decompiler.TypeSystem.MainModule.TypeDefinitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (scanType.ParentModule != decompiler.TypeSystem.MainModule)
                        continue;

                    foreach (var method in scanType.Methods)
                    {
                        if (method.MetadataToken.IsNil)
                            continue;

                        var methodHandle = (MethodDefinitionHandle)method.MetadataToken;
                        var methodDef = reader.GetMethodDefinition(methodHandle);

                        if (methodDef.RelativeVirtualAddress == 0)
                            continue; // Abstract or extern

                        try
                        {
                            var body = metadataFile.GetMethodBody(methodDef.RelativeVirtualAddress);
                            if (body == null) continue;

                            var ilReader = body.GetILReader();
                            ScanILForUsages(ref ilReader, reader, targetTokens, scanType, method, results);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger.LogDebug(ex, "Skipping method {Method} during IL scan", method.FullName);
                        }
                    }
                }

                return results;
            }
            catch (TypeNotFoundException) { throw; }
            catch (MethodNotFoundException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find usages of {MemberName} in {TypeName} from {Assembly}",
                    memberName, typeName.FullName, assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ImplementorResult>> FindImplementorsAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var targetType = decompiler.TypeSystem.MainModule.GetTypeDefinition(
                    new FullTypeName(typeName.FullName));

                if (targetType == null)
                    throw new TypeNotFoundException(typeName.FullName, assemblyPath.Value);

                var results = new List<ImplementorResult>();
                var mainModule = decompiler.TypeSystem.MainModule;

                foreach (var candidate in mainModule.TypeDefinitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (candidate.ParentModule != mainModule)
                        continue;

                    // Skip the target type itself
                    if (candidate.FullName == targetType.FullName)
                        continue;

                    // Check if candidate directly implements/extends the target
                    bool isDirect = false;
                    foreach (var baseType in candidate.DirectBaseTypes)
                    {
                        if (baseType.FullName == targetType.FullName)
                        {
                            isDirect = true;
                            break;
                        }
                    }

                    if (isDirect)
                    {
                        results.Add(new ImplementorResult
                        {
                            TypeFullName = candidate.FullName,
                            TypeShortName = candidate.Name,
                            IsDirect = true,
                            Kind = MapTypeKind(candidate.Kind)
                        });
                    }
                }

                // Second pass: find indirect implementors (types that extend direct implementors)
                var directNames = new HashSet<string>(results.Select(r => r.TypeFullName));
                foreach (var candidate in mainModule.TypeDefinitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (candidate.ParentModule != mainModule)
                        continue;

                    if (candidate.FullName == targetType.FullName)
                        continue;

                    if (directNames.Contains(candidate.FullName))
                        continue;

                    // Check if any base type is a direct implementor
                    foreach (var baseType in candidate.DirectBaseTypes)
                    {
                        if (directNames.Contains(baseType.FullName))
                        {
                            results.Add(new ImplementorResult
                            {
                                TypeFullName = candidate.FullName,
                                TypeShortName = candidate.Name,
                                IsDirect = false,
                                Kind = MapTypeKind(candidate.Kind)
                            });
                            break;
                        }
                    }
                }

                return results;
            }
            catch (TypeNotFoundException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find implementors of {TypeName} from {Assembly}",
                    typeName.FullName, assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DependencyResult>> FindDependenciesAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        string? methodName = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var type = decompiler.TypeSystem.MainModule.GetTypeDefinition(
                    new FullTypeName(typeName.FullName));

                if (type == null)
                    throw new TypeNotFoundException(typeName.FullName, assemblyPath.Value);

                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
                var reader = metadataFile.Metadata;
                var results = new List<DependencyResult>();
                var seen = new HashSet<string>();
                var assemblyDirectory = Path.GetDirectoryName(assemblyPath.Value);
                var resolverCache = new Dictionary<string, (string terminal, string? note)>(StringComparer.Ordinal);

                // Determine which methods to scan
                IEnumerable<IMethod> methods;
                if (!string.IsNullOrEmpty(methodName))
                {
                    methods = type.Methods.Where(m => m.Name == methodName);
                    if (!methods.Any())
                        throw new MethodNotFoundException(methodName, typeName.FullName);
                }
                else
                {
                    methods = type.Methods;
                }

                foreach (var method in methods)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (method.MetadataToken.IsNil)
                        continue;

                    var methodHandle = (MethodDefinitionHandle)method.MetadataToken;
                    var methodDef = reader.GetMethodDefinition(methodHandle);

                    if (methodDef.RelativeVirtualAddress == 0)
                        continue;

                    try
                    {
                        var body = metadataFile.GetMethodBody(methodDef.RelativeVirtualAddress);
                        if (body == null) continue;

                        var ilReader = body.GetILReader();
                        ScanILForDependencies(ref ilReader, reader, typeName.FullName, assemblyDirectory, resolverCache, results, seen);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogDebug(ex, "Skipping method {Method} during dependency scan", method.FullName);
                    }
                }

                return results;
            }
            catch (TypeNotFoundException) { throw; }
            catch (MethodNotFoundException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find dependencies of {TypeName} from {Assembly}",
                    typeName.FullName, assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<InstantiationResult>> FindInstantiationsAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var targetType = decompiler.TypeSystem.MainModule.GetTypeDefinition(
                    new FullTypeName(typeName.FullName));

                if (targetType == null)
                    throw new TypeNotFoundException(typeName.FullName, assemblyPath.Value);

                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
                var reader = metadataFile.Metadata;
                var results = new List<InstantiationResult>();

                // Scan all method bodies for newobj targeting this type
                foreach (var scanType in decompiler.TypeSystem.MainModule.TypeDefinitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (scanType.ParentModule != decompiler.TypeSystem.MainModule)
                        continue;

                    foreach (var method in scanType.Methods)
                    {
                        if (method.MetadataToken.IsNil)
                            continue;

                        var methodHandle = (MethodDefinitionHandle)method.MetadataToken;
                        var methodDef = reader.GetMethodDefinition(methodHandle);

                        if (methodDef.RelativeVirtualAddress == 0)
                            continue;

                        try
                        {
                            var body = metadataFile.GetMethodBody(methodDef.RelativeVirtualAddress);
                            if (body == null) continue;

                            var ilReader = body.GetILReader();
                            ScanILForInstantiations(ref ilReader, reader, typeName.FullName, scanType, method, results);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger.LogDebug(ex, "Skipping method {Method} during instantiation scan", method.FullName);
                        }
                    }
                }

                return results;
            }
            catch (TypeNotFoundException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find instantiations of {TypeName} from {Assembly}",
                    typeName.FullName, assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    // ---- Private IL scanning helpers ----

    /// <summary>
    /// Gets metadata tokens for all members of a type matching the given name.
    /// Returns a dictionary mapping token int -> UsageKind category.
    /// </summary>
    private static Dictionary<int, UsageKind> GetMemberTokens(
        ITypeDefinition type, string memberName, CSharpDecompiler decompiler)
    {
        var tokens = new Dictionary<int, UsageKind>();

        foreach (var method in type.Methods.Where(m => m.Name == memberName))
        {
            if (!method.MetadataToken.IsNil)
            {
                var kind = method.IsVirtual || method.IsAbstract ? UsageKind.VirtualCall : UsageKind.Call;
                tokens[MetadataTokens.GetToken(method.MetadataToken)] = kind;
            }
        }

        foreach (var field in type.Fields.Where(f => f.Name == memberName))
        {
            if (!field.MetadataToken.IsNil)
                tokens[MetadataTokens.GetToken(field.MetadataToken)] = UsageKind.FieldRead;
        }

        foreach (var prop in type.Properties.Where(p => p.Name == memberName))
        {
            if (prop.Getter != null && !prop.Getter.MetadataToken.IsNil)
                tokens[MetadataTokens.GetToken(prop.Getter.MetadataToken)] = UsageKind.PropertyGet;
            if (prop.Setter != null && !prop.Setter.MetadataToken.IsNil)
                tokens[MetadataTokens.GetToken(prop.Setter.MetadataToken)] = UsageKind.PropertySet;
        }

        return tokens;
    }

    /// <summary>
    /// Scans IL bytes for call/callvirt/ldfld/stfld/ldsfld/stsfld instructions
    /// and checks if they reference any of the target tokens.
    /// </summary>
    private static void ScanILForUsages(
        ref BlobReader ilReader,
        MetadataReader reader,
        Dictionary<int, UsageKind> targetTokens,
        ITypeDefinition scanType,
        IMethod method,
        List<UsageResult> results)
    {
        while (ilReader.RemainingBytes > 0)
        {
            int offset = ilReader.Offset;
            var opCode = ILParsingHelper.ReadILOpCode(ref ilReader);

            if (ILParsingHelper.IsTokenReferenceOpCode(opCode))
            {
                int token = ilReader.ReadInt32();
                if (targetTokens.TryGetValue(token, out var baseKind))
                {
                    // Refine the kind based on the actual opcode
                    var kind = RefineUsageKind(opCode, baseKind);
                    results.Add(new UsageResult
                    {
                        DeclaringType = scanType.FullName,
                        MethodName = method.Name,
                        ILOffset = offset,
                        Kind = kind,
                        MethodSignature = FormatMethodSignature(method)
                    });
                }
            }
            else
            {
                ILParsingHelper.SkipOperand(ref ilReader, opCode);
            }
        }
    }

    /// <summary>
    /// Scans IL bytes for outward references (calls, field accesses) from a method.
    /// </summary>
    private static void ScanILForDependencies(
        ref BlobReader ilReader,
        MetadataReader reader,
        string sourceTypeName,
        string? assemblyDirectory,
        Dictionary<string, (string terminal, string? note)> resolverCache,
        List<DependencyResult> results,
        HashSet<string> seen)
    {
        while (ilReader.RemainingBytes > 0)
        {
            var opCode = ILParsingHelper.ReadILOpCode(ref ilReader);

            if (ILParsingHelper.IsTokenReferenceOpCode(opCode))
            {
                int token = ilReader.ReadInt32();
                var handle = MetadataTokens.EntityHandle(token);

                string? targetMember = null;
                string? targetType = null;
                DependencyKind kind = DependencyKind.MethodCall;

                try
                {
                    if (handle.Kind == HandleKind.MemberReference)
                    {
                        var memberRef = reader.GetMemberReference((MemberReferenceHandle)handle);
                        var memberRefName = reader.GetString(memberRef.Name);
                        targetType = GetMemberReferenceDeclaringType(reader, memberRef);
                        targetMember = $"{targetType}.{memberRefName}";

                        kind = memberRef.GetKind() == MemberReferenceKind.Method
                            ? (opCode == ILOpCode.Callvirt ? DependencyKind.VirtualCall : DependencyKind.MethodCall)
                            : DependencyKind.FieldAccess;
                    }
                    else if (handle.Kind == HandleKind.MethodDefinition)
                    {
                        var methodDef = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                        var methodName = reader.GetString(methodDef.Name);
                        var declaringTypeHandle = methodDef.GetDeclaringType();
                        targetType = GetTypeFullName(reader, declaringTypeHandle);
                        targetMember = $"{targetType}.{methodName}";
                        kind = opCode == ILOpCode.Callvirt ? DependencyKind.VirtualCall : DependencyKind.MethodCall;
                    }
                    else if (handle.Kind == HandleKind.FieldDefinition)
                    {
                        var fieldDef = reader.GetFieldDefinition((FieldDefinitionHandle)handle);
                        var fieldName = reader.GetString(fieldDef.Name);
                        var declaringTypeHandle = fieldDef.GetDeclaringType();
                        targetType = GetTypeFullName(reader, declaringTypeHandle);
                        targetMember = $"{targetType}.{fieldName}";
                        kind = DependencyKind.FieldAccess;
                    }
                }
                catch
                {
                    // Skip unresolvable tokens
                    continue;
                }

                if (targetMember != null && targetType != null
                    && targetType != sourceTypeName
                    && seen.Add(targetMember))
                {
                    var definingAssembly = ResolveDefiningAssembly(reader, handle, assemblyDirectory, resolverCache, out var resolutionNote);
                    results.Add(new DependencyResult
                    {
                        TargetMember = targetMember,
                        TargetType = targetType,
                        Kind = kind,
                        DefiningAssembly = definingAssembly,
                        ResolutionNote = resolutionNote
                    });
                }
            }
            else
            {
                ILParsingHelper.SkipOperand(ref ilReader, opCode);
            }
        }
    }

    /// <summary>
    /// Scans IL bytes for newobj instructions targeting the specified type.
    /// </summary>
    private static void ScanILForInstantiations(
        ref BlobReader ilReader,
        MetadataReader reader,
        string targetTypeName,
        ITypeDefinition scanType,
        IMethod method,
        List<InstantiationResult> results)
    {
        while (ilReader.RemainingBytes > 0)
        {
            int offset = ilReader.Offset;
            var opCode = ILParsingHelper.ReadILOpCode(ref ilReader);

            if (opCode == ILOpCode.Newobj)
            {
                int token = ilReader.ReadInt32();
                var handle = MetadataTokens.EntityHandle(token);

                string? constructorType = null;
                try
                {
                    if (handle.Kind == HandleKind.MemberReference)
                    {
                        var memberRef = reader.GetMemberReference((MemberReferenceHandle)handle);
                        constructorType = GetMemberReferenceDeclaringType(reader, memberRef);
                    }
                    else if (handle.Kind == HandleKind.MethodDefinition)
                    {
                        var methodDef = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                        constructorType = GetTypeFullName(reader, methodDef.GetDeclaringType());
                    }
                }
                catch
                {
                    // Skip unresolvable tokens
                }

                if (constructorType == targetTypeName)
                {
                    results.Add(new InstantiationResult
                    {
                        DeclaringType = scanType.FullName,
                        MethodName = method.Name,
                        ILOffset = offset,
                        MethodSignature = FormatMethodSignature(method)
                    });
                }
            }
            else if (ILParsingHelper.IsTokenReferenceOpCode(opCode))
            {
                ilReader.ReadInt32(); // Skip the token
            }
            else
            {
                ILParsingHelper.SkipOperand(ref ilReader, opCode);
            }
        }
    }

    /// <summary>
    /// Refines the usage kind based on the actual IL opcode encountered.
    /// </summary>
    private static UsageKind RefineUsageKind(ILOpCode opCode, UsageKind baseKind)
    {
        return opCode switch
        {
            ILOpCode.Call or ILOpCode.Ldftn => baseKind == UsageKind.PropertyGet ? UsageKind.PropertyGet
                : baseKind == UsageKind.PropertySet ? UsageKind.PropertySet
                : UsageKind.Call,
            ILOpCode.Callvirt or ILOpCode.Ldvirtftn => baseKind == UsageKind.PropertyGet ? UsageKind.PropertyGet
                : baseKind == UsageKind.PropertySet ? UsageKind.PropertySet
                : UsageKind.VirtualCall,
            ILOpCode.Ldfld or ILOpCode.Ldsfld or ILOpCode.Ldflda or ILOpCode.Ldsflda => UsageKind.FieldRead,
            ILOpCode.Stfld or ILOpCode.Stsfld => UsageKind.FieldWrite,
            _ => baseKind
        };
    }

    /// <summary>
    /// Gets the declaring type name from a MemberReference.
    /// </summary>
    private static string? GetMemberReferenceDeclaringType(MetadataReader reader, MemberReference memberRef)
    {
        var parent = memberRef.Parent;
        if (parent.Kind == HandleKind.TypeReference)
        {
            var typeRef = reader.GetTypeReference((TypeReferenceHandle)parent);
            var ns = reader.GetString(typeRef.Namespace);
            var name = reader.GetString(typeRef.Name);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }
        else if (parent.Kind == HandleKind.TypeDefinition)
        {
            return GetTypeFullName(reader, (TypeDefinitionHandle)parent);
        }
        return null;
    }

    /// <summary>
    /// Gets the full name of a type from its TypeDefinitionHandle.
    /// </summary>
    private static string GetTypeFullName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var typeDef = reader.GetTypeDefinition(handle);
        var ns = reader.GetString(typeDef.Namespace);
        var name = reader.GetString(typeDef.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    /// <summary>
    /// Formats a method signature for display.
    /// </summary>
    private static string FormatMethodSignature(IMethod method)
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.FullName} {p.Name}"));
        return $"{method.ReturnType.FullName} {method.Name}({parameters})";
    }

    /// <summary>
    /// Walks TypeReference.ResolutionScope -> AssemblyReference for the given handle,
    /// optionally loads the referenced assembly from the analyzed assembly's directory
    /// via PEFile, chases type-forwards (bounded depth 5), and returns the terminal
    /// assembly name. Fail-soft: on any failure, returns the immediate AssemblyReference
    /// name and populates resolutionNote with a descriptive string that MUST NOT contain
    /// any filesystem path.
    /// </summary>
    private static string ResolveDefiningAssembly(
        MetadataReader reader,
        EntityHandle memberOrTypeHandle,
        string? assemblyDirectory,
        Dictionary<string, (string terminal, string? note)> cache,
        out string? resolutionNote)
    {
        resolutionNote = null;
        try
        {
            // Extract the declaring type reference handle from the member/type handle
            TypeReferenceHandle? typeRefHandle = null;
            if (memberOrTypeHandle.Kind == HandleKind.MemberReference)
            {
                var memberRef = reader.GetMemberReference((MemberReferenceHandle)memberOrTypeHandle);
                if (memberRef.Parent.Kind == HandleKind.TypeReference)
                    typeRefHandle = (TypeReferenceHandle)memberRef.Parent;
            }
            else if (memberOrTypeHandle.Kind == HandleKind.TypeReference)
            {
                typeRefHandle = (TypeReferenceHandle)memberOrTypeHandle;
            }
            else if (memberOrTypeHandle.Kind == HandleKind.MethodDefinition
                  || memberOrTypeHandle.Kind == HandleKind.FieldDefinition
                  || memberOrTypeHandle.Kind == HandleKind.TypeDefinition)
            {
                // Defined in THIS assembly — not a cross-assembly reference
                return reader.GetAssemblyDefinition().GetAssemblyName().Name ?? "(unknown)";
            }

            if (typeRefHandle == null)
            {
                resolutionNote = "unresolved: unsupported handle kind for defining-assembly resolution";
                return "(unknown)";
            }

            var typeRef = reader.GetTypeReference(typeRefHandle.Value);
            if (typeRef.ResolutionScope.Kind != HandleKind.AssemblyReference)
            {
                // Nested scope or module scope — fall back gracefully
                resolutionNote = "unresolved: resolution scope is not an AssemblyReference";
                return "(unknown)";
            }

            var asmRefHandle = (AssemblyReferenceHandle)typeRef.ResolutionScope;
            var asmRef = reader.GetAssemblyReference(asmRefHandle);
            var immediateName = reader.GetString(asmRef.Name);

            // Cache hit?
            if (cache.TryGetValue(immediateName, out var cached))
            {
                resolutionNote = cached.note;
                return cached.terminal;
            }

            if (string.IsNullOrEmpty(assemblyDirectory))
            {
                resolutionNote = "unresolved: no analyzed-assembly directory available for sibling lookup";
                cache[immediateName] = (immediateName, resolutionNote);
                return immediateName;
            }

            // Try to chase the type-forward chain bounded to 5 hops
            var typeNamespace = reader.GetString(typeRef.Namespace);
            var typeName = reader.GetString(typeRef.Name);
            var terminalName = ChaseTypeForward(assemblyDirectory, immediateName, typeNamespace, typeName, maxHops: 5, out var note);
            cache[immediateName] = (terminalName, note);
            resolutionNote = note;
            return terminalName;
        }
        catch
        {
            resolutionNote = "unresolved: exception during defining-assembly resolution";
            return "(unknown)";
        }
    }

    /// <summary>
    /// Loads the referenced assembly from the analyzed-assembly directory and chases
    /// ExportedType forwards to the terminal assembly. Non-throwing — returns the
    /// starting name with a populated note on any failure.
    /// </summary>
    private static string ChaseTypeForward(
        string assemblyDirectory,
        string startAssemblyName,
        string targetNamespace,
        string targetTypeName,
        int maxHops,
        out string? note)
    {
        note = null;
        var currentName = startAssemblyName;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int hop = 0; hop < maxHops; hop++)
        {
            if (!visited.Add(currentName))
            {
                note = "unresolved: type-forward cycle detected";
                return startAssemblyName;
            }

            // Try .dll first, then .exe
            var candidateDll = Path.Combine(assemblyDirectory, currentName + ".dll");
            var candidateExe = Path.Combine(assemblyDirectory, currentName + ".exe");
            var path = File.Exists(candidateDll) ? candidateDll
                     : File.Exists(candidateExe) ? candidateExe
                     : null;

            if (path == null)
            {
                note = hop == 0
                    ? "unresolved: referenced assembly not present in analyzed assembly directory"
                    : "unresolved: type-forward target assembly not present";
                return startAssemblyName;
            }

            try
            {
                using var pe = new ICSharpCode.Decompiler.Metadata.PEFile(path);
                var peReader = pe.Metadata;

                // Scan ExportedType rows for a matching namespace+name
                foreach (var exportedHandle in peReader.ExportedTypes)
                {
                    var exported = peReader.GetExportedType(exportedHandle);
                    var ns = peReader.GetString(exported.Namespace);
                    var name = peReader.GetString(exported.Name);
                    if (ns == targetNamespace && name == targetTypeName)
                    {
                        if (exported.Implementation.Kind == HandleKind.AssemblyReference)
                        {
                            var nextAsmRef = peReader.GetAssemblyReference((AssemblyReferenceHandle)exported.Implementation);
                            currentName = peReader.GetString(nextAsmRef.Name);
                            goto nextHop;  // recurse via loop
                        }
                    }
                }

                // No forward found — this IS the terminal assembly
                return currentName;
            }
            catch (ICSharpCode.Decompiler.Metadata.MetadataFileNotSupportedException)
            {
                note = "unresolved: referenced file is not a .NET metadata assembly";
                return startAssemblyName;
            }
            catch (BadImageFormatException)
            {
                note = "unresolved: referenced assembly is corrupt or not a valid PE file";
                return startAssemblyName;
            }
            catch
            {
                note = "unresolved: exception while loading referenced assembly for type-forward chase";
                return startAssemblyName;
            }

            nextHop: ;
        }

        note = "unresolved: type-forward chase exceeded maximum depth";
        return currentName;
    }

    private static DomainTypeKind MapTypeKind(ICSharpCode.Decompiler.TypeSystem.TypeKind kind) => kind switch
    {
        ICSharpCode.Decompiler.TypeSystem.TypeKind.Class => DomainTypeKind.Class,
        ICSharpCode.Decompiler.TypeSystem.TypeKind.Interface => DomainTypeKind.Interface,
        ICSharpCode.Decompiler.TypeSystem.TypeKind.Struct => DomainTypeKind.Struct,
        ICSharpCode.Decompiler.TypeSystem.TypeKind.Enum => DomainTypeKind.Enum,
        ICSharpCode.Decompiler.TypeSystem.TypeKind.Delegate => DomainTypeKind.Delegate,
        _ => DomainTypeKind.Unknown
    };
}
