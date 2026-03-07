# Windows Desktop Automation

JD.AI supports full Windows desktop automation through the [Windows-MCP](https://github.com/CursorTouch/Windows-MCP) integration. This allows any agent running through JD.AI — including those bridged via the OpenClaw channel — to capture screenshots, interact with the mouse and keyboard, manage applications, and control the Windows OS.

## What is Windows-MCP?

[Windows-MCP](https://windowsmcp.io) is an open-source MCP server (2M+ users, MIT license) from CursorTouch that gives AI agents native control over Windows. It runs as a local stdio MCP server via `uvx windows-mcp` and exposes 19 tools covering the full desktop automation surface.

## Available Tools

| Tool | Description |
|------|-------------|
| `Snapshot` | Capture the full desktop state: screenshot + list of open windows and interactive elements |
| `Click` | Click the mouse at screen coordinates (left, right, double) |
| `Type` | Type text at coordinates; optionally clear existing content |
| `Scroll` | Scroll vertically or horizontally at coordinates |
| `Move` | Move mouse cursor; supports drag-and-drop |
| `Shortcut` | Execute keyboard shortcuts (e.g. `ctrl+c`, `alt+f4`, `win+d`) |
| `App` | Launch, resize, or close applications |
| `Shell` | Execute PowerShell commands |
| `File` | Read, write, copy, move, delete files |
| `Scrape` | Fetch web page content (HTTP or active browser DOM) |
| `Wait` | Pause execution for UI to settle |
| `MultiSelect` | Select multiple items (Ctrl+click) |
| `MultiEdit` | Enter text into multiple fields at once |
| `Clipboard` | Get or set clipboard content |
| `Process` | List, start, or kill processes |
| `SystemInfo` | CPU, memory, disk, network, uptime |
| `Notification` | Send a Windows toast notification |
| `LockScreen` | Lock the workstation |
| `Registry` | Read or write Windows Registry values |

## Configuration

Windows-MCP is registered in JD.AI's MCP configuration at `~/.jdai/jdai.mcp.json`:

```json
{
  "mcpServers": {
    "windows-mcp": {
      "command": "C:\\Users\\<user>\\.local\\bin\\uvx.exe",
      "args": ["windows-mcp"]
    }
  }
}
```

It is also registered in Claude Desktop's config (`%APPDATA%\Claude\claude_desktop_config.json`) so it is available to Claude Code (OpenClaw) sessions directly.

> **Tip:** Use the full path to `uvx.exe` (from `where uvx` in a terminal) rather than `uvx` alone. Electron-based apps like Claude Desktop do not always inherit `PATH`.

## Usage via JD.AI CLI

You can verify discovery with:

```shell
jdai mcp list
```

`windows-mcp` should appear with status `enabled`.

## Usage via OpenClaw Bridge

When working through the OpenClaw/Claude Code bridge, all 19 windows-mcp tools are available automatically. Ask the agent to:

- **Capture the screen:** *"Take a screenshot of the desktop and describe what you see."*
- **List open windows:** *"What windows are currently open?"*
- **Interact with an app:** *"Click on the Start button and open Notepad."*
- **Type text:** *"Focus the search bar and type 'hello world'."*
- **Run a command:** *"Open PowerShell and run `Get-Process | Sort-Object CPU -Descending | Select-Object -First 10`."*

The `Snapshot` tool returns both a screenshot image and a structured list of UI elements, so vision-capable models (Claude Sonnet/Opus) can fully understand what's on screen.

## Prerequisites

- Windows 10 or 11
- [uv](https://astral.sh/uv) package manager (`pip install uv`)
- `uvx` in PATH (installed alongside `uv`)

No additional Python installation is required — `uvx` manages its own isolated Python environment for `windows-mcp`.

## Security Considerations

Windows-MCP gives agents full control over your desktop. Use it only with trusted models and sessions. The agent can:

- Read and write files anywhere on disk
- Execute arbitrary PowerShell commands
- Control any running application
- Lock the screen or modify the registry

Follow least-privilege principles: only enable this MCP for sessions where desktop automation is explicitly needed.
