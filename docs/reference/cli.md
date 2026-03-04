---
title: CLI Reference
description: "Complete reference for JD.AI command-line flags, environment variables, exit codes, data directories, and piped input."
---

# CLI Reference

## Usage

```
jdai [options] [query]
jdai -p|--print <query>
jdai mcp <subcommand>
echo "input" | jdai --print "query"
```

## Options

### Session control

| Flag | Description |
|---|---|
| `--new` | Start a fresh session (skip loading previous) |
| `--resume <id>`, `-r <id>` | Resume a specific session by ID |
| `--continue`, `-c` | Auto-resume the most recent session for the current project |

### Provider and model selection

| Flag | Description |
|---|---|
| `--provider <name>` | Filter models to a specific provider (e.g. `openai`, `ollama`, `anthropic`) |
| `--model <name>` | Select model by name or ID; skips the interactive picker. Fuzzy-matched. |

### Print mode (non-interactive)

| Flag | Description |
|---|---|
| `--print`, `-p` | Non-interactive mode. Sends the query, prints the response to stdout, and exits. |
| `--output-format <fmt>` | Output format for print mode: `text` (default) or `json` |
| `--max-turns <n>` | Maximum agent loop turns before exiting (print mode). Exits with code 1 if exceeded. |

### System prompt

| Flag | Description |
|---|---|
| `--system-prompt <text>` | Replace the default system prompt with `<text>` |
| `--append-system-prompt <text>` | Append `<text>` to the default system prompt |
| `--system-prompt-file <path>` | Replace the default system prompt with the contents of `<path>` |
| `--append-system-prompt-file <path>` | Append the contents of `<path>` to the default system prompt |

### Tool control

| Flag | Description |
|---|---|
| `--allowedTools <list>` | Comma-separated list of tool names to enable. All other tools are removed. |
| `--disallowedTools <list>` | Comma-separated list of tool names to disable. |
| `--dangerously-skip-permissions` | Skip all tool confirmation prompts for the lifetime of the process |

### Directories

| Flag | Description |
|---|---|
| `--add-dir <path>` | Add an additional working directory. Can be specified multiple times. |

### Diagnostics

| Flag | Description |
|---|---|
| `--verbose` | Enable verbose/debug output |
| `--force-update-check` | Force NuGet update check on startup |

### Gateway mode

| Flag | Description |
|---|---|
| `--gateway` | Start in gateway mode (HTTP/SignalR control plane alongside TUI) |
| `--gateway-port <port>` | Port for the gateway API (default: `5100`) |

### MCP subcommand

```
jdai mcp list
jdai mcp <subcommand>
```

Runs JD.AI as an MCP (Model Context Protocol) server. Subcommands are handled by the MCP CLI handler.

## Environment variables

### Provider API keys

| Variable | Description | Default |
|---|---|---|
| `OPENAI_API_KEY` | OpenAI / Codex API key | — |
| `ANTHROPIC_API_KEY` | Anthropic API key | — |
| `GOOGLE_AI_API_KEY` | Google Gemini API key | — |
| `MISTRAL_API_KEY` | Mistral API key | — |
| `HUGGINGFACE_API_KEY` | HuggingFace Inference API key | — |
| `AZURE_OPENAI_API_KEY` | Azure OpenAI API key | — |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI endpoint URL | — |
| `AWS_ACCESS_KEY_ID` | AWS Bedrock access key | — |
| `AWS_SECRET_ACCESS_KEY` | AWS Bedrock secret key | — |
| `AWS_REGION` | AWS Bedrock region | `us-east-1` |
| `GROQ_API_KEY` | Groq API key (OpenAI-compatible) | — |
| `TOGETHER_API_KEY` | Together AI API key | — |
| `DEEPSEEK_API_KEY` | DeepSeek API key | — |
| `OPENROUTER_API_KEY` | OpenRouter API key | — |
| `FIREWORKS_API_KEY` | Fireworks AI API key | — |
| `PERPLEXITY_API_KEY` | Perplexity API key | — |
| `CODEX_TOKEN` | Codex CLI access token override | — |
| `HF_TOKEN` | HuggingFace API token (legacy) | — |

### Runtime configuration

| Variable | Description | Default |
|---|---|---|
| `OLLAMA_ENDPOINT` | Ollama API endpoint URL | `http://localhost:11434` |
| `OLLAMA_CHAT_MODEL` | Default chat model for Ollama | `llama3.2:latest` |
| `OLLAMA_EMBEDDING_MODEL` | Default embedding model for Ollama | `all-minilm:latest` |
| `JDAI_MODELS_DIR` | Local GGUF model storage directory | `~/.jdai/models/` |
| `HF_HOME` | HuggingFace cache directory | `~/.cache/huggingface/` |

### Telemetry

| Variable | Description | Default |
|---|---|---|
| `OTEL_SERVICE_NAME` | Override service name in traces/metrics | `jdai` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Activate OTLP exporter and set endpoint | — |

See [Environment Variables](environment-variables.md) for the full consolidated reference.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Normal exit |
| `1` | Unhandled error, or `--max-turns` exceeded in print mode |

## Data directories

| Path | Purpose |
|---|---|
| `~/.jdai/` | JD.AI data root |
| `~/.jdai/config.json` | Global default provider/model configuration |
| `~/.jdai/sessions.db` | SQLite session database |
| `~/.jdai/exports/` | Exported session JSON files |
| `~/.jdai/models/` | Local GGUF models and registry |
| `~/.jdai/models/registry.json` | Local model manifest |
| `~/.jdai/credentials/` | Encrypted credential store |
| `~/.jdai/update-check.json` | NuGet update check cache (24h TTL) |
| `.jdai/defaults.json` | Per-project default provider/model overrides |
| `~/.dotnet/tools/jdai` | Tool binary location |

## Piped input

JD.AI reads from stdin when input is redirected. Combine with `--print` for scripting:

```bash
# Pipe file content as context
cat README.md | jdai --print "Summarize this file"

# Pipe command output
dotnet test 2>&1 | jdai -p "Explain these test failures"

# Combine piped input with a query
echo "SELECT * FROM users" | jdai --print "Optimize this SQL query"

# JSON output for scripting
echo "hello" | jdai --print "translate to French" --output-format json
```

Piped input is prepended to the query separated by `---`.

## Examples

```bash
# Start interactive session in current directory
jdai

# Start fresh session (no persistence restore)
jdai --new

# Resume a specific session
jdai --resume abc123

# Continue most recent session for this project
jdai --continue

# Select a specific model
jdai --model gpt-4o --provider openai

# Non-interactive print mode
jdai --print "Explain this codebase"

# Print mode with JSON output and turn limit
jdai --print "Refactor auth" --output-format json --max-turns 5

# Custom system prompt
jdai --system-prompt "You are a code reviewer. Be concise."

# Append to default system prompt
jdai --append-system-prompt "Always use British English."

# Add additional working directories
jdai --add-dir ../shared-lib --add-dir ../common

# Restrict available tools
jdai --allowedTools read_file,grep,glob,list_directory

# Start with gateway enabled
jdai --gateway --gateway-port 9090

# Skip all tool confirmations
jdai --dangerously-skip-permissions
```

## See also

- [Commands Reference](commands.md) — interactive slash commands
- [Configuration Reference](configuration.md) — config files and precedence
- [Environment Variables](environment-variables.md) — all env vars
- [User Guide: Quickstart](../user-guide/quickstart.md)
