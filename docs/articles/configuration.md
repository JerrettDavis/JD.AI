# Configuration

JD.AI is configured through project instruction files and runtime commands. No external config files or environment variables are required to get started — sensible defaults apply out of the box.

## Project instructions (JDAI.md)

`JDAI.md` is a special file that JD.AI reads at the start of every session. Place it in your repository root to declare build commands, code style rules, and project-specific conventions. JD.AI injects its contents into the system prompt so every response respects your project's standards.

### File search order

JD.AI searches for instruction files in this priority order:

1. `JDAI.md` — JD.AI native format
2. `CLAUDE.md` — Claude Code compatibility
3. `AGENTS.md` — Codex CLI compatibility
4. `.github/copilot-instructions.md` — Copilot compatibility
5. `.jdai/instructions.md` — dot-directory variant

All discovered files are merged into the system prompt, with `JDAI.md` taking the highest priority when directives overlap.

### Writing effective instructions

A good instruction file is concise and focused on information the AI cannot infer from the code alone.

**Good JDAI.md example:**

```markdown
# Build & Test
- Build: `dotnet build MyProject.slnx`
- Test: `dotnet test --filter "Category!=Integration"`
- Format: `dotnet format MyProject.slnx`
- Lint: build must pass with zero warnings

# Code Style
- File-scoped namespaces
- XML doc comments on all public APIs
- Async/await throughout (no .Result or .Wait())
- ILogger<T> for logging, never Console.WriteLine

# Git Conventions
- Conventional commits (feat:, fix:, chore:, etc.)
- PR branches: feature/, fix/, chore/
- Always rebase on main before merging

# Project Notes
- Authentication module is in src/Auth/ — uses JWT
- Database migrations: `dotnet ef database update`
```

**What to include:**

| ✅ Include | ❌ Exclude |
|---|---|
| Build/test commands | Obvious language conventions |
| Code style rules that differ from defaults | Standard patterns the AI already knows |
| Project-specific conventions | Detailed API documentation |
| Architecture decisions | File-by-file code descriptions |
| Environment quirks | Information that changes frequently |

### View loaded instructions

```text
/instructions    # Show all loaded instruction content
```

## Directory structure

JD.AI stores local state in `~/.jdai/`:

```text
~/.jdai/
├── config.json          # Global default provider/model
├── sessions.db          # SQLite session database
├── update-check.json    # NuGet update cache
├── exports/             # Exported session JSON files
└── models/              # Local GGUF models and registry
    └── registry.json    # Model manifest
```

## Data directories

JD.AI stores local state and models in the following directories:

```text
~/.jdai/
├── config.json          # Global default provider/model
├── sessions.db          # SQLite session database
├── update-check.json    # NuGet update cache
├── exports/             # Exported session JSON files
└── models/              # Local GGUF models and registry
    └── registry.json    # Model manifest
```

### Data root resolution

JD.AI resolves the data root in this order:

1. `JDAI_DATA_DIR` (if set)
2. Current user profile `~/.jdai`
3. Existing `.jdai` under discovered user profiles
4. Machine fallback (`%ProgramData%/JD.AI` on Windows, `/var/lib/jdai` on Linux/macOS)

### Data files and folders

| Path | Purpose |
|---|---|
| `config.json` | Global default provider/model settings |
| `sessions.db` | Session persistence store |
| `vectors.db` | Vector memory database |
| `exports/` | Exported session JSON files |
| `models/` | Local GGUF model registry/storage |
| `workflows/` | Workflow catalog artifacts |
| `agents.json` | Local agent profiles (`/agents`) |
| `hooks.json` | Local hook profiles (`/hooks`) |
| `tui-settings.json` | Theme/vim/spinner/output settings |
| `update-check.json` | Update check cache |

## Skills, plugins, and hooks

JD.AI loads Claude Code extensions from standard locations:

