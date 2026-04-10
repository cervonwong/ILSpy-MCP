---
phase: 12
slug: il-token-resolution-search-enrichment-truncation-reporting
status: verified
threats_open: 0
asvs_level: 1
created: 2026-04-10
---

# Phase 12 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| MCP client -> Transport tool | resolveDeep is a boolean parameter; no untrusted string input beyond existing assemblyPath/typeName validation | bool parameter |
| MCP client -> search tools | regexPattern already validated with timeout; no new untrusted input | existing validated input |
| MCP client -> source tools | No new untrusted input; truncation is server-side enforcement | server-side only |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-12-01 | D (DoS) | ILSpyDisassemblyService deep resolution | accept | Post-processing is O(n) on output string length, bounded by MaxDecompilationSize truncation cap (Plan 03) | closed |
| T-12-02 | D (DoS) | ILSpySearchService surrounding IL capture | mitigate | Method disassembly for IL window bounded by MaxDecompilationSize (1MB). Per-method caching avoids redundant disassembly within same scan. | closed |
| T-12-03 | I (Info Disclosure) | Surrounding IL instructions | accept | IL instructions already returned by disassemble_method tool; surrounding context adds no new information exposure | closed |
| T-12-04 | D (DoS) | DisassembleMethodUseCase / DisassembleTypeUseCase | mitigate | New MaxDecompilationSize byte cap on disassemble tools via IOptions<ILSpyOptions> injection (previously unbounded) | closed |
| T-12-05 | D (DoS) | AnalyzeAssemblyUseCase | mitigate | maxDisplayTypes=200 cap prevents assemblies with thousands of public types from producing multi-MB output | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-12-01 | T-12-01 | Deep resolution post-processing is O(n) on output length. Output is already bounded by MaxDecompilationSize (1MB). No amplification vector. | gsd-secure-phase | 2026-04-10 |
| AR-12-02 | T-12-03 | Surrounding IL context exposes the same IL instructions already accessible via disassemble_method. No incremental information disclosure. | gsd-secure-phase | 2026-04-10 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-04-10 | 5 | 5 | 0 | gsd-secure-phase |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-04-10
