---
_layout: landing
description: "AI-powered terminal assistant built on Microsoft Semantic Kernel. Multi-provider support for Claude Code, GitHub Copilot, and Ollama with tool execution, subagent swarms, and team orchestration."
---

# JD.AI

An AI-powered terminal assistant built on Microsoft Semantic Kernel. Connect to Claude Code, GitHub Copilot, or Ollama — and get a full coding agent in your terminal.

[Get started](articles/getting-started.md) ·
[View on GitHub](https://github.com/JerrettDavis/JD.AI) ·
[NuGet package](https://www.nuget.org/packages/JD.AI)

![JD.AI terminal session](images/demo-startup.png)

## Install in one command

```bash
dotnet tool install --global JD.AI
jdai
```

JD.AI auto-detects available providers. No manual configuration needed.

## What you can do

| Task | Example prompt |
|------|---------------|
| **Explore codebases** | `what does this project do?` |
| **Fix bugs** | `the login fails after timeout, fix it and verify with tests` |
| **Refactor code** | `refactor the auth module to use async/await` |
| **Write tests** | `write unit tests for the Calculator.Divide method` |
| **Create commits** | `commit my changes with a descriptive message` |
| **Spawn subagents** | `use an explore agent to find how caching works` |
| **Orchestrate teams** | `use a debate team to discuss microservices vs monolith` |
| **Search the web** | `what are the latest features in .NET 10?` |

![Chat with streaming responses](images/demo-chat.png)

## Features at a glance

| Feature | Description |
|---------|-------------|
| **Multi-provider** | [Claude Code, GitHub Copilot, Ollama, OpenAI Codex, Local Models](articles/providers.md) — auto-detected on startup |
| **25+ slash commands** | [Model switching, sessions, context management](articles/commands-reference.md) |
| **13 tool categories** | [Files, search, shell, git, web, memory, subagents, think, environment, tasks, code execution, clipboard, questions](articles/tools-reference.md) |
| **5 subagent types** | [Explore, task, plan, review, general-purpose](articles/subagents.md) |
| **Team orchestration** | [Sequential, fan-out, supervisor, debate](articles/orchestration.md) strategies |
| **Session persistence** | [Save, load, export conversations](articles/persistence.md) across sessions |
| **Project instructions** | [JDAI.md for project-specific context](articles/configuration.md) |
| **Git checkpointing** | [Safe rollback with stash/directory/commit](articles/checkpointing.md) strategies |
| **Skills & plugins** | [Claude Code skills/plugins/hooks](articles/skills-and-plugins.md) integration |
| **Auto-update** | Check and apply updates via NuGet from your terminal |

![Subagent execution and team orchestration](images/demo-orchestration.png)

## Documentation

### Getting started

- [Overview](articles/overview.md) — What JD.AI is and what it offers
- [Getting Started](articles/getting-started.md) — Installation and provider setup
- [Quickstart](articles/quickstart.md) — Your first task, step by step

### Using JD.AI

- [Best Practices](articles/best-practices.md) — Tips for effective prompting and context management
- [Common Workflows](articles/common-workflows.md) — Bug fixing, refactoring, testing, PRs
- [Configuration](articles/configuration.md) — JDAI.md and project settings

### Reference

- [Tools Reference](articles/tools-reference.md) — All tools with parameters and examples
- [Commands Reference](articles/commands-reference.md) — All 25+ slash commands
- [CLI Reference](articles/cli-reference.md) — Flags, environment variables, exit codes
- [AI Providers](articles/providers.md) — Provider setup and comparison

### Advanced

- [Subagents](articles/subagents.md) — Specialized AI instances for scoped tasks
- [Team Orchestration](articles/orchestration.md) — Multi-agent coordination strategies
- [Skills, Plugins, and Hooks](articles/skills-and-plugins.md) — Extension system
- [Extending JD.AI](articles/extending.md) — Writing custom tools and providers

### Support

- [Troubleshooting](articles/troubleshooting.md) — Common issues and solutions
- [API Reference](api/) — Generated API documentation