- `~/.claude/skills/` — Personal skills
- `~/.claude/plugins/` — Personal plugins
- `.claude/skills/` — Project skills
- `.claude/plugins/` — Project plugins

These extensions are registered as Semantic Kernel functions and filters at startup.

See [Skills and Plugins](skills-and-plugins.md) for details.

## Default provider and model

JD.AI supports persistent defaults for provider and model selection at both global and per-project levels.

### Global defaults

Global defaults are stored in `~/.jdai/config.json` and apply to all sessions unless overridden:

```json
{
  "defaultProvider": "openai",
  "defaultModel": "gpt-4o"
}
```

Set global defaults with:

```text
/default provider openai     # Set global default provider
/default model gpt-4o        # Set global default model
/default                     # Show current defaults
```

### Per-project defaults

Per-project defaults are stored in `.jdai/defaults.json` in the project root and override global defaults when working in that directory:

```json
{
  "defaultProvider": "ollama",
  "defaultModel": "llama3.2:latest"
}
```

Set per-project defaults with:

```text
/default project provider ollama
/default project model llama3.2:latest
```

### Resolution priority

When determining which provider and model to use, JD.AI resolves through this priority chain:

1. **CLI flags** — `--provider` and `--model` arguments passed at launch
2. **Session state** — provider/model set during the current session via `/model` or `/provider`
3. **Per-project defaults** — `.jdai/defaults.json` in the project root
4. **Global defaults** — `~/.jdai/config.json`
5. **First available** — the first provider with a valid connection (startup detection order)

## Runtime configuration

### Auto-approve mode

```text
/autorun        # Toggle auto-approve for tools
/permissions    # Toggle all permission checks
```

### Context management

```text
/compact        # Force context compaction
/clear          # Clear conversation history
```

### Runtime command settings

JD.AI persists TUI/runtime settings in `tui-settings.json` under the resolved data root.

The following commands update persisted runtime state:

- `/spinner`
- `/theme`
- `/vim`
- `/output-style`
- `/config set ...`

#### `/config` keys

| Key | Meaning | Example |
|---|---|---|
| `theme` | Terminal theme token | `/config set theme nord` |
| `vim_mode` | Vim editing mode | `/config set vim_mode on` |
| `output_style` | Output renderer mode | `/config set output_style compact` |
| `spinner_style` | Spinner/progress style | `/config set spinner_style rich` |
| `autorun` | Auto-run tool confirmation behavior | `/config set autorun off` |
| `permissions` | Global permission checks | `/config set permissions on` |
| `plan_mode` | Plan mode state | `/config set plan_mode off` |

## Environment variables

| Variable | Description | Default |
|---|---|---|
| `JDAI_DATA_DIR` | Override JD.AI data directory root | auto-resolved |
| `OLLAMA_ENDPOINT` | Ollama API URL | `http://localhost:11434` |
| `OLLAMA_CHAT_MODEL` | Default Ollama chat model | `llama3.2:latest` |
| `OLLAMA_EMBEDDING_MODEL` | Default embedding model | `all-minilm:latest` |
| `OPENAI_API_KEY` | OpenAI / Codex API key (if not using CLI auth) | — |
| `CODEX_TOKEN` | Codex CLI access token override | — |
| `JDAI_MODELS_DIR` | Local model storage and registry directory | `~/.jdai/models/` |
| `HF_HOME` | HuggingFace cache directory (for local model scanning) | `~/.cache/huggingface/` |
| `HF_TOKEN` | HuggingFace API token for authenticated access | — |
| `OTEL_SERVICE_NAME` | Override `Gateway:Telemetry:ServiceName` in traces/metrics | `"jdai"` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Activate OTLP exporter and set its endpoint (e.g. `http://localhost:4317`) | — |

See [Observability](observability.md) for full telemetry configuration and exporter options.

## Related docs

- [Interactive Mode](interactive-mode.md)
- [Commands Reference](commands-reference.md)
- [CLI Reference](cli-reference.md)
- [Tools Reference](tools-reference.md)
