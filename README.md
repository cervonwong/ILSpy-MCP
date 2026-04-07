# ILSpy MCP Server

MCP server that gives AI assistants .NET decompilation and static analysis capabilities via [ILSpy](https://github.com/icsharpcode/ILSpy).

Ask your favourite AI Chatbot to explain how to use ILSpy MCP Server: [![ChatGPT](https://img.shields.io/badge/ChatGPT-Read%20README-74aa9c?logo=openai&logoColor=white)](https://chatgpt.com/?q=Read%20https%3A%2F%2Fgithub.com%2Fcervonwong%2FILSpy-MCP%2Fblob%2Fmain%2FREADME.md)
[![Claude](https://img.shields.io/badge/Claude-Read%20README-d4a27f?logo=claude&logoColor=white)](https://claude.ai/new?q=Read%20https%3A%2F%2Fgithub.com%2Fcervonwong%2FILSpy-MCP%2Fblob%2Fmain%2FREADME.md)

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

## How It Works

You don't interact with ILSpy MCP directly. Your MCP client (Claude Code, Cursor, etc.) launches and communicates with it automatically in the background.

To analyze a .NET assembly, just **mention the full path to the DLL** in your chat. The AI assistant will call the appropriate ILSpy tools for you.

## Usage Examples

Ask your AI assistant to work with .NET assemblies using natural language. Replace the paths with actual DLL paths on your machine:

- **Decompile a type** -- "Decompile the String class from `C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.0\System.Runtime.dll`"
- **List all types** -- "List all types in `C:\Users\me\projects\MyApp\bin\Debug\net10.0\MyLibrary.dll`"
- **Find a method** -- "Find the CalculateTotal method in `D:\libs\Calculator.dll`"
- **Decompile a constructor** -- "Decompile the .ctor constructor of UserService in `C:\repos\MyApp\bin\Debug\net10.0\MyApp.dll`"
- **Analyze type hierarchy** -- "Show me the type hierarchy for ProductService in `C:\repos\ECommerce\bin\Release\net10.0\ECommerce.dll`"
- **Search members** -- "Search for members containing 'Authenticate' in `C:\repos\AuthService\bin\Debug\net10.0\Auth.dll`"

## Available Tools

| Tool | Description |
|------|-------------|
| `decompile_type` | Decompile and analyze a .NET type from a DLL |
| `decompile_method` | Decompile and analyze a specific method or constructor (`.ctor`/`.cctor`) |
| `list_assembly_types` | List all types in an assembly |
| `analyze_assembly` | Get architectural overview of an assembly |
| `get_type_members` | Get complete API surface of a type (constructors, methods, properties, fields, events) |
| `find_type_hierarchy` | Find inheritance relationships |
| `search_members_by_name` | Search for members by name |
| `find_extension_methods` | Find extension methods for a type |

## Running as an HTTP Server (Remote / VM)

By default the server uses **stdio** — the MCP client launches it and communicates via stdin/stdout. For remote access (e.g. running on an analysis VM while the MCP client runs on your workstation), switch to **HTTP mode**.

### Starting the HTTP server

Pick whichever matches how you installed it:

**Pre-built binary:**
```bash
# From the directory where you extracted the release archive
./ILSpy.Mcp --transport http        # Linux / macOS
.\ILSpy.Mcp.exe --transport http    # Windows
```

**dotnet tool:**
```bash
ilspy-mcp --transport http
```

**From source:**
```bash
# From the repo root (where ILSpy.Mcp.csproj lives)
dotnet run -- --transport http
```

The server starts and prints:
```
ILSpy MCP server listening on http://0.0.0.0:3001
```

It stays running in the foreground until you stop it (Ctrl+C).

### Connecting your MCP client

From the machine running your AI assistant, point the MCP client at the server's HTTP endpoint. Replace `analysis-vm` with the server's hostname or IP:

**Claude Code:**
```bash
claude mcp add ilspy-mcp --transport http http://<analysis-vm>:3001/mcp --scope user    # available across all your projects
claude mcp add ilspy-mcp --transport http http://<analysis-vm>:3001/mcp --scope project # shared with team via .mcp.json in repo
claude mcp add ilspy-mcp --transport http http://<analysis-vm>:3001/mcp --scope local   # current project only (default)
```

**Claude Desktop / Cursor (MCP settings JSON):**
```json
{
  "mcpServers": {
    "ilspy-mcp": {
      "type": "http",
      "url": "http://<analysis-vm>:3001/mcp"
    }
  }
}
```

### Changing port and host

| Setting | Default | CLI | Env Variable | appsettings.json |
|---------|---------|-----|--------------|------------------|
| Transport | stdio | `--transport http` | `ILSPY_TRANSPORT=http` | `Transport:Type` |
| Port | 3001 | — | `Transport__Http__Port` | `Transport:Http:Port` |
| Host | 0.0.0.0 | — | `Transport__Http__Host` | `Transport:Http:Host` |

Transport mode is resolved in priority order: CLI arg > env var > appsettings.json.

To make HTTP the permanent default, edit `appsettings.json` (located next to the binary):
```json
{
  "Transport": {
    "Type": "http",
    "Http": {
      "Port": 3001,
      "Host": "0.0.0.0"
    }
  }
}
```

### Running as a background service

To keep the server running after you disconnect from the VM:

**Linux (systemd):**
```bash
# /etc/systemd/system/ilspy-mcp.service
[Unit]
Description=ILSpy MCP Server
After=network.target

[Service]
ExecStart=/path/to/ILSpy.Mcp --transport http
Restart=on-failure
User=youruser

[Install]
WantedBy=multi-user.target
```
```bash
sudo systemctl enable --now ilspy-mcp
```

**Windows (Task Scheduler or sc.exe):**
```powershell
# Quick background run with nohup equivalent
Start-Process -NoNewWindow -FilePath .\ILSpy.Mcp.exe -ArgumentList "--transport http"
```

**Docker / tmux / screen** also work — the server is a single self-contained binary with no external dependencies.

### Security

No authentication is built in. The server binds to `0.0.0.0` (all interfaces) by default. Protect it with:
- Firewall rules limiting access to trusted IPs
- A reverse proxy (nginx, Caddy) adding TLS and/or auth
- VPN or SSH tunnel between client and server
- Binding to `127.0.0.1` and using SSH port forwarding

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

## Acknowledgements

Forked from [bivex/ILSpy-Mcp](https://github.com/bivex/ILSpy-Mcp).

## License

MIT -- see [LICENSE](LICENSE) for details.
