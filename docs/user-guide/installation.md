---
title: Installation
description: Install JD.AI, configure prerequisites, and run it for the first time.
---

# Installation

Get JD.AI up and running on your machine. This page covers prerequisites, installation methods, first run, and basic CLI options. For provider-specific setup, see [Provider Setup](provider-setup.md).

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- At least one AI provider configured (see [Provider Setup](provider-setup.md))

Verify your .NET version:

```bash
dotnet --version
# Should output 10.0.x or later
```

## Install as a global .NET tool

The recommended way to install JD.AI is as a global .NET tool from NuGet:

```bash
dotnet tool install --global JD.AI
```

This makes the `jdai` command available system-wide.

### Update to the latest version

```bash
dotnet tool update --global JD.AI
```

You can also check for updates from inside JD.AI with the `/update` command.

### Install to a local path

If you prefer not to install globally, use `--tool-path`:

```bash
dotnet tool install JD.AI --tool-path ./tools
./tools/jdai
```

## Install from source

Clone the repository and build locally:

```bash
git clone https://github.com/JerrettDavis/JD.AI.git
cd JD.AI
dotnet build
dotnet run --project src/JD.AI
```

## First run

Launch JD.AI in any project directory:

```bash
cd /path/to/your/project
jdai
```

On startup, JD.AI:

1. Checks for available AI providers
2. Displays detected providers and models
3. Selects the best available provider
4. Loads project instructions (`JDAI.md`, `CLAUDE.md`, etc.)
5. Shows the welcome banner

If no providers are detected, JD.AI will prompt you to configure one. The quickest way is to set an API key environment variable:

```bash
export OPENAI_API_KEY=sk-...
jdai --provider openai
```

See [Provider Setup](provider-setup.md) for all 15 providers.

## Common CLI options

| Flag | Description |
|------|-------------|
| `--provider <name>` | Use a specific provider (e.g. `openai`, `anthropic`) |
| `--model <name>` | Use a specific model |
| `--resume <id>` | Resume a previous session by ID |
| `--continue` | Continue the most recent session |
| `--new` | Start a fresh session |
| `--print` | Non-interactive mode — print the response and exit |
| `--output-format <fmt>` | Output format: `text`, `json`, or `markdown` |
| `--verbose` | Enable debug logging |
| `--dangerously-skip-permissions` | Skip all tool confirmations |

For the full list, run `jdai --help`.

## Verify the installation

After installing, confirm everything works:

```bash
jdai --print "hello, are you working?"
```

JD.AI should detect a provider, send the prompt, and print a response.

## Uninstall

```bash
dotnet tool uninstall --global JD.AI
```

## Next steps

- [Quickstart](quickstart.md) — walk through your first real task
- [Provider Setup](provider-setup.md) — configure AI providers
- [Configuration](configuration.md) — customize JD.AI for your projects
