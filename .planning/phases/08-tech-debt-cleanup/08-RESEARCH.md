# Phase 8: Tech Debt Cleanup - Research

**Researched:** 2026-04-09
**Domain:** Remediation — four discrete, locked tech-debt items on the v1.0 baseline (error code normalization, Application→Transport layer violation, SUMMARY.md frontmatter backfill, Phase 7 runtime verification)
**Confidence:** HIGH

## Summary

All four remediation items are well-scoped by `08-CONTEXT.md`, which locks the implementation decisions with exact file paths and patterns. The role of this research is to verify the locked decisions against the current repo state, surface two factual corrections that will save the planner from shipping broken plans, and enrich each decision with the exact code snippets, test assertions, and frontmatter shapes needed to write task-level plans.

Three of the four items are pure code/doc mechanics (DEBT-01, DEBT-02, DEBT-03). DEBT-04 is a verification item that re-runs the full test suite to produce an evidence artifact for Phase 7 SUMMARY files. The `dotnet` CLI (10.0.201) is confirmed available in this environment, which unblocks DEBT-04.

**Primary recommendation:** Execute in the order DEBT-02 → DEBT-01 → DEBT-03 → DEBT-04. DEBT-02 has the largest diff and the highest regression risk (new domain class, use case rewire, transport rewire) so it goes first and benefits most from the final `dotnet test` run. DEBT-01 is a one-line code change plus optional test assertion. DEBT-03 is doc-only (safe to land any time). DEBT-04 runs last because it is the validator for everything that preceded it.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**DEBT-01 — Error code normalization**
- **Target code: `MEMBER_NOT_FOUND`** for `FindDependenciesTool`. Cross-reference tools operate on "members" (methods, fields, properties) as a category; sibling tools `FindUsagesTool` and `GetMemberAttributesTool` already use `MEMBER_NOT_FOUND`. Aligning `FindDependenciesTool` closes the audit finding with the smallest diff and the most semantically correct code.
- **Scope is narrow on purpose.** Do NOT touch `DecompileMethodTool` / `DisassembleMethodTool` — those are method-specific operations where `METHOD_NOT_FOUND` is correct and agent-meaningful. Do NOT touch `AnalyzeReferencesTool` — it is deleted in Phase 9 (CLEAN-01), so any change is wasted work.
- **Domain exception stays.** `MethodNotFoundException` (Domain/Errors/) is kept unchanged — it is a semantic domain concept. Only the wire error code string in the Transport-layer catch handler changes.
- **Test update required.** `FindDependenciesToolTests` has at least one assertion expecting `METHOD_NOT_FOUND`. Update it to expect `MEMBER_NOT_FOUND`. This is a load-bearing part of the change — without it DEBT-04 (runtime verification) fails. *(NOTE: Research finding — this statement is factually wrong; see Specific Findings §DEBT-01 below. Planner should treat this as "add a test assertion for the member-not-found path", not "update an existing one".)*

**DEBT-02 — Domain exception for output directory**
- **Create `OutputDirectoryNotEmptyException : DomainException`** in `Domain/Errors/` following the `NamespaceNotFoundException` pattern exactly: single sealed class, base-class-carried error code string, structured properties (`OutputDirectory`), message explaining why and what to do. Error code: `DIRECTORY_NOT_EMPTY` (unchanged from current wire contract).
- **Rewire `ExportProjectUseCase`:**
  - Remove `using ILSpy.Mcp.Transport.Mcp.Errors;` (this is the architecture violation).
  - Throw `OutputDirectoryNotEmptyException` instead of `McpToolException("DIRECTORY_NOT_EMPTY", ...)`.
  - Remove the `catch (McpToolException) { throw; }` handler — it becomes dead once the transport exception is gone.
- **Rewire `ExportProjectTool`:**
  - Add a `catch (OutputDirectoryNotEmptyException ex)` clause that maps to `McpToolException("DIRECTORY_NOT_EMPTY", ErrorSanitizer.SanitizePath(ex.Message))`.
  - Remove the dead `catch (McpToolException) { throw; }` clause.
- **ArgumentException at line 46 is OUT OF SCOPE.** The whitespace-check `ArgumentException` is a BCL exception, not a Transport-layer type, so it does not violate the layer boundary. Normalizing it to a value object (e.g. `OutputDirectoryPath.Create`) is scope creep — leave it for a future cleanup if it ever matters.
- **Tests:** `ExportProjectToolTests` asserts on the `DIRECTORY_NOT_EMPTY` error code via `McpToolException`. Re-run after the change and confirm the wire contract is preserved — the error code that reaches agents must be identical.

**DEBT-03 — Frontmatter backfill**
- **Field name: `requirements-completed:` (hyphenated).** Matches Phase 7 precedent. Precedent beats documentation wording — the actual frontmatter in the repo is what tools read. If the REQUIREMENTS.md doc wording becomes wrong, that is a documentation fix for a later pass.
- **Scope is minimal: only add the missing field.** Do NOT normalize frontmatter shapes. Phase 1's nested `dependency_graph:` vs Phase 4+'s flat `requires:`/`provides:` is a cosmetic inconsistency the audit did NOT flag; touching it is churn that dilutes the fix and expands the diff for no benefit.
- **Per-plan granularity.** Each SUMMARY.md file gets its own `requirements-completed:` array listing only the requirements that specific plan satisfied. Mapping source (in priority order): (1) the plan's `Accomplishments` / `Task Commits` sections if explicit, (2) the phase's own ROADMAP.md requirements slot cross-referenced against each plan's scope, (3) the v1.0 milestone audit's requirement-to-phase mapping as a sanity check.
- **Files to touch (14 SUMMARY.md files in `.planning/milestones/v1.0-phases/`):** 01-01, 01-02, 02-01, 02-02, 02-03, 03-01, 03-02, 04-01, 04-02, 05-01, 05-02, 06-01, 06-02. Phase 7's three SUMMARY files already have the field — skip them. *(NOTE: Research finding — CONTEXT.md's file count is wrong and 7 of the 13 listed files ALREADY have the field. See Specific Findings §DEBT-03 for the correct list of 6 files that genuinely need backfill.)*
- **Ambiguity policy:** If a plan's exact requirement mapping is unclear, list the phase-level requirements on the *first* plan and an empty array on subsequent plans, and note this as a deviation in the execution summary.

**DEBT-04 — Runtime verification of Phase 7 tests**
- **`dotnet` is available in this environment** (`dotnet --version` → `10.0.201`). The blocker that prevented runtime verification during Phase 7 execution is gone.
- **Verification command:** Run the full test suite (`dotnet test`), not just Phase 7 filters. Reason: DEBT-01 and DEBT-02 also touch tests in the same run, and a full green bar is the fastest way to prove that (a) Phase 7 tests actually pass at runtime, (b) the DEBT-01/02 changes did not regress anything else. A filtered Phase 7 run is a strict subset and gives weaker evidence.
- **Evidence artifact:** Capture the `dotnet test` final summary (total/passed/failed counts) and record it in each Phase 7 SUMMARY.md file as a `## Runtime Verification` section appended to the existing `## Self-Check: PASSED` block, with date, command, and the pass/fail count. This closes the loop on the "tests verified by code inspection only" note that currently sits in those summaries.
- **Failure policy:** If Phase 7 tool tests (`DecompileNamespaceToolTests`, `ExportProjectToolTests`, any phase 7 plan 3 tests) fail at runtime, the failures are in-scope for this phase — fix them here. Reason: the whole purpose of DEBT-04 is to prove the baseline is green. Punting a failure defeats the purpose. Non-Phase-7 failures surfaced by the full-suite run are also in-scope only if they are caused by DEBT-01 or DEBT-02 changes (which is expected and must be fixed); unrelated pre-existing failures get deferred as a new tech-debt item and noted in the phase summary.
- **Ordering:** DEBT-04 runs LAST in the phase so it also validates the DEBT-01/02/03 changes. Running it first would only verify the pre-Phase-8 baseline, which is less useful.

