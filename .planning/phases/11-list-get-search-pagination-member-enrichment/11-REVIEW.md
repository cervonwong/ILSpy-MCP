---
phase: 11-list-get-search-pagination-member-enrichment
reviewed: 2026-04-10T17:45:00Z
depth: standard
files_reviewed: 14
files_reviewed_list:
  - Transport/Mcp/Tools/ListAssemblyTypesTool.cs
  - Transport/Mcp/Tools/ListEmbeddedResourcesTool.cs
  - Transport/Mcp/Tools/SearchMembersByNameTool.cs
  - Application/UseCases/ListAssemblyTypesUseCase.cs
  - Application/UseCases/ListEmbeddedResourcesUseCase.cs
  - Application/UseCases/SearchMembersByNameUseCase.cs
  - Tests/Tools/ListAssemblyTypesToolTests.cs
  - Tests/Tools/ListEmbeddedResourcesToolTests.cs
  - Tests/Tools/SearchMembersByNameToolTests.cs
  - Domain/Models/TypeInfo.cs
  - Infrastructure/Decompiler/ILSpyDecompilerService.cs
  - Application/UseCases/GetTypeMembersUseCase.cs
  - Transport/Mcp/Tools/GetTypeMembersTool.cs
  - Tests/Tools/GetTypeMembersToolTests.cs
findings:
  critical: 0
  warning: 3
  info: 3
  total: 6
status: issues_found
---

# Phase 11: Code Review Report

**Reviewed:** 2026-04-10T17:45:00Z
**Depth:** standard
**Files Reviewed:** 14
**Status:** issues_found

## Summary

Phase 11 adds pagination (maxResults/offset) to four tools: `list_assembly_types`, `list_embedded_resources`, `search_members_by_name`, and `get_type_members`. It also enriches `get_type_members` with inherited members, modifiers (virtual/override/sealed), and attribute tags. The implementation follows a consistent pattern across all tools with shared pagination via `PaginationEnvelope`.

The code is well-structured and follows existing project conventions. Error handling is thorough with proper exception mapping at the tool layer. No security issues found. Three warnings relate to missing validation for negative `offset` values, which could silently produce confusing output but not crashes or data corruption. Three informational items on code quality.

## Warnings

### WR-01: Negative offset not validated in any tool

**File:** `Transport/Mcp/Tools/ListAssemblyTypesTool.cs:30`
**Issue:** All four tool handlers validate `maxResults` (must be 1-500) but do not validate `offset`. A negative offset (e.g., `offset: -5`) is passed directly to LINQ `.Skip()`, which treats negative values as `.Skip(0)`. This silently ignores the invalid input and returns the first page -- the caller gets no indication their parameter was wrong. This is inconsistent with the strict validation applied to `maxResults` and could confuse AI assistant callers that expect pagination to behave predictably.

The same issue applies to all four tools:
- `ListAssemblyTypesTool.cs:30`
- `ListEmbeddedResourcesTool.cs:32`
- `SearchMembersByNameTool.cs:31`
- `GetTypeMembersTool.cs:30`

**Fix:** Add offset validation alongside maxResults in each tool handler:
```csharp
if (offset < 0)
{
    throw new McpToolException("INVALID_PARAMETER",
        "offset must be >= 0.");
}
```

### WR-02: SearchMembersAsync only searches public types

**File:** `Infrastructure/Decompiler/ILSpyDecompilerService.cs:311`
**Issue:** `SearchMembersAsync` filters to `Accessibility.Public` types only (line 311). If a caller searches for a member name that exists on an internal or protected type, they will get zero results with no indication the member exists but is inaccessible. This differs from `ListTypesAsync` (line 167), which returns all types regardless of accessibility. The inconsistency means a user can see a type in `list_assembly_types` output but searching for its members yields nothing.

**Fix:** Either remove the accessibility filter to match `ListTypesAsync` behavior, or add an optional `includeNonPublic` parameter. If restricting to public is intentional, the tool description should state this:
```csharp
// Option A: Remove filter (match ListTypesAsync)
foreach (var type in mainModule.TypeDefinitions
    .Where(t => t.ParentModule == mainModule))

// Option B: Keep filter but document it
[Description("...Searches public types only...")]
```

### WR-03: Inherited member resolution only traverses direct base types (single level)

**File:** `Infrastructure/Decompiler/ILSpyDecompilerService.cs:427`
**Issue:** `MapToTypeInfoWithInheritance` iterates over `type.DirectBaseTypes` (line 427), which only includes the immediate parent class and directly-implemented interfaces. For a class like `C : B : A`, calling `get_type_members` on `C` will show members inherited from `B` but not from `A`. This is a shallow traversal that misses deeply inherited members, which could mislead callers into thinking those members do not exist.

**Fix:** Walk the full inheritance chain recursively:
```csharp
private static IEnumerable<ITypeDefinition> GetBaseTypeChain(ITypeDefinition type)
{
    foreach (var baseType in type.DirectBaseTypes)
    {
        var baseDef = baseType.GetDefinition();
        if (baseDef == null || baseDef.FullName == "System.Object") continue;
        yield return baseDef;
        foreach (var ancestor in GetBaseTypeChain(baseDef))
            yield return ancestor;
    }
}
```

## Info

### IN-01: Duplicate pagination validation could be extracted

**File:** `Transport/Mcp/Tools/ListAssemblyTypesTool.cs:36-45`
**Issue:** The same maxResults validation block (lines 36-45) is copy-pasted identically across all four tool handlers. This is not a bug but increases maintenance cost -- if the cap changes from 500, four files must be updated.

**Fix:** Extract a shared validation helper:
```csharp
// In a shared class, e.g., PaginationValidator.cs
public static class PaginationValidator
{
    public static void Validate(int maxResults, int offset)
    {
        if (maxResults > 500)
            throw new McpToolException("INVALID_PARAMETER", "maxResults cannot exceed 500. Use offset to paginate.");
        if (maxResults <= 0)
            throw new McpToolException("INVALID_PARAMETER", "maxResults must be >= 1.");
        if (offset < 0)
            throw new McpToolException("INVALID_PARAMETER", "offset must be >= 0.");
    }
}
```

### IN-02: ListAssemblyTypesUseCase sorts twice

**File:** `Application/UseCases/ListAssemblyTypesUseCase.cs:49`
**Issue:** The use case sorts by `FullName` (line 49: `.OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase)`), but the service layer (`ILSpyDecompilerService.ListTypesAsync`, line 174) already returns results sorted by `FullName` via `.OrderBy(t => t.FullName)`. The second sort is redundant. Not a bug -- just wasted work.

**Fix:** Remove the duplicate sort in the use case since the service already orders results. If the intent is to enforce a specific string comparer, apply it only in one place.

### IN-03: Domain model name conflicts with System types

**File:** `Domain/Models/TypeInfo.cs:43-93`
**Issue:** The domain model defines `MethodInfo`, `PropertyInfo`, `FieldInfo`, `EventInfo`, and `ParameterInfo` which shadow `System.Reflection.MethodInfo`, etc. While this works because the project avoids `using System.Reflection;` at the domain level, it creates potential confusion when navigating or referencing these types alongside .NET reflection APIs in infrastructure code.

**Fix:** No immediate action required. This is a naming consideration worth noting if future phases introduce `System.Reflection.Metadata` usage in the same files.

---

_Reviewed: 2026-04-10T17:45:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
