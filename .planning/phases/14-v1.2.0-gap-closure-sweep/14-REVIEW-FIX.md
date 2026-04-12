---
phase: 14-v1.2.0-gap-closure-sweep
fixed_at: 2026-04-12T00:00:00Z
review_path: .planning/phases/14-v1.2.0-gap-closure-sweep/14-REVIEW.md
iteration: 1
findings_in_scope: 3
fixed: 3
skipped: 0
status: all_fixed
---

# Phase 14: Code Review Fix Report

**Fixed at:** 2026-04-12
**Source review:** `.planning/phases/14-v1.2.0-gap-closure-sweep/14-REVIEW.md`
**Iteration:** 1

**Summary:**
- Findings in scope: 3 (critical + warning; 6 info findings deferred per fix_scope)
- Fixed: 3
- Skipped: 0

## Fixed Issues

### WR-01: Multi-overload method disassembly silently discards overload info and misreports as not-found

**Files modified:** `Domain/Errors/MethodNotFoundException.cs`, `Infrastructure/Decompiler/ILSpyDisassemblyService.cs`
**Commit:** `db83707`
**Applied fix:** Added a new `MethodNotFoundException(methodName, typeName, message)` overload that lets callers pass a custom diagnostic message while preserving the `METHOD_NOT_FOUND` error code and `MethodName`/`TypeName` properties. `ILSpyDisassemblyService.DisassembleMethodAsync` now threads the previously-dead `overloads` signature list into the thrown exception's message (e.g. "Multiple overloads found for 'Foo' in type 'N.T': Foo(Int32 x), Foo(String s). The current tool does not accept a parameter-signature selector."). Took the minimal "at-minimum" option from the review guidance rather than introducing a new `AmbiguousMethodException` + `AMBIGUOUS_METHOD` error code, which would require transport-layer handler changes in every `MethodNotFoundException` catch site; that broader refactor is noted as a "Consider" item in the review and can be pursued separately.

### WR-02: String truncation by code-unit count can split UTF-16 surrogate pairs

**Files modified:** `Application/Pagination/SafeTruncate.cs` (new), `Application/UseCases/DecompileMethodUseCase.cs`, `Application/UseCases/DecompileTypeUseCase.cs`, `Application/UseCases/DisassembleMethodUseCase.cs`, `Application/UseCases/DisassembleTypeUseCase.cs`, `Application/UseCases/AnalyzeAssemblyUseCase.cs`
**Commit:** `c2f067e`
**Applied fix:** Added a new `SafeTruncate.Chars(string, int)` helper in `ILSpy.Mcp.Application.Pagination` (same namespace as `PaginationEnvelope`, which every affected file already imports). The helper backs the cut point off by one char if the candidate cut would land between a high and low surrogate. Replaced the naive `raw[..maxBytes]` / `sourceCode[..maxBytes]` / `methodCode[..maxBytes]` slices in all five bounded-output use cases with `SafeTruncate.Chars(..., maxBytes)`. Did NOT rename `MaxDecompilationSize` or the footer's `total`/`returned` fields from "bytes" to "chars", nor switch to UTF-8 byte counting -- those are naming/semantic contract changes that would cascade into `docs/PAGINATION.md` and every integration test's footer expectations, and should be sequenced as a dedicated change. The surrogate-splitting correctness bug (the review's explicit point (b)) is fixed; the naming-mismatch point (a) remains as-is.

### WR-03: `ExportProjectUseCase` creates output directory as a side effect of validation

**Files modified:** `Application/UseCases/ExportProjectUseCase.cs`
**Commit:** `f344090`
**Applied fix:** Split the single `Directory.CreateDirectory(outputDirectory)` + emptiness-check pair into a branch on `Directory.Exists`: if the directory already exists, enumerate entries and throw `OutputDirectoryNotEmptyException` when non-empty; otherwise (directory does not exist) create it. A non-empty existing path now fails validation WITHOUT materializing the path on disk, eliminating the stray-empty-directory side effect for mistyped paths. Updated the adjacent comment to describe the new contract. Both branches remain TOCTOU-tolerant.

## Skipped Issues

None.

---

_Fixed: 2026-04-12_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
