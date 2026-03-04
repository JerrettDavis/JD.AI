---
title: Environment Variables
description: "Consolidated reference of all environment variables that affect JD.AI behavior — provider keys, configuration, runtime, and telemetry."
---

# Environment Variables

All environment variables recognized by JD.AI, grouped by category.

## Provider API keys

| Variable | Description | Default | Example |
|---|---|---|---|
| `OPENAI_API_KEY` | OpenAI API key (also used by Codex provider) | — | `sk-proj-abc123...` |
| `ANTHROPIC_API_KEY` | Anthropic API key | — | `sk-ant-abc123...` |
| `GOOGLE_AI_API_KEY` | Google Gemini API key | — | `AIza...` |
| `MISTRAL_API_KEY` | Mistral API key | — | `mis-abc123...` |
| `HUGGINGFACE_API_KEY` | HuggingFace Inference API key | — | `hf_abc123...` |
| `AZURE_OPENAI_API_KEY` | Azure OpenAI API key | — | `abc123...` |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI endpoint URL | — | `https://myinstance.openai.azure.com/` |
| `AWS_ACCESS_KEY_ID` | AWS access key for Bedrock | — | `AKIA...` |
| `AWS_SECRET_ACCESS_KEY` | AWS secret key for Bedrock | — | `wJalr...` |
| `AWS_REGION` | AWS region for Bedrock | `us-east-1` | `us-west-2` |
| `CODEX_TOKEN` | Codex CLI access token override | — | `tok-abc123...` |
| `HF_TOKEN` | HuggingFace API token (legacy, also used for gated repos) | — | `hf_abc123...` |

## OpenAI-Compatible provider keys

| Variable | Description | Default | Example |
|---|---|---|---|
| `GROQ_API_KEY` | Groq API key | — | `gsk_abc123...` |
| `TOGETHER_API_KEY` | Together AI API key | — | `tog-abc123...` |
| `DEEPSEEK_API_KEY` | DeepSeek API key | — | `sk-abc123...` |
| `OPENROUTER_API_KEY` | OpenRouter API key | — | `sk-or-abc123...` |
| `FIREWORKS_API_KEY` | Fireworks AI API key | — | `fw-abc123...` |
| `PERPLEXITY_API_KEY` | Perplexity API key | — | `pplx-abc123...` |

## Ollama and local models

| Variable | Description | Default | Example |
|---|---|---|---|
| `OLLAMA_ENDPOINT` | Ollama API endpoint URL | `http://localhost:11434` | `http://192.168.1.50:11434` |
| `OLLAMA_CHAT_MODEL` | Default chat model for Ollama provider | `llama3.2:latest` | `qwen3:30b` |
| `OLLAMA_EMBEDDING_MODEL` | Default embedding model for Ollama | `all-minilm:latest` | `nomic-embed-text` |
| `JDAI_MODELS_DIR` | Local GGUF model storage directory | `~/.jdai/models/` | `/opt/llm-models/` |
| `HF_HOME` | HuggingFace cache directory (model scanning) | `~/.cache/huggingface/` | `/data/hf-cache/` |

## Telemetry and observability

| Variable | Description | Default | Example |
|---|---|---|---|
| `OTEL_SERVICE_NAME` | Override service name in OpenTelemetry traces and metrics | `jdai` | `jdai-production` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Activate OTLP exporter and set its endpoint | — | `http://localhost:4317` |

## Governance and workflow store

| Variable | Description | Default | Example |
|---|---|---|---|
| `JDAI_ORG_CONFIG` | Path to directory containing organization policy YAML files | — | `/etc/jdai/policies/` |
| `JDAI_WORKFLOW_STORE_URL` | Git remote URL for shared workflow store (falls back to local file store) | — | `https://github.com/org/jdai-workflows.git` |

## Gateway runtime

| Variable | Description | Default | Example |
|---|---|---|---|
| `DOTNET_ENVIRONMENT` | .NET hosting environment | `Production` | `Development` |
| `ASPNETCORE_URLS` | ASP.NET Core listen URLs (overrides `--gateway-port`) | — | `http://localhost:5100` |

## Variable precedence

For provider credentials, environment variables are the lowest priority source:

1. **Secure credential store** (`~/.jdai/credentials/`) — set via `/provider add`
2. **Configuration files** (`appsettings.json`)
3. **Environment variables**

For CLI behavior, environment variables are overridden by CLI flags:

1. **CLI flags** (e.g. `--model`, `--provider`)
2. **Session state** (e.g. `/model`, `/provider`)
3. **Per-project defaults** (`.jdai/defaults.json`)
4. **Global defaults** (`~/.jdai/config.json`)
5. **Environment variables**
6. **Built-in defaults**

## Setting environment variables

### Linux / macOS

```bash
# Temporary (current shell)
export OPENAI_API_KEY="sk-proj-abc123"

# Permanent (add to ~/.bashrc, ~/.zshrc, etc.)
echo 'export OPENAI_API_KEY="sk-proj-abc123"' >> ~/.bashrc
```

### Windows (PowerShell)

```powershell
# Temporary (current session)
$env:OPENAI_API_KEY = "sk-proj-abc123"

# Permanent (user-level)
[Environment]::SetEnvironmentVariable("OPENAI_API_KEY", "sk-proj-abc123", "User")
```

### Windows (Command Prompt)

```cmd
set OPENAI_API_KEY=sk-proj-abc123
setx OPENAI_API_KEY sk-proj-abc123
```

## See also

- [Providers Reference](providers.md) — provider details and capabilities
- [CLI Reference](cli.md) — CLI flags and options
- [Configuration Reference](configuration.md) — config files and precedence
- [Observability](../operations/observability.md) — telemetry configuration
- [Governance & Policies](../user-guide/governance.md) — governance environment variables
