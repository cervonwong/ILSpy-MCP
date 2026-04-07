# ILSpy MCP Server

MCP server that gives AI assistants .NET decompilation and static analysis capabilities via [ILSpy](https://github.com/icsharpcode/ILSpy).

## What is this?

ILSpy MCP Server lets AI assistants decompile, inspect, and analyze .NET assemblies through natural language. It integrates [ILSpy](https://github.com/icsharpcode/ILSpy) as a Model Context Protocol (MCP) server, so tools like Claude Code, Cursor, and Claude Desktop can perform .NET reverse engineering directly.

## Install

### Option A: Pre-built Binary (Recommended -- no .NET required)

1. Download the latest release for your platform from [Releases](https://github.com/cervonwong/ILSpy-MCP/releases):

   | Platform | File |
   |----------|------|
   | Windows x64 | `ilspy-mcp-win-x64.zip` |
   | Linux x64 | `ilspy-mcp-linux-x64.tar.gz` |
   | Linux ARM64 | `ilspy-mcp-linux-arm64.tar.gz` |
   | macOS x64 | `ilspy-mcp-osx-x64.zip` |
   | macOS ARM64 | `ilspy-mcp-osx-arm64.zip` |

   Each release includes SHA256 checksums (`.sha256` files) for verifying download integrity.

2. Extract the archive:

   **Windows (PowerShell):**
   ```powershell
   Expand-Archive ilspy-mcp-win-x64.zip -DestinationPath ilspy-mcp
   ```

   **Linux:**
   ```bash
   tar -xzf ilspy-mcp-linux-x64.tar.gz -C ilspy-mcp
   chmod +x ilspy-mcp/ILSpy.Mcp
   ```

   **macOS:**
   ```bash
   unzip ilspy-mcp-osx-arm64.zip -d ilspy-mcp
   chmod +x ilspy-mcp/ILSpy.Mcp
   ```

3. Continue to [Configure Your MCP Client](#configure-your-mcp-client) below.

### Option B: dotnet tool

1. Install as a global tool:
   ```bash
   dotnet tool install -g ILSpyMcp.Server
   ```

2. To update later:
   ```bash
   dotnet tool update -g ILSpyMcp.Server
   ```

3. Continue to [Configure Your MCP Client](#configure-your-mcp-client) below.

### Option C: Build from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/cervonwong/ILSpy-MCP.git
   cd ILSpy-Mcp
   ```

2. Build the project:
   ```bash
   dotnet build
   ```

3. The binary is at `src/ILSpy.Mcp/bin/Debug/net10.0/ILSpy.Mcp`. Use this path when configuring your MCP client below.

## Configure Your MCP Client

Pick your client and follow the steps.

### Claude Code

1. Run:
   ```bash
   claude mcp add ilspy-mcp --command "ilspy-mcp" --scope user
   ```
   If using a pre-built binary, use the full path instead:
   ```bash
   claude mcp add ilspy-mcp --command "/path/to/ilspy-mcp/ILSpy.Mcp" --scope user
   ```

2. Restart Claude Code. The tools are now available.

### Cursor

1. Add to your MCP settings JSON:
   ```json
   {
     "mcpServers": {
       "ilspy-mcp": {
         "command": "ilspy-mcp",
         "args": []
       }
     }
   }
   ```
   For pre-built binary, replace `"ilspy-mcp"` with the full path to the `ILSpy.Mcp` executable.

2. Restart Cursor.

### Claude Desktop

1. Add to `claude_desktop_config.json`:
   ```json
   {
     "mcpServers": {
       "ilspy-mcp": {
         "command": "ilspy-mcp",
         "args": []
       }
     }
   }
   ```
   For pre-built binary, replace `"ilspy-mcp"` with the full path to the `ILSpy.Mcp` executable.

2. Restart Claude Desktop.

## Usage Examples

Once configured, ask your AI assistant to work with .NET assemblies using natural language:

- **Decompile a type** -- "Decompile the String class from /path/to/System.Runtime.dll"
- **List all types** -- "List all types in the assembly /path/to/MyLibrary.dll"
- **Find a method** -- "Find the CalculateTotal method in /path/to/Calculator.dll"
- **Analyze type hierarchy** -- "Show me the type hierarchy for ProductService in /path/to/ECommerce.dll"
- **Search members** -- "Search for members containing 'Authenticate' in /path/to/Auth.dll"

## Available Tools

| Tool | Description |
|------|-------------|
| `decompile_type` | Decompile and analyze a .NET type from a DLL |
| `decompile_method` | Decompile and analyze a specific method |
| `list_assembly_types` | List all types in an assembly |
| `analyze_assembly` | Get architectural overview of an assembly |
| `get_type_members` | Get complete API surface of a type |
| `find_type_hierarchy` | Find inheritance relationships |
| `search_members_by_name` | Search for members by name |
| `find_extension_methods` | Find extension methods for a type |

<details>
<summary>Transport Modes (stdio / HTTP)</summary>

### Stdio (Default)

The default mode. The MCP client launches and communicates with the server via stdin/stdout. No additional configuration needed.

```bash
ilspy-mcp
```

### HTTP

HTTP mode runs the server as a standalone HTTP service, useful for remote access or running in a VM.

```bash
ilspy-mcp --transport http
```

By default, the HTTP server listens on `http://0.0.0.0:3001`.

**Configuration priority** (highest first):

| Priority | Method | Example |
|----------|--------|---------|
| 1 | CLI argument | `--transport http` |
| 2 | Environment variable | `ILSPY_TRANSPORT=http` |
| 3 | appsettings.json | `"Transport": { "Type": "http" }` |

Port and host are configurable:

| Setting | Default | Env Variable | appsettings.json Path |
|---------|---------|--------------|----------------------|
| Port | 3001 | `Transport__Http__Port` | `Transport:Http:Port` |
| Host | 0.0.0.0 | `Transport__Http__Host` | `Transport:Http:Host` |

**MCP client configuration for HTTP:**

```json
{
  "mcpServers": {
    "ilspy-mcp": {
      "type": "http",
      "url": "http://localhost:3001/"
    }
  }
}
```

**Note:** No authentication is applied. Rely on network-level security (firewall rules, VM networking) to control access.

</details>

<details>
<summary>Configuration Reference</summary>

The server can be configured via environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `ILSpy__MaxDecompilationSize` | 1048576 (1 MB) | Maximum size of decompiled code in bytes |
| `ILSpy__DefaultTimeoutSeconds` | 30 | Default timeout for operations in seconds |
| `ILSpy__MaxConcurrentOperations` | 10 | Maximum number of concurrent operations |
| `ILSPY_TRANSPORT` | `stdio` | Transport mode: `stdio` or `http` |
| `Transport__Http__Port` | 3001 | HTTP server port |
| `Transport__Http__Host` | 0.0.0.0 | HTTP server bind address |

</details>

## Architecture

The server follows a clean layered architecture: **Domain** (core entities), **Application** (use cases), **Infrastructure** (ILSpy and file system adapters), and **Transport** (MCP protocol layer). All operations are read-only -- the server never modifies files on disk.

## License

MIT -- see [LICENSE](LICENSE) for details.
