# CLI Reference

![JD.AI CLI usage and flags](../images/demo-cli.png)

## Usage
```
jdai [options]
```

## Options
| Flag | Description |
|------|-------------|
| `--new` | Start a fresh session (skip loading previous) |
| `--resume <id>` | Resume a specific session by ID |
| `--model <name>` | Select model by name (skips interactive picker) |
| `--provider <name>` | Filter models to a specific provider |
| `--force-update-check` | Force NuGet update check on startup |
| `--dangerously-skip-permissions` | Skip all tool confirmation prompts |
| `--gateway` | Start in gateway mode (HTTP/SignalR control plane) |
| `--gateway-port <port>` | Port for gateway API (default: `18789`) |

## Examples
```bash
# Start in current directory
jdai

# Resume a specific session
jdai --resume abc123

# Start fresh, no persistence
jdai --new

# Skip all permissions (use with caution)
jdai --dangerously-skip-permissions

# Start gateway control plane
jdai --gateway

# Start gateway on a custom port
jdai --gateway --gateway-port 9090
```

## Exit codes
| Code | Meaning |
|------|---------|
| 0 | Normal exit |
| 1 | Unhandled error |

## Environment variables
| Variable | Description | Default |
|----------|-------------|---------|
| `OLLAMA_ENDPOINT` | Ollama API endpoint | `http://localhost:11434` |
| `OLLAMA_CHAT_MODEL` | Default chat model for Ollama | `llama3.2:latest` |
| `OLLAMA_EMBEDDING_MODEL` | Default embedding model | `all-minilm:latest` |
| `OPENAI_API_KEY` | OpenAI / Codex API key | — |
| `CODEX_TOKEN` | Codex CLI access token override | — |
| `JDAI_MODELS_DIR` | Local model storage directory | `~/.jdai/models/` |
| `HF_HOME` | HuggingFace cache directory | `~/.cache/huggingface/` |
| `HF_TOKEN` | HuggingFace API token | — |

## Data directories
| Path | Purpose |
|------|---------|
| `~/.jdai/sessions.db` | SQLite session database |
| `~/.jdai/exports/` | Exported session JSON files |
| `~/.jdai/update-check.json` | Update check cache (24h) |
| `~/.dotnet/tools/jdai` | Tool binary location |

## Slash commands
See [Commands Reference](commands-reference.md) for the full list of 20 interactive commands.
