# Getting Started with JD.AI

## Prerequisites

- .NET 10.0 SDK or later
- At least one AI provider configured:
  - **Claude Code**: Install and authenticate via `claude auth login`
  - **GitHub Copilot**: Authenticate via VS Code or `gh auth login`
  - **Ollama**: Install and run `ollama serve`

## Installation

Install JD.AI as a global .NET tool:

```bash
dotnet tool install --global JD.AI
```

## First Run

Launch the TUI:

```bash
jdai
```

JD.AI will automatically detect available providers and select the best one.

## Slash Commands

| Command | Description |
|---------|-------------|
| `/help` | Show available commands |
| `/model <name>` | Switch AI model |
| `/provider <name>` | Switch AI provider |
| `/compact` | Compact conversation history |
| `/save [path]` | Save current session |
| `/load [path]` | Load a saved session |
| `/quit` | Exit the application |