### Claude's Discretion

- Exact file layout for the new `OutputDirectoryNotEmptyException` (constructor signature, property names) — follow `NamespaceNotFoundException` shape
- Which of the Phase 7 SUMMARY files need the `Runtime Verification` block (probably all three, but only the ones whose tests actually ran)
- Exact sub-ordering of the four DEBT items within the phase plan structure (e.g., DEBT-02 and DEBT-01 can share a single plan since both touch the same Transport/Application boundary; DEBT-03 is a doc-only plan; DEBT-04 is the final validation plan)
- Whether to combine DEBT-01 + DEBT-02 into one plan or keep them separate (small, targeted plans match v1.0 velocity; recommendation is combine since both touch `Transport/Mcp/Tools/Find*Tool.cs`-adjacent areas and both need the same `dotnet test` pass)

### Deferred Ideas (OUT OF SCOPE)

- Full normalization of Phase 1-6 SUMMARY.md frontmatter shapes (nested `dependency_graph:` vs flat `requires:`) — cosmetic, not flagged by audit, defer
- Wider METHOD_NOT_FOUND vs MEMBER_NOT_FOUND audit across all 6 tools that use either code — audit only flagged one, fix just the one
- `OutputDirectoryPath` value object for directory validation (mirroring `AssemblyPath.Create`) — would also encapsulate the whitespace `ArgumentException` case cleanly, but not required to close the layer violation and expands scope
- REQUIREMENTS.md doc wording fix (`requirements_completed` underscored in docs vs `requirements-completed` hyphenated in actual frontmatter) — doc-only fix, not code, defer
- Backfilling VALIDATION.md Nyquist compliance for Phases 1-7 (flagged in the v1.0 audit as "overall: MISSING") — that is a separate validation effort, not part of the tech debt cleanup scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| DEBT-01 | `FindDependenciesTool` error codes normalized (resolve `METHOD_NOT_FOUND` vs `MEMBER_NOT_FOUND` inconsistency) | Exact current state captured: `Transport/Mcp/Tools/FindDependenciesTool.cs:47` throws `McpToolException("METHOD_NOT_FOUND", ...)` in the `MethodNotFoundException` catch. Sibling `FindUsagesTool.cs:47` and `GetMemberAttributesTool.cs:47` use `"MEMBER_NOT_FOUND"` for the identical catch structure. One-line change. The domain exception `MethodNotFoundException` (Domain/Errors/) keeps its own `METHOD_NOT_FOUND` base code — the tool's catch clause is the only site that needs to change. See §DEBT-01 Exact Current State. |
| DEBT-02 | `ExportProjectUseCase` no longer imports `McpToolException` from Transport layer (fix Application→Transport layer violation) | Exact current state captured: the import is at `Application/UseCases/ExportProjectUseCase.cs:8`; the violation site is the `throw new McpToolException("DIRECTORY_NOT_EMPTY", ...)` at line 60; the dead catch is at lines 141-144. The pattern for the new domain exception is `Domain/Errors/NamespaceNotFoundException.cs` (sealed class, `DomainException` base, one structured property, descriptive message). The transport-layer catch site is `Transport/Mcp/Tools/ExportProjectTool.cs:41-44`. The wire contract (`DIRECTORY_NOT_EMPTY`) is preserved and is asserted by `ExportProjectToolTests.cs:95`. See §DEBT-02 Exact Current State. |
| DEBT-03 | v1.0 Phase 1-6 SUMMARY.md frontmatter gaps filled (add missing `requirements-completed` fields) | File-by-file ground truth captured: **only 6 files actually lack the `requirements-completed` key** (not 13 as CONTEXT.md suggests). The authoritative list, mapped to requirements, is in §DEBT-03 Per-Plan Mapping. Existing Phase 3/4/5/6-02/7 SUMMARY files already have the key and are the shape-of-truth reference. Phase 2 SUMMARY files 02-02 and 02-03 use a differently-named `requirements:` key which is the closest "near-miss" case. |
| DEBT-04 | Phase 7 tool tests runtime-verified (not just code inspection) | `dotnet --version` returns `10.0.201` — the CLI is available. The test runner is xUnit 2.9.x with a shared `ToolTestFixture`. Phase 7 test files are `Tests/Tools/DecompileNamespaceToolTests.cs` (6 tests) and `Tests/Tools/ExportProjectToolTests.cs` (5 tests). Phase 7 plan 3 is docs-only (README.md) and has no tests. Full suite runs via `dotnet test ILSpy.Mcp.sln` from repo root. See §DEBT-04 Runtime Verification Recipe. |
</phase_requirements>

## Specific Findings — Exact Current State (Code Ground Truth)

### DEBT-01 — FindDependenciesTool error code override

**File:** `Transport/Mcp/Tools/FindDependenciesTool.cs`

**Exact current catch block (lines 44-48):**
```csharp
catch (MethodNotFoundException ex)
{
    _logger.LogWarning("Method not found: {MethodName} in {TypeName}", ex.MethodName, ex.TypeName);
    throw new McpToolException("METHOD_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
}
```

**Target state after DEBT-01:**
```csharp
catch (MethodNotFoundException ex)
{
    _logger.LogWarning("Member not found: {MemberName} in {TypeName}", ex.MethodName, ex.TypeName);
    throw new McpToolException("MEMBER_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
}
```

Two changes: the wire code string `"METHOD_NOT_FOUND"` → `"MEMBER_NOT_FOUND"` (required), and the log template wording `"Method not found"` → `"Member not found"` (optional but matches the sibling tools for consistency).

**Sibling reference (the precedent — `Transport/Mcp/Tools/FindUsagesTool.cs:44-48`):**
```csharp
catch (MethodNotFoundException ex)
{
    _logger.LogWarning("Member not found: {MemberName} in {TypeName}", ex.MethodName, ex.TypeName);
    throw new McpToolException("MEMBER_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
}
```

**Second sibling reference (`Transport/Mcp/Tools/GetMemberAttributesTool.cs:44-48`)** is identical to `FindUsagesTool.cs` for this catch block.

**Domain exception is unchanged — `Domain/Errors/MethodNotFoundException.cs`:**
```csharp
public sealed class MethodNotFoundException : DomainException
{
    public string MethodName { get; }
    public string TypeName { get; }

    public MethodNotFoundException(string methodName, string typeName)
        : base("METHOD_NOT_FOUND", $"Method '{methodName}' not found in type '{typeName}'")
    {
        MethodName = methodName;
        TypeName = typeName;
    }
}
```
The exception's own base-class error code `METHOD_NOT_FOUND` is IGNORED by the transport catch — every tool explicitly passes its chosen wire code to `McpToolException`. This is the established convention (confirmed across `DecompileNamespaceTool.cs:42` which similarly hardcodes `"NAMESPACE_NOT_FOUND"` rather than reading `ex.ErrorCode`). Do not "fix" this by making Transport read `ex.ErrorCode`; that would change behavior across 6+ tools.

