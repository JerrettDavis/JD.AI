---
description: "Install JD.AI and connect to Claude Code, GitHub Copilot, OpenAI Codex, Ollama, or local models in minutes."
---

# Getting Started

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- At least one AI provider configured (see below)

## Installation

Install JD.AI as a global .NET tool:

```bash
dotnet tool install --global JD.AI
```

To update to the latest version:

```bash
dotnet tool update --global JD.AI
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
4. Loads project instructions (JDAI.md, CLAUDE.md, etc.)
5. Shows the welcome banner

![JD.AI startup showing provider detection](../images/demo-startup.png)

## Provider setup

You need at least one AI provider. JD.AI auto-detects all available providers.

### Claude Code

1. Install the Claude Code CLI:
   ```bash
   npm install -g @anthropic-ai/claude-code
   ```
2. Authenticate:
   ```bash
   claude auth login
   ```
3. JD.AI detects the session automatically on next launch.

### GitHub Copilot

1. Authenticate via GitHub CLI:
   ```bash
   gh auth login --scopes copilot
   ```
   Or sign in through the VS Code GitHub Copilot extension.
2. JD.AI detects available Copilot models automatically.

### OpenAI Codex

1. Install the Codex CLI:
   ```bash
   npm install -g @openai/codex
   ```
2. Authenticate:
   ```bash
   codex auth login
   ```
   Or set the `OPENAI_API_KEY` environment variable directly.
3. JD.AI detects the session automatically on next launch.

### OpenAI

Use the CLI flag or set an environment variable:

```bash
# CLI flag
jdai --provider openai

# Or set the environment variable
export OPENAI_API_KEY=sk-...
```

When using the CLI flag without an environment variable, JD.AI prompts for the API key on first use and stores it in the encrypted credential store.

### Azure OpenAI

Provide your Azure endpoint and API key:

```bash
jdai --provider azure-openai
```

You will be prompted for:
- **Endpoint** — your Azure OpenAI resource URL (e.g. `https://myresource.openai.azure.com/`)
- **API Key** — your Azure OpenAI key

Both can also be set via the `/provider add` wizard.

### Anthropic

```bash
# CLI flag
jdai --provider anthropic

# Or set the environment variable
export ANTHROPIC_API_KEY=sk-ant-...
```

### Google Gemini

```bash
# CLI flag
jdai --provider google-gemini

# Or set the environment variable
export GOOGLE_AI_API_KEY=...
```

### Mistral

```bash
# CLI flag
jdai --provider mistral

# Or set the environment variable
export MISTRAL_API_KEY=...
```

### AWS Bedrock

```bash
jdai --provider aws-bedrock
```

Uses your standard AWS credentials (environment variables, `~/.aws/credentials`, or IAM role). Ensure `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and `AWS_REGION` are configured.

### HuggingFace

```bash
# CLI flag
jdai --provider huggingface

# Or set the environment variable
export HUGGINGFACE_API_KEY=hf_...
```

### OpenAI-Compatible

Connect to any OpenAI-compatible API (LM Studio, vLLM, text-generation-webui, etc.):

```bash
jdai --provider openai-compatible
```

You will be prompted for:
- **Base URL** — the API endpoint (e.g. `http://localhost:1234/v1`)
- **API Key** — optional, depending on the server

### Ollama (local, free)

1. Install Ollama from [ollama.com](https://ollama.com)
2. Start the server:
   ```bash
   ollama serve
   ```
3. Pull a chat model:
   ```bash
   ollama pull llama3.2
   ```
4. Optionally pull an embedding model for semantic memory:
   ```bash
   ollama pull all-minilm
   ```

### Local models (GGUF, fully standalone)

No external service needed — run GGUF models directly in-process via LLamaSharp:

```bash
jdai --provider local
```

1. Place `.gguf` model files in `~/.jdai/models/` (or any directory).
2. JD.AI detects them automatically on startup.
3. Or use the interactive commands to search and download:
   ```text
   /local search llama 7b
   /local download TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF
   ```

See [Local Models](local-models.md) for the full guide.

### Microsoft Foundry Local

```bash
jdai --provider foundry-local
```

Uses locally available Foundry models. No API key required — models run on-device via the Foundry Local runtime.

## Credential management

JD.AI provides multiple ways to configure and secure provider credentials.

### Interactive setup with `/provider add`

The fastest way to configure a new provider is the interactive wizard:

```text
/provider add
```

The wizard walks you through selecting a provider, entering credentials, and verifying connectivity. Credentials are saved to the encrypted store automatically.

### Encrypted credential store

All credentials saved through the wizard or CLI are stored in an encrypted credential store:

- **Windows** — DPAPI (Data Protection API)
- **macOS / Linux** — AES-256 encryption with a machine-scoped key

Credentials are stored in `~/.jdai/credentials.enc` and are never written in plain text.

### Environment variable alternatives

Every provider supports environment variables as an alternative to the credential store. Environment variables take precedence when both are present.

### Credential resolution chain

When JD.AI needs a credential, it checks sources in this order:

1. **CLI flags** — `--provider`, `--api-key`, etc.
2. **Environment variables** — `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, etc.
3. **Encrypted credential store** — `~/.jdai/credentials.enc`
4. **OAuth / session tokens** — Claude Code, GitHub Copilot, Codex sessions

The first source that provides a valid credential wins.

## Default configuration

### Setting defaults

Use the `/default` command to persist your preferred provider and model:

```text
/default provider openai
/default model gpt-4.1
```

These are saved to `~/.jdai/config.json` and used for all future sessions unless overridden.

### Global config file

`~/.jdai/config.json` stores global defaults:

```json
{
  "defaultProvider": "openai",
  "defaultModel": "gpt-4.1"
}
```

Edit this file directly or use the `/default` commands.

### Per-project configuration

Create a `JDAI.md` file in your repository root to set project-specific instructions, preferred providers, or model overrides. JD.AI reads this file automatically on startup.

## Switching providers and models

```
/providers     # List all detected providers with status
/provider      # Show current provider and model
/models        # List all available models across providers
/model <name>  # Switch to a specific model
```

## CLI options

| Flag | Description |
|------|-------------|
| `--provider <name>` | Use a specific provider (e.g. `openai`, `anthropic`, `azure-openai`) |
| `--model <name>` | Use a specific model |
| `--resume <id>` | Resume a previous session by ID |
| `--continue` | Continue the most recent session |
| `--session-id <id>` | Attach to or create a session with a specific ID |
| `--new` | Start a fresh session |
| `--print` | Run in non-interactive mode and print the response |
| `--output-format <fmt>` | Output format for non-interactive mode (`text`, `json`, `markdown`) |
| `--system-prompt <text>` | Override the system prompt |
| `--append-system-prompt <text>` | Append to the default system prompt |
| `--max-turns <n>` | Limit the number of agentic turns |
| `--verbose` | Enable verbose/debug logging |
| `--force-update-check` | Force NuGet update check |
| `--dangerously-skip-permissions` | Skip all tool confirmations |
| `--gateway` | Start in gateway mode |
| `--gateway-port <port>` | Port for gateway API (default: `5100`) |

## What's next

- [Quickstart](quickstart.md) — Walk through your first real task
- [Interactive Mode](interactive-mode.md) — Prompt controls, keybindings, and streaming interactions
- [Best Practices](best-practices.md) — Tips for effective prompting
- [Commands Reference](commands-reference.md) — All 35+ slash commands
- [Providers](providers.md) — Detailed provider documentation
