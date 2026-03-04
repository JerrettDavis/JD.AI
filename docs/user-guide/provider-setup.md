---
title: Provider Setup
description: Configure any of JD.AI's 14 supported AI providers — cloud APIs, local models, and OAuth-based services.
---

# Provider Setup

JD.AI supports 14 AI providers. This guide walks through setting up each one — from quick environment variable configuration to the interactive `/provider add` wizard.

## Provider overview

JD.AI detects providers automatically on startup. Providers fall into three authentication categories:

| Category | Providers | Auth method |
|----------|-----------|-------------|
| **OAuth / session** | Claude Code, GitHub Copilot, OpenAI Codex | CLI authentication |
| **Local** | Ollama, Local (LLamaSharp), Foundry Local | No auth needed |
| **API key** | OpenAI, Azure OpenAI, Anthropic, Google Gemini, Mistral, AWS Bedrock, HuggingFace, OpenAI-Compatible | API key or credentials |

## Quick setup with `/provider add`

The fastest way to configure any API-key provider is the interactive wizard:

```text
/provider add
```

The wizard walks you through selecting a provider, entering credentials, and verifying connectivity. Credentials are saved to the encrypted store automatically.

You can also target a specific provider directly:

```text
/provider add openai
/provider add anthropic
/provider add azure-openai
```

## OAuth / session providers

These providers authenticate through their own CLI tools. Install the CLI, log in, and JD.AI discovers the session automatically.

### Claude Code

1. Install the Claude Code CLI:
   ```bash
   npm install -g @anthropic-ai/claude-code
   ```
2. Authenticate:
   ```bash
   claude auth login
   ```
3. Launch JD.AI — it detects the session automatically.

**Models available:** Claude Sonnet, Opus, and Haiku.

> [!TIP]
> Best for complex reasoning, long-form code generation, and nuanced analysis.

### GitHub Copilot

1. Authenticate via GitHub CLI:
   ```bash
   gh auth login --scopes copilot
   ```
   Or sign in through the VS Code GitHub Copilot extension.
2. JD.AI detects available Copilot models automatically.

**Models available:** GPT-4o, Claude Sonnet, Gemini, and more (depends on your subscription).

> [!TIP]
> Best for teams already using GitHub who want access to multiple model families.

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
3. JD.AI detects the session automatically.

**Models available:** All OpenAI chat models (GPT-4o, GPT-4.1, o3, etc.).

> [!TIP]
> Best for teams using OpenAI models who want the Codex CLI's authentication flow.

## Local providers

These providers run models on your machine. No internet connection or API keys required.

### Ollama

