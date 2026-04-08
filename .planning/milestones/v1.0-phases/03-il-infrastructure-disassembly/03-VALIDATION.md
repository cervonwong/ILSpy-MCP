---
phase: 3
slug: il-infrastructure-disassembly
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-07
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x + FluentAssertions 8.9.0 |
| **Config file** | `Tests/ILSpyMcp.Tests.csproj` |
| **Quick run command** | `dotnet test Tests/ILSpyMcp.Tests.csproj --filter "ClassName~Disassemble" --no-build` |
| **Full suite command** | `dotnet test Tests/ILSpyMcp.Tests.csproj` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test Tests/ILSpyMcp.Tests.csproj --filter "ClassName~Disassemble" --no-build`
- **After every plan wave:** Run `dotnet test Tests/ILSpyMcp.Tests.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 03-01-01 | 01 | 1 | IL-01 | integration | `dotnet test --filter "ClassName~DisassembleTypeTool"` | ❌ W0 | ⬜ pending |
| 03-01-02 | 01 | 1 | IL-02 | integration | `dotnet test --filter "ClassName~DisassembleMethodTool"` | ❌ W0 | ⬜ pending |
| 03-02-01 | 02 | 2 | TEST-03 | integration | `dotnet test --filter "ClassName~Disassemble"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Tests/Tools/DisassembleTypeToolTests.cs` — stubs for IL-01
- [ ] `Tests/Tools/DisassembleMethodToolTests.cs` — stubs for IL-02
- [ ] Test fixtures extend existing `ToolTestFixture` — no new infrastructure needed

*Existing test infrastructure (xUnit, FluentAssertions, ToolTestFixture, TestTargets) covers all phase requirements.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| MCP tool discovery | IL-01, IL-02 | Requires running MCP server | Start server, verify `disassemble_type` and `disassemble_method` appear in tool list |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
