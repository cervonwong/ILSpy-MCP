# Quick Task 260410: Audit MCP tools for AI-effectiveness and document design principles - Context

**Gathered:** 2026-04-09
**Status:** Ready for planning

<domain>
## Task Boundary

Audit all 29 MCP tools for AI-effectiveness (naming, scoping, descriptions, output richness, pagination) and document design principles. Produce an audit report and codify principles for v1.1 improvements.

</domain>

<decisions>
## Implementation Decisions

### Where to document design principles
- Create a Claude skill (`.claude/skills/`) for MCP tool design principles so they're enforced during development
- Add a "Design Philosophy" section to README.md for external visibility
- Both locations should be consistent but README is user-facing, skill is dev-facing

### Audit output format
- Themed narrative grouping findings by category (naming, output richness, pagination, etc.)
- Plus a summary table at the end with per-tool status for quick reference as a v1.1 checklist

### Scope of changes
- Document only — no implementation changes to tools in this task
- Findings will inform separate v1.1 tasks for actual tool improvements

### Claude's Discretion
- Audit criteria and scoring approach
- Organization of themed sections in the report

</decisions>

<specifics>
## Specific Ideas

- Tools should include nested references by default (e.g., during disassembly) so AI agents have context for next steps
- AI agents are "lazy" — tool outputs should preemptively provide enough context to prevent hallucination and reduce round-trips
- Any tool with unbounded output must have pagination — this is a hard rule
- Tool descriptions should express scenarios/use cases, not just what the tool does
- Balance: rich output vs. not flooding the context window

</specifics>
