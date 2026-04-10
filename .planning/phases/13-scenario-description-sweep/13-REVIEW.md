---
phase: 13-scenario-description-sweep
reviewed: 2026-04-11T00:00:00Z
depth: standard
files_reviewed: 9
files_reviewed_list:
  - Application/UseCases/FindUsagesUseCase.cs
  - Application/UseCases/ListNamespaceTypesUseCase.cs
  - Program.cs
  - Tests/Fixtures/ToolTestFixture.cs
  - Tests/Tools/FindUsagesToolTests.cs
  - Tests/Tools/ListNamespaceTypesToolTests.cs
  - Transport/Mcp/Tools/FindUsagesTool.cs
  - Transport/Mcp/Tools/ListAssemblyTypesTool.cs
  - Transport/Mcp/Tools/ListNamespaceTypesTool.cs
findings:
  critical: 0
  warning: 5
  info: 3
  total: 8
status: issues_found
---

# Phase 13: Code Review Report

**Reviewed:** 2026-04-11T00:00:00Z
**Depth:** standard
**Files Reviewed:** 9
**Status:** issues_found

## Summary

This pass covers the two new use cases (`FindUsagesUseCase`, `ListNamespaceTypesUseCase`), their MCP tool handlers (`FindUsagesTool`, `ListAssemblyTypesTool`, `ListNamespaceTypesTool`), DI wiring in `Program.cs` and `ToolTestFixture`, and two test files.

`Program.cs` and `ToolTestFixture.cs` are correct and complete — all new use cases and tools are registered in both locations with no omissions or duplicates.

The test files provide good coverage of happy-path, error, and pagination scenarios. One dead variable was found in a test.

The main concerns span three areas:

1. **Description framing (Principle 1):** Two tool descriptions use mechanical "Lists/Finds…" framing instead of the required scenario-oriented "Use this when…" opening.
2. **Pagination completeness (Principle 4 — HARD RULE):** `list_assembly_types` has no `offset` at all; `list_namespace_types` has `maxTypes` but no `offset`. Both expose data-dependent output lists without a full pagination contract.
3. **Input validation gap:** `FindUsagesTool` validates `maxResults` but not `offset`, allowing a negative value to propagate to `Enumerable.Skip` and surface as a misleading `INTERNAL_ERROR`.

---

## Warnings

### WR-01: `find_usages` description uses mechanical framing, not scenario framing

**File:** `Transport/Mcp/Tools/FindUsagesTool.cs:28`
**Issue:** The `[Description]` opens with "Find all call sites, field accesses, and property usages of a specific member across an assembly." This describes mechanics. The `mcp-tool-design` skill (Principle 1) requires descriptions to open with the user's situation so an agent scanning 28+ tools can filter intent in one pass. The second sentence ("Use this to trace where a method is called…") partially addresses this, but it is in the wrong position — agents read the first sentence as the primary signal.
**Fix:**
```csharp
[Description("Use this when you need to assess the blast radius before renaming or changing a method, field, or property. Returns every call site, field access, and property usage across the assembly so you can judge impact before refactoring.")]
```

---

### WR-02: `list_namespace_types` description uses mechanical framing

**File:** `Transport/Mcp/Tools/ListNamespaceTypesTool.cs:28`
**Issue:** The `[Description]` reads "Lists all types in a namespace with full signatures, member counts, and public method signatures." This is a capabilities list, not a situation framing. An agent cannot determine from this text when to prefer this tool over `list_assembly_types`. Per Principle 1 of the `mcp-tool-design` skill the opening must answer "when would I reach for this?"
**Fix:**
```csharp
[Description("Use this when you know the namespace and want a structured overview of every type in it — interfaces, classes, enums, delegates — before diving into individual decompilations. Returns member counts and public method signatures as a summary; use decompile_type for full source. For a flat catalogue of all types across namespaces, use list_assembly_types instead.")]
```

---

### WR-03: `list_assembly_types` lacks an `offset` parameter — pagination contract incomplete (Principle 4 HARD RULE)

**File:** `Transport/Mcp/Tools/ListAssemblyTypesTool.cs:26-29`
**Issue:** The tool lists all types in an assembly. Output count depends entirely on the assembly contents — a large assembly produces thousands of entries. The tool has no `offset` parameter. Principle 4 of the `mcp-tool-design` skill is a HARD RULE: any tool whose output count is data-dependent must expose `maxResults` + `offset`, and the response must report `total` and `returned` so the agent knows whether more pages exist. Without `offset`, an agent that receives a truncated result has no way to retrieve the remainder.
**Fix:** Add `maxResults` and `offset` parameters and wire them through `ListAssemblyTypesUseCase`. The response footer must use `PaginationEnvelope.AppendFooter` to report counts, matching the pattern in `FindUsagesTool`:
```csharp
public async Task<string> ExecuteAsync(
    [Description("Path to the .NET assembly file")] string assemblyPath,
    [Description("Optional: filter types by namespace (case-insensitive)")] string? namespaceFilter = null,
    [Description("Maximum types to return (default 200)")] int maxResults = 200,
    [Description("Types to skip for pagination (default 0)")] int offset = 0,
    CancellationToken cancellationToken = default)
```

---

### WR-04: `list_namespace_types` has `maxTypes` but no `offset` — pagination contract incomplete (Principle 4 HARD RULE)

