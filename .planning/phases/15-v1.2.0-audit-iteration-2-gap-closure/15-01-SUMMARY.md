---
phase: 15-v1.2.0-audit-iteration-2-gap-closure
plan: 01
subsystem: search
tags: [OUTPUT-06, search_strings, il-window, gap-closure]
requirements-completed: [OUTPUT-06]
dependency-graph:
  requires:
    - Infrastructure/Decompiler/ILParsingHelper (ReadILOpCode, IsTokenReferenceOpCode, SkipOperand, GetOperandSize)
    - Application/Pagination/PaginationEnvelope
  provides:
    - StringSearchResult.SurroundingIL (N=3 before/after IL window under each ldstr hit)
    - search_strings formatted output with "surrounding IL:" block + canonical pagination footer
  affects:
    - Transport/Mcp/Tools/SearchStringsTool description (promise is now honest)
tech-stack:
  added: []
  patterns:
    - Two-phase IL scan (accumulate rendered instructions, then slice window around matches)
    - Windowed context under search hits (mirrors disassembler-style IL rendering)
key-files:
  created:
    - Tests/Tools/SearchStringsToolTests.cs (new [Fact] EmitsSurroundingILWindow)
  modified:
    - Domain/Models/SearchResult.cs
    - Infrastructure/Decompiler/ILSpySearchService.cs
    - Application/UseCases/SearchStringsUseCase.cs
decisions:
  - OUTPUT-06 resolved end-to-end: domain field, infrastructure population, formatter emission, test proof
  - Rule 2 add: PaginationEnvelope.AppendFooter added to SearchStringsUseCase for contract parity with other search/find tools (previously missing)
  - RenderInstruction helper keeps ScanILForStrings readable and handles all operand shapes (ldstr, ldc.*, branches, token-refs, switch, default) via a single switch block
  - ldstr literals truncated to 64 chars + "..." in rendered window lines (T-15-01-03 DoS mitigation)
metrics:
  duration: ~10 min
  completed: 2026-04-12
---

# Phase 15 Plan 01: v1.2.0 Audit Iteration-2 OUTPUT-06 Gap Closure Summary

One-liner: Closed OUTPUT-06 by wiring a 3-before/3-after IL window through StringSearchResult into search_strings output, backed by a new formatter assertion test; also added the missing PaginationEnvelope footer for contract parity.

## What Shipped

- **StringSearchResult.SurroundingIL** — new `IReadOnlyList<string>` init-only property, defaults to `Array.Empty<string>()` so existing constructors (tests, future callers) compile unchanged.
- **Two-phase ScanILForStrings** — Phase 1 walks the method body once and accumulates `(offset, rendered, isLdstrHit, ldstrValue)` tuples. Phase 2 iterates hits, runs the regex, and for each match slices `[i-3 .. i+3]` clamped to body bounds into the window. totalCount/matchCap semantics preserved exactly.
- **RenderInstruction helper** — renders each IL instruction as `IL_XXXX: opcode [operand]`. Handles ldstr (with 64-char truncation), ldc.i4/ldc.i4.s/ldc.i8/ldc.r4/ldc.r8, short- and long-form branches (computes target offset), token-reference opcodes (`call/callvirt/newobj/ldfld/...` rendered as `token:0x{X8}`), switch, and fallback by operand size.
- **SearchStringsUseCase formatter** — under every match line, emits `surrounding IL:` followed by indented rendered IL lines. Appends `PaginationEnvelope.AppendFooter` as final emission.
- **EmitsSurroundingILWindow test** — asserts `Hello, World!` search output contains `surrounding IL:`, a `IL_XXXX:` line under it, and `IL_XXXX: ldstr` somewhere in the window.

## Commits

| Task | Commit | Message |
|------|--------|---------|
| 1 | `1db84d5` | feat(15-01): add SurroundingIL field to StringSearchResult |
| 2 | `369206d` | feat(15-01): populate SurroundingIL window in ldstr scan |
| 3 | `e3aa9e9` | feat(15-01): emit surrounding IL window in search_strings output |

## Verification

