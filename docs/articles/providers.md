---
description: "Connect to Claude Code, GitHub Copilot, OpenAI Codex, Ollama, LLamaSharp — or any API-key provider: OpenAI, Azure OpenAI, Anthropic, Google Gemini, Mistral, AWS Bedrock, HuggingFace, and any OpenAI-compatible endpoint."
---

# AI Providers

JD.AI detects and connects to AI providers automatically on startup. OAuth-based providers (Claude Code, Copilot, Codex) are discovered via local credentials. API-key-based providers are discovered via the secure credential store, configuration files, or environment variables.

## Provider detection

On startup, JD.AI checks for available providers in this order:

**OAuth / credential-harvesting providers:**

1. **Claude Code** — checks for a local CLI session
2. **GitHub Copilot** — checks for GitHub authentication
3. **OpenAI Codex** — checks for Codex CLI authentication

**Local providers:**

4. **Ollama** — checks for a local server at `localhost:11434`
5. **Local (LLamaSharp)** — scans `~/.jdai/models/` for GGUF model files

**API key providers:**

6. **OpenAI** — `OPENAI_API_KEY` env var or secure store
7. **Azure OpenAI** — `AZURE_OPENAI_API_KEY` + `AZURE_OPENAI_ENDPOINT`
8. **Anthropic** — `ANTHROPIC_API_KEY` env var or secure store
9. **Google Gemini** — `GOOGLE_AI_API_KEY` env var or secure store
10. **Mistral** — `MISTRAL_API_KEY` env var or secure store
11. **AWS Bedrock** — `AWS_ACCESS_KEY_ID` + `AWS_SECRET_ACCESS_KEY` + `AWS_REGION`
12. **HuggingFace** — `HUGGINGFACE_API_KEY` env var or secure store
13. **OpenAI-Compatible** — custom endpoints (Groq, Together, DeepSeek, OpenRouter, etc.)

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

## OpenAI (direct API key)

**Setup:**

1. Set `OPENAI_API_KEY` environment variable, or
2. Run `/provider add openai` and enter your API key (stored in secure credential store).
3. JD.AI discovers available models via the `/v1/models` endpoint.

**How it works:**

- Uses the official `Microsoft.SemanticKernel.Connectors.OpenAI` package.
- Discovers chat models dynamically (GPT-4o, GPT-4.1, o3, o4, etc.).
- API key is resolved from: secure store → `IConfiguration` → `OPENAI_API_KEY` env var.

> [!TIP]
> **Best for:** Direct OpenAI API access without the Codex CLI overhead.

## Azure OpenAI

**Setup:**

1. Set `AZURE_OPENAI_API_KEY` and `AZURE_OPENAI_ENDPOINT` environment variables, or
2. Run `/provider add azure-openai` and enter your API key, endpoint, and deployment names.

**How it works:**

- Uses the official `Microsoft.SemanticKernel.Connectors.AzureOpenAI` package.
- Requires an endpoint URL and one or more deployment names.
- Default deployments (if not specified): `gpt-4o`, `gpt-4o-mini`, `gpt-4`.

> [!TIP]
> **Best for:** Enterprise environments using Azure AI services with private endpoints and compliance requirements.

## Anthropic (direct API key)

**Setup:**

1. Set `ANTHROPIC_API_KEY` environment variable, or
2. Run `/provider add anthropic` and enter your API key.

**How it works:**

- Routes through the OpenAI-compatible connector with Anthropic's API endpoint.
- Provides Claude Opus 4, Sonnet 4, 3.7 Sonnet, 3.5 Haiku, and 3.5 Sonnet v2.
- Separate from the Claude Code provider (which uses OAuth via the CLI).

> [!TIP]
> **Best for:** Direct Anthropic API access when you have an API key but don't use the Claude Code CLI.

## Google Gemini

**Setup:**

1. Set `GOOGLE_AI_API_KEY` environment variable, or
2. Run `/provider add google-gemini` and enter your API key.

**How it works:**

- Uses the official `Microsoft.SemanticKernel.Connectors.Google` package.
- Provides Gemini 2.5 Pro, 2.5 Flash, 2.0 Flash, 1.5 Pro, and 1.5 Flash.

> [!TIP]
> **Best for:** Access to Google's Gemini models with multimodal capabilities.

## Mistral

**Setup:**

1. Set `MISTRAL_API_KEY` environment variable, or
2. Run `/provider add mistral` and enter your API key.

**How it works:**

- Uses the official `Microsoft.SemanticKernel.Connectors.MistralAI` package.
- Provides Mistral Large, Medium, Small, Codestral, Nemo, and Ministral 8B.

> [!TIP]
> **Best for:** European AI with strong multilingual support and competitive pricing.

## AWS Bedrock

**Setup:**

1. Set `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and `AWS_REGION` environment variables, or
2. Run `/provider add bedrock` and enter your AWS credentials.

**How it works:**

- Uses the official `Microsoft.SemanticKernel.Connectors.Amazon` package.
- Provides Claude (via Bedrock), Amazon Nova, Llama, and other foundation models.
- Supports IAM-based authentication via the AWS SDK credential chain.

> [!TIP]
> **Best for:** Enterprise AWS environments with Bedrock access and IAM governance.

## HuggingFace Inference API

**Setup:**

1. Set `HUGGINGFACE_API_KEY` environment variable, or
2. Run `/provider add huggingface` and enter your API key.

**How it works:**

- Uses the official `Microsoft.SemanticKernel.Connectors.HuggingFace` package.
- Provides access to curated models: Llama 3.3 70B, Llama 3.1 8B, Mixtral 8x7B, Phi-3, Qwen 2.5 72B.

> [!TIP]
> **Best for:** Access to open-source models via HuggingFace's hosted inference infrastructure.

## OpenAI-Compatible endpoints

**Setup:**

1. Set provider-specific environment variables (e.g. `GROQ_API_KEY`, `TOGETHER_API_KEY`, `DEEPSEEK_API_KEY`), or
2. Run `/provider add openai-compat` and enter an alias, base URL, and API key.

**Supported providers (auto-detected via env vars):**

| Provider | Env Variable | Base URL |
|---|---|---|
| Groq | `GROQ_API_KEY` | `https://api.groq.com/openai/v1` |
| Together AI | `TOGETHER_API_KEY` | `https://api.together.xyz/v1` |
| DeepSeek | `DEEPSEEK_API_KEY` | `https://api.deepseek.com/v1` |
| OpenRouter | `OPENROUTER_API_KEY` | `https://openrouter.ai/api/v1` |
| Fireworks AI | `FIREWORKS_API_KEY` | `https://api.fireworks.ai/inference/v1` |
| Perplexity | `PERPLEXITY_API_KEY` | `https://api.perplexity.ai` |

