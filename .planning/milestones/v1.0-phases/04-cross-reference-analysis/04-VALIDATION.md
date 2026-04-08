---
phase: 4
slug: cross-reference-analysis
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-08
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x + FluentAssertions 8.9.0 |
| **Config file** | `tests/ILSpyMCP.Tests/ILSpyMCP.Tests.csproj` |
| **Quick run command** | `dotnet test tests/ILSpyMCP.Tests --filter "Category=CrossRef"` |
| **Full suite command** | `dotnet test tests/ILSpyMCP.Tests` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/ILSpyMCP.Tests --filter "Category=CrossRef"`
- **After every plan wave:** Run `dotnet test tests/ILSpyMCP.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 04-01-01 | 01 | 1 | XREF-01 | integration | `dotnet test --filter "FindUsages"` | ❌ W0 | ⬜ pending |
| 04-01-02 | 01 | 1 | XREF-02 | integration | `dotnet test --filter "FindImplementations"` | ❌ W0 | ⬜ pending |
| 04-01-03 | 01 | 1 | XREF-03 | integration | `dotnet test --filter "FindDependencies"` | ❌ W0 | ⬜ pending |
| 04-01-04 | 01 | 1 | XREF-04 | integration | `dotnet test --filter "FindInstantiations"` | ❌ W0 | ⬜ pending |
| 04-02-01 | 02 | 2 | TEST-02 | integration | `dotnet test tests/ILSpyMCP.Tests` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Test stubs for cross-reference analysis tools (find_usages, find_implementations, find_dependencies, find_instantiations)
- [ ] Shared fixtures for assembly loading (existing `TestAssemblyFixture` may cover this)

*Existing test infrastructure from Phase 1 covers framework setup. Wave 0 only needs cross-ref specific stubs.*

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
