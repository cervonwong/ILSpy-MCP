# Phase 6: Search & Cross-Assembly - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-08
**Phase:** 06-search-cross-assembly
**Areas discussed:** Tool design, Search result format, Cross-assembly semantics

---

## Tool Design

### Tool Organization

| Option | Description | Selected |
|--------|-------------|----------|
| 4 dedicated tools (Recommended) | search_strings, search_constants, resolve_type, load_assembly_directory — one per requirement. Consistent with 1-tool-per-concern pattern. | ✓ |
| 4 dedicated + 1 dispatcher | Same 4 tools plus a unified search_assembly dispatcher (like analyze_references in Phase 4). | |
| 2 tools + 2 tools | search_assembly (strings + constants via search_type param) + resolve_type + load_assembly_directory. | |

**User's choice:** 4 dedicated tools
**Notes:** None

### search_constants Value Matching

| Option | Description | Selected |
|--------|-------------|----------|
| Exact value match (Recommended) | Takes a numeric value and finds all ldc.* instructions loading that value. | ✓ |
| Range + exact match | Support both exact match and range queries. | |
| You decide | Claude picks the approach. | |

**User's choice:** Exact value match (defaulted after clarification)
**Notes:** User initially didn't understand the distinction. Clarified with examples — exact match covers the primary use case.

### Search Service Interface

| Option | Description | Selected |
|--------|-------------|----------|
| New ISearchService (Recommended) | Separate domain interface for search operations. Follows Phase 3 pattern. | ✓ |
| Extend ICrossReferenceService | Add SearchStrings and SearchConstants to existing cross-ref interface. | |
| You decide | Claude picks based on codebase fit. | |

**User's choice:** New ISearchService
**Notes:** None

### Cross-Assembly Service Interface

| Option | Description | Selected |
|--------|-------------|----------|
| New ICrossAssemblyService (Recommended) | Separate domain interface for cross-assembly operations. | ✓ |
| Add to IDecompilerService | Directory loading and type resolution as assembly-level operations on existing interface. | |
| You decide | Claude picks based on codebase fit. | |

**User's choice:** New ICrossAssemblyService
**Notes:** None

---

## Search Result Format

### Context Per Hit

| Option | Description | Selected |
|--------|-------------|----------|
| Method context (Recommended) | Matched value, containing type full name, containing method signature, IL offset. Matches Phase 4 style. | ✓ |
| Method + code snippet | Same plus decompiled code snippet around usage. Richer but slower. | |
| Minimal (method only) | Just matched string and containing method name. Lightweight. | |

**User's choice:** Method context
**Notes:** None

### Result Limit Handling

| Option | Description | Selected |
|--------|-------------|----------|
| Default limit with param (Recommended) | Max 100 results by default, optional max_results parameter. Shows total count. | |
| No limit | Return all matches. | |
| You decide | Claude picks a sensible default. | |

**User's choice:** Default limit with offset parameter
**Notes:** User specified they want an offset parameter for pagination in addition to the limit.

---

## Cross-Assembly Semantics

### Directory Scanning Depth

| Option | Description | Selected |
|--------|-------------|----------|
| Top-level only (Recommended) | Only scan files directly in the specified directory. | |
| Recursive with depth limit | Scan subdirectories up to a configurable depth. | ✓ |
| Recursive, no limit | Scan all subdirectories. | |

**User's choice:** Recursive with depth limit
**Notes:** None

### Unloadable Assembly Handling

| Option | Description | Selected |
|--------|-------------|----------|
| Skip with warning (Recommended) | Log warning, continue loading rest. Return skipped files list. | ✓ |
| Fail fast | Error if any assembly can't be loaded. | |
| Silent skip | Quietly skip, no mention in output. | |

**User's choice:** Skip with warning
**Notes:** None

### Ambiguous Type Resolution

| Option | Description | Selected |
|--------|-------------|----------|
| Return all matches (Recommended) | List every assembly defining the type. Let AI assistant pick. | ✓ |
| Return first match | Return first assembly found. | |
| Error on ambiguity | Error asking user to narrow search. | |

**User's choice:** Return all matches
**Notes:** None

### Type Name Matching

| Option | Description | Selected |
|--------|-------------|----------|
| Partial match (Recommended) | Match by simple name or partial namespace. More discoverable. | ✓ |
| Full name only | Require namespace-qualified name. Precise. | |
| Both via parameter | Default partial, optional exact_match param. | |

**User's choice:** Partial match
**Notes:** None

---

## Claude's Discretion

- Exact domain model types for results
- Default result limit value and default directory scan depth
- Whether to extract shared IL scanning helpers
- Infrastructure service internals
- TestTargets extensions
- Error handling edge cases

## Deferred Ideas

None — discussion stayed within phase scope.
