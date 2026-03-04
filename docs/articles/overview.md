---
description: "What JD.AI is, how it works, and what it offers — an AI-powered terminal assistant with 14 providers, 17 tool categories, and 33+ slash commands built on Microsoft Semantic Kernel."
---

# Overview

JD.AI is an AI-powered terminal assistant built on [Microsoft Semantic Kernel](https://learn.microsoft.com/semantic-kernel/). It brings intelligent code understanding, generation, and project management directly to your command line — with support for multiple AI providers and a rich set of built-in tools.

Open source under the [MIT license](https://github.com/JerrettDavis/JD.AI/blob/main/LICENSE). Available on [NuGet](https://www.nuget.org/packages/JD.AI) and [GitHub](https://github.com/JerrettDavis/JD.AI).

![JD.AI startup screen showing provider detection and welcome banner](../images/demo-startup.png)

## Get started

Install JD.AI as a global .NET tool and launch it:

### [Windows](#tab/windows)

```powershell
dotnet tool install --global JD.AI
jdai
```

### [macOS](#tab/macos)

```bash
dotnet tool install --global JD.AI
jdai
```

### [Linux](#tab/linux)

```bash
dotnet tool install --global JD.AI
jdai
```

---

> [!NOTE]
> Requires .NET 10.0 SDK or later and at least one configured AI provider.

JD.AI automatically detects available providers and selects the best one. Choose from **14 providers** — Claude Code, GitHub Copilot, OpenAI Codex, Ollama, Foundry Local, local GGUF models, OpenAI, Azure OpenAI, Anthropic, Google Gemini, Mistral, AWS Bedrock, HuggingFace, or any OpenAI-compatible endpoint — or switch on the fly with `/provider`.

## What you can do

| Category | Capabilities |
|---|---|
| **Explore** | Navigate codebases, search files, understand architecture |
| **Fix** | Diagnose bugs, apply targeted fixes, validate with tests |
| **Refactor** | Restructure code, rename symbols, extract methods |
| **Test** | Generate unit tests, integration tests, and test fixtures |
| **Collaborate** | Create pull requests, write commit messages, review diffs |
| **Document** | Generate and update READMEs, API docs, and inline comments |
| **Research** | Search the web for libraries, APIs, and best practices |
| **Orchestrate** | Delegate to subagents and coordinate multi-agent teams |

## Core features

JD.AI ships with a broad set of capabilities out of the box:

| Feature | Summary | Learn more |
|---|---|---|
| **14 AI providers** | Claude Code, Copilot, Codex, Ollama, Foundry Local, Local GGUF, OpenAI, Azure OpenAI, Anthropic, Gemini, Mistral, Bedrock, HuggingFace, and OpenAI-compatible endpoints | [Providers](providers.md) |
| **Local model inference** | Run GGUF models in-process via LLamaSharp — fully offline | [Local models](local-models.md) |
| **17 tool categories** | File, search, shell, git, web, web search, memory, subagents, think, environment, tasks, code execution, clipboard, questions, diff/patch, batch edit, and usage tracking | [Tools reference](tools-reference.md) |
| **33+ slash commands** | `/help`, `/model`, `/local`, `/default`, `/spinner`, `/workflow`, and others | [Commands reference](commands-reference.md) |
| **5 subagent types** | Specialized agents for explore, code review, testing, and more | [Subagents](subagents.md) |
| **4 orchestration strategies** | Coordinate teams of agents for complex tasks | [Orchestration](orchestration.md) |
| **Session persistence** | Save and resume conversations across sessions | [Persistence](persistence.md) |
| **Project instructions** | Configure behavior per-project via `JDAI.md` files | [Configuration](configuration.md) |
| **Git checkpointing** | Automatic checkpoints before destructive operations | [Checkpointing](checkpointing.md) |
| **Auto-update** | Stay current via `dotnet tool update --global JD.AI` | — |

## Architecture highlights

JD.AI is organized into **17 projects** (14 `src` + 3 `test`) with **772+ tests**:

| Layer | Description |
|---|---|
| **Core** | Providers, tools, agents, config (`AtomicConfigStore` with `~/.jdai/config.json`), and MCP integration |
| **Telemetry** | OpenTelemetry distributed tracing, metrics, and health checks via `JD.AI.Telemetry` |
| **Workflows** | Replayable, refinable multi-step workflows via `JD.AI.Workflows` |
| **Channels** | 6 channel adapters — Discord, Signal, Slack, Telegram, Web, and OpenClaw |
| **Gateway** | Central HTTP gateway for multi-channel routing and the Blazor dashboard |

**Provider authentication** spans four methods: OAuth (Claude Code, Copilot, Codex), API key (OpenAI, Anthropic, Gemini, Mistral, HuggingFace, OpenAI-compatible), local/file (Ollama, Foundry Local, LLamaSharp GGUF), and AWS SDK (Bedrock). Credentials are resolved through an encrypted secure store (`~/.jdai/credentials/`), configuration files, and environment variables — in that priority order.

**Dynamic model switching** uses `ConversationTransformer` with 5 modes: Preserve (keep full history), Compact (summarize), Transform (handoff briefing), Fresh (clean slate), and Cancel. The `/model search` command searches across Ollama, HuggingFace, and Foundry Local catalogs.

**Global defaults** are managed by `AtomicConfigStore`, persisted to `~/.jdai/config.json`, and configurable via `/default provider`, `/default model`, and per-project overrides.

## Next steps

- **[Quickstart](quickstart.md)** — end-to-end setup in under five minutes
- **[Best practices](best-practices.md)** — tips for writing effective prompts and structuring projects
- **[Troubleshooting](troubleshooting.md)** — common issues and how to resolve them
