---
description: "Install JD.AI and connect to Claude Code, GitHub Copilot, or Ollama in minutes."
---

# Getting Started

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- At least one AI provider configured (see below)

## Installation

Install JD.AI as a global .NET tool:

```bash
dotnet tool install --global JD.AI
```

To update to the latest version:

```bash
dotnet tool update --global JD.AI
```

## First run

Launch JD.AI in any project directory:

```bash
cd /path/to/your/project
jdai
```

On startup, JD.AI:
1. Checks for available AI providers
2. Displays detected providers and models
3. Selects the best available provider
4. Loads project instructions (JDAI.md, CLAUDE.md, etc.)
5. Shows the welcome banner

![JD.AI startup showing provider detection](../images/demo-startup.png)

## Provider setup

You need at least one AI provider. JD.AI auto-detects all available providers.

### Claude Code

1. Install the Claude Code CLI:
   ```bash
   npm install -g @anthropic-ai/claude-code
   ```
2. Authenticate:
   ```bash
   claude auth login
   ```
3. JD.AI detects the session automatically on next launch.

### GitHub Copilot

1. Authenticate via GitHub CLI:
   ```bash
   gh auth login --scopes copilot
   ```
   Or sign in through the VS Code GitHub Copilot extension.
2. JD.AI detects available Copilot models automatically.

### Ollama (local, free)

1. Install Ollama from [ollama.com](https://ollama.com)
2. Start the server:
   ```bash
   ollama serve
   ```
3. Pull a chat model:
   ```bash
   ollama pull llama3.2
   ```
4. Optionally pull an embedding model for semantic memory:
   ```bash
   ollama pull all-minilm
   ```

## Switching providers and models

```
/providers     # List all detected providers with status
/provider      # Show current provider and model
/models        # List all available models across providers
/model <name>  # Switch to a specific model
```

## CLI options

| Flag | Description |
|------|-------------|
| `--resume <id>` | Resume a previous session by ID |
| `--new` | Start a fresh session |
| `--force-update-check` | Force NuGet update check |
| `--dangerously-skip-permissions` | Skip all tool confirmations |

## What's next

- [Quickstart](quickstart.md) — Walk through your first real task
- [Best Practices](best-practices.md) — Tips for effective prompting
- [Commands Reference](commands-reference.md) — All 20 slash commands
- [Providers](providers.md) — Detailed provider documentation
