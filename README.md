# JD.AI

AI-powered terminal assistant built on [Semantic Kernel](https://github.com/microsoft/semantic-kernel) — like Claude Code, but .NET.

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

## Dependencies

- [JD.SemanticKernel.Extensions](https://github.com/JerrettDavis/JD.SemanticKernel.Extensions) — Compaction, memory, skills, hooks
- [JD.SemanticKernel.Connectors.ClaudeCode](https://www.nuget.org/packages/JD.SemanticKernel.Connectors.ClaudeCode)
- [JD.SemanticKernel.Connectors.GitHubCopilot](https://www.nuget.org/packages/JD.SemanticKernel.Connectors.GitHubCopilot)

## License

MIT
