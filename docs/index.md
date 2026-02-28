---
_layout: landing
---

# JD.AI

An AI-powered terminal assistant built on Microsoft Semantic Kernel. Connect to Claude Code, GitHub Copilot, or Ollama and get an intelligent coding agent right in your terminal.

## Get started

```bash
dotnet tool install --global JD.AI
cd your-project
jdai
```

That's it. JD.AI auto-detects your AI providers and you're ready to code.

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

## Features

| Feature | Description |
|---------|-------------|
| **Multi-provider** | [Claude Code, GitHub Copilot, Ollama](articles/providers.md) — auto-detected |
| **20 slash commands** | [Model switching, sessions, context management](articles/commands-reference.md) |
| **8 tool categories** | [Files, search, shell, git, web, memory, subagents](articles/tools-reference.md) |
| **5 subagent types** | [Explore, task, plan, review, general](articles/subagents.md) |
| **Team orchestration** | [Sequential, fan-out, supervisor, debate](articles/orchestration.md) |
| **Session persistence** | [Save, load, export conversations](articles/persistence.md) |
| **Project instructions** | [JDAI.md for project-specific context](articles/configuration.md) |
| **Git checkpointing** | [Safe rollback with stash/directory/commit strategies](articles/checkpointing.md) |
| **Skills & plugins** | [Claude Code skills/plugins/hooks integration](articles/skills-and-plugins.md) |
| **Auto-update** | Check and apply updates via NuGet |

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
- [Commands Reference](articles/commands-reference.md) — All 20 slash commands
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
