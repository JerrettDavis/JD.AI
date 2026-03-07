---
title: OpenRouter
description: Access hundreds of AI models from multiple vendors through a single OpenRouter API key — with automatic model discovery, pricing, and capability detection.
---

# OpenRouter

[OpenRouter](https://openrouter.ai) is a unified API that routes requests to AI models from multiple vendors — OpenAI, Anthropic, Google, Meta, Mistral, and more — through a single endpoint. JD.AI integrates with OpenRouter as a first-class provider, automatically discovering the full model catalog and importing pricing and capability metadata.

## Quick start

1. Get an API key from [openrouter.ai/keys](https://openrouter.ai/keys).

2. Configure JD.AI:

   ```bash
   # Environment variable
   export OPENROUTER_API_KEY=sk-or-...

   # Or interactive setup
   /provider add openrouter
   ```

3. Launch JD.AI — models are discovered automatically:

   ```text
   Detecting providers...
     ✓ OpenRouter: 247 model(s)
   ```

## How it works

OpenRouter acts as a proxy that routes your requests to the underlying vendor APIs (OpenAI, Anthropic, Google, etc.). JD.AI communicates with OpenRouter using the OpenAI-compatible chat completions format through `https://openrouter.ai/api/v1`.

On startup, JD.AI fetches the full model catalog from the OpenRouter API, extracts metadata for each model, and makes them available through `/models` and `/model`. When you send a message, JD.AI forwards the request to OpenRouter, which routes it to the appropriate vendor.

## Model IDs

OpenRouter uses a `vendor/model` naming convention:

| Example ID | Vendor | Model |
|-----------|--------|-------|
| `anthropic/claude-sonnet-4` | Anthropic | Claude Sonnet 4 |
| `openai/gpt-4.1` | OpenAI | GPT-4.1 |
| `google/gemini-2.5-pro` | Google | Gemini 2.5 Pro |
| `meta-llama/llama-4-maverick` | Meta | Llama 4 Maverick |
| `mistralai/mistral-large` | Mistral | Mistral Large |

Use the full `vendor/model` ID when selecting a model:

```text
/model anthropic/claude-sonnet-4
```

Or pass it on the command line:

```bash
jdai --model anthropic/claude-sonnet-4
```

## Model discovery

JD.AI fetches the full model catalog by calling `GET /api/v1/models` with your API key on startup. Each model entry includes context length, max output tokens, pricing, supported parameters, and input/output modalities.

The discovery process:

1. Sends a GET request with Bearer token authentication
2. Deserializes the catalog response
3. Filters to text-capable models (excludes image-only output models)
4. Extracts metadata and capabilities for each model

When discovery fails — due to network issues, an invalid API key, or a timeout — JD.AI falls back to five built-in models (see [Fallback models](#fallback-models)).

> [!TIP]
> Use `/models` to browse all discovered models, filter by name, and see capability badges.

## Capabilities and metadata

JD.AI extracts the following metadata for each discovered model:

| Field | Source | Default |
|-------|--------|---------|
| **Context window** | `context_length` | 128,000 tokens |
| **Max output tokens** | `top_provider.max_completion_tokens` | 16,384 tokens |
| **Input cost per token** | `pricing.prompt` | 0 |
| **Output cost per token** | `pricing.completion` | 0 |
| **Tool calling** | `supported_parameters` contains `tools` or `tool_choice` | — |
| **Vision** | `architecture.input_modalities` contains `image` | — |

Capability badges appear in the `/models` list:

- **Chat** — all models include this
- **Tool Calling** — model supports function/tool calling via structured schemas
- **Vision** — model accepts image inputs

## Pricing

OpenRouter charges per token with prices set individually per model. JD.AI imports these prices so `/cost` shows accurate estimates for your session.

Example pricing (varies — check [openrouter.ai/models](https://openrouter.ai/models) for current rates):

| Model | Input (per 1M tokens) | Output (per 1M tokens) |
|-------|----------------------|------------------------|
| `anthropic/claude-sonnet-4` | $3.00 | $15.00 |
| `openai/gpt-4.1` | $2.00 | $8.00 |
| `google/gemini-2.5-pro` | $1.25 | $10.00 |
| `meta-llama/llama-4-maverick` | $0.20 | $0.60 |
| `mistralai/mistral-large` | $2.00 | $6.00 |

> [!NOTE]
> Prices are fetched from the OpenRouter API at startup. The values above are examples and may change. Use `/cost` during a session for real-time cost tracking.

## Choosing a model

| Goal | Recommended models | Why |
|------|-------------------|-----|
| **Best reasoning** | `anthropic/claude-sonnet-4`, `openai/gpt-4.1` | Strong at complex code generation and analysis |
| **Fastest responses** | `google/gemini-2.5-flash`, `meta-llama/llama-4-maverick` | Optimized for low latency |
| **Lowest cost** | Small open-source models (Llama, Mistral Small, Qwen) | Fraction of the cost of large models |
| **Largest context** | `google/gemini-2.5-pro`, `openai/gpt-4.1` | 1M+ token context windows |
| **Tool calling** | `anthropic/claude-sonnet-4`, `openai/gpt-4.1` | Reliable structured output and function calling |

Browse the full catalog with `/models` and filter by name to find models matching your needs.

## Fallback models

When live discovery is unavailable (network issues, invalid key, timeout), JD.AI uses five built-in models:

| Model ID | Display name | Context | Max output |
|----------|-------------|---------|------------|
| `anthropic/claude-sonnet-4` | Claude Sonnet 4 | 200K | 16K |
| `openai/gpt-4.1` | GPT-4.1 | 1M | 32K |
| `google/gemini-2.5-pro` | Gemini 2.5 Pro | 1M | 64K |
| `meta-llama/llama-4-maverick` | Llama 4 Maverick | 1M | 1M |
| `mistralai/mistral-large` | Mistral Large | 128K | 128K |

> [!NOTE]
> Fallback models do not include pricing metadata. `/cost` estimates will not be available until discovery succeeds.

## Configuration

| Setting | Value |
|---------|-------|
| **Environment variable** | `OPENROUTER_API_KEY` |
| **Credential store path** | `~/.jdai/credentials.enc` |
| **API endpoint** | `https://openrouter.ai/api/v1` |
| **Discovery timeout** | 10 seconds |
| **Request timeout** | 10 minutes |

**Credential resolution chain** (first match wins):

1. CLI flags — `--api-key`
2. Environment variable — `OPENROUTER_API_KEY`
3. Encrypted credential store — `~/.jdai/credentials.enc`

## Troubleshooting

### OpenRouter shows unavailable

Check that your API key is set and valid:

```bash
echo $OPENROUTER_API_KEY
```

Re-run the setup wizard to re-enter your key:

```text
/provider add openrouter
```

### Only 5 models listed

Discovery failed and JD.AI is using the fallback list. Common causes:

- **Invalid or expired API key** — regenerate at [openrouter.ai/keys](https://openrouter.ai/keys)
- **Network issues** — check internet connectivity
- **Timeout** — the discovery request has a 10-second timeout; try again

Once the issue is resolved, restart JD.AI or run `/provider test openrouter` to retry.

### Model not found

Use the full `vendor/model` ID — for example, `anthropic/claude-sonnet-4` not just `claude-sonnet-4`. Browse available models with `/models`.

### Unexpected pricing or capabilities

Pricing and capabilities are fetched from the OpenRouter API at startup. If values seem stale, restart JD.AI to re-fetch the catalog.

## See also

- [Provider Setup](provider-setup.md) — all 15 supported providers
- [Commands](commands.md) — `/models`, `/model`, `/provider`, and `/cost` commands
- [Configuration](configuration.md) — environment variables and credential storage
