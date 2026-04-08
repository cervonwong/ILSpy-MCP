# Phase 5: Assembly Inspection - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-08
**Phase:** 05-assembly-inspection
**Areas discussed:** Tool granularity, Metadata depth, Resource extraction, Compiler-generated types

---

## Tool Granularity (Overall Design)

User provided free-text guidance before area selection:

**User's choice:** "Prioritise hierarchical discoverability and clear tool names with broad functionalities"
**Notes:** This shaped all subsequent decisions — fewer broad tools over many narrow ones.

---

## Assembly Metadata

| Option | Description | Selected |
|--------|-------------|----------|
| One unified tool | Single `get_assembly_metadata` returns all PE header info plus assembly references in one call | ✓ |
| Two tools: info + refs | Separate `get_assembly_metadata` for PE/framework info and `list_assembly_references` for dependency list | |

**User's choice:** One unified tool (Recommended)
**Notes:** AI assistants get the full picture in one shot. Keeps tool count low.

---

## Custom Attributes

| Option | Description | Selected |
|--------|-------------|----------|
| One tool with scope param | `get_custom_attributes` with `scope` parameter for assembly/type/member levels | |
| Separate per level | `get_assembly_attributes`, `get_type_attributes`, `get_member_attributes` — three distinct tools | ✓ |

**User's choice:** Separate per level
**Notes:** Clearer tool names per level despite more tools. Consistent with hierarchical discoverability principle.

### Attribute Inheritance

| Option | Description | Selected |
|--------|-------------|----------|
| Declared only | Only show attributes directly on the target | ✓ |
| Include inherited | Show all attributes including from base types | |

**User's choice:** Declared only (Recommended)
**Notes:** Simpler, predictable. Users trace inheritance via get_base_types if needed.

---

## Embedded Resources

| Option | Description | Selected |
|--------|-------------|----------|
| One tool with optional filter | `get_embedded_resources` — no filter returns list, with filter returns content | |
| Two tools: list + extract | `list_embedded_resources` for catalog, `extract_resource` for content | ✓ |

**User's choice:** Two tools: list + extract
**Notes:** Explicit separation of listing vs reading.

### Binary Resource Size Handling

| Option | Description | Selected |
|--------|-------------|----------|
| Base64 with size cap | Return base64 up to configurable limit, larger resources return metadata only | |
| Always return full base64 | No size limit — always return full content | |
| Truncate with offset/limit | Support offset/limit parameters for paginated binary extraction | ✓ |

**User's choice:** Truncate with offset/limit
**Notes:** More complex but handles any size resource.

---

## Compiler-Generated Types

| Option | Description | Selected |
|--------|-------------|----------|
| Extend list_assembly_types | Add `include_nested` and `include_compiler_generated` boolean flags to existing tool | |
| New dedicated tool | `find_compiler_generated_types` as a separate tool | ✓ |

**User's choice:** New dedicated tool
**Notes:** Dedicated to showing DisplayClass, async state machines, closures with their parent types.

### Parent Context Display

| Option | Description | Selected |
|--------|-------------|----------|
| Show parent context | Each compiler-generated type shows its originating method/type when detectable | ✓ |
| List with attribute only | Just list types with [CompilerGenerated] attribute | |

**User's choice:** Show parent context (Recommended)
**Notes:** More useful for reverse engineering than a flat list.

---

## Final Tool Set Review

| Option | Description | Selected |
|--------|-------------|----------|
| Looks good | 7 tools: get_assembly_metadata, get_assembly_attributes, get_type_attributes, get_member_attributes, list_embedded_resources, extract_resource, find_compiler_generated_types | ✓ |
| Too many tools | Want to consolidate further | |
| Missing something | Expected additional capability | |

**User's choice:** Looks good
**Notes:** None

---

## Claude's Discretion

- Domain model types for results
- Infrastructure service internals
- TestTargets additions
- Parent method detection heuristics for compiler-generated types
- Offset/limit semantics for extract_resource
- Attribute argument formatting

## Deferred Ideas

None — discussion stayed within phase scope.
