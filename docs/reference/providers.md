---
title: Providers Reference
description: "Provider comparison table, capabilities matrix, credential resolution chain, environment variables, and OpenAI-compatible sub-providers."
---

# Providers Reference

JD.AI supports 14 AI providers. This reference covers capabilities, authentication, environment variables, and comparison tables. For setup guides, see [AI Providers](../user-guide/provider-setup.md).

## Provider summary

| # | Provider | Package / Connector | Auth Type | Requires Internet |
|---|---|---|---|---|
| 1 | Claude Code | Claude Code CLI (OAuth) | OAuth | Yes |
| 2 | GitHub Copilot | Copilot token exchange | OAuth | Yes |
| 3 | OpenAI Codex | Codex CLI (OAuth) | OAuth | Yes |
| 4 | Ollama | Local REST API | None | No |
| 5 | Local (LLamaSharp) | LLamaSharp (in-process) | File | No |
| 6 | OpenAI | `Microsoft.SemanticKernel.Connectors.OpenAI` | API Key | Yes |
| 7 | Azure OpenAI | `Microsoft.SemanticKernel.Connectors.AzureOpenAI` | API Key | Yes |
| 8 | Anthropic | `Anthropic.SDK` (native Messages API) | API Key | Yes |
| 9 | Google Gemini | `Microsoft.SemanticKernel.Connectors.Google` | API Key | Yes |
| 10 | Mistral | `Microsoft.SemanticKernel.Connectors.MistralAI` | API Key | Yes |
| 11 | AWS Bedrock | `Microsoft.SemanticKernel.Connectors.Amazon` | AWS IAM | Yes |
| 12 | HuggingFace | `Microsoft.SemanticKernel.Connectors.HuggingFace` | API Key | Yes |
| 13 | OpenAI-Compatible | OpenAI connector + custom base URL | API Key | Yes |

## Provider capabilities matrix

| Provider | Streaming | Tool Calling | Embeddings | Model Discovery | Cost |
|---|:-:|:-:|:-:|:-:|---|
| Claude Code | ✅ | ✅ | ❌ | ✅ | Subscription |
| GitHub Copilot | ✅ | ✅ | ❌ | ✅ | Subscription |
| OpenAI Codex | ✅ | ✅ | ❌ | ✅ | Subscription |
| Ollama | ✅ | ✅ | ✅ | ✅ | Free |
| Local (LLamaSharp) | ✅ | ❌ | ✅ | ✅ | Free |
| OpenAI | ✅ | ✅ | ✅ | ✅ | Pay-per-use |
| Azure OpenAI | ✅ | ✅ | ✅ | ❌ | Pay-per-use |
| Anthropic | ✅ | ✅ | ❌ | ❌ | Pay-per-use |
| Google Gemini | ✅ | ✅ | ❌ | ❌ | Pay-per-use |
| Mistral | ✅ | ✅ | ❌ | ❌ | Pay-per-use |
| AWS Bedrock | ✅ | ✅ | ✅ | ❌ | Pay-per-use |
| HuggingFace | ✅ | ❌ | ❌ | ❌ | Free/Pay |
| OpenAI-Compatible | ✅ | Varies | Varies | ✅ | Varies |

## Claude prompt caching (API key + OAuth)

Anthropic API-key sessions and Claude Code OAuth sessions both use Anthropic's native Messages API path and support automatic prompt caching controls.

Default behavior:

- Prompt caching is enabled by default (`prompt_cache=on`).
- Cache TTL defaults to `5m` (`prompt_cache_ttl=5m`).
- Optional extended TTL: `1h`.

Policy details:

- Auto-enable threshold for Sonnet/Opus models: `>=1024` estimated prompt tokens.
- Auto-enable threshold for Haiku models: `>=2048` estimated prompt tokens.
- Caching directives are applied only when thresholds are met and the runtime toggle is enabled.

Configuration commands:

```text
/config get prompt_cache
/config get prompt_cache_ttl
/config set prompt_cache on
/config set prompt_cache_ttl 1h
```

See [Prompt Caching Reference](prompt-caching.md) for full behavior and execution coverage.

## Provider detection order

On startup, JD.AI checks providers in this order. The first with a valid connection becomes the default:

1. **Claude Code** — local CLI session
2. **GitHub Copilot** — GitHub authentication
3. **OpenAI Codex** — Codex CLI authentication
4. **Ollama** — local server at `localhost:11434`
5. **Local (LLamaSharp)** — GGUF files in `~/.jdai/models/`
6. **OpenAI** — `OPENAI_API_KEY` or secure store
7. **Azure OpenAI** — `AZURE_OPENAI_API_KEY` + `AZURE_OPENAI_ENDPOINT`
8. **Anthropic** — `ANTHROPIC_API_KEY` or secure store
9. **Google Gemini** — `GOOGLE_AI_API_KEY` or secure store
10. **Mistral** — `MISTRAL_API_KEY` or secure store
11. **AWS Bedrock** — `AWS_ACCESS_KEY_ID` + `AWS_SECRET_ACCESS_KEY` + `AWS_REGION`
12. **HuggingFace** — `HUGGINGFACE_API_KEY` or secure store
13. **OpenAI-Compatible** — custom endpoints via env vars or secure store

## Credential resolution chain

API-key providers resolve credentials in this priority order:

