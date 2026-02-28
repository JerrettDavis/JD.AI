# JD.AI

AI-powered terminal assistant and multi-channel AI platform built on [Semantic Kernel](https://github.com/microsoft/semantic-kernel) — like Claude Code, but .NET. Includes a TUI client, gateway control plane, six channel adapters, and a plugin SDK across 12 projects.

## Architecture

```
┌─────────────────────────────────────┐
│           TUI Client (jdai)         │
└──────────────────┬──────────────────┘
                   │
┌──────────────────▼──────────────────┐
│     Gateway Control Plane           │
│   (REST API + SignalR Hubs)         │
└──────────────────┬──────────────────┘
                   │
┌──────────────────▼──────────────────┐
│      Agent Pool + Routing           │
│  (Single / Multi-turn / Teams)      │
└───────┬─────────────────┬───────────┘
        │                 │
┌───────▼───────┐ ┌───────▼───────────┐
│   Channel     │ │  Memory /         │
│   Adapters    │ │  Embeddings       │
│ (6 channels)  │ │  (SQLite vectors) │
└───────────────┘ └───────────────────┘
        │
┌───────▼───────────────────────────┐
│         Plugin System             │
│  (SDK + extensible tools/hooks)   │
└───────────────────────────────────┘
```

## Features

- **Multi-provider support** — Claude Code, GitHub Copilot, and Ollama out of the box
- **Developer tools** — File, Git, Shell, Search, and Web tools with permission management
- **Semantic memory** — SQLite-backed vector memory with conversation recall
- **Context compaction** — Automatic conversation summarization to stay within context limits
- **Subagent orchestration** — Single-turn, multi-turn, and team-based agent execution
- **Session persistence** — Full conversation history with resume, replay, and export
- **Interactive TUI** — Spectre.Console rendering with streaming, thinking display, and tab-completion

## Install

```bash
dotnet tool install -g JD.AI
```

## Usage

```bash
jdai
```

## Providers

| Provider | Detection | Auth |
|----------|-----------|------|
| Claude Code | `claude` CLI | OAuth via Claude CLI |
| GitHub Copilot | VS Code extension | Device flow |
| Ollama | HTTP localhost:11434 | None (local) |

## Commands

Type `/help` in the TUI for a full list of slash commands.

## Gateway Control Plane

The gateway (`JD.AI.Gateway`) is an ASP.NET Core service that exposes a REST API and SignalR hubs for real-time agent communication. It handles authentication, rate limiting, channel management, and plugin orchestration.

See [docs/articles/gateway-api.md](docs/articles/gateway-api.md) for endpoint reference and configuration.

## Channel Adapters

| Channel | Description |
|---------|-------------|
| **Discord** | Guild channels, DMs, and threads via Discord.Net |
| **Signal** | Encrypted messaging via signal-cli JSON-RPC bridge |
| **Slack** | Workspace integration using SlackNet Socket Mode |
| **Telegram** | Private chats, groups, and inline commands via Telegram.Bot SDK |
| **WebChat** | Browser-based chat using SignalR |
| **OpenClaw** | Cross-gateway message forwarding to external OpenClaw instances |

See [docs/articles/channels.md](docs/articles/channels.md) for setup and configuration.

## Plugin SDK

The Plugin SDK (`JD.AI.Plugins.SDK`) defines the contract for extensible plugins — custom functions, tools, hooks, and configuration. Build and register plugins to extend agent capabilities without modifying the core.

See [docs/articles/plugins.md](docs/articles/plugins.md) for the plugin development guide.

## OpenClaw Integration

OpenClaw enables cross-gateway orchestration by forwarding messages between JD.AI instances and external OpenClaw gateways over HTTP. This allows federated agent collaboration across deployments.

See [docs/articles/openclaw-integration.md](docs/articles/openclaw-integration.md) for details.

## Project Structure

```
src/
├── JD.AI                    # TUI client (dotnet tool)
├── JD.AI.Core               # Shared: agents, providers, sessions, tools, memory, event bus
├── JD.AI.Daemon             # Background service host (Windows/Linux)
├── JD.AI.Gateway            # ASP.NET Core control plane (REST + SignalR)
├── JD.AI.Plugins.SDK        # Plugin interface library
├── JD.AI.Channels.Discord   # Discord adapter
├── JD.AI.Channels.Signal    # Signal adapter
├── JD.AI.Channels.Slack     # Slack adapter
├── JD.AI.Channels.Telegram  # Telegram adapter
├── JD.AI.Channels.Web       # WebChat adapter
└── JD.AI.Channels.OpenClaw  # OpenClaw bridge adapter
tests/
├── JD.AI.Tests              # Unit tests
├── JD.AI.Gateway.Tests      # Gateway unit tests
└── JD.AI.IntegrationTests   # Integration tests
```

## Dependencies

- [JD.SemanticKernel.Extensions](https://github.com/JerrettDavis/JD.SemanticKernel.Extensions) — Compaction, memory, skills, hooks
- [JD.SemanticKernel.Connectors.ClaudeCode](https://www.nuget.org/packages/JD.SemanticKernel.Connectors.ClaudeCode)
- [JD.SemanticKernel.Connectors.GitHubCopilot](https://www.nuget.org/packages/JD.SemanticKernel.Connectors.GitHubCopilot)

## License

MIT
