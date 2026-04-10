# Phase 11: List/Get/Search Pagination & Member Enrichment - Context

**Gathered:** 2026-04-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Apply the pagination contract to the four remaining list/enumeration tools and enrich `get_type_members` with inherited/declared distinction and modifier context. Requirements: PAGE-03, PAGE-04, PAGE-05, OUTPUT-05.

**In scope:**
- `list_assembly_types` — add `(maxResults, offset)` pagination (PAGE-03)
- `list_embedded_resources` — add `(maxResults, offset)` pagination (PAGE-03)
- `get_type_members` — add `(maxResults, offset)` pagination (PAGE-04) + inherited/declared/modifiers/attributes enrichment (OUTPUT-05)
- `search_members_by_name` — add `(maxResults, offset)` pagination (PAGE-05)

**Not in scope:**
- `list_namespace_types` — already paginated in Phase 9 (PAGE-06 closed)
- IL token resolution, search enrichment (Phase 12)
- Description rewrites (Phase 13)
- New tools or capabilities

</domain>

<decisions>
## Implementation Decisions

### Pagination contract application (PAGE-03, PAGE-04, PAGE-05)

- **D-01:** All four tools follow the exact Phase 10 pattern: `maxResults=100` default, `offset=0` default, `500` cap with `INVALID_ARGUMENT` error on exceed, `PaginationEnvelope.AppendFooter()` for the `[pagination:{total,returned,offset,truncated,nextOffset}]` footer. No deviations from established contract.
- **D-02:** Pagination operates over **flat item lists**, not groups. `list_assembly_types` paginates over types (not namespace groups). `search_members_by_name` paginates over individual member matches (not type groups). `get_type_members` paginates over all members combined (constructors + methods + properties + fields + events flattened). `list_embedded_resources` paginates over resources. The namespace/type grouping in output formatting is a presentation concern applied after slicing.
- **D-03:** Validation lives in the Transport layer (Tool classes), same as Phase 10 find-tools. Use cases accept `(maxResults, offset)` without re-validating.
- **D-04:** `total` always counts the full unfiltered-by-pagination result set. For `list_assembly_types` with `namespaceFilter`, `total` counts the filtered set (types matching the filter), not all types in the assembly. This matches how `list_namespace_types` works.

### Member enrichment (OUTPUT-05)

- **D-05:** Add `IsInherited` (bool) to all member record types (`MethodInfo`, `PropertyInfo`, `FieldInfo`, `EventInfo`). `true` means the member is declared in a base type, not in the type being inspected. Per Principle 3 (assume lazy agent): an agent inspecting a type needs to know immediately which members are "new here" vs inherited, without a follow-up call.
- **D-06:** Add `IsSealed` (bool) and `IsOverride` (bool) to `MethodInfo`. These complete the modifier picture alongside existing `IsStatic`, `IsAbstract`, `IsVirtual`. The `sealed` keyword in C# is `IsSealed && IsOverride` — but exposing both flags independently lets agents reason about the IL-level semantics directly.
- **D-07:** Add `Attributes` as `IReadOnlyList<string>` to all member record types. Each entry is the short attribute type name without `Attribute` suffix (e.g., `"Obsolete"`, `"JsonProperty"`, `"CompilerGenerated"`). Per Principle 5 (rich but not flooding): attribute names only, not parameters/values. If the agent wants attribute details, it can use `get_member_attributes`.
- **D-08:** The `get_type_members` output formatter adds an `[inherited]` tag after inherited members and shows `sealed override`, `override`, `virtual`, `abstract` modifiers inline. Example: `  public override string ToString()`  vs `  public virtual string ToString()  [inherited]`.
- **D-09:** The `IDecompilerService.GetTypeInfoAsync` implementation needs to be updated to populate the new fields. ICSharpCode.Decompiler's `ITypeDefinition` gives access to `Members` (declared) and base type members (via walking `DirectBaseTypes`). The inherited/declared distinction comes from comparing the member's `DeclaringTypeDefinition` against the target type.

### Pagination unit ordering

- **D-10:** `list_assembly_types` sorts types alphabetically by full name (namespace.type) before pagination slicing. This is deterministic and matches agent expectations for stable page boundaries.
- **D-11:** `get_type_members` orders members by category (constructors, methods, properties, fields, events) then alphabetically within each category. Declared members sort before inherited members within each category. This gives agents the most relevant members first.
- **D-12:** `search_members_by_name` sorts matches alphabetically by declaring type, then by member name. Stable ordering for pagination.
- **D-13:** `list_embedded_resources` sorts by resource name alphabetically.

### Claude's Discretion