| Priority | Source | Example |
|---|---|---|
| 1 | **Secure credential store** | `~/.jdai/credentials/` — encrypted with DPAPI (Windows) or AES (Linux/macOS) |
| 2 | **Configuration** | `appsettings.json` → `Providers:{name}:{field}` |
| 3 | **Environment variables** | `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, etc. |

The `/provider add` wizard stores keys in the secure credential store. Environment variables are picked up automatically.

## Environment variables per provider

| Variable | Provider | Description |
|---|---|---|
| `OPENAI_API_KEY` | OpenAI / Codex | OpenAI API key |
| `AZURE_OPENAI_API_KEY` | Azure OpenAI | Azure OpenAI API key |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI | Azure OpenAI endpoint URL |
| `ANTHROPIC_API_KEY` | Anthropic | Anthropic API key |
| `GOOGLE_AI_API_KEY` | Google Gemini | Google AI API key |
| `MISTRAL_API_KEY` | Mistral | Mistral API key |
| `AWS_ACCESS_KEY_ID` | AWS Bedrock | AWS access key ID |
| `AWS_SECRET_ACCESS_KEY` | AWS Bedrock | AWS secret access key |
| `AWS_REGION` | AWS Bedrock | AWS region (default: `us-east-1`) |
| `HUGGINGFACE_API_KEY` | HuggingFace | HuggingFace Inference API key |
| `HF_TOKEN` | HuggingFace | HuggingFace API token (legacy) |
| `CODEX_TOKEN` | OpenAI Codex | Codex CLI access token override |
| `OLLAMA_ENDPOINT` | Ollama | Ollama API URL (default: `http://localhost:11434`) |
| `JDAI_MODELS_DIR` | Local Models | Model storage directory (default: `~/.jdai/models/`) |
| `HF_HOME` | HuggingFace | HuggingFace cache directory |

## OpenAI-Compatible sub-providers

These providers are auto-detected via environment variables and use the OpenAI connector with a custom base URL:

| Provider | Env Variable | Base URL | Notable Models |
|---|---|---|---|
| Groq | `GROQ_API_KEY` | `https://api.groq.com/openai/v1` | Llama 3, Mixtral |
| Together AI | `TOGETHER_API_KEY` | `https://api.together.xyz/v1` | Open-source models |
| DeepSeek | `DEEPSEEK_API_KEY` | `https://api.deepseek.com/v1` | DeepSeek Coder, Chat |
| OpenRouter | `OPENROUTER_API_KEY` | `https://openrouter.ai/api/v1` | Multi-provider routing |
| Fireworks AI | `FIREWORKS_API_KEY` | `https://api.fireworks.ai/inference/v1` | Fast inference |
| Perplexity | `PERPLEXITY_API_KEY` | `https://api.perplexity.ai` | Search-augmented models |

Custom endpoints can also be configured interactively:

```text
/provider add openai-compat   # Prompts for alias, base URL, and API key
```

Supports unlimited named instances — each with its own alias, URL, and key. Works with self-hosted endpoints (vLLM, LocalAI, LiteLLM, etc.).

## Codex credential resolution

The OpenAI Codex provider has its own extended credential chain:

1. API key (direct)
2. Access token
3. `OPENAI_API_KEY` environment variable
4. `CODEX_TOKEN` environment variable
5. `~/.codex/auth.json` file
6. Device code login (interactive)

## Provider comparison: privacy and cost

| Feature | Claude Code | Copilot | Codex | Ollama | Local | OpenAI | Azure | Anthropic | Gemini | Mistral | Bedrock | HuggingFace | OAI-Compat |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| **Auth** | OAuth | OAuth | OAuth | None | File | Key | Key | Key | Key | Key | AWS | Key | Key |
| **Internet** | Yes | Yes | Yes | No | No | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes |
| **Cost** | Sub | Sub | Sub | Free | Free | PPU | PPU | PPU | PPU | PPU | PPU | Free/Pay | Varies |
| **Privacy** | Cloud | Cloud | Cloud | Local | Local | Cloud | Cloud | Cloud | Cloud | Cloud | Cloud | Cloud | Varies |

*Sub = Subscription, PPU = Pay-per-use*

## Managing providers at runtime

```text
/providers               # List all detected providers with status
/provider                # Interactive picker
/provider list           # Detailed provider list
/provider add openai     # Configure interactively
/provider remove openai  # Remove stored credentials
/provider test           # Test all connections
/provider test openai    # Test specific provider
/models                  # List all available models
/model gpt-4o            # Switch to a specific model
```

### Mid-session model switching

When switching models during a conversation, JD.AI prompts for a transition mode:

| Mode | Description |
|---|---|
| **Preserve** | Keep full conversation history as-is |
| **Compact** | Summarize conversation before switching |
| **Transform** | Re-format messages for the new model's style |
| **Fresh** | Start clean (history discarded) |
| **Cancel** | Abort the switch |

Each switch creates a **fork point** in session history. Use `/history` to view fork points and double-ESC to roll back.

## See also

- [AI Providers (guide)](../user-guide/provider-setup.md) — setup instructions for each provider
- [Environment Variables](environment-variables.md) — consolidated env var reference
- [CLI Reference](cli.md) — `--provider` and `--model` flags
- [Configuration Reference](configuration.md) — default provider/model settings
