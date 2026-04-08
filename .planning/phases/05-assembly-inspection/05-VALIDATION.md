---
phase: 5
slug: assembly-inspection
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-08
---

# Phase 5 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 + FluentAssertions 8.9.0 |
| **Config file** | Tests/ILSpy.Mcp.Tests.csproj |
| **Quick run command** | `dotnet test Tests/ILSpy.Mcp.Tests.csproj --filter "FullyQualifiedName~Assembly"` |
| **Full suite command** | `dotnet test Tests/ILSpy.Mcp.Tests.csproj` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test Tests/ILSpy.Mcp.Tests.csproj --filter "FullyQualifiedName~Assembly"` 
- **After every plan wave:** Run `dotnet test Tests/ILSpy.Mcp.Tests.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 05-01-01 | 01 | 1 | META-01, META-02 | integration | `dotnet test Tests --filter "FullyQualifiedName~GetAssemblyMetadataToolTests"` | ❌ W0 | ⬜ pending |
| 05-01-02 | 01 | 1 | META-03 | integration | `dotnet test Tests --filter "FullyQualifiedName~GetAssemblyAttributesToolTests"` | ❌ W0 | ⬜ pending |
| 05-01-03 | 01 | 1 | META-04 | integration | `dotnet test Tests --filter "FullyQualifiedName~GetTypeAttributesToolTests"` | ❌ W0 | ⬜ pending |
| 05-01-04 | 01 | 1 | META-04 | integration | `dotnet test Tests --filter "FullyQualifiedName~GetMemberAttributesToolTests"` | ❌ W0 | ⬜ pending |
| 05-02-01 | 02 | 2 | RES-01 | integration | `dotnet test Tests --filter "FullyQualifiedName~ListEmbeddedResourcesToolTests"` | ❌ W0 | ⬜ pending |
| 05-02-02 | 02 | 2 | RES-02 | integration | `dotnet test Tests --filter "FullyQualifiedName~ExtractResourceToolTests"` | ❌ W0 | ⬜ pending |
| 05-02-03 | 02 | 2 | TYPE-01, TYPE-02 | integration | `dotnet test Tests --filter "FullyQualifiedName~FindCompilerGeneratedTypesToolTests"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Tests/Tools/GetAssemblyMetadataToolTests.cs` — stubs for META-01, META-02
- [ ] `Tests/Tools/GetAssemblyAttributesToolTests.cs` — stubs for META-03
- [ ] `Tests/Tools/GetTypeAttributesToolTests.cs` — stubs for META-04 (type level)
- [ ] `Tests/Tools/GetMemberAttributesToolTests.cs` — stubs for META-04 (member level)
- [ ] `Tests/Tools/ListEmbeddedResourcesToolTests.cs` — stubs for RES-01
- [ ] `Tests/Tools/ExtractResourceToolTests.cs` — stubs for RES-02
- [ ] `Tests/Tools/FindCompilerGeneratedTypesToolTests.cs` — stubs for TYPE-01, TYPE-02
- [ ] `TestTargets/Resources/sample.txt` — text embedded resource
- [ ] `TestTargets/Resources/sample.bin` — binary embedded resource
- [ ] `TestTargets/Types/CompilerGeneratedTestTypes.cs` — async + lambda types
- [ ] `TestTargets/ILSpy.Mcp.TestTargets.csproj` — EmbeddedResource items added

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