- `dotnet build ILSpy.Mcp.sln` — 0 errors, 2 warnings (pre-existing in TestTargets).
- `dotnet test --filter "FullyQualifiedName~SearchStrings"` — 8/8 pass (7 existing + new EmitsSurroundingILWindow).
- `dotnet test ILSpy.Mcp.sln` — 235/235 pass, full suite regression clean.
- Grep acceptance:
  - `IReadOnlyList<string> SurroundingIL` in `Domain/Models/SearchResult.cs` — 1 match (inside StringSearchResult, not ConstantSearchResult).
  - `SurroundingIL` in `Infrastructure/Decompiler/ILSpySearchService.cs` — 4 matches (constant, window bounds, assignment); 0 matches in ScanILForConstants block.
  - `Window size` comment with `N = 3` present.
  - `result.SurroundingIL` in `SearchStringsUseCase.cs` — 1 loop.
  - `surrounding IL:` in `SearchStringsUseCase.cs` — 1 header line.
  - `PaginationEnvelope.AppendFooter` in `SearchStringsUseCase.cs` — 1 call (newly added for parity).
  - `EmitsSurroundingILWindow` in `SearchStringsToolTests.cs` — 1 test.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing functionality] Added PaginationEnvelope.AppendFooter to SearchStringsUseCase**

- **Found during:** Task 3 (plan's verify script required `PaginationEnvelope.AppendFooter` to appear exactly once)
- **Issue:** Plan preamble claimed the footer was "preserved from Phase 14", but the current `SearchStringsUseCase.FormatResults` had no footer at all (unlike the 7 Find* / ConstantSearch use cases that all emit one). This means `search_strings` was silently non-compliant with the pagination contract documented in `docs/PAGINATION.md`.
- **Fix:** Added `PaginationEnvelope.AppendFooter(sb, results.TotalCount, results.Results.Count, results.Offset);` as the final formatter emission (after the match + window block, before `return sb.ToString()`).
- **Files modified:** `Application/UseCases/SearchStringsUseCase.cs` (added `using ILSpy.Mcp.Application.Pagination;` and the call)
- **Commit:** `e3aa9e9`
- **Justification:** Rule 2 (missing critical functionality for correctness) — the pagination contract is a cross-tool invariant; every other search/find tool enforces it. Fixing it here completes OUTPUT-06 with a consistent output shape rather than leaving a silent contract violation.

### Environment setup

Worktree required an explicit `dotnet restore` before first build (TestTargets/obj/project.assets.json absent in a fresh worktree). Routine setup step, not a plan deviation — noted for orchestrator awareness.

## Known Stubs

None. All three layers (Domain → Infrastructure → Application) are wired; the test proves the window round-trips to tool output against the real test assembly.

## Threat Flags

None. No new trust boundaries introduced — the IL scan already reads method bodies via MetadataReader/BlobReader; the window is a bounded read-only projection of the same bytes. T-15-01-03 (huge string DoS) mitigated via 64-char + "..." truncation in RenderInstruction.

## Success Criteria Status

| # | Criterion | Status |
|---|-----------|--------|
| 1 | `StringSearchResult` exposes SurroundingIL field | PASS (Domain/Models/SearchResult.cs:47) |
| 2 | `ILSpySearchService` populates window with N=3 before/after | PASS (Infrastructure/Decompiler/ILSpySearchService.cs:177,218-219,233) |
| 3 | Formatter emits window + test asserts it | PASS (SearchStringsUseCase.cs:109-117 + SearchStringsToolTests.cs:EmitsSurroundingILWindow) |
| 5 | `search_strings` description promise now honest | PASS (description unchanged; runtime now delivers the "surrounding IL context" it advertises) |

## Self-Check: PASSED

- FOUND: Domain/Models/SearchResult.cs (SurroundingIL field present at line 47)
- FOUND: Infrastructure/Decompiler/ILSpySearchService.cs (2-phase scan + RenderInstruction helper)
- FOUND: Application/UseCases/SearchStringsUseCase.cs (window emission + pagination footer)
- FOUND: Tests/Tools/SearchStringsToolTests.cs (EmitsSurroundingILWindow test)
- FOUND: 1db84d5, 369206d, e3aa9e9 in `git log --oneline -5`
- FOUND: .planning/phases/15-v1.2.0-audit-iteration-2-gap-closure/15-01-SUMMARY.md (this file)