**File:** `Transport/Mcp/Tools/ListNamespaceTypesTool.cs:29-33`
**Issue:** The tool accepts `maxTypes` to cap output but has no `offset`. A namespace with more types than `maxTypes` is silently truncated. The header line "Namespace: X (N types)" (produced by `ListNamespaceTypesUseCase.FormatOutput`) correctly advertises the true total, so the agent knows more exist — but it has no parameter to retrieve the remainder. Principle 4 requires both halves of the pagination contract.
**Fix:** Add an `offset` parameter and wire it through `ListNamespaceTypesUseCase.ExecuteAsync`. Apply `Skip(offset)` before `Take(maxTypes)` on the sorted top-level types list, then append a `PaginationEnvelope.AppendFooter` call to the output:
```csharp
public async Task<string> ExecuteAsync(
    [Description("Path to the .NET assembly file")] string assemblyPath,
    [Description("Full namespace name (e.g., 'System.Collections.Generic')")] string namespaceName,
    [Description("Maximum number of types to return (default 200)")] int maxTypes = 200,
    [Description("Types to skip for pagination (default 0)")] int offset = 0,
    CancellationToken cancellationToken = default)
```

---

### WR-05: Negative `offset` not validated — propagates to `Enumerable.Skip` and throws as `INTERNAL_ERROR`

**File:** `Transport/Mcp/Tools/FindUsagesTool.cs:43-48` and `Application/UseCases/FindUsagesUseCase.cs:53`
**Issue:** `FindUsagesTool.ExecuteAsync` validates `maxResults > 500` and `maxResults <= 0` but does not guard `offset`. A caller passing `offset = -1` causes `results.Skip(-1)` at `FindUsagesUseCase.cs:53`. `Enumerable.Skip` throws `ArgumentOutOfRangeException` for negative arguments. That exception is not a `DomainException`, so the use case's `catch (Exception ex)` handler re-throws it and the tool's outer `catch (Exception ex)` handler surfaces it as `INTERNAL_ERROR` — an unhelpful error for what is straightforward bad input.
**Fix:** Add the guard in `FindUsagesTool.ExecuteAsync` alongside the existing checks:
```csharp
if (offset < 0)
{
    throw new McpToolException("INVALID_PARAMETER",
        "offset must be >= 0.");
}
```

---

## Info

### IN-01: Error messages omit next-tool hints (Principle 8)

**File:** `Transport/Mcp/Tools/FindUsagesTool.cs:53-66` and `Transport/Mcp/Tools/ListNamespaceTypesTool.cs:39-42`
**Issue:** Per Principle 8 of the `mcp-tool-design` skill, every error should name a recovery tool. The current messages are plain sanitized exception text with no hint:
- `TypeNotFoundException` in `FindUsagesTool` → no hint to use `list_assembly_types` or `resolve_type`.
- `MethodNotFoundException` in `FindUsagesTool` → no hint to use `get_type_members`.
- `NamespaceNotFoundException` in `ListNamespaceTypesTool` → no hint to use `list_assembly_types`.

**Fix:** Append recovery suggestions to each `McpToolException` message:
```csharp
// FindUsagesTool — TypeNotFoundException
throw new McpToolException("TYPE_NOT_FOUND",
    $"{ErrorSanitizer.SanitizePath(ex.Message)} Use list_assembly_types to discover available types, or resolve_type if the type may be in a referenced assembly.");

// FindUsagesTool — MethodNotFoundException
throw new McpToolException("MEMBER_NOT_FOUND",
    $"{ErrorSanitizer.SanitizePath(ex.Message)} Use get_type_members to list available members on this type.");

// ListNamespaceTypesTool — NamespaceNotFoundException
throw new McpToolException("NAMESPACE_NOT_FOUND",
    $"{ErrorSanitizer.SanitizePath(ex.Message)} Use list_assembly_types to enumerate available namespaces in this assembly.");
```

---

### IN-02: Namespace header shows total type count but `maxTypes` silently truncates output without a notice

**File:** `Application/UseCases/ListNamespaceTypesUseCase.cs:107-112` and `182-203`
**Issue:** `TotalTypeCount` is set to `exactMatches.Count` (all types, including nested and those beyond `maxTypes`), but `summary.Types` holds only the top `maxTypes` entries after `Take(maxTypes)` at line 101. The header "Namespace: X (50 types)" followed by only 2 entries in the body will confuse an agent without an explicit truncation notice. This will become a more visible problem once WR-04 (offset) is implemented.
**Fix:** Append a truncation notice in `FormatOutput` when `summary.Types.Count < summary.TotalTypeCount`:
```csharp
if (summary.Types.Count < summary.TotalTypeCount)
{
    sb.AppendLine();
    sb.AppendLine($"[Showing {summary.Types.Count} of {summary.TotalTypeCount} types. Increase maxTypes or use offset to retrieve more.]");
}
```

---

### IN-03: Unused variable `typeEntries` in `MaxTypesLimitsOutput` test

**File:** `Tests/Tools/ListNamespaceTypesToolTests.cs:103-109`
**Issue:** The variable `typeEntries` is computed by counting all kind-label lines (lines 103-109) but is never referenced in an assertion. Only `topLevelEntries` is asserted at line 121. This is dead code — it adds noise, may mislead future maintainers about test intent, and performs a wasted LINQ evaluation per test run.
**Fix:** Remove the `typeEntries` variable and its computation (lines 103-109), keeping only the `topLevelEntries` counting and the final assertion.

---

_Reviewed: 2026-04-11T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
