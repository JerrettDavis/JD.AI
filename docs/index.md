---
_layout: landing
---

# JD.AI

An AI-powered terminal assistant built on Microsoft Semantic Kernel.

## Overview

JD.AI is an interactive TUI agent that supports multiple AI providers (Claude Code, GitHub Copilot, Ollama) with features like streaming responses, tool execution, slash commands, session persistence, subagent orchestration, and more.

## Installation

```bash
dotnet tool install --global JD.AI
```

## Quick Start

```bash
# Launch the TUI
jdai

# Use with a specific provider
jdai --provider ollama --model llama3.2
```

## Features

| Feature | Description |
|---------|-------------|
| Multi-provider | Claude Code, GitHub Copilot, Ollama |
| Streaming | Real-time token streaming with thinking blocks |
| Tools | File operations, shell commands, web search |
| Slash commands | /help, /model, /provider, /compact, /save, /load |
| Subagents | Spawn specialized agents for explore, task, plan, review |
| Team orchestration | Sequential, fan-out, supervisor, debate strategies |
| Session persistence | Save and restore conversation history |
| Clipboard support | Paste text blocks, images, and files |

## Getting Started

- [Articles](articles/) — Guides and usage documentation
- [API Reference](api/) — Full API documentation
