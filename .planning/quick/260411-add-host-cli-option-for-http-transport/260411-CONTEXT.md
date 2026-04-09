---
name: Quick Task 260411 Context — Add --host CLI option
description: Locked implementation decisions for the --host / --port CLI flags for HTTP transport
type: quick-task-context
---

# Quick Task 260411: Add --host CLI option for HTTP transport — Context

**Gathered:** 2026-04-09
**Status:** Ready for planning

<domain>
## Task Boundary

Add `--host <value>` and `--port <value>` CLI options that resolve to the HTTP transport's bind address, without breaking existing `--transport`, env, or config behavior. The feature only takes effect in HTTP mode; in stdio mode, providing either flag is an error.

**In scope:**
- New CLI flags `--host` and `--port` mapped to `Transport:Http:Host` / `Transport:Http:Port`
- Precedence: CLI > env (`Transport__Http__Host` / `Transport__Http__Port`) > appsettings.json > defaults (`0.0.0.0` / `3001`)
- Fail-fast rejection when `--host` or `--port` is used with stdio transport
- Unit tests for the resolution helper
- README update: add CLI columns for host and port, add usage examples

**Out of scope:**
- Changing `--transport` resolution logic
- Changing env var or config key names
- Custom host-string validation (Kestrel handles bind-time errors)
- New CLI framework or dependencies (no System.CommandLine, no Spectre.Console)
- `--urls` style ASP.NET Core flag
- Authentication, TLS, or other security changes
</domain>

<decisions>
## Implementation Decisions

### Implementation approach: switch mappings, not hand-rolled parsing

Use `IConfigurationBuilder.AddCommandLine(args, switchMappings)` with:
```csharp
var switchMappings = new Dictionary<string, string>
{
    { "--host", "Transport:Http:Host" },
    { "--port", "Transport:Http:Port" },
};
```

This provider is **added after** `WebApplication.CreateBuilder(args)` so it wins over the default command-line provider, env provider, and appsettings.json provider — giving the required precedence chain for free.

Rationale:
- `--transport` is hand-rolled because it runs before the builder exists (selects `WebApplication` vs `Host`). `--host`/`--port` run after the builder, so the framework's own provider is the right tool.
- Framework handles "missing value after flag" with a clear `FormatException`; no manual branch needed.
- Adding `--port` parity costs one dictionary entry, so it's **in scope**, not optional.

### Custom host validation: dropped

No new validation layer for host strings. Kestrel throws `InvalidOperationException` at bind time (`app.Run($"http://{host}:{port}")`) with a precise message for malformed hosts or conflicts. A second validator would be arbitrary and could reject hosts Kestrel would accept.

### Stdio-mode behavior: reject with clear error

If `--transport stdio` (explicit or resolved) AND `args` contains `--host` or `--port`, fail fast before building the host:
- Write a clear error to `Console.Error` (e.g., `"--host is only valid with --transport http"`)
- Exit with non-zero code

Check is a simple `args.Contains("--host") || args.Contains("--port")` in the stdio branch. Fail-fast prevents silent misconfiguration when users flip transport via env var and forget the flags no longer apply.

### Test seam: static local/helper in Program.cs

Extract HTTP binding resolution into a static helper (local function or private static method) in `Program.cs`, e.g.:

```csharp
static (string host, int port) ResolveHttpBinding(IConfiguration configuration)
// OR operating on args + environment directly if that's cleaner
```

Unit-test the helper in a new `Tests/Program/HttpBindingResolutionTests.cs` (or similar) using a synthetic `IConfiguration` built from in-memory providers. Do **not** launch processes.

Rationale: there are currently zero Program.cs tests. Extracting a helper is the smallest seam that enables real unit coverage of the precedence chain without mocking frameworks or integration test infrastructure.

### Test coverage (what must be proven)

1. `--host` overrides env and config in HTTP mode
2. `--port` overrides env and config in HTTP mode
3. Both flags together produce the expected binding
4. Without any CLI flag, env wins over config
5. Without env or CLI flag, config wins over default
6. Defaults are `0.0.0.0` / `3001` when nothing is set
7. Stdio mode + `--host` → error, non-zero exit (process-level or argument-precheck test)
8. Stdio mode + `--port` → error, non-zero exit

(7) and (8) may be tested at the arg-precheck layer without launching the full host.

### README update scope

Update the "Changing port and host" table at README.md:1419 to fill in the CLI column:
- Row 2 (Port): CLI = `--port <value>`
- Row 3 (Host): CLI = `--host <value>`

Add usage examples under the HTTP server section:
```bash
dotnet run -- --transport http --host 127.0.0.1
dotnet run -- --transport http --host 127.0.0.1 --port 8080
```

Add one line clarifying that the CLI flags only apply in HTTP mode and will error in stdio mode.

### Commit scope: one atomic commit

Single commit containing:
- `Program.cs` changes (switch mappings + stdio-mode reject + helper extraction)
- New test file(s)
- `README.md` updates

### Claude's Discretion

- Exact helper name and signature (`ResolveHttpBinding`, `TryResolveHttpBinding`, or similar)
- Exact error wording for stdio-mode rejection — must be clear and mention the offending flag
- Exact test class name and file location under `Tests/`
- Whether to also list `--host` / `--port` in the Configuration Reference table at README.md:1486 (yes if it fits the existing table shape)
</decisions>

<specifics>
## Specific References

- `Program.cs:15-42` — existing `--transport` hand-rolled resolution (stays as-is)
- `Program.cs:44-76` — HTTP branch; switch mappings inserted here after `CreateBuilder(args)`, before reading `host`/`port`
- `Program.cs:67-69` — current `builder.Configuration.GetValue<...>` calls; these pick up the new CLI values automatically once the switch-mapped provider is added
- `Program.cs:74` — startup log already uses resolved host/port, no change
- `Program.cs:77-101` — stdio branch; precheck for `--host`/`--port` added at the top of this branch
- `README.md:1417-1425` — "Changing port and host" section to update
- `README.md:1484-1494` — Configuration Reference table, possibly extended with CLI column

No specific references beyond the files above — the change is localized.
</specifics>
