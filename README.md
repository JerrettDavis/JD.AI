# JD.AI

[![CI](https://github.com/JerrettDavis/JD.AI/actions/workflows/ci.yml/badge.svg)](https://github.com/JerrettDavis/JD.AI/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/JerrettDavis/JD.AI/graph/badge.svg)](https://codecov.io/gh/JerrettDavis/JD.AI)
[![NuGet](https://img.shields.io/nuget/v/JD.AI.svg)](https://www.nuget.org/packages/JD.AI)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)

AI-powered terminal assistant and multi-channel platform built on [Semantic Kernel](https://github.com/microsoft/semantic-kernel). 14 AI providers, 17 tool categories, 33+ slash commands, MCP server integration, workflow engine, subagent orchestration, team strategies, and six channel adapters — across 18 projects with 772+ tests.

![JD.AI terminal startup](docs/images/demo-startup.png)
![JD.AI dashboard overview](docs/images/dashboard/dashboard-overview.png)

## Architecture

```
┌──────────────────────────────────────────────┐
│              TUI Client (jdai)               │
│  33+ slash commands · model search · sessions│
└──────────────────────┬───────────────────────┘
                       │
┌──────────────────────▼───────────────────────┐
│         Gateway Control Plane                │
│       (REST API + SignalR Hubs)              │
└──────┬────────────┬────────────┬─────────────┘
       │            │            │
┌──────▼──────┐ ┌───▼──────┐ ┌──▼─────────────┐
│  Telemetry  │ │ Workflows│ │  MCP Servers    │
│ OpenTelemetry│ │  Engine  │ │ (add/list/rm)  │
│ tracing,    │ │ run/list │ │                 │
│ metrics,    │ │ /refine  │ │                 │
│ health      │ │          │ │                 │
└─────────────┘ └──────────┘ └─────────────────┘
       │
┌──────▼──────────────────────────────────────┐
│     Agent Pool + Routing                    │
│  5 subagent types · 4 team strategies       │
│  ConversationTransformer (5 modes)          │
└──────┬──────────────────┬───────────────────┘
       │                  │
┌──────▼──────┐   ┌───────▼───────────┐
│  Channel    │   │  Memory /         │
│  Adapters   │   │  Embeddings       │
│ (6 channels)│   │  (SQLite vectors) │
└─────────────┘   └───────────────────┘
       │
┌──────▼──────────────────────────────────────┐
│   14 AI Providers · 17 Tool Categories      │
│   Plugin SDK · Credential Store · Sessions  │
└─────────────────────────────────────────────┘
```

## Features

| Area | Details |
|------|---------|
| **AI Providers** | 14 providers — OAuth, API key, local, and AWS SDK auth (see [table below](#providers)) |
| **Tools** | 17 categories: File, Search, Shell, Git, Web, Web Search, Memory, Subagent, Think, Environment, Tasks, Code Execution, Clipboard, Questions, Diff & Patch, Batch Edit, Usage Tracking |
| **Slash Commands** | 33+ commands for model management, sessions, providers, workflows, MCP, diagnostics, and more |
| **Subagents** | 5 types: Explore, Task, Plan, Review, General-purpose |
| **Team Orchestration** | 4 strategies: Sequential, Fan-Out, Supervisor, Debate |
| **MCP Integration** | `/mcp add`, `/mcp list`, `/mcp remove` — connect external tool servers |
| **Workflows** | `/workflow run`, `/workflow list`, `/workflow refine` — composable multi-step automation |
| **Dynamic Switching** | 5-mode ConversationTransformer (Preserve, Compact, Transform, Fresh, Cancel) with fork points |
| **Model Search** | `/model search` across Ollama, HuggingFace, and Foundry Local remote catalogs |
| **Session Persistence** | SQLite-backed history, model switch tracking, fork points; `--continue`/`--resume` restores model state |
| **Git Checkpointing** | Stash, directory, and commit strategies for safe rollback |
| **Credentials** | Encrypted credential store (DPAPI/AES); `/provider add` wizard |
| **Global Defaults** | `~/.jdai/config.json` with per-project overrides via AtomicConfigStore |
| **Observability** | OpenTelemetry tracing, metrics, and health checks (JD.AI.Telemetry) |
| **Dashboard** | Blazor WebAssembly dashboard with MudBlazor UI (JD.AI.Dashboard.Wasm) |
| **Interactive TUI** | Spectre.Console rendering with streaming, thinking display, and tab-completion |

## Quick Start

```bash
dotnet tool install -g JD.AI   # Install globally
jdai                            # Launch the TUI
/provider add                   # Add an AI provider
/help                           # List all commands
```

## Providers

### OAuth / Local (zero API key)

| Provider | Auth | Notes |
|----------|------|-------|
| Claude Code | OAuth (claude CLI) | Requires `claude` installed |
| GitHub Copilot | OAuth (device flow) | Uses VS Code Copilot token |
| OpenAI Codex | OAuth | Codex CLI authentication |
| Ollama | None (local) | HTTP `localhost:11434` |
| Local Models / LLamaSharp | None (local) | Load `.gguf` files directly |
| Microsoft Foundry Local | None (local) | Microsoft Foundry runtime |

### API Key

| Provider | Auth | Notes |
|----------|------|-------|
| OpenAI | API key | GPT-4o, o1, o3, etc. |
| Azure OpenAI | API key | Azure-hosted OpenAI deployments |
| Anthropic | API key | Claude 3.5/4 via Anthropic API |
| Google Gemini | API key | Gemini 2.x models |
| Mistral | API key | Mistral / Mixtral models |
| HuggingFace | API key | Inference API models |
| OpenAI-Compatible | API key | Groq, Together, DeepSeek, OpenRouter, Fireworks, Perplexity |

### AWS SDK

| Provider | Auth | Notes |
|----------|------|-------|
| AWS Bedrock | AWS SDK credentials | Claude, Titan, Llama via Bedrock |

## Key Commands

| Command | Description |
|---------|-------------|
| `/help` | List all available commands |
| `/model` | Show or switch the active model |
| `/model search <query>` | Search Ollama, HuggingFace, Foundry Local catalogs |
| `/models` | List available models for the current provider |
| `/provider` | Show or switch the active provider |
| `/providers` | List all configured providers |
| `/provider add` | Interactive provider setup wizard |
| `/default provider <name>` | Set default provider |
| `/default model <name>` | Set default model |
| `/compact` | Compact conversation context |
| `/save` | Save the current session |
| `/sessions` | List saved sessions |
| `/resume` | Resume a saved session (restores model state) |
| `/fork` | Fork conversation at the current point |
| `/cost` | Show token usage and estimated cost |
| `/mcp add <url>` | Register an MCP tool server |
| `/workflow run <name>` | Execute a named workflow |
| `/autorun` | Toggle automatic tool execution |
| `/doctor` | Run environment diagnostics |
| `/export` | Export conversation to file |

Run `/help` in the TUI for the complete list.

## Gateway & Channels

The **Gateway** (`JD.AI.Gateway`) is an ASP.NET Core control plane exposing REST endpoints and SignalR hubs for real-time agent communication, authentication, rate limiting, and plugin orchestration.

Six channel adapters connect the agent pool to external platforms:

| Channel | Technology |
|---------|------------|
| Discord | Discord.Net — guilds, DMs, threads |
| Signal | signal-cli JSON-RPC bridge |
| Slack | SlackNet Socket Mode |
| Telegram | Telegram.Bot SDK |
| WebChat | SignalR browser client |
| OpenClaw | Cross-gateway HTTP forwarding |

See the [Gateway API docs](docs/developer-guide/gateway-api.md) and [Channel Adapters guide](docs/developer-guide/channels.md).

## Project Structure

```
src/
├── JD.AI                      # TUI client (dotnet tool)
├── JD.AI.Core                 # Agents, providers, sessions, tools, memory, event bus
├── JD.AI.Gateway              # ASP.NET Core control plane (REST + SignalR)
├── JD.AI.Daemon               # Background service host (Windows/Linux)
├── JD.AI.Plugins.SDK          # Plugin interface library
├── JD.AI.Workflows            # Workflow engine (run/list/refine)
├── JD.AI.Telemetry            # OpenTelemetry tracing, metrics, health checks
├── JD.AI.Dashboard.Wasm       # Blazor WASM dashboard (MudBlazor)
├── JD.AI.Channels.Discord     # Discord adapter
├── JD.AI.Channels.Signal      # Signal adapter
├── JD.AI.Channels.Slack       # Slack adapter
├── JD.AI.Channels.Telegram    # Telegram adapter
├── JD.AI.Channels.Web         # WebChat adapter
└── JD.AI.Channels.OpenClaw    # OpenClaw bridge adapter
tests/
├── JD.AI.Tests                # Core unit tests
├── JD.AI.Gateway.Tests        # Gateway unit tests
├── JD.AI.Workflows.Tests      # Workflow engine tests
└── JD.AI.IntegrationTests     # End-to-end integration tests
```

## Dependencies

| Package | Purpose |
|---------|---------|
| [JD.SemanticKernel.Extensions](https://github.com/JerrettDavis/JD.SemanticKernel.Extensions) | Compaction, memory, skills, hooks |
| [JD.SemanticKernel.Extensions.Mcp](https://www.nuget.org/packages/JD.SemanticKernel.Extensions.Mcp) | MCP server integration |
| [JD.SemanticKernel.Connectors.ClaudeCode](https://www.nuget.org/packages/JD.SemanticKernel.Connectors.ClaudeCode) | Claude Code OAuth connector |
| [JD.SemanticKernel.Connectors.GitHubCopilot](https://www.nuget.org/packages/JD.SemanticKernel.Connectors.GitHubCopilot) | GitHub Copilot OAuth connector |
| [JD.SemanticKernel.Connectors.OpenAICodex](https://www.nuget.org/packages/JD.SemanticKernel.Connectors.OpenAICodex) | OpenAI Codex OAuth connector |
| [Microsoft.SemanticKernel](https://www.nuget.org/packages/Microsoft.SemanticKernel) | Core AI orchestration (v1.72.0) |
| [LLamaSharp](https://www.nuget.org/packages/LLamaSharp) | Local GGUF model inference |
| [OpenTelemetry](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting) | Distributed tracing and metrics |
| [MudBlazor](https://www.nuget.org/packages/MudBlazor) | Dashboard UI components |

## Documentation

Full documentation is built with [docfx](https://dotnet.github.io/docfx/) and published from the `docs/` directory.

- [API Reference](docs/api/) — Auto-generated from XML doc comments
- [User Guide](docs/user-guide/) — Installation, commands, workflows, best practices
- [Developer Guide](docs/developer-guide/) — Architecture, custom tools, plugins, gateway API
- [Operations](docs/operations/) — Deployment, observability, security, governance
- [Reference](docs/reference/) — CLI flags, commands, tools, providers, environment variables

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines. The project uses central package management, Nerdbank.GitVersioning, and enforces code style via Meziantou.Analyzer.

## License

[MIT](LICENSE) © JD
