# Overview

JD.AI is an AI-powered terminal assistant built on [Microsoft Semantic Kernel](https://learn.microsoft.com/semantic-kernel/). It brings intelligent code understanding, generation, and project management directly to your command line — with support for multiple AI providers and a rich set of built-in tools.

Open source under the [MIT license](https://github.com/JerrettDavis/JD.AI/blob/main/LICENSE). Available on [NuGet](https://www.nuget.org/packages/JD.AI) and [GitHub](https://github.com/JerrettDavis/JD.AI).

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

JD.AI automatically detects available providers and selects the best one. Choose from **Claude Code**, **GitHub Copilot**, or **Ollama** — or switch on the fly with `/provider`.

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
| **Multi-provider support** | Switch between Claude Code, GitHub Copilot, and Ollama | [Providers](providers.md) |
| **8 tool categories** | File I/O, shell, search, git, web, and more | [Tools reference](tools-reference.md) |
| **20 slash commands** | `/help`, `/model`, `/compact`, `/save`, `/load`, and others | [Commands reference](commands-reference.md) |
| **5 subagent types** | Specialized agents for explore, code review, testing, and more | [Subagents](subagents.md) |
| **4 orchestration strategies** | Coordinate teams of agents for complex tasks | [Orchestration](orchestration.md) |
| **Session persistence** | Save and resume conversations across sessions | [Persistence](persistence.md) |
| **Project instructions** | Configure behavior per-project via `JDAI.md` files | [Configuration](configuration.md) |
| **Git checkpointing** | Automatic checkpoints before destructive operations | [Checkpointing](checkpointing.md) |
| **Auto-update** | Stay current via `dotnet tool update --global JD.AI` | — |

## Next steps

- **[Getting started quickstart](getting-started-quickstart.md)** — end-to-end setup in under five minutes
- **[Best practices](best-practices.md)** — tips for writing effective prompts and structuring projects
- **[Troubleshooting](troubleshooting.md)** — common issues and how to resolve them