1. Install Ollama from [ollama.com](https://ollama.com).
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

JD.AI auto-detects all available models at `http://localhost:11434`.

**Environment variable:** `OLLAMA_ENDPOINT` (default: `http://localhost:11434`)

> [!TIP]
> Best for offline usage, privacy, fast iteration, and experimentation.

### Local models (LLamaSharp / GGUF)

Run GGUF models directly in-process — no external service needed:

```bash
jdai --provider local
```

1. Place `.gguf` model files in `~/.jdai/models/` (or any directory).
2. JD.AI detects them automatically on startup.
3. Or download interactively:
   ```text
   /local search llama 7b
   /local download TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF
   ```

**Environment variable:** `JDAI_MODELS_DIR` (default: `~/.jdai/models/`)

See [Local Models](local-models.md) for the full guide.

> [!TIP]
> Best for air-gapped environments, privacy-sensitive workloads, and fully standalone operation.

### Microsoft Foundry Local

```bash
jdai --provider foundry-local
```

Uses locally available Foundry models. No API key required — models run on-device via the Foundry Local runtime.

## API key providers

Set an environment variable or use `/provider add` to store credentials securely.

### OpenAI

```bash
# Environment variable
export OPENAI_API_KEY=sk-...

# Or CLI flag
jdai --provider openai

# Or interactive setup
/provider add openai
```

**Environment variable:** `OPENAI_API_KEY`

**Models available:** GPT-4o, GPT-4.1, o3, o4, and more (discovered dynamically).

> [!TIP]
> Best for direct OpenAI API access without the Codex CLI overhead.

### Azure OpenAI

```bash
# Environment variables
export AZURE_OPENAI_API_KEY=...
export AZURE_OPENAI_ENDPOINT=https://myresource.openai.azure.com/

# Or CLI flag (prompts for credentials)
jdai --provider azure-openai

# Or interactive setup
/provider add azure-openai
```

**Environment variables:** `AZURE_OPENAI_API_KEY`, `AZURE_OPENAI_ENDPOINT`

**Default deployments:** `gpt-4o`, `gpt-4o-mini`, `gpt-4`

> [!TIP]
> Best for enterprise environments with private endpoints and compliance requirements.

### Anthropic

```bash
# Environment variable
export ANTHROPIC_API_KEY=sk-ant-...

# Or CLI flag
jdai --provider anthropic

# Or interactive setup
/provider add anthropic
```

**Environment variable:** `ANTHROPIC_API_KEY`

**Models available:** Claude Opus 4, Sonnet 4, 3.7 Sonnet, 3.5 Haiku, 3.5 Sonnet v2.

> [!NOTE]
> This is separate from the Claude Code provider, which uses OAuth via the CLI.
>
> JD.AI automatically enables Anthropic prompt caching for larger prompts by default. Control it with `/config set prompt_cache on|off` and `/config set prompt_cache_ttl 5m|1h`.

### Google Gemini

```bash
# Environment variable
export GOOGLE_AI_API_KEY=...

# Or interactive setup
/provider add google-gemini
```

**Environment variable:** `GOOGLE_AI_API_KEY`

**Models available:** Gemini 2.5 Pro, 2.5 Flash, 2.0 Flash, 1.5 Pro, 1.5 Flash.

### Mistral

```bash
# Environment variable
export MISTRAL_API_KEY=...

# Or interactive setup
/provider add mistral
```

**Environment variable:** `MISTRAL_API_KEY`

**Models available:** Mistral Large, Medium, Small, Codestral, Nemo, Ministral 8B.

### AWS Bedrock

```bash
# Environment variables
export AWS_ACCESS_KEY_ID=...
export AWS_SECRET_ACCESS_KEY=...
export AWS_REGION=us-east-1

# Or interactive setup
/provider add bedrock
```

**Environment variables:** `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION`

Uses IAM-based authentication via the AWS SDK credential chain.

**Models available:** Claude (via Bedrock), Amazon Nova, Llama, and other foundation models.

### HuggingFace

```bash
# Environment variable
export HUGGINGFACE_API_KEY=hf_...

# Or interactive setup
/provider add huggingface
```

**Environment variable:** `HUGGINGFACE_API_KEY`

**Models available:** Llama 3.3 70B, Llama 3.1 8B, Mixtral 8x7B, Phi-3, Qwen 2.5 72B.

### OpenAI-Compatible endpoints

Connect to any OpenAI-compatible API — Groq, Together AI, DeepSeek, OpenRouter, Fireworks, Perplexity, LM Studio, vLLM, and more.

```bash
# Provider-specific environment variables (auto-detected)
export GROQ_API_KEY=...
export TOGETHER_API_KEY=...
export DEEPSEEK_API_KEY=...

# Or interactive setup for custom endpoints
/provider add openai-compat
```

**Auto-detected providers and their environment variables:**

| Provider | Env variable | Base URL |
|----------|-------------|----------|
| Groq | `GROQ_API_KEY` | `https://api.groq.com/openai/v1` |
| Together AI | `TOGETHER_API_KEY` | `https://api.together.xyz/v1` |
| DeepSeek | `DEEPSEEK_API_KEY` | `https://api.deepseek.com/v1` |
| OpenRouter | `OPENROUTER_API_KEY` | `https://openrouter.ai/api/v1` |
| Fireworks AI | `FIREWORKS_API_KEY` | `https://api.fireworks.ai/inference/v1` |
| Perplexity | `PERPLEXITY_API_KEY` | `https://api.perplexity.ai` |

For custom endpoints (LM Studio, vLLM, etc.), use `/provider add openai-compat` and enter an alias, base URL, and optional API key.

## Credential resolution chain

When JD.AI needs a credential, it checks sources in this order:

1. **CLI flags** — `--provider`, `--api-key`, etc.
2. **Environment variables** — `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, etc.
3. **Encrypted credential store** — `~/.jdai/credentials.enc`
4. **OAuth / session tokens** — Claude Code, GitHub Copilot, Codex sessions

The first source that provides a valid credential wins.

### Encrypted credential store

All credentials saved through the `/provider add` wizard are stored encrypted:

- **Windows** — DPAPI (Data Protection API)
- **macOS / Linux** — AES-256 encryption with a machine-scoped key

Credentials are stored in `~/.jdai/credentials.enc` and are never written in plain text.

## Switching providers and models

Use slash commands to manage providers at any time during a session:

```text
/providers               # List all detected providers with status
/provider                # Interactive picker to switch providers
/provider list           # Detailed provider list
/provider test           # Test all provider connections
/provider test openai    # Test a specific provider
/provider remove mistral # Remove stored credentials
/models                  # List all available models
/model gpt-4.1           # Switch to a specific model
```

When you switch models mid-session, JD.AI prompts you to choose a transition mode:

| Mode | Description |
|------|-------------|
| **Preserve** | Keep the full conversation history as-is |
| **Compact** | Summarize the conversation before switching |
| **Transform** | Re-format messages for the new model's style |
| **Fresh** | Start a clean conversation (history discarded) |
| **Cancel** | Abort the switch |

## Environment variables reference

| Variable | Provider | Description |
|----------|----------|-------------|
| `OPENAI_API_KEY` | OpenAI / Codex | OpenAI API key |
| `AZURE_OPENAI_API_KEY` | Azure OpenAI | Azure OpenAI API key |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI | Azure OpenAI endpoint URL |
| `ANTHROPIC_API_KEY` | Anthropic | Anthropic API key |
| `GOOGLE_AI_API_KEY` | Google Gemini | Google AI API key |
| `MISTRAL_API_KEY` | Mistral | Mistral API key |
| `AWS_ACCESS_KEY_ID` | AWS Bedrock | AWS access key ID |
| `AWS_SECRET_ACCESS_KEY` | AWS Bedrock | AWS secret access key |
| `AWS_REGION` | AWS Bedrock | AWS region (default: `us-east-1`) |
| `HUGGINGFACE_API_KEY` | HuggingFace | HuggingFace API token |
| `GROQ_API_KEY` | Groq | Groq API key |
| `TOGETHER_API_KEY` | Together AI | Together AI API key |
| `DEEPSEEK_API_KEY` | DeepSeek | DeepSeek API key |
| `OPENROUTER_API_KEY` | OpenRouter | OpenRouter API key |
| `FIREWORKS_API_KEY` | Fireworks AI | Fireworks AI API key |
| `PERPLEXITY_API_KEY` | Perplexity | Perplexity API key |
| `OLLAMA_ENDPOINT` | Ollama | Ollama API URL (default: `http://localhost:11434`) |
| `JDAI_MODELS_DIR` | Local Models | Model directory (default: `~/.jdai/models/`) |
| `HF_TOKEN` | HuggingFace | HuggingFace token (legacy) |

## See also

- [Local Models](local-models.md) — full guide to running GGUF models locally
- [Configuration](configuration.md) — global defaults, per-project overrides, and environment variables
- [Troubleshooting](troubleshooting.md) — provider connection issues and solutions