**IMPORTANT CORRECTION to CONTEXT.md:**

CONTEXT.md claims:
> "**Test update required.** `FindDependenciesToolTests` has at least one assertion expecting `METHOD_NOT_FOUND`. Update it to expect `MEMBER_NOT_FOUND`."

This is **factually wrong**. The actual current contents of `Tests/Tools/FindDependenciesToolTests.cs` (verified 2026-04-09):

```csharp
[Collection("ToolTests")]
public class FindDependenciesToolTests
{
    // ... 3 tests:
    //  1. FindDependencies_SpecificMethod_ReturnsDeps — positive path, asserts on output strings
    //  2. FindDependencies_TypeLevel_ReturnsAllMethodDeps — positive path, asserts on output strings
    //  3. FindDependencies_NonExistentType_ThrowsTypeNotFound — asserts TYPE_NOT_FOUND only
}
```

There is **no existing assertion on `METHOD_NOT_FOUND`** in this test file. The non-existent-member path is currently not covered by a test at all.

**Planner guidance:** Treat the DEBT-01 test work as "add a new test `FindDependencies_NonExistentMethod_ThrowsMemberNotFound`" that asserts `MEMBER_NOT_FOUND` after the fix. This is a small net-add, not an update. It gives DEBT-04 something meaningful to verify at runtime for this path — otherwise the wire-code change has no direct test coverage.

**Confidence:** HIGH — verified directly from repo files.

---

### DEBT-02 — Application→Transport layer violation

**Violation site — `Application/UseCases/ExportProjectUseCase.cs`:**

Line 8 contains the forbidden using directive:
```csharp
using ILSpy.Mcp.Transport.Mcp.Errors;   // ← REMOVE (layer violation)
```

Line 60 is the only consumer of that namespace inside the use case:
```csharp
if (Directory.EnumerateFileSystemEntries(outputDirectory).Any())
{
    throw new McpToolException("DIRECTORY_NOT_EMPTY",
        $"Output directory is not empty: {outputDirectory}. Specify an empty or non-existent directory.");
}
```

Lines 141-144 are the dead `McpToolException` passthrough catch:
```csharp
catch (McpToolException)
{
    throw;
}
```

The `ExportProjectUseCase` also has a `catch (DomainException) { throw; }` block at lines 156-159, which is the correct pattern and which the new `OutputDirectoryNotEmptyException` (as a `DomainException` subclass) will flow through unchanged.

**Target state — delete line 8, replace lines 58-62 with:**
```csharp
if (Directory.EnumerateFileSystemEntries(outputDirectory).Any())
{
    throw new OutputDirectoryNotEmptyException(outputDirectory);
}
```

**Delete lines 141-144** (the `catch (McpToolException) { throw; }` handler). After this, the `catch (DomainException) { throw; }` handler at line 156-159 correctly passes the new exception through to the tool layer.

**Pattern to copy — `Domain/Errors/NamespaceNotFoundException.cs` (entire file, verified):**
```csharp
namespace ILSpy.Mcp.Domain.Errors;

public sealed class NamespaceNotFoundException : DomainException
{
    public string NamespaceName { get; }
    public string AssemblyPath { get; }

    public NamespaceNotFoundException(string namespaceName, string assemblyPath)
        : base("NAMESPACE_NOT_FOUND", $"Namespace '{namespaceName}' not found in assembly '{assemblyPath}'. Use list_namespaces to see available namespaces.")
    {
        NamespaceName = namespaceName;
        AssemblyPath = assemblyPath;
    }
}
```

**Target new file — `Domain/Errors/OutputDirectoryNotEmptyException.cs`:**
```csharp
namespace ILSpy.Mcp.Domain.Errors;

public sealed class OutputDirectoryNotEmptyException : DomainException
{
    public string OutputDirectory { get; }

    public OutputDirectoryNotEmptyException(string outputDirectory)
        : base("DIRECTORY_NOT_EMPTY", $"Output directory is not empty: {outputDirectory}. Specify an empty or non-existent directory.")
    {
        OutputDirectory = outputDirectory;
    }
}
```

Note: a single structured property (`OutputDirectory`) — the `NamespaceNotFoundException` has two because namespaces are scoped to an assembly; output directories are not. The error code `DIRECTORY_NOT_EMPTY` is verbatim from the current wire contract (preserves the client-side assertion at `ExportProjectToolTests.cs:95`).

**Transport rewire — `Transport/Mcp/Tools/ExportProjectTool.cs`:**

Current catch structure (lines 36-66), abridged:
```csharp
try
{
    return await _useCase.ExecuteAsync(...);
}
catch (McpToolException)           // ← REMOVE this passthrough
{
    throw;
}
catch (AssemblyLoadException ex)   // keep
{ ... }
catch (TimeoutException ex)        // keep
{ ... }
catch (OperationCanceledException) // keep
{ ... }
catch (Exception ex)               // keep
{ ... }
```

**Target state — replace the `catch (McpToolException)` block with a specific `OutputDirectoryNotEmptyException` catch, placed first (before `AssemblyLoadException`, matching the specific-to-general ordering used by every other tool):**
```csharp
catch (OutputDirectoryNotEmptyException ex)
{
    _logger.LogWarning("Output directory not empty: {Directory}", ex.OutputDirectory);
    throw new McpToolException("DIRECTORY_NOT_EMPTY", ErrorSanitizer.SanitizePath(ex.Message));
}
```

This preserves the wire contract (`DIRECTORY_NOT_EMPTY` error code, sanitized message) and matches the specific-catch-first ordering used by `FindDependenciesTool.cs`, `FindUsagesTool.cs`, `DecompileNamespaceTool.cs`, etc.

**Existing test that pins the wire contract — `Tests/Tools/ExportProjectToolTests.cs:89-97`:**
```csharp
var act = () => tool.ExecuteAsync(
    _fixture.TestAssemblyPath,
    tempDir,
    cancellationToken: CancellationToken.None);

var ex = await act.Should().ThrowAsync<McpToolException>();
ex.Which.ErrorCode.Should().Be("DIRECTORY_NOT_EMPTY");
```

This test must continue to pass after DEBT-02 unchanged. It is the load-bearing guarantee that the refactor is behaviorally invisible to MCP clients.

**Confidence:** HIGH — all line numbers and code verified directly from repo.

---

### DEBT-03 — Frontmatter backfill: actual file-level ground truth

**IMPORTANT CORRECTION to CONTEXT.md:**

CONTEXT.md says "**Files to touch (14 SUMMARY.md files in `.planning/milestones/v1.0-phases/`):** 01-01, 01-02, 02-01, 02-02, 02-03, 03-01, 03-02, 04-01, 04-02, 05-01, 05-02, 06-01, 06-02." — but (a) that's 13 files listed while the header says 14, and (b) **7 of those 13 files already have the `requirements-completed:` key**. A `grep -l "requirements-completed" .planning/milestones/v1.0-phases/**/SUMMARY.md` on 2026-04-09 returns the following files that ALREADY have the key:

