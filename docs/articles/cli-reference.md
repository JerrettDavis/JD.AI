# CLI Reference

![JD.AI CLI usage and flags](../images/demo-cli.png)

## Usage

```text
jdai [options]
jdai mcp [subcommand] [args]
```

## Primary modes

### Interactive mode (default)

```bash
jdai
```

### Print mode (non-interactive)

```bash
jdai --print "summarize this file"
cat README.md | jdai -p "extract key risks" --output-format json
```

### MCP management mode

```bash
jdai mcp list
jdai mcp add localfs --transport stdio --command npx --args -y @modelcontextprotocol/server-filesystem .
```

## Options

| Flag | Mode | Description |
|---|---|---|
| `--new` | Interactive | Start a fresh session instead of resuming |
| `--resume <id>` | Interactive | Resume a specific session ID |
| `-c`, `--continue` | Interactive | Resume most recent session for current project |
| `--model <name>` | All | Select model by ID/display-name substring |
| `--provider <name>` | All | Prefer provider by name |
| `--force-update-check` | Interactive | Force NuGet update check on startup |
| `--dangerously-skip-permissions` | All | Disable tool confirmations |
| `--gateway` | Interactive | Start embedded gateway host |
| `--gateway-port <port>` | Interactive | Gateway host port (default `5100`) |
| `-p`, `--print <query>` | Print | Run one-shot request and exit |
| `--output-format <text\|json>` | Print | Output format for print mode (default `text`) |
| `--max-turns <n>` | Print | Max turn cap for print mode |
| `--system-prompt <text>` | All | Replace default system prompt |
| `--append-system-prompt <text>` | All | Append text to system prompt |
| `--system-prompt-file <path>` | All | Replace system prompt from file |
| `--append-system-prompt-file <path>` | All | Append system prompt text from file |
| `--allowedTools <csv>` | All | Allow-list tool/plugin function exposure |
| `--disallowedTools <csv>` | All | Deny-list tool/plugin exposure |
| `--verbose` | All | Enable verbose session mode |
| `--add-dir <path>` | All | Additional workspace directory hint (repeatable) |

## Examples

```bash
# Interactive session, auto-resume latest project conversation
jdai --continue

# Start fresh session on a specific provider/model
jdai --new --provider OpenAI --model gpt-4o

# One-shot JSON output for automation
jdai --print "summarize open PR risks" --output-format json --max-turns 2

# Run with prompt override
jdai --system-prompt "You are a strict static-analysis assistant"

# Restrict tools to read/search only
jdai --allowedTools read_file,grep,glob,list_directory
```

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Normal exit |
| `1` | Unhandled error or invalid startup state |

## Environment variables

| Variable | Description | Default |
|---|---|---|
| `OLLAMA_ENDPOINT` | Ollama API endpoint | `http://localhost:11434` |
| `OLLAMA_CHAT_MODEL` | Default Ollama chat model | `llama3.2:latest` |
| `OLLAMA_EMBEDDING_MODEL` | Default embedding model | `all-minilm:latest` |
| `OPENAI_API_KEY` | OpenAI / Codex API key | — |
| `CODEX_TOKEN` | Codex CLI access token override | — |
| `JDAI_MODELS_DIR` | Local model storage directory | `~/.jdai/models/` |
| `HF_HOME` | HuggingFace cache directory | `~/.cache/huggingface/` |
| `HF_TOKEN` | HuggingFace API token | — |

## Data directories

| Path | Purpose |
|---|---|
| `~/.jdai/sessions.db` | SQLite session database |
| `~/.jdai/exports/` | Exported session JSON files |
| `~/.jdai/models/` | Local model storage and registry |
| `~/.jdai/update-check.json` | Update check cache |
| `~/.jdai/workflows/` | Workflow catalog artifacts |
| `~/.jdai/agents.json` | Agent profile store |
| `~/.jdai/hooks.json` | Hook profile store |

## Interactive references

- [Interactive Mode](interactive-mode.md)
- [Commands Reference](commands-reference.md)
- [Tools Reference](tools-reference.md)