**How it works:**

- Uses the OpenAI connector with a custom base URL.
- Discovers models dynamically via the `/v1/models` endpoint.
- Supports unlimited named instances — each with its own alias, URL, and key.

> [!TIP]
> **Best for:** Accessing any OpenAI-compatible API (including self-hosted vLLM, LocalAI, LiteLLM, etc.).

## Switching providers and models

Use slash commands to manage providers and models at any time during a session:

```text
/providers               # List all detected providers with status
/provider                # Interactive picker to switch providers
/provider list           # Detailed provider list with auth method and model count
/provider add openai     # Configure an API-key provider interactively
/provider remove openai  # Remove stored credentials for a provider
/provider test           # Test all provider connections
/provider test openai    # Test a specific provider
/models                  # List all available models across providers
/model qwen3:30b         # Switch to a specific model
/default                 # Show current default provider and model
/default provider openai # Set global default provider
/default model gpt-4o    # Set global default model
```

### Mid-session model switching

When you switch models or providers during an active conversation, JD.AI uses a **ConversationTransformer** to handle the transition. You are prompted to choose a transition mode:

| Mode | Description |
|---|---|
| **Preserve** | Keep the full conversation history as-is for the new model |
| **Compact** | Summarize the conversation before switching (reduces token usage) |
| **Transform** | Re-format messages to match the new model's expected style |
| **Fresh** | Start a clean conversation with the new model (history is discarded) |
| **Cancel** | Abort the switch and stay on the current model |

### Fork points and reverting

Each model switch creates a **fork point** in the session history. You can revert to a previous fork point to undo a switch and return to the prior model and conversation state. Use `/history` to view fork points and double-ESC to roll back.

### Remote model search

Search for models across remote catalogs without leaving JD.AI:

```text
/model search llama 70b           # Search Ollama, HuggingFace, and Foundry Local
/model search codestral           # Find Mistral's code model
/model url https://ollama.com/library/deepseek-coder   # Pull from a direct URL
```

Supported catalogs:

- **Ollama** — searches the Ollama model library
- **HuggingFace** — searches GGUF-tagged repositories on HuggingFace Hub
- **Foundry Local** — searches the Microsoft Foundry Local model catalog

## Credential resolution

API-key providers resolve credentials through a priority chain:

1. **Secure credential store** (`~/.jdai/credentials/`) — encrypted with DPAPI (Windows) or AES (Linux/macOS)
2. **Configuration** (`appsettings.json` → `Providers:{name}:{field}`)
3. **Environment variables** (e.g. `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`)

The `/provider add` wizard stores keys in the secure credential store. Environment variables are picked up automatically without any setup.

## Provider comparison

| Feature | Claude Code | Copilot | Codex | Ollama | Local | OpenAI | Azure | Anthropic | Gemini | Mistral | Bedrock | HuggingFace | OAI-Compat |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| **Auth** | OAuth | OAuth | OAuth | None | File | API Key | API Key | API Key | API Key | API Key | AWS | API Key | API Key |
| **Internet** | Yes | Yes | Yes | No | No | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes |
| **Cost** | Subscription | Subscription | Subscription | Free | Free | Pay-per-use | Pay-per-use | Pay-per-use | Pay-per-use | Pay-per-use | Pay-per-use | Free/Pay | Varies |
| **Privacy** | Cloud | Cloud | Cloud | Local | Local | Cloud | Cloud | Cloud | Cloud | Cloud | Cloud | Cloud | Varies |

## Environment variables

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
| `HUGGINGFACE_API_KEY` | HuggingFace | HuggingFace API token |
| `GROQ_API_KEY` | Groq (OAI-compat) | Groq API key |
| `TOGETHER_API_KEY` | Together (OAI-compat) | Together AI API key |
| `DEEPSEEK_API_KEY` | DeepSeek (OAI-compat) | DeepSeek API key |
| `OPENROUTER_API_KEY` | OpenRouter (OAI-compat) | OpenRouter API key |
| `FIREWORKS_API_KEY` | Fireworks (OAI-compat) | Fireworks AI API key |
| `PERPLEXITY_API_KEY` | Perplexity (OAI-compat) | Perplexity API key |
| `OLLAMA_ENDPOINT` | Ollama | Ollama API URL (default: `http://localhost:11434`) |
| `CODEX_TOKEN` | OpenAI Codex | Codex CLI access token override |
| `JDAI_MODELS_DIR` | Local Models | Local model storage directory (default: `~/.jdai/models/`) |
| `HF_HOME` | HuggingFace | HuggingFace cache directory |
| `HF_TOKEN` | HuggingFace | HuggingFace API token (legacy) |

## See also

- [Overview](overview.md)
- [Quickstart](quickstart.md)
- [Local Models](local-models.md)
