---
_layout: landing
description: "AI-powered terminal assistant built on Microsoft Semantic Kernel. 14 AI providers, 17 tool categories, 35+ slash commands, subagent swarms, team orchestration, and 6 channel adapters."
---

# JD.AI

An AI-powered terminal assistant and multi-channel platform built on Microsoft Semantic Kernel. Connect to 14 AI providers — from cloud APIs to fully local models — and get a complete coding agent in your terminal.

[Get started](user-guide/installation.md) ·
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
| **Multi-provider** | [14 AI providers](user-guide/provider-setup.md) including Claude Code, Copilot, Ollama, OpenAI, Anthropic, Azure, Gemini, Bedrock, Foundry Local, and more |
| **35+ slash commands** | [Model switching, sessions, context, workflows, defaults](user-guide/commands.md) |
| **17 tool categories** | [Files, search, shell, git, web, memory, subagents, think, tasks, code execution, clipboard, questions, diff/patch, batch edit, usage tracking](user-guide/tools.md) |
| **Tool loadouts** | [Curated tool bundles](developer-guide/tool-loadouts.md) — expose only the tools each agent needs; 5 built-ins (minimal, developer, research, devops, full) |
| **5 subagent types** | [Explore, task, plan, review, general-purpose](developer-guide/subagents.md) |
| **4 orchestration strategies** | [Sequential, fan-out, supervisor, debate](developer-guide/orchestration.md) team coordination |
| **6 channel adapters** | [Discord, Signal, Slack, Telegram, Web, OpenClaw](developer-guide/channels.md) |
| **Session persistence** | [Save, load, export conversations](user-guide/sessions.md) across sessions |
| **MCP integration** | [Connect external tool servers](developer-guide/mcp-integration.md) via Model Context Protocol |
| **Workflows** | [Composable multi-step automation](developer-guide/workflows.md) with /workflow commands |
| **Observability** | [OpenTelemetry tracing, metrics, health checks](operations/observability.md) |
| **Git checkpointing** | [Safe rollback with stash/directory/commit](user-guide/checkpointing.md) strategies |
| **Skills & plugins** | [Native managed skills/plugins/hooks lifecycle](developer-guide/plugins.md) |
| **Auto-update** | Check and apply updates via NuGet from your terminal |

![Subagent execution and team orchestration](images/demo-orchestration.png)

## Documentation

### [User Guide](user-guide/index.md) — Using JD.AI

For anyone using `jdai` as a tool. Installation, provider setup, commands, workflows, and best practices.

- [Installation](user-guide/installation.md) — Get JD.AI running in minutes
- [Quickstart](user-guide/quickstart.md) — Your first task, step by step
- [Provider Setup](user-guide/provider-setup.md) — Configure any of the 14 providers
- [Commands](user-guide/commands.md) — All 35+ slash commands at a glance
- [Common Workflows](user-guide/common-workflows.md) — Bug fixing, refactoring, testing, PRs
- [Best Practices](user-guide/best-practices.md) — Effective prompting and context management

### [Developer Guide](developer-guide/index.md) — Extending & integrating

For developers building on JD.AI. Architecture, custom tools, providers, plugins, and the gateway API.

- [Architecture Overview](developer-guide/index.md) — How JD.AI is built
- [Custom Tools](developer-guide/custom-tools.md) — Write your own Semantic Kernel tools
- [Tool Loadouts](developer-guide/tool-loadouts.md) — Curated tool bundles for agents and workflows
- [Custom Providers](developer-guide/custom-providers.md) — Add new AI providers
- [Plugin SDK](developer-guide/plugins.md) — Build distributable plugins
- [Gateway API](developer-guide/gateway-api.md) — REST & SignalR integration

### [Operations](operations/index.md) — Deploy, monitor, govern

For ops teams and administrators. Deployment, observability, security, and enterprise governance.

- [Service Deployment](operations/deployment.md) — Windows Service, systemd, containers
- [Observability](operations/observability.md) — OpenTelemetry, health checks, Kubernetes
- [Security & Credentials](operations/security.md) — Encrypted storage, API key management
- [Enterprise Governance](operations/governance.md) — Usage limits, policies, compliance

### [Reference](reference/index.md) — Quick lookup

Complete reference material for CLI flags, commands, tools, providers, and configuration.

- [CLI Reference](reference/cli.md) — All flags, environment variables, exit codes
- [Commands Reference](reference/commands.md) — Full 35+ command documentation
- [Tools Reference](reference/tools.md) — All 17 tool categories with parameters
- [Providers Reference](reference/providers.md) — Comparison tables and capabilities
- [API Reference](https://jerrettdavis.github.io/JD.AI/api/) — Generated API documentation
