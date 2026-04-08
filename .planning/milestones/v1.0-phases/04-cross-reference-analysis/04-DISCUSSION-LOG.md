# Phase 4: Cross-Reference Analysis - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-08
**Phase:** 04-cross-reference-analysis
**Areas discussed:** Tool Design, Result Format, IL Scanner Service, TestTargets Design

---

## Tool Design

### How should the cross-reference tools be exposed?

| Option | Description | Selected |
|--------|-------------|----------|
| 4 separate tools | find_usages, find_implementors, find_dependencies, find_instantiations — each a dedicated MCP tool | |
| 2 combined tools | find_references + find_relationships, distinguished by mode param | |
| 1 unified tool | analyze_references with analysis_type parameter | |
| You decide | Claude picks best decomposition | ✓ |

**User's choice:** "You decide" with note: "4 separated tools AND 1 unified tool together"
**Notes:** User wants both — 4 dedicated tools for targeted use AND a unified analyze_references dispatcher for discovery.

### Unified tool behavior?

| Option | Description | Selected |
|--------|-------------|----------|
| Thin dispatcher | Routes to same use case as dedicated tool. No extra logic. | ✓ |
| Multi-analysis combiner | Accepts multiple analysis_type values, returns combined results | |

**User's choice:** Thin dispatcher
**Notes:** None

### find_usages member_name required or optional?

| Option | Description | Selected |
|--------|-------------|----------|
| Required | member_name always required for find_usages | ✓ |
| Optional | If omitted, returns all references to the type itself | |

**User's choice:** Required
**Notes:** None

---

## Result Format

### What detail level per hit?

| Option | Description | Selected |
|--------|-------------|----------|
| Method context | Containing type, method signature, IL offset, and opcode | ✓ |
| Minimal | Containing type and method name only | |
| Rich with IL snippet | Method context + surrounding IL instructions | |

**User's choice:** Method context (with preview approved)
**Notes:** User reviewed the preview format and confirmed.

### find_implementors detail level?

| Option | Description | Selected |
|--------|-------------|----------|
| Type names only | List of implementing/extending types with relationship | ✓ |
| Include member mappings | Shows which interface members each type implements | |

**User's choice:** Type names only
**Notes:** None

---

## IL Scanner Service

### Service structure?

| Option | Description | Selected |
|--------|-------------|----------|
| Single ICrossReferenceService | Dedicated domain interface with 4 xref methods. Shared IL helpers at infrastructure level. | ✓ |
| Low-level IILScannerService | Generic scanning interface with predicate/visitor pattern | |
| You decide | Claude picks architecture | |

**User's choice:** Single ICrossReferenceService
**Notes:** User reviewed the interface preview and approved.

### find_implementors location?

| Option | Description | Selected |
|--------|-------------|----------|
| Keep on ICrossReferenceService | All 4 xref methods on one interface, conceptually unified | ✓ |
| Split to IDecompilerService | Type-system work goes on existing service | |

**User's choice:** Keep on ICrossReferenceService
**Notes:** None

---

## TestTargets Design

### Test infrastructure scope?

| Option | Description | Selected |
|--------|-------------|----------|
| Focused xref types | ~5-8 new purpose-built types with traceable relationships | ✓ |
| Reuse existing types | Maximize reuse, add minimal new types | |
| You decide | Claude designs the test graph | |

**User's choice:** Focused xref types
**Notes:** None

---

## Claude's Discretion

- Exact domain model result types (field names, shapes)
- Infrastructure service internal organization
- Specific TestTargets type names and relationship graph
- analyze_references dispatcher implementation pattern
- Error handling edge cases
- Whether find_dependencies member_name is optional

## Deferred Ideas

None — discussion stayed within phase scope.