| File | Current value |
|------|---------------|
| `03-il-infrastructure-disassembly/03-01-SUMMARY.md` | `[IL-01, IL-02]` |
| `03-il-infrastructure-disassembly/03-02-SUMMARY.md` | `[IL-01, IL-02, TEST-03]` |
| `04-cross-reference-analysis/04-01-SUMMARY.md` | `[XREF-01, XREF-02, XREF-03, XREF-04]` |
| `04-cross-reference-analysis/04-02-SUMMARY.md` | `[XREF-01, XREF-02, XREF-03, XREF-04, TEST-02]` |
| `05-assembly-inspection/05-01-SUMMARY.md` | `[META-01, META-02, META-03, META-04, RES-01, RES-02, TYPE-01, TYPE-02]` |
| `05-assembly-inspection/05-02-SUMMARY.md` | `[META-01, META-02, META-03, META-04, RES-01, RES-02, TYPE-01, TYPE-02]` |
| `06-search-cross-assembly/06-02-SUMMARY.md` | `[XASM-01, XASM-02]` |

Those 7 files must be left alone per the CONTEXT.md "do not churn" policy.

**The actual set of files that genuinely need `requirements-completed` added is 6 files:**

| # | File | Current frontmatter status | Proposed `requirements-completed` |
|---|------|---------------------------|----------------------------------|
| 1 | `01-test-infrastructure-baseline/01-01-SUMMARY.md` | Uses outlier `dependency_graph:` nested shape; no `requirements-completed` and no `requirements:` | `[]` (empty — this plan created TestTargets + fixture; TEST-01 is only SATISFIED by 01-02's regression tests. See ambiguity note below.) |
| 2 | `01-test-infrastructure-baseline/01-02-SUMMARY.md` | Uses outlier `dependency_graph:` nested shape; no `requirements-completed` | `[TEST-01]` (31 regression tests for all 8 tools) |
| 3 | `02-sdk-upgrades-bug-fixes/02-01-SUMMARY.md` | Uses `dependency_graph:` nested shape; no `requirements-completed` and no `requirements:` | `[SDK-01, SDK-02]` (decompiler 9.1→10.0 and MCP SDK 0.4.0→1.2.0 confirmed in summary "Package versions confirmed: ICSharpCode.Decompiler 10.0.0.8330, ModelContextProtocol 1.2.0") |
| 4 | `02-sdk-upgrades-bug-fixes/02-02-SUMMARY.md` | Has `requirements:` key (wrong name) with value `[SDK-03, SDK-04]` | `[SDK-03, SDK-04]` — **add new `requirements-completed:` key alongside existing `requirements:` key, or rename**. Recommendation: **add** (safer, preserves any tool that reads `requirements:`). |
| 5 | `02-sdk-upgrades-bug-fixes/02-03-SUMMARY.md` | Has `requirements:` key (wrong name) with value `[SDK-05, TEST-04]` | `[SDK-05, TEST-04]` — **add** new `requirements-completed:` key alongside existing `requirements:` key. |
| 6 | `06-search-cross-assembly/06-01-SUMMARY.md` | Uses `dependency_graph:` nested shape; no `requirements-completed` | `[SRCH-01, SRCH-02]` (search_strings and search_constants tools) |

**Placement rule:** Insert `requirements-completed:` in the frontmatter immediately **before** the `metrics:` / `# Metrics` block (matches Phase 3/4/5/7 placement). For files with the outlier `dependency_graph:` shape, insert before the `metrics:` key. Do NOT reshape `dependency_graph:` into `requires:`/`provides:` — that is explicitly deferred.

**Ambiguity notes (to record in the phase execution summary per CONTEXT.md policy):**
- **01-01 receives `[]`** because the v1.0 REQUIREMENTS TEST-01 definition — "All existing tools have regression tests that pass after SDK upgrades" — is satisfied by 01-02's 31-test regression suite, not by the TestTargets/fixture foundation that 01-01 built. 01-01 is infrastructure-only. Alternative: list `[TEST-01]` on both plans if the planner prefers every requirement to have at least one "claimed by" plan. **Recommendation: `[]` on 01-01, `[TEST-01]` on 01-02.** This matches the CONTEXT.md ambiguity policy ("list the phase-level requirements on the *first* plan and an empty array on subsequent plans") interpreted in reverse: the *first* plan producing a user-observable effect gets the credit. Either choice is defensible; document the choice in the execution summary as a deviation.
- **02-01 `[SDK-01, SDK-02]` grouping**: SDK-01 is the MCP SDK upgrade (0.4.0-preview.3 → 1.2.0) and SDK-02 is the decompiler upgrade. The 02-01 summary says "Task 1: Verify removed API non-usage and upgrade decompiler package" but also lists the MCP SDK version in verification results. Both requirements were landed in the same commit (`1439c8e`). Listing both is correct.

**Frontmatter shape precedent — Phase 7 (kept verbatim, do not churn the rest):**
```yaml
---
phase: 07-bulk-operations-documentation
plan: 01
subsystem: api
tags: [mcp, decompilation, namespace, bulk-operations]

requires:
  - phase: 06-search-cross-assembly
    provides: Decompiler service, type listing, DI patterns
provides:
  - decompile_namespace MCP tool for namespace type listing
  - ...
affects: [07-02, bulk-operations]

tech-stack:
  added: []
  patterns: [...]

key-files:
  created: [...]
  modified: [...]

key-decisions:
  - "..."

patterns-established:
  - "..."

requirements-completed: [BULK-01]    # ← THIS is the key and format

duration: 6min
completed: 2026-04-08
---
```

For the files using the Phase 1/2 `dependency_graph:` nested shape, `requirements-completed` should be inserted at the same logical position (before the `metrics:` block):
```yaml
---
phase: 02-sdk-upgrades-bug-fixes
plan: 01
subsystem: core-dependencies
tags: [sdk-upgrade, decompiler, testing]
dependency_graph:
  requires: []
  provides: [decompiler-10.0, test-deps-current]
  affects: [all-decompiler-features, all-tests]
tech_stack:
  added: []
  patterns: [big-bang-upgrade-with-regression-baseline]
key_files:
  created: []
  modified:
    - ILSpy.Mcp.csproj
    - Tests/ILSpy.Mcp.Tests.csproj
decisions:
  - "..."
requirements-completed: [SDK-01, SDK-02]   # ← ADD this line here
metrics:
  duration: 1m
  completed: 2026-04-07T07:53:13Z
  tasks_completed: 1
  tasks_total: 1
---
```

YAML-wise, placing a kebab-case key next to snake_case keys (`tech_stack`, `key_files`) is legal — YAML does not care about key casing consistency, only uniqueness. No tooling in this repo parses these files with a strict schema (confirmed: no `schema.json`, no parser with required fields). Adding one hyphenated key alongside underscored keys is safe.

**Confidence:** HIGH — file inventory confirmed by ripgrep, each target file read directly.

---

### DEBT-04 — Runtime verification recipe

**Environment probe (verified 2026-04-09):**
- `dotnet --version` → `10.0.201` ✓
- Repo root contains `ILSpy.Mcp.sln` and `ILSpy.Mcp.csproj`
- Test project at `Tests/ILSpy.Mcp.Tests.csproj` (targets net10.0, xUnit 2.9.x)
- TestTargets project at `TestTargets/ILSpy.Mcp.TestTargets.csproj` (provides the assembly-under-test)

**Phase 7 test inventory (what DEBT-04 must verify):**

| Phase 7 plan | Test file | Test count | Test class |
|--------------|-----------|------------|------------|
| 07-01 (decompile_namespace) | `Tests/Tools/DecompileNamespaceToolTests.cs` | 6 | `DecompileNamespaceToolTests` |
| 07-02 (export_project) | `Tests/Tools/ExportProjectToolTests.cs` | 5 | `ExportProjectToolTests` |
| 07-03 (documentation) | (none — README only) | 0 | — |

The 07-01 test class contains: `ListsTypesInNamespace`, `OrdersByKindThenAlphabetically`, `NestedTypesIndentedUnderParent`, `InvalidNamespace_ThrowsNamespaceNotFound`, `MaxTypesLimitsOutput`, `InvalidAssembly_ThrowsError`. The 07-02 test class contains: `ExportsProjectToDirectory`, `CreatesDirectoryIfNotExists`, `FailsOnNonEmptyDirectory`, `ReturnsFileListingWithRelativePaths`, `InvalidAssembly_ThrowsError`.

**Full-suite test command (from repo root):**
```bash
dotnet test ILSpy.Mcp.sln
```

This single command restores, builds, and runs the entire suite. The v1.0 milestone audit reports the Phase 7 baseline as "~148 (unconfirmed)" tests; after DEBT-01's added test (recommendation above) the expected count is ~149.

**Phase 7-only command (for spot-checking, NOT for the evidence artifact):**
```bash
dotnet test ILSpy.Mcp.sln --filter "FullyQualifiedName~DecompileNamespaceToolTests|FullyQualifiedName~ExportProjectToolTests"
```
CONTEXT.md locks the decision to use the full suite, not the filter — keeping the filter here only as a quick debugging aid if a failure appears.

**Capturing evidence artifact:**
After the full suite runs green, record the summary in Phase 7 SUMMARY files. The exact text to append to `07-01-SUMMARY.md` and `07-02-SUMMARY.md` (07-03 has no tests, so it gets a one-line note):

```markdown
## Runtime Verification

- **Verified:** 2026-04-09 (Phase 8 DEBT-04)
- **Command:** `dotnet test ILSpy.Mcp.sln`
- **Result:** {N} passed, 0 failed, 0 skipped
- **Phase 7 tests in run:** {DecompileNamespaceToolTests (6 tests) | ExportProjectToolTests (5 tests)}
- **Closes:** Phase 7 self-check "dotnet CLI not available in execution environment" caveat
```

For `07-03-SUMMARY.md`, the appended block is:
```markdown
## Runtime Verification

- **Verified:** 2026-04-09 (Phase 8 DEBT-04)
- **Applicable tests:** None — this plan was documentation-only (README.md rewrite)
- **Closes:** N/A (07-03 had no runtime claim to verify)
```

**Failure handling (per CONTEXT.md failure policy, operationalized):**
1. If any `DecompileNamespaceToolTests` or `ExportProjectToolTests` test fails → fix in this phase. These are the ones DEBT-04 exists to verify.
2. If any non-Phase-7 test fails AND the failure is introduced by DEBT-01 or DEBT-02 (i.e., it asserts on `METHOD_NOT_FOUND` for FindDependenciesTool or on `McpToolException` from the use case layer) → fix in this phase. This is expected cleanup.
3. If any non-Phase-7 test fails for an unrelated reason → defer as new tech-debt item, record in phase summary, and DEBT-04 marks the Phase 7 verification passing even so (because the Phase 7 tests specifically passed).

**Confidence:** HIGH — dotnet version verified, repo layout verified, test files enumerated.

## Architecture Patterns

### Layered architecture (the rule DEBT-02 enforces)

The project uses a strict `Domain → Infrastructure → Application → Transport` dependency direction:

```
Domain/
├── Errors/       # Exceptions (DomainException base, concrete narrow types)
├── Models/       # Records, value objects
└── Services/     # Port interfaces (I* abstractions)

Infrastructure/
└── Decompiler/   # Adapters that implement Domain/Services interfaces

Application/
├── Services/     # Cross-cutting (TimeoutService, ConcurrencyLimiter)
└── UseCases/     # Orchestrators that depend on Domain interfaces + Application services

Transport/
└── Mcp/
    ├── Errors/   # McpToolException, ErrorSanitizer (transport-specific)
    └── Tools/    # MCP tool attributes + catch blocks mapping Domain exceptions to wire codes
```

**The rule:** An inner layer must NEVER reference an outer layer. Specifically, `Application/UseCases/*.cs` must never `using ILSpy.Mcp.Transport.*`. This is exactly the rule `ExportProjectUseCase.cs:8` currently violates.

**Enforcement via convention, not tooling:** There is no compiled-in architecture test (no NetArchTest, no ArchUnit-style check). The layer boundaries are maintained by code review. DEBT-02 is a manual discovery of a drift. Fix the drift, don't add a tool.

### Exception handling pattern (the rule DEBT-01 and DEBT-02 both use)

```
Domain layer:       Throws specific DomainException subclass (carries error code)
                            │
                            ▼
Application layer:  Does NOT catch; lets DomainException flow through
                            │
                            ▼
Transport layer:    Catches specific DomainException type, maps to McpToolException
                    with wire error code string (which may or may not match ex.ErrorCode)
```

**Specific-to-general catch ordering** (used by every tool):
1. Specific domain exceptions first (`TypeNotFoundException`, `MethodNotFoundException`, `NamespaceNotFoundException`, `AssemblyLoadException`, and — after DEBT-02 — `OutputDirectoryNotEmptyException`)
2. Then `TimeoutException` → `"TIMEOUT"`
3. Then `OperationCanceledException` → `"CANCELLED"`
4. Finally `Exception` → `"INTERNAL_ERROR"`

The `catch (McpToolException) { throw; }` passthrough is an **antipattern** left over from the DEBT-02 violation. It only exists because the use case throws a transport exception. After DEBT-02 it is strictly dead code and must be removed from both sites (`ExportProjectUseCase` and `ExportProjectTool`).

### Frontmatter shape convention (relevant to DEBT-03)

The project has **two** frontmatter shapes that coexist:
- **Phase 1-2 shape (outlier):** `dependency_graph:` nested, `tech_stack:` underscored, `key_files:` underscored, `metrics:` block
- **Phase 3+ shape (current):** `requires:`/`provides:`/`affects:` flat, `tech-stack:` kebab, `key-files:` kebab, `key-decisions:` kebab, `requirements-completed:` kebab, `duration:` + `completed:` flat

CONTEXT.md explicitly says: **do not normalize the shapes.** DEBT-03 is purely additive — add a `requirements-completed:` key to the 6 missing files in a position that works for their existing shape, and do not rename anything else.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Transport-layer code reuse from Application layer | Import `McpToolException` from Application (the current violation) | Domain exception subclass + tool-layer catch | The whole point of DEBT-02. Domain exceptions are the inter-layer currency. |
| Custom architecture enforcement tests | A NetArchTest-style "Application cannot reference Transport" check | Code review + CLAUDE.md rule | Adds a new test dependency for a one-off fix. Out of scope per CONTEXT.md deferred items. |
| Path sanitization in new tool catch | Hand-rolled path-strip | `ErrorSanitizer.SanitizePath(ex.Message)` | Already used by every tool. Regex-based, consistent. |
| Manual test filtering for Phase 7 | A custom test categories / traits scheme | Run full suite | CONTEXT.md locks this. Full suite gives the strongest evidence for DEBT-04. |
| YAML frontmatter parser | Any write-side tooling to insert `requirements-completed:` | Direct text editing of 6 files | The files are hand-maintained. A parser is overkill for 6 files. |

## Common Pitfalls

### Pitfall 1: Changing `MethodNotFoundException.ErrorCode` instead of the Transport catch clause

**What goes wrong:** A well-intentioned "fix at the source" change to `Domain/Errors/MethodNotFoundException.cs` would change the base error code from `"METHOD_NOT_FOUND"` to `"MEMBER_NOT_FOUND"`, which would ripple to every tool that catches this exception. That includes `DecompileMethodTool`, `DisassembleMethodTool`, `GetMemberAttributesTool`, `FindUsagesTool`, `FindInstantiationsTool` — many of which correctly use `METHOD_NOT_FOUND`.

**Why it happens:** The exception carries an error code, so it looks like "the source of truth". But Transport tools don't read `ex.ErrorCode`; they hardcode the wire string. The exception's own code is effectively unused.

**How to avoid:** Change only line 47 of `FindDependenciesTool.cs`. Leave `MethodNotFoundException.cs` alone. CONTEXT.md explicitly says "Domain exception stays."

**Warning signs:** Any diff in DEBT-01 that touches `Domain/Errors/MethodNotFoundException.cs` is wrong.

### Pitfall 2: Over-scoping DEBT-02 by introducing an `OutputDirectoryPath` value object

**What goes wrong:** Building an `OutputDirectoryPath.Create(...)` value object that wraps the whitespace `ArgumentException` check + the existence check + the non-empty check. Mirrors `AssemblyPath.Create` pattern, feels clean, but expands the diff significantly and is explicitly deferred.

**Why it happens:** `AssemblyPath` is a local role model. The symmetry is tempting.

**How to avoid:** Scope the new domain exception to ONLY the "directory is not empty" case. Leave `ArgumentException` alone. CONTEXT.md deferred ideas list explicitly names this.

**Warning signs:** Any new file under `Domain/Models/` as part of DEBT-02. The only new file should be `Domain/Errors/OutputDirectoryNotEmptyException.cs`.

### Pitfall 3: Normalizing Phase 1-2 frontmatter shape while in DEBT-03

**What goes wrong:** While adding `requirements-completed:` to `02-01-SUMMARY.md`, it's tempting to also convert `dependency_graph:` → `requires:`/`provides:`/`affects:`, and `tech_stack:` → `tech-stack:`, and `key_files:` → `key-files:`. Suddenly a 1-line add becomes a 15-line reshape across 6 files.

**Why it happens:** Developer instinct toward consistency. The outlier shape "looks wrong" next to the Phase 3+ shape.

**How to avoid:** The audit did not flag shape inconsistency. Only the missing `requirements-completed:` field. CONTEXT.md deferred items list explicitly says shape normalization is out of scope. Add the one key, stop.

**Warning signs:** Any diff in DEBT-03 that touches an existing key on a Phase 1 or Phase 2 SUMMARY file. The diff should be `+ requirements-completed: [...]` on a single line (or two lines for multi-entry) and nothing else.

### Pitfall 4: Running `dotnet test` from the wrong directory

**What goes wrong:** Running `dotnet test` from inside `Tests/` picks up only the test project's local resolution and can fail to locate `TestTargets` binaries. `dotnet test ILSpy.Mcp.sln` from repo root is the verified-working command.

**Why it happens:** Habit of cd-ing to the test project before running tests.

**How to avoid:** Always run `dotnet test ILSpy.Mcp.sln` from the repo root. The Phase 7 SUMMARY files also confirm `dotnet test` worked for earlier phases from repo root — consistent with the solution structure (`ILSpy.Mcp.sln` at root).

**Warning signs:** A "test file not found" or "assembly could not be loaded" error on what should be a green suite.

### Pitfall 5: Appending the DEBT-04 evidence block inside the YAML frontmatter

**What goes wrong:** Putting the `## Runtime Verification` block inside the `---` YAML frontmatter instead of in the body. This breaks the frontmatter parser (whatever reads it) and injects markdown into a YAML region.

**Why it happens:** The Phase 7 `Self-Check: PASSED` block is already in the body (after the frontmatter), but the CONTEXT.md wording "append to the existing `## Self-Check: PASSED` block" might be read as "merge with".

**How to avoid:** The Runtime Verification block is a new `## Runtime Verification` markdown section in the body, inserted after `## Self-Check: PASSED` and before the trailing `---` phase footer line. It is NOT a YAML key.

**Warning signs:** Any DEBT-04 diff that inserts content between the two `---` markers at the top of a Phase 7 SUMMARY file.

## Code Examples (Verified Patterns)

### Adding a new domain exception (template from `NamespaceNotFoundException.cs`)
```csharp
// File: Domain/Errors/OutputDirectoryNotEmptyException.cs
namespace ILSpy.Mcp.Domain.Errors;

public sealed class OutputDirectoryNotEmptyException : DomainException
{
    public string OutputDirectory { get; }

    public OutputDirectoryNotEmptyException(string outputDirectory)
        : base("DIRECTORY_NOT_EMPTY",
               $"Output directory is not empty: {outputDirectory}. Specify an empty or non-existent directory.")
    {
        OutputDirectory = outputDirectory;
    }
}
```

### Throwing a domain exception from a use case (replacing `McpToolException`)
```csharp
// File: Application/UseCases/ExportProjectUseCase.cs
// BEFORE (lines 58-62):
if (Directory.EnumerateFileSystemEntries(outputDirectory).Any())
{
    throw new McpToolException("DIRECTORY_NOT_EMPTY",
        $"Output directory is not empty: {outputDirectory}. Specify an empty or non-existent directory.");
}

// AFTER:
if (Directory.EnumerateFileSystemEntries(outputDirectory).Any())
{
    throw new OutputDirectoryNotEmptyException(outputDirectory);
}
```

### Catching a domain exception in a transport tool (template from `FindUsagesTool.cs`)
```csharp
// File: Transport/Mcp/Tools/ExportProjectTool.cs
// Insert BEFORE the existing AssemblyLoadException catch (lines 45-49):
catch (OutputDirectoryNotEmptyException ex)
{
    _logger.LogWarning("Output directory not empty: {Directory}", ex.OutputDirectory);
    throw new McpToolException("DIRECTORY_NOT_EMPTY", ErrorSanitizer.SanitizePath(ex.Message));
}
```

### Adding a test for the DEBT-01 member-not-found path
```csharp
// File: Tests/Tools/FindDependenciesToolTests.cs
// New test, appended after FindDependencies_NonExistentType_ThrowsTypeNotFound:
[Fact]
public async Task FindDependencies_NonExistentMember_ThrowsMemberNotFound()
{
    using var scope = _fixture.CreateScope();
    var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

    var act = () => tool.ExecuteAsync(
        _fixture.TestAssemblyPath,
        "ILSpy.Mcp.TestTargets.CrossRef.DataService",
        "NonExistentMethod",
        CancellationToken.None);

    var ex = await act.Should().ThrowAsync<McpToolException>();
    ex.Which.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `throw new McpToolException` from use case layer (ExportProjectUseCase) | Domain exception thrown from use case, caught and mapped in Transport | Phase 8 (this phase) | Restores strict layering; any future audit check stays clean |
| `"METHOD_NOT_FOUND"` wire code on `FindDependenciesTool` member-not-found path | `"MEMBER_NOT_FOUND"` (matches `FindUsagesTool`, `GetMemberAttributesTool`) | Phase 8 (this phase) | Cross-reference tools speak a consistent wire contract |
| Phase 1-6 SUMMARY files silent on requirements traceability | `requirements-completed:` key populated per plan | Phase 8 (this phase) | Audit tooling can verify requirement→plan mapping without reading body text |
| Phase 7 tests "verified by code inspection only" note | `## Runtime Verification` block with actual `dotnet test` result | Phase 8 (this phase) | Phase 7 self-checks become verifiable claims rather than hedged statements |

**Deprecated / not worth fighting:**
- Nested `dependency_graph:` in Phase 1-2 SUMMARY files — the outlier shape stays per CONTEXT.md. The Phase 3+ flat shape is the modern convention but migration is explicitly deferred.
- `requirements:` key (singular name, used only in 02-02 and 02-03) — will coexist with the new `requirements-completed:` key after DEBT-03 rather than be renamed. CONTEXT.md deferred ideas list the underscored-vs-hyphenated fix as a doc-only concern for later.

## Open Questions

All questions are minor and have recommended answers:

1. **Should `01-01-SUMMARY.md` get `requirements-completed: []` or `requirements-completed: [TEST-01]`?**
   - What we know: 01-01 built TestTargets + ToolTestFixture. 01-02 built the 31 regression tests.
   - What's unclear: TEST-01 says "All existing tools have regression tests" — the tests live in 01-02, but the infrastructure they depend on is 01-01.
   - Recommendation: `[]` on 01-01, `[TEST-01]` on 01-02. Document as a deviation in the phase execution summary per CONTEXT.md ambiguity policy. The alternative (`[TEST-01]` on both) is also defensible; the planner should pick one and move on.

2. **Should `02-02-SUMMARY.md` and `02-03-SUMMARY.md` retain their existing `requirements:` key or have it renamed?**
   - What we know: Both files have `requirements:` with the correct values. Neither file has `requirements-completed:`.
   - What's unclear: Rename or add?
   - Recommendation: **Add** `requirements-completed:` alongside the existing `requirements:` key. This is strictly additive, matches the CONTEXT.md "do not churn" policy, and leaves the existing key functioning as-is for any consumer.

3. **Should the new DEBT-01 test be committed with DEBT-01 or DEBT-04?**
   - What we know: The test asserts `MEMBER_NOT_FOUND` which only becomes true after the DEBT-01 code change. So it must land in the same commit as or after DEBT-01. It is also direct evidence for DEBT-04.
   - Recommendation: Same commit as the DEBT-01 code change. Keeps the commit atomic and self-validating (the commit changes the tool + adds a test that fails before and passes after, classic refactor pattern).

4. **07-03-SUMMARY.md Runtime Verification block — needed or skip?**
   - What we know: 07-03 was documentation-only, no code and no tests. It has a `Self-Check: PASSED` block that says "FOUND: README.md, commit 5341dc7".
   - What's unclear: CONTEXT.md says "Which of the Phase 7 SUMMARY files need the `Runtime Verification` block (probably all three, but only the ones whose tests actually ran)" — explicitly Claude's discretion.
   - Recommendation: Add a one-line "N/A — documentation-only plan" Runtime Verification block to 07-03 as well, for symmetry. It closes the DEBT-04 loop on all three Phase 7 plans uniformly and the cost is a 4-line addition.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | None — tests registered via `[Collection("ToolTests")]` + shared `ToolTestFixture` |
| Quick run command (specific test) | `dotnet test ILSpy.Mcp.sln --filter "FullyQualifiedName~{TestClass}.{TestMethod}"` |
| Phase 7 filter command | `dotnet test ILSpy.Mcp.sln --filter "FullyQualifiedName~DecompileNamespaceToolTests\|FullyQualifiedName~ExportProjectToolTests"` |
| Full suite command | `dotnet test ILSpy.Mcp.sln` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DEBT-01 | `FindDependenciesTool` returns `MEMBER_NOT_FOUND` when a method name is not found on the target type | unit (integration against TestTargets) | `dotnet test ILSpy.Mcp.sln --filter "FullyQualifiedName~FindDependenciesToolTests.FindDependencies_NonExistentMember_ThrowsMemberNotFound"` | **Needs Wave 0 add** — new test `FindDependencies_NonExistentMember_ThrowsMemberNotFound` does not exist in `Tests/Tools/FindDependenciesToolTests.cs` yet. |
| DEBT-02 (functional preservation) | `ExportProjectTool` still returns `DIRECTORY_NOT_EMPTY` wire code on a non-empty output directory | unit (integration against TestTargets) | `dotnet test ILSpy.Mcp.sln --filter "FullyQualifiedName~ExportProjectToolTests.FailsOnNonEmptyDirectory"` | ✅ Exists at `Tests/Tools/ExportProjectToolTests.cs:78-103` (the `FailsOnNonEmptyDirectory` test). No new test needed — this test is the refactor guardrail. |
| DEBT-02 (layering invariant) | `Application/UseCases/ExportProjectUseCase.cs` must not import `ILSpy.Mcp.Transport.*` | static / grep | `! grep -q "using ILSpy.Mcp.Transport" Application/UseCases/ExportProjectUseCase.cs` (POSIX) — inverted success: non-zero exit = layer clean | ✅ Verifiable by grep; no test file required. This is a code-level invariant, not a runtime behavior. |
| DEBT-03 | Each of 6 Phase 1-6 SUMMARY files contains a `requirements-completed:` key | static / grep | `for f in 01-01 01-02 02-01 02-02 02-03 06-01; do grep -q "^requirements-completed:" .planning/milestones/v1.0-phases/*/${f}-SUMMARY.md \|\| echo MISSING: ${f}; done` — no `MISSING:` output = all backfilled | ✅ Verifiable by grep; no test file. This is doc content, not runtime behavior. |
| DEBT-04 | Full `dotnet test` suite returns zero failures; Phase 7 test classes appear in the run summary | integration (full suite) | `dotnet test ILSpy.Mcp.sln` — the command IS the test | ✅ Runner and suite exist; the verification itself is the artifact. |

### Sampling Rate
- **Per task commit (DEBT-01, DEBT-02):** Run the targeted filter for the touched file — `dotnet test ILSpy.Mcp.sln --filter "FullyQualifiedName~FindDependenciesToolTests"` or `...~ExportProjectToolTests"`. Fast enough for tight feedback during task work (<30s on this project size).
- **Per plan merge (end of DEBT-01+02 combined plan):** Full suite — `dotnet test ILSpy.Mcp.sln`.
- **DEBT-03 (doc-only plan):** Grep verification — no test runner involvement. Verify with `grep -c "^requirements-completed:" .planning/milestones/v1.0-phases/**/SUMMARY.md` and confirm count = (previously-existing 10 files) + 6 = 16.
- **Phase gate (DEBT-04):** Full suite green before `/gsd:verify-work`. DEBT-04 is literally this gate promoted to a first-class work item so the evidence is captured in Phase 7 SUMMARY files.

### Wave 0 Gaps
- [ ] `Tests/Tools/FindDependenciesToolTests.cs` — add `FindDependencies_NonExistentMember_ThrowsMemberNotFound` test. This is the single new test needed to give DEBT-01 direct runtime coverage. Without it, DEBT-01 only has indirect coverage (the tool still compiles, the build passes, but no test asserts the new wire code).
- [ ] No framework install needed: `dotnet --version` is `10.0.201`, xUnit 2.9.x is in `Tests/ILSpy.Mcp.Tests.csproj`, `ToolTestFixture` shared collection already handles DI wiring for all tools including `FindDependenciesTool` and `ExportProjectTool`.
- [ ] No new fixtures / test targets needed: `CrossRef.DataService` and the temp-dir helper already exist in the test targets and `ExportProjectToolTests` respectively. The DEBT-01 new test reuses `_fixture.TestAssemblyPath` + `CrossRef.DataService` (proven by the existing positive-path tests in the same file).

## Sources

### Primary (HIGH confidence) — repo files verified directly 2026-04-09
- `.planning/phases/08-tech-debt-cleanup/08-CONTEXT.md` — user-locked decisions (the authority for this phase)
- `.planning/REQUIREMENTS.md` — DEBT-01..04 definitions
- `.planning/ROADMAP.md` — Phase 8 section, success criteria
- `.planning/STATE.md` — current position, velocity metrics
- `.planning/milestones/v1.0-REQUIREMENTS.md` — archived v1.0 requirements (TEST-01, SDK-01..05, IL-01..02, XREF-01..04, META-01..04, RES-01..02, TYPE-01..02, SRCH-01..02, XASM-01..02, BULK-01..02, DOC-01, TEST-01..04)
- `.planning/milestones/v1.0-MILESTONE-AUDIT.md` — audit that surfaced all four DEBT items, explicit wording at lines 20-28
- `Transport/Mcp/Tools/FindDependenciesTool.cs` — the file DEBT-01 edits
- `Transport/Mcp/Tools/FindUsagesTool.cs` — the precedent DEBT-01 matches
- `Transport/Mcp/Tools/GetMemberAttributesTool.cs` — second precedent for `MEMBER_NOT_FOUND`
- `Transport/Mcp/Tools/ExportProjectTool.cs` — the file DEBT-02 edits
- `Transport/Mcp/Tools/DecompileNamespaceTool.cs` — reference for the specific-to-general catch ordering + `ErrorSanitizer` usage
- `Application/UseCases/ExportProjectUseCase.cs` — the file DEBT-02 edits (lines 8, 60, 141-144)
- `Domain/Errors/NamespaceNotFoundException.cs` — exact pattern to copy for `OutputDirectoryNotEmptyException`
- `Domain/Errors/DomainException.cs` — base class contract
- `Domain/Errors/MethodNotFoundException.cs` — unchanged by DEBT-01; verified it carries `METHOD_NOT_FOUND` internally
- `Domain/Errors/TypeNotFoundException.cs`, `Domain/Errors/AssemblyLoadException.cs` — pattern confirmation
- `Tests/Tools/FindDependenciesToolTests.cs` — **verified: no `METHOD_NOT_FOUND` assertion exists** (contradicts CONTEXT.md claim)
- `Tests/Tools/ExportProjectToolTests.cs:95` — the `DIRECTORY_NOT_EMPTY` assertion that pins the DEBT-02 wire contract
- `Tests/Tools/DecompileNamespaceToolTests.cs` — Phase 7 plan 1 test file
- `.planning/milestones/v1.0-phases/07-bulk-operations-documentation/07-01-SUMMARY.md` — Phase 7 frontmatter shape reference
- `.planning/milestones/v1.0-phases/07-bulk-operations-documentation/07-02-SUMMARY.md` — same
- `.planning/milestones/v1.0-phases/07-bulk-operations-documentation/07-03-SUMMARY.md` — same
- `.planning/milestones/v1.0-phases/{01..06}/` — 13 SUMMARY files surveyed; 6 confirmed missing `requirements-completed:`
- `.claude/skills/mcp-tool-design/SKILL.md` — error design Principle 8 ("Errors carry next-step hints") applies to the new `OutputDirectoryNotEmptyException` message

### Secondary (MEDIUM confidence) — transitive or inferred
- `dotnet --version` → `10.0.201` — verified by bash probe, current environment
- Project uses xUnit 2.9.x, FluentAssertions 8.9.x — confirmed via CLAUDE.md stack table (HIGH) + test file imports (HIGH)

### Tertiary (LOW confidence) — none

This phase is an entirely internal remediation. There are no external APIs, no library decisions, no version choices. Every claim in this research is backed by a specific line in a specific file in this repository.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new libraries, uses existing xUnit + FluentAssertions + dotnet CLI; stack is frozen by CLAUDE.md
- Architecture: HIGH — patterns verified by reading the actual sibling tools and exceptions
- Code ground truth (DEBT-01 / DEBT-02 / DEBT-03 / DEBT-04): HIGH — all files read directly and verified
- Pitfalls: HIGH — derived from observed code state and CONTEXT.md deferred-ideas list, not speculation
- Frontmatter backfill file list: HIGH — verified by ripgrep and individual reads; **CONTEXT.md's 13-file list is wrong, the correct count is 6**
- DEBT-01 test update claim in CONTEXT.md: HIGH — verified false (no existing assertion to update); the planner should substitute "add new test" for "update existing test"

**Research date:** 2026-04-09
**Valid until:** Stable until Phase 8 executes. No external time-bomb.

## Next Steps for Planner

1. Write a single plan covering DEBT-01 + DEBT-02 (shared Transport/Application boundary, shared full-suite run). Suggested tasks:
   - Task 1: Create `Domain/Errors/OutputDirectoryNotEmptyException.cs` (new file, copy-paste adapted from `NamespaceNotFoundException.cs`)
   - Task 2: Rewire `Application/UseCases/ExportProjectUseCase.cs` (remove transport import, replace throw, remove dead catch)
   - Task 3: Rewire `Transport/Mcp/Tools/ExportProjectTool.cs` (add specific catch, remove dead passthrough catch) + change `Transport/Mcp/Tools/FindDependenciesTool.cs:47` wire code + add `Tests/Tools/FindDependenciesToolTests.cs` new test
   - Task 4: Targeted `dotnet test` for affected test classes to validate the combined change in isolation
2. Write a second plan for DEBT-03 (doc-only; 6 files, additive edits). Suggested single-task plan with a verification step grepping for the new key in all 6 files.
3. Write a third plan for DEBT-04 (runtime verification; runs the full suite; records evidence in three Phase 7 SUMMARY files). Suggested single-task plan with the `dotnet test ILSpy.Mcp.sln` run as the task action and the SUMMARY.md updates as the artifact.
4. Ordering inside the phase: Plan A (DEBT-01+02) → Plan B (DEBT-03) → Plan C (DEBT-04).

The planner has everything needed; no further research pass is required.
