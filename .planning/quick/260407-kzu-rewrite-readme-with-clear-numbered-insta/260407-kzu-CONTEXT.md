# Quick Task 260407-kzu: Rewrite README - Context

**Gathered:** 2026-04-07
**Status:** Ready for planning

<domain>
## Task Boundary

Full rewrite of README.md with clear numbered instructions, optimized for AI tool users who want to install and use the MCP server quickly.

</domain>

<decisions>
## Implementation Decisions

### Target audience
- **AI tool users**: People adding .NET decompilation to their AI assistant (Claude Code, Cursor, Claude Desktop). May not be deeply technical.

### Install paths ordering
- **Pre-built binary first**: Lead with download-and-run (simplest, no .NET needed). Then dotnet tool. Source build last or in a Contributing section.

### Section structure
- **Full rewrite**: Restructure the entire README for clarity. Numbered steps throughout, cleaner flow from install -> configure -> use.

### Claude's Discretion
- Exact section ordering and headings
- Whether to use collapsible sections for advanced config
- Tone and wording choices

</decisions>

<specifics>
## Specific Ideas

- Numbered step-by-step instructions for each install method
- Clear flow: What is it -> Install -> Configure MCP client -> Use
- Pre-built binary = primary/first install path (no .NET required)
- Keep transport modes docs (stdio/HTTP) but integrate naturally
- Tools table and reference info should stay but not dominate

</specifics>
