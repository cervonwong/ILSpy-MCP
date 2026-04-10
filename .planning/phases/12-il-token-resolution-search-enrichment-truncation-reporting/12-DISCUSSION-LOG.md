# Phase 12: IL Token Resolution, Search Enrichment & Truncation Reporting - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-10
**Phase:** 12-il-token-resolution-search-enrichment-truncation-reporting
**Areas discussed:** IL token resolution depth, Search context window, Truncation detection, Search model enrichment

---

## Gray Area Selection

| Option | Description | Selected |
|--------|-------------|----------|
| IL token resolution depth | How deep should inline resolution go? Which opcodes? What does resolveDeep add? | |
| Search context window | Surrounding IL instructions for search matches — how many, fixed or configurable? | |
| Truncation detection | How to detect and report truncation for source/bounded tools | |
| Search model enrichment | Method FQN format, MethodSignature population, domain model changes | |

**User's choice:** "Use your best understanding from my mcp-tool-design skill"
**Notes:** User delegated all four gray areas to Claude's judgment, guided by the mcp-tool-design skill principles (Principles 2, 3, 5 for resolution/enrichment depth; Principle 4 for truncation visibility).

---

## Claude's Discretion

All four gray areas were resolved by applying mcp-tool-design principles:
- **IL resolution**: Principle 2 (nested references) — resolve all token operands inline; Principle 5 (rich but not flooding) — default is standard FQN+assembly, resolveDeep is opt-in
- **Search context**: Principle 3 (lazy agent) — include 3 instructions before/after for strings (enough to see usage); Principle 5 — no window for constants (value IS the context)
- **Truncation**: Principle 4 (pagination mandatory) extended to non-list tools — structured JSON footer with line/type counts
- **Search models**: Principle 2+3 — add MethodSignature for full FQN, SurroundingInstructions for string matches

## Deferred Ideas

None
