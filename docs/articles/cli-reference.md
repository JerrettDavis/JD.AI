# CLI Reference

## Usage
```
jdai [options]
```

## Options
| Flag | Description |
|------|-------------|
| `--resume <id>` | Resume a specific session by ID |
| `--new` | Start a fresh session (skip loading previous) |
| `--force-update-check` | Force NuGet update check on startup |
| `--dangerously-skip-permissions` | Skip all tool confirmation prompts |

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

## Data directories
| Path | Purpose |
|------|---------|
| `~/.jdai/sessions.db` | SQLite session database |
| `~/.jdai/exports/` | Exported session JSON files |
| `~/.jdai/update-check.json` | Update check cache (24h) |
| `~/.dotnet/tools/jdai` | Tool binary location |

## Slash commands
See [Commands Reference](commands-reference.md) for the full list of 20 interactive commands.
