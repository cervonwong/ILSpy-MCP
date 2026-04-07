---
phase: quick
plan: 260407-kzu
type: execute
wave: 1
depends_on: []
files_modified: [README.md]
autonomous: true
requirements: []

must_haves:
  truths:
    - "Reader knows what ILSpy MCP is within 10 seconds"
    - "Reader can install via pre-built binary following numbered steps"
    - "Reader can install via dotnet tool following numbered steps"
    - "Reader can configure their MCP client following numbered steps"
    - "Transport modes, tools table, and config reference are findable but not dominant"
  artifacts:
    - path: "README.md"
      provides: "Rewritten README with numbered install/use instructions"
      min_lines: 150
  key_links: []
---

<objective>
Rewrite README.md with clear numbered install and use instructions, optimized for AI tool users who want to get running fast.

Purpose: Current README has good content but buries the quickest install path (pre-built binary) below dotnet tool, lacks numbered steps, and mixes config details into the main flow. Rewrite puts the simplest path first and uses numbered steps throughout.
Output: README.md fully rewritten
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@README.md
@.planning/quick/260407-kzu-rewrite-readme-with-clear-numbered-insta/260407-kzu-CONTEXT.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Rewrite README.md</name>
  <files>README.md</files>
  <action>
Rewrite README.md with the following structure and content. Use the existing README as source material for facts (tool names, config vars, platform files, etc.) but restructure completely.

**Target audience:** AI tool users adding .NET decompilation to Claude Code, Cursor, or Claude Desktop. May not be deeply technical.

**Tone:** Direct, concise, action-oriented. No filler paragraphs.

## Full Structure

### 1. Title and one-line description
- `# ILSpy MCP Server`
- One sentence: MCP server that gives AI assistants .NET decompilation and analysis capabilities via ILSpy.

### 2. What is this? (2-3 sentences max)
- What it does: lets AI assistants decompile, inspect, and analyze .NET assemblies
- How: integrates ILSpy as an MCP server
- Link to ILSpy repo

### 3. Install (numbered steps, tabbed or sectioned by method)

**Option A: Pre-built Binary (Recommended — no .NET required)**

1. Download the latest release for your platform from [Releases](https://github.com/gentledepp/ILSpy-Mcp/releases)
   - Platform table (Windows x64, Linux x64, Linux ARM64, macOS x64, macOS ARM64 with filenames)
2. Extract the archive
   - Show platform-specific extract commands (powershell for Windows, tar for Linux, unzip for macOS)
3. Continue to "Configure Your MCP Client" below

**Option B: dotnet tool**

1. Run `dotnet tool install -g ILSpyMcp.Server`
2. (Update: `dotnet tool update -g ILSpyMcp.Server`)
3. Continue to "Configure Your MCP Client" below

**Option C: Build from Source**
- Brief: clone, `dotnet build`, point to output binary. Keep to 3-4 lines. Or link to CONTRIBUTING.md if it exists.

### 4. Configure Your MCP Client (numbered steps)

Present as: "Pick your client and follow the steps."

**Claude Code:**
1. Run: `claude mcp add ilspy-mcp --command "ilspy-mcp" --scope user`
   - Or if using pre-built binary: `claude mcp add ilspy-mcp --command "/path/to/ILSpy.Mcp" --scope user`
2. Done — restart Claude Code

**Cursor:**
1. Add to MCP settings JSON (show the JSON snippet with `"command": "ilspy-mcp"`)
2. For pre-built binary, use full path to ILSpy.Mcp executable

**Claude Desktop:**
1. Add to `claude_desktop_config.json` (show JSON snippet)
2. For pre-built binary, use full path to ILSpy.Mcp executable

### 5. Usage Examples
- Keep the existing natural-language examples (decompile a type, list types, find method, analyze hierarchy, search members)
- Present as a short list, each with a one-line prompt example

### 6. Available Tools
- Keep the existing tools table exactly (8 tools with descriptions)

### 7. Transport Modes (collapsible or clearly secondary)
Use a `<details>` collapsible section with summary "Transport Modes (stdio / HTTP)".
- Stdio is default, no config needed
- HTTP mode: `ilspy-mcp --transport http`, default `http://0.0.0.0:3001`
- Config priority table (CLI > env var > appsettings.json)
- HTTP client config JSON snippet
- Security note about no auth

### 8. Configuration Reference (collapsible)
Use a `<details>` collapsible section with summary "Configuration Reference".
- List all env vars with defaults (ILSpy__MaxDecompilationSize, ILSpy__DefaultTimeoutSeconds, ILSpy__MaxConcurrentOperations, ILSPY_TRANSPORT, Transport__Http__Port, Transport__Http__Host)

### 9. Architecture (brief, 3-4 lines)
- Keep the 4-layer description (Domain, Application, Infrastructure, Transport)
- One sentence about read-only security model

### 10. License
- MIT with link to LICENSE

**Key rules:**
- Every multi-step process uses numbered steps (1, 2, 3...)
- Pre-built binary install path comes FIRST (it's the simplest)
- No duplicate JSON config blocks — each client shown once
- SHA256 checksum note stays (one sentence near the download step)
- Do NOT add emojis
  </action>
  <verify>
    <automated>node -e "const fs=require('fs'); const r=fs.readFileSync('README.md','utf8'); const lines=r.split('\n'); console.log('Lines:',lines.length); const hasNumbered=r.match(/^\d+\./gm); console.log('Numbered steps:',hasNumbered?hasNumbered.length:0); const hasPrebuiltFirst=r.indexOf('Pre-built')&lt;r.indexOf('dotnet tool'); console.log('Pre-built before dotnet:',hasPrebuiltFirst); process.exit(lines.length>100 &amp;&amp; hasNumbered &amp;&amp; hasNumbered.length>=8 &amp;&amp; hasPrebuiltFirst ? 0 : 1)"</automated>
  </verify>
  <done>README.md is fully rewritten with numbered install steps, pre-built binary as primary path, clear configure-your-client section, and advanced config in collapsible sections. All factual content from original preserved.</done>
</task>

</tasks>

<verification>
- README.md exists and has 150+ lines
- Numbered steps appear in Install, Configure, and any multi-step section
- Pre-built binary section appears before dotnet tool section
- All 8 tools listed in tools table
- Transport and config reference present (in collapsible sections)
</verification>

<success_criteria>
- A new user can go from zero to working MCP server in under 5 minutes following the numbered steps
- Pre-built binary is the first and most prominent install path
- Advanced configuration does not clutter the main flow
</success_criteria>

<output>
After completion, create `.planning/quick/260407-kzu-rewrite-readme-with-clear-numbered-insta/260407-kzu-SUMMARY.md`
</output>
