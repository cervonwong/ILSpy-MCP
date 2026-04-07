# ILSpy MCP Server

A Model Context Protocol (MCP) server that provides .NET assembly decompilation and analysis capabilities.

## What is this?

ILSpy MCP Server enables AI assistants (like Claude Code, Cursor) to decompile and analyze .NET assemblies directly through natural language commands. It integrates [ILSpy](https://github.com/icsharpcode/ILSpy) to provide powerful reverse-engineering capabilities.

## Quick Start

### Prerequisites

- .NET 9.0 SDK or higher
- MCP-compatible client (Claude Code, Cursor, Claude Desktop, etc.)

### Installation

Install as a global dotnet tool from NuGet:

```bash
dotnet tool install -g ILSpyMcp.Server
```

To update to the latest version:

```bash
dotnet tool update -g ILSpyMcp.Server
```

### Configure MCP Client

For **Claude Code**, register the MCP server:

```bash
claude mcp add ilspy-mcp --command "ilspy-mcp" --scope user
```

Or create/update `.mcp.json` in your project root:

```json
{
  "mcpServers": {
    "ilspy-mcp": {
      "type": "stdio",
      "command": "ilspy-mcp",
      "args": []
    }
  }
}
```

For **Cursor**, add to your MCP settings:

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

For **Claude Desktop**, add to `claude_desktop_config.json`:

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

## Transport Modes

ILSpy MCP Server supports two transport modes:

### Stdio (Default)

The default mode. The MCP client launches and communicates with the server process via stdin/stdout. This is the standard mode for local usage.

```bash
ilspy-mcp
```

### HTTP

HTTP mode runs the server as a standalone HTTP service, useful for remote access (e.g., running in a VM and connecting from the host machine).

```bash
ilspy-mcp --transport http
```

By default, the HTTP server listens on `http://0.0.0.0:3001`.

#### Configuration

The transport mode can be set through three layers (highest priority first):

1. **CLI argument**: `--transport http`
2. **Environment variable**: `ILSPY_TRANSPORT=http`
3. **appsettings.json**: Set `"Transport": { "Type": "http" }`

Port and host are configurable:

| Setting | Default | Env Variable | appsettings.json Path |
|---------|---------|--------------|----------------------|
| Port | 3001 | `Transport__Http__Port` | `Transport:Http:Port` |
| Host | 0.0.0.0 | `Transport__Http__Host` | `Transport:Http:Host` |

#### MCP Client Configuration (HTTP)

To connect an MCP client to the HTTP server, configure it to use the Streamable HTTP endpoint:

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

> **Note:** No authentication is applied. Rely on network-level security (firewall rules, VM networking) to control access.

## Usage Examples

### Decompile a Type
```
Decompile the String class from /path/to/System.Runtime.dll
```

### List All Types
```
List all types in the assembly /path/to/MyLibrary.dll
```

### Find a Specific Method
```
Find the CalculateTotal method in /path/to/Calculator.dll
```

### Analyze Type Hierarchy
```
Show me the type hierarchy for ProductService in /path/to/ECommerce.dll
```

### Search Members
```
Search for members containing "Authenticate" in /path/to/Auth.dll
```

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

## Configuration

The server can be configured via environment variables:

- `ILSpy__MaxDecompilationSize`: Maximum size of decompiled code in bytes (default: 1048576 = 1 MB)
- `ILSpy__DefaultTimeoutSeconds`: Default timeout for operations in seconds (default: 30)
- `ILSpy__MaxConcurrentOperations`: Maximum number of concurrent operations (default: 10)
- `ILSPY_TRANSPORT`: Transport mode — `stdio` (default) or `http`
- `Transport__Http__Port`: HTTP server port (default: 3001)
- `Transport__Http__Host`: HTTP server bind address (default: 0.0.0.0)

## Architecture

This server follows a clean architecture with clear separation of concerns:

- **Domain**: Core business logic and entities
- **Application**: Use cases and application services
- **Infrastructure**: External system adapters (ILSpy, file system)
- **Transport**: MCP protocol layer

## Security

- All operations are read-only (no file modifications)
- Assembly path validation
- Timeout and cancellation support
- Request context propagation

## License

MIT — see [LICENSE](LICENSE) for details.
