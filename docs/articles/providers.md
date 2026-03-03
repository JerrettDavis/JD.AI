---
description: "Connect to Claude Code, GitHub Copilot, OpenAI Codex, Ollama, or run models locally via LLamaSharp — auto-detected on startup with no manual configuration."
---

# AI Providers

JD.AI detects and connects to AI providers automatically on startup. No manual configuration is required — install a provider, authenticate if needed, and JD.AI handles the rest.

## Provider detection

On startup, JD.AI checks for available providers in this order:

1. **Claude Code** — checks for a local CLI session
2. **GitHub Copilot** — checks for GitHub authentication
3. **OpenAI Codex** — checks for Codex CLI authentication or `OPENAI_API_KEY`
4. **Ollama** — checks for a local server at `localhost:11434`
5. **Local (LLamaSharp)** — scans `~/.jdai/models/` for GGUF model files

The first provider with a valid connection becomes the default. Results are shown on startup:

![Provider detection on startup](../images/demo-providers.png)

## Claude Code

**Setup:**

1. Install Claude Code: `npm install -g @anthropic-ai/claude-code`
2. Authenticate: `claude auth login`
3. JD.AI detects the session automatically

**How it works:**

- Uses the Claude Code CLI's OpenAI-compatible endpoint.
- Supports Claude Sonnet, Opus, and Haiku models.
- Auto-refresh: JD.AI attempts to renew expired sessions.

> [!TIP]
> **Best for:** Complex reasoning, long-form code generation, and nuanced analysis.

## GitHub Copilot

**Setup:**

1. Authenticate via GitHub CLI: `gh auth login --scopes copilot`
2. Or via VS Code: sign in to the GitHub Copilot extension.
3. JD.AI detects available Copilot models.

**How it works:**

- Uses Copilot token exchange for OpenAI-compatible API access.
- Available models include GPT-4o, Claude Sonnet, Gemini, and more.
- Auto-refresh: JD.AI attempts to renew expired tokens.

> [!TIP]
> **Best for:** Teams already using GitHub and access to multiple model families.

## Ollama

**Setup:**

1. Install Ollama from <https://ollama.com>.
2. Start the server: `ollama serve`
3. Pull models: `ollama pull llama3.2` (chat) and `ollama pull all-minilm` (embeddings).
4. JD.AI auto-detects all available models.

**How it works:**

- Connects to the local Ollama API at `http://localhost:11434`.
- Lists all locally available models.
- No authentication required.

> [!TIP]
> **Best for:** Offline usage, privacy, fast iteration, and experimentation.

## OpenAI Codex

**Setup:**

1. Install the Codex CLI: `npm install -g @openai/codex`
2. Authenticate: `codex auth login` (or set `OPENAI_API_KEY` environment variable)
3. JD.AI detects the session automatically.

**How it works:**

- Uses the Codex CLI's OAuth token exchange for API access.
- Credential resolution: API key → access token → `OPENAI_API_KEY` env → `CODEX_TOKEN` env → `~/.codex/auth.json` → device code login.
- Supports all OpenAI chat models (GPT-4o, GPT-4.1, o3, etc.).

> [!TIP]
> **Best for:** Teams using OpenAI models who want the Codex CLI's authentication flow.

## Local models (LLamaSharp)

**Setup:**

1. Place `.gguf` model files in `~/.jdai/models/` (or any directory).
2. JD.AI detects them automatically on startup.
3. Or download directly: `/local download TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF`

**How it works:**

- Loads GGUF models in-process via [LLamaSharp](https://github.com/SciSharp/LLamaSharp) (C# bindings for llama.cpp).
- Auto-detects GPU hardware (CUDA, Metal) and falls back to CPU.
- No external service or internet connection required.
- Manage models with `/local` commands: list, add, scan, search, download, remove.

> [!TIP]
> **Best for:** Air-gapped environments, privacy-sensitive workloads, and fully standalone operation.
>
> See [Local Models](local-models.md) for the full guide.

## Switching providers and models

Use slash commands to manage providers and models at any time during a session:

```text
/providers          # List all detected providers with status
/provider           # Show current provider and model
/models             # List all available models across providers
/model qwen3:30b    # Switch to a specific model
```

## Provider comparison

| Feature | Claude Code | GitHub Copilot | OpenAI Codex | Ollama | Local (LLamaSharp) |
|---|:-:|:-:|:-:|:-:|:-:|
| **Setup** | CLI auth | GitHub auth | CLI auth / API key | Local install | Drop in `.gguf` files |
| **Internet required** | Yes | Yes | Yes | No | No |
| **Cost** | Claude subscription | Copilot subscription | OpenAI subscription | Free (local) | Free (local) |
| **Model variety** | Claude family | Multi-family | OpenAI family | Open source | Any GGUF model |
| **Speed** | Fast | Fast | Fast | Depends on hardware | Depends on hardware |
| **Privacy** | Cloud | Cloud | Cloud | Fully local | Fully local |
| **Embedding support** | Yes | Limited | Yes | Yes | No |

## Environment variables

| Variable | Description | Default |
|---|---|---|
| `OLLAMA_ENDPOINT` | Ollama API URL | `http://localhost:11434` |
| `OPENAI_API_KEY` | OpenAI / Codex API key (if not using CLI auth) | — |
| `CODEX_TOKEN` | Codex CLI access token override | — |
| `JDAI_MODELS_DIR` | Local model storage directory | `~/.jdai/models/` |
| `HF_HOME` | HuggingFace cache directory | `~/.cache/huggingface/` |
| `HF_TOKEN` | HuggingFace API token | — |

## See also

- [Overview](overview.md)
- [Quickstart](quickstart.md)
- [Local Models](local-models.md)
