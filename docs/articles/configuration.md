# Configuration

JD.AI configuration comes from three layers:

- Project instruction files (`JDAI.md`, `CLAUDE.md`, `AGENTS.md`, and compatible variants)
- Runtime command settings (`/config`, `/theme`, `/vim`, `/spinner`, `/output-style`)
- Environment variables and CLI flags

## Project instructions (`JDAI.md` and compatible files)

JD.AI loads instruction files into the system prompt at startup.

### Search order

1. `JDAI.md`
2. `CLAUDE.md`
3. `AGENTS.md`
4. `.github/copilot-instructions.md`
5. `.jdai/instructions.md`

Use `/instructions` to inspect what was loaded.

### Quick starter template

```markdown
# Build and Test
- Build: `dotnet build JD.AI.slnx`
- Test: `dotnet test JD.AI.slnx`
- Format: `dotnet format JD.AI.slnx --verify-no-changes`

# Code Style
- File-scoped namespaces
- Nullable reference types enabled
- Public APIs require XML docs

# Repo conventions
- Conventional commits
- Feature work must include tests
```

## Runtime command settings

JD.AI persists TUI/runtime settings in:

- `~/.jdai/tui-settings.json` (or resolved JD.AI data root)

The following commands update persisted state:

- `/spinner`
- `/theme`
- `/vim`
- `/output-style`
- `/config set ...`

### `/config` keys

| Key | Meaning | Example |
|---|---|---|
| `theme` | Terminal theme token | `/config set theme nord` |
| `vim_mode` | Vim editing mode | `/config set vim_mode on` |
| `output_style` | Output renderer mode | `/config set output_style compact` |
| `spinner_style` | Spinner/progress style | `/config set spinner_style rich` |
| `autorun` | Auto-run tool confirmation behavior | `/config set autorun off` |
| `permissions` | Global permission checks | `/config set permissions on` |
| `plan_mode` | Plan mode state | `/config set plan_mode off` |

## Data directory resolution

JD.AI data root is resolved in this order:

1. `JDAI_DATA_DIR` (if set)
2. Current user profile `~/.jdai`
3. Existing `.jdai` under discovered user profiles
4. Machine fallback (`%ProgramData%/JD.AI` on Windows, `/var/lib/jdai` on Linux/macOS)

## Data files and folders

| Path | Purpose |
|---|---|
| `sessions.db` | Session persistence store |
| `vectors.db` | Vector memory database |
| `exports/` | Exported session JSON |
| `models/` | Local GGUF model registry/storage |
| `workflows/` | Workflow catalog artifacts |
| `agents.json` | Local agent profiles (`/agents`) |
| `hooks.json` | Local hook profiles (`/hooks`) |
| `tui-settings.json` | Theme/vim/spinner/output settings |
| `update-check.json` | Update check cache |

## Environment variables

| Variable | Description | Default |
|---|---|---|
| `JDAI_DATA_DIR` | Override JD.AI data directory root | auto-resolved |
| `OLLAMA_ENDPOINT` | Ollama API URL | `http://localhost:11434` |
| `OLLAMA_CHAT_MODEL` | Default Ollama chat model | `llama3.2:latest` |
| `OLLAMA_EMBEDDING_MODEL` | Default Ollama embedding model | `all-minilm:latest` |
| `OPENAI_API_KEY` | OpenAI/Codex API key | — |
| `CODEX_TOKEN` | Codex token override | — |
| `JDAI_MODELS_DIR` | Local model directory override | `~/.jdai/models/` |
| `HF_HOME` | HuggingFace cache path | `~/.cache/huggingface/` |
| `HF_TOKEN` | HuggingFace API token | — |

## Related docs

- [Interactive Mode](interactive-mode.md)
- [Commands Reference](commands-reference.md)
- [CLI Reference](cli-reference.md)
- [Tools Reference](tools-reference.md)
