---
phase: 7
slug: bulk-operations-documentation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-08
---

# Phase 7 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 + FluentAssertions 8.9.0 |
| **Config file** | Tests project references |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~DecompileNamespace\|FullyQualifiedName~ExportProject"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "FullyQualifiedName~DecompileNamespace|FullyQualifiedName~ExportProject"`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 07-01-01 | 01 | 1 | BULK-01 | integration | `dotnet test --filter "FullyQualifiedName~DecompileNamespaceToolTests"` | ❌ W0 | ⬜ pending |
| 07-01-02 | 01 | 1 | BULK-01 | integration | `dotnet test --filter "FullyQualifiedName~DecompileNamespaceToolTests"` | ❌ W0 | ⬜ pending |
| 07-01-03 | 01 | 1 | BULK-01 | integration | `dotnet test --filter "FullyQualifiedName~DecompileNamespaceToolTests"` | ❌ W0 | ⬜ pending |
| 07-01-04 | 01 | 1 | BULK-01 | integration | `dotnet test --filter "FullyQualifiedName~DecompileNamespaceToolTests"` | ❌ W0 | ⬜ pending |
| 07-02-01 | 02 | 1 | BULK-02 | integration | `dotnet test --filter "FullyQualifiedName~ExportProjectToolTests"` | ❌ W0 | ⬜ pending |
| 07-02-02 | 02 | 1 | BULK-02 | integration | `dotnet test --filter "FullyQualifiedName~ExportProjectToolTests"` | ❌ W0 | ⬜ pending |
| 07-02-03 | 02 | 1 | BULK-02 | integration | `dotnet test --filter "FullyQualifiedName~ExportProjectToolTests"` | ❌ W0 | ⬜ pending |
| 07-02-04 | 02 | 1 | BULK-02 | integration | `dotnet test --filter "FullyQualifiedName~ExportProjectToolTests"` | ❌ W0 | ⬜ pending |
| 07-03-01 | 03 | 2 | DOC-01 | manual-only | Visual review | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Tests/Tools/DecompileNamespaceToolTests.cs` — stubs for BULK-01 test scenarios
- [ ] `Tests/Tools/ExportProjectToolTests.cs` — stubs for BULK-02 test scenarios (needs temp directory cleanup)
- [ ] `Tests/Fixtures/ToolTestFixture.cs` — DI registration for new use cases and tools

*Wave 0 creates test stubs; implementation fills them in.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| README documents all 28 tools with examples | DOC-01 | Content accuracy requires human review | Review README.md: verify all tools listed, examples accurate, disclosure accordions work |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
