---
title: "MCP Integration"
description: "Model Context Protocol (MCP) server integration — connecting to MCP servers, configuration, writing MCP-compatible tool servers, and security."
---

# MCP Integration

JD.AI integrates with [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) servers to extend its tool capabilities. MCP is an open standard that allows AI assistants to connect to external tool servers over a standardized protocol.

## What MCP provides

MCP servers expose tools, resources, and prompts that JD.AI can discover and use alongside its built-in tools. This enables:

- **External service integration** — connect to databases, APIs, cloud services
- **Shared tooling** — use the same MCP server across multiple AI assistants
- **Community tools** — leverage the growing MCP ecosystem

## How JD.AI connects to MCP servers

The `McpManager` in `src/JD.AI.Core/Mcp/` aggregates MCP servers from multiple discovery sources:

```
McpManager
  ├── Claude Code MCP config      (~/.claude/mcp.json)
  ├── Claude Desktop config       (~/.claude/claude_desktop_config.json)
  ├── VS Code MCP config          (.vscode/mcp.json)
  ├── Codex MCP config            (~/.codex/mcp.json)
  ├── Copilot MCP config          (~/.github/copilot/mcp.json)
  └── JD.AI managed config        (~/.jdai/jdai.mcp.json)
```

### Discovery flow

1. On startup, `McpManager` queries all registered `IMcpDiscoveryProvider` instances
2. Each provider returns `McpServerDefinition` objects describing available servers
3. JD.AI connects to discovered servers and registers their tools as SK functions
4. Server connection state is tracked via `McpConnectionState`

```csharp
public class McpManager
{
    public async Task<IReadOnlyList<McpServerDefinition>> GetAllServersAsync(
        CancellationToken ct = default);

    public McpServerStatus GetStatus(string serverName);
}
```

## MCP commands

Manage MCP servers interactively:

| Command | Description |
|---------|-------------|
| `/mcp list` | List all discovered MCP servers and their status |
| `/mcp add <name>` | Add a new MCP server configuration |
| `/mcp remove <name>` | Remove an MCP server |

### Adding an MCP server

```text
> /mcp add github
Enter server command: npx -y @modelcontextprotocol/server-github
Enter environment variables (KEY=VALUE, empty to finish):
  GITHUB_TOKEN=ghp_xxxxxxxxxxxx

Added MCP server "github" to ~/.jdai/jdai.mcp.json
```

## Configuration

### JD.AI managed config (`~/.jdai/jdai.mcp.json`)

```json
{
  "mcpServers": {
    "github": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github"],
      "env": {
        "GITHUB_TOKEN": "ghp_xxxxxxxxxxxx"
      }
    },
    "filesystem": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/home/user/projects"]
    },
    "sqlite": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sqlite", "--db-path", "./data.db"]
    }
  }
}
```

### McpServerDefinition

```csharp
public record McpServerDefinition
{
    public string Name { get; init; }
    public string Command { get; init; }
    public IReadOnlyList<string>? Args { get; init; }
    public IReadOnlyDictionary<string, string>? Env { get; init; }
    public string? Source { get; init; }  // Which config file it came from
}
```

### Server status tracking

```csharp
public record McpServerStatus(
    string ServerName,
    McpConnectionState State,    // Disconnected, Connecting, Connected, Error
    int ToolCount,
    string? ErrorMessage);
```

## Writing an MCP-compatible tool server

MCP servers can be written in any language. Here's a minimal C# example using stdio transport:

```csharp
using System.Text.Json;

// MCP server that provides a "current_time" tool
var input = Console.OpenStandardInput();
var output = Console.OpenStandardOutput();

while (true)
{
    var request = await JsonSerializer.DeserializeAsync<McpRequest>(input);

    if (request?.Method == "tools/list")
    {
        await RespondAsync(output, request.Id, new
        {
            tools = new[]
            {
                new
                {
                    name = "current_time",
                    description = "Get the current UTC time",
                    inputSchema = new { type = "object", properties = new { } }
                }
            }
        });
    }
    else if (request?.Method == "tools/call" && request.Params?.Name == "current_time")
    {
        await RespondAsync(output, request.Id, new
        {
            content = new[] { new { type = "text", text = DateTime.UtcNow.ToString("O") } }
        });
    }
}
```

For production MCP servers, use the official SDKs:

- **TypeScript:** `@modelcontextprotocol/sdk`
- **Python:** `mcp`
- **C#:** Community packages available on NuGet

## JD.AI's MCP discovery providers

The `JdAiMcpDiscoveryProvider` manages JD.AI's own server configurations:

```csharp
public class JdAiMcpDiscoveryProvider : IMcpDiscoveryProvider
{
    // Reads from ~/.jdai/jdai.mcp.json
    public async Task<IReadOnlyList<McpServerDefinition>> DiscoverAsync(
        CancellationToken ct = default);

    // Writes a new server definition
    public async Task PersistAsync(
        McpServerDefinition definition,
        CancellationToken ct = default);
}
```

Other discovery providers read from their respective config files (Claude Code, VS Code, etc.) without modifying them.

## Security considerations

### Local-only by default

MCP servers run as **local processes** on the same machine as JD.AI. There is no remote server support by default. This limits the attack surface to:

- Local process execution
- Environment variable exposure (API keys passed to servers)
- File system access (servers may read/write files)

### Best practices

- **Review server commands** before adding — `/mcp add` shows the exact command that will execute
- **Scope environment variables** — only pass the minimum required credentials to each server
- **Use read-only servers** where possible — filesystem servers should be scoped to specific directories
- **Audit server tools** — use `/mcp list` to review what tools each server provides
- **Isolate sensitive servers** — run servers that access production systems in separate, controlled environments

### Tool confirmation

MCP server tools flow through the same `ToolConfirmationFilter` as built-in tools. Users are prompted before execution unless auto-approved via `/autorun`.

## See also

- [Architecture Overview](index.md) — MCP in the system architecture
- [Custom Tools](custom-tools.md) — built-in tool development
- [Configuration](../user-guide/configuration.md) — JD.AI configuration files