- Exact `NamespaceTypeSummary`-equivalent models for `list_assembly_types` and `search_members_by_name` if needed, or reuse existing formatting with pagination appended
- Whether to extract a shared pagination validation helper in the Transport layer (all four tools do the same `maxResults > 500` / `maxResults <= 0` check) or keep it inline per tool
- Test fixture design for pagination tests (reuse `PaginationTestTargets` from Phase 9 or extend)
- Plan structure: whether to split pagination-only tools into one plan and member enrichment into another, or organize by wave

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Pagination contract
- `.claude/skills/mcp-tool-design/skill.md` -- Design principles 1-8, especially Principle 4 (pagination mandatory) and Principle 5 (rich but not flooding)
- `Application/Pagination/PaginationEnvelope.cs` -- Canonical footer emitter; all paginated tools use this

### Phase 10 reference implementations (pagination + enrichment pattern)
- `Transport/Mcp/Tools/FindUsagesTool.cs` -- Transport-layer pagination param validation pattern
- `Application/UseCases/FindUsagesUseCase.cs` -- Use-case-level pagination integration pattern

### Phase 9 reference implementation (list-tool pagination)
- `Application/UseCases/ListNamespaceTypesUseCase.cs` -- Closest analog for list_assembly_types pagination (list-type tool with namespace grouping)

### Domain models to extend
- `Domain/Models/TypeInfo.cs` -- `MethodInfo`, `PropertyInfo`, `FieldInfo`, `EventInfo` records need new fields for OUTPUT-05

### Target tool files
- `Transport/Mcp/Tools/ListAssemblyTypesTool.cs` -- PAGE-03
- `Transport/Mcp/Tools/ListEmbeddedResourcesTool.cs` -- PAGE-03
- `Transport/Mcp/Tools/GetTypeMembersTool.cs` -- PAGE-04, OUTPUT-05
- `Transport/Mcp/Tools/SearchMembersByNameTool.cs` -- PAGE-05

### Target use case files
- `Application/UseCases/ListAssemblyTypesUseCase.cs` -- PAGE-03
- `Application/UseCases/ListEmbeddedResourcesUseCase.cs` -- PAGE-03
- `Application/UseCases/GetTypeMembersUseCase.cs` -- PAGE-04, OUTPUT-05
- `Application/UseCases/SearchMembersByNameUseCase.cs` -- PAGE-05

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `PaginationEnvelope.AppendFooter(sb, total, returned, offset)` — canonical footer emitter, all paginated tools use this
- `SearchResults<T>` in `Domain/Models/SearchResult.cs` — generic paginated wrapper, but current tools return formatted `string` directly (SearchResults<T> is used internally by some services but not on the wire)
- `PaginationTestTargets` fixture from Phase 9 — test assembly with known type counts for pagination boundary testing
- Phase 10 transport-layer validation pattern: `maxResults > 500` -> INVALID_ARGUMENT, `maxResults <= 0` -> INVALID_ARGUMENT

### Established Patterns
- All paginated tools return `string` with `[pagination:{...}]` JSON footer appended via `PaginationEnvelope`
- Transport layer validates pagination params; use case accepts them without re-validating
- Use cases follow `_limiter.ExecuteAsync(async () => { ... })` concurrency pattern
- Domain models are `sealed record` types with `required` properties and `init`-only collections

### Integration Points
- `IDecompilerService.GetTypeInfoAsync` — returns `TypeInfo` which needs enrichment for OUTPUT-05 (inherited flag, sealed, override, attributes)
- `Infrastructure/Decompiler/DecompilerService.cs` — actual implementation that maps ICSharpCode.Decompiler's `ITypeDefinition` to `TypeInfo`
- `IAssemblyInspectionService` — used by `list_assembly_types` and `list_embedded_resources`
- DI registration in `Program.cs` — no new services needed, just updated use case signatures

</code_context>

<specifics>
## Specific Ideas

- PAGE-06 (`list_namespace_types` pagination) was pulled into Phase 9 per the roadmap ripple decision — Phase 11 scope is reduced to PAGE-03, PAGE-04, PAGE-05 only
- The `get_type_members` enrichment (OUTPUT-05) is the most complex item — it requires changes at all layers (Domain model, Infrastructure decompiler mapping, Application use case formatting, Transport tool params)
- Consider extracting a shared `PaginationValidator.Validate(maxResults, offset)` in Transport layer since all 10+ paginated tools repeat the same bounds check — but only if it simplifies, not if it adds abstraction for its own sake

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 11-list-get-search-pagination-member-enrichment*
*Context gathered: 2026-04-10*
