---
title: Configuration Reference
description: "Full configuration reference — config.json schema, JDAI.md syntax, per-project overrides, AtomicConfigStore behavior, and configuration precedence."
---

# Configuration Reference

JD.AI is configured through JSON files, project instruction files, slash commands, and CLI flags. No configuration is required to get started — sensible defaults apply out of the box.

## Configuration precedence

Settings are resolved in this priority order (highest wins):

| Priority | Source | Scope | Set via |
|---|---|---|---|
| 1 | CLI flags | Process | `--provider`, `--model`, `--system-prompt`, etc. |
| 2 | Session state | Session | `/model`, `/provider`, `/autorun`, `/permissions` |
| 3 | Per-project defaults | Project | `/default project provider`, `/default project model` |
| 4 | Global defaults | User | `/default provider`, `/default model` |
| 5 | Environment variables | System | `OPENAI_API_KEY`, `OLLAMA_ENDPOINT`, etc. |
| 6 | Built-in defaults | Application | Hard-coded fallbacks |

## Global configuration: `~/.jdai/config.json`

### Full schema

```json
{
  "defaults": {
    "provider": "openai",
    "model": "gpt-4o"
  },
  "projectDefaults": {
    "/home/user/projects/my-app": {
      "provider": "ollama",
      "model": "llama3.2:latest"
    },
    "C:\\Projects\\enterprise-api": {
      "provider": "azure-openai",
      "model": "gpt-4o"
    }
  }
}
```

### Fields

| Field | Type | Description |
|---|---|---|
| `defaults` | object | Global default provider and model |
| `defaults.provider` | string? | Provider identifier (e.g. `"openai"`, `"ollama"`, `"anthropic"`) |
| `defaults.model` | string? | Model identifier (e.g. `"gpt-4o"`, `"llama3.2:latest"`) |
| `projectDefaults` | object | Per-project overrides, keyed by absolute project path |
| `projectDefaults.<path>.provider` | string? | Project-specific provider override |
| `projectDefaults.<path>.model` | string? | Project-specific model override |

All fields are optional. Null/missing values fall through to the next priority level.

### Setting defaults via commands

```text
/default                           # Show current defaults (global + project)
/default provider openai           # Set global default provider
/default model gpt-4o              # Set global default model
/default project provider ollama   # Set per-project default provider
/default project model llama3.2    # Set per-project default model
```

## Per-project defaults: `.jdai/defaults.json`

Per-project defaults are stored in `.jdai/defaults.json` in the project root directory:

```json
{
  "defaultProvider": "ollama",
  "defaultModel": "llama3.2:latest"
}
```

These override global defaults when JD.AI is launched from that project directory.

## Project instructions: JDAI.md

`JDAI.md` is a Markdown file placed in the repository root. JD.AI reads it at session start and injects its contents into the system prompt.

### File search order

JD.AI searches for instruction files in priority order. All discovered files are merged, with `JDAI.md` taking highest priority:

| Priority | File | Compatibility |
|---|---|---|
| 1 | `JDAI.md` | JD.AI native |
| 2 | `CLAUDE.md` | Claude Code |
| 3 | `AGENTS.md` | Codex CLI |
| 4 | `.github/copilot-instructions.md` | GitHub Copilot |
| 5 | `.jdai/instructions.md` | Dot-directory variant |

### Example JDAI.md

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

# Project Notes
- Authentication module is in src/Auth/ — uses JWT
- Database migrations: `dotnet ef database update`
```

### Content guidelines

| ✅ Include | ❌ Exclude |
|---|---|
| Build/test commands | Obvious language conventions |
| Code style rules that differ from defaults | Standard patterns the AI already knows |
| Project-specific conventions | Detailed API documentation |
| Architecture decisions | File-by-file code descriptions |
| Environment quirks | Information that changes frequently |

### Viewing loaded instructions

```text
/instructions    # Show all loaded instruction content
```

## AtomicConfigStore behavior

`~/.jdai/config.json` is managed by the `AtomicConfigStore` class, which provides:

### File locking

- **Cross-process safety**: File-level lock via `config.json.lock`
- **In-process safety**: `SemaphoreSlim` for concurrent access
- **Retry policy**: 5 retries with exponential backoff (50ms → 100ms → 200ms → 400ms → 800ms)

### Atomic writes

1. Current config is backed up to `config.json.bak`
2. New config is written to `config.json.tmp`
3. Temp file is atomically moved to `config.json`

### Corruption recovery

- If `config.json` is empty or contains invalid JSON, a new empty config is returned
- The `.bak` file can be manually restored if needed
- Round-trip validation: serialized JSON is deserialized before writing to catch serialization bugs

### Read behavior

```
File exists + valid JSON  → deserialized config
File exists + empty       → empty config (defaults)
File exists + invalid     → empty config (defaults)
File missing              → empty config (defaults)
```

## Skills, plugins, and hooks

JD.AI uses native `.jdai` locations for skills/plugins and keeps `.claude` skills as lower-precedence compatibility inputs.

### Skills

| Path | Scope |
|---|---|
| `<install>/skills/` | Bundled baseline skills |
| `~/.jdai/skills/` | User-managed skills |
| `.jdai/skills/` | Workspace skills |
| `~/.claude/skills/` | Legacy compatibility skills (lower precedence) |
| `.claude/skills/` | Legacy compatibility skills (lower precedence) |

Skill runtime policy/config:

| Path | Scope |
|---|---|
| `~/.jdai/skills.json` | User skill lifecycle config |
| `.jdai/skills.json` | Workspace overrides |

### Plugins and hooks

| Path | Scope |
|---|---|
| `~/.jdai/plugins/` | Personal plugins |
| `.jdai/plugins/` | Project plugins |
| `~/.jdai/hooks.json` | Hook profiles and toggles |

See [Skills and Plugins](../developer-guide/plugins.md) for lifecycle, precedence, and gating details.

## Data directory structure

```text
~/.jdai/
├── config.json          # Global defaults (managed by AtomicConfigStore)
├── config.json.bak      # Backup of previous config
├── config.json.lock     # File lock (transient)
├── skills/              # User-managed skills
├── skills.json          # User skills lifecycle config
├── runtime/
│   └── skills/          # Active staged skills for current runtime
├── credentials/         # Encrypted credential store
├── sessions.db          # SQLite session database
├── update-check.json    # NuGet update cache (24h TTL)
├── exports/             # Exported session JSON files
└── models/              # Local GGUF models
    └── registry.json    # Model manifest
```

## Runtime configuration commands

| Command | Effect |
|---|---|
| `/autorun` | Toggle auto-approve for tool execution |
| `/permissions` | Toggle all permission checks |
| `/compact` | Force context compaction |
| `/clear` | Clear conversation history |
| `/spinner [style]` | Change spinner animation style |
| `/config list` | Show persisted runtime settings |
| `/config get <key>` | Read a persisted runtime setting |
| `/config set <key> <value>` | Write a persisted runtime setting |

### `/config` keys

| Key | Meaning | Example |
|---|---|---|
| `theme` | Terminal theme token | `/config set theme nord` |
| `vim_mode` | Vim editing mode | `/config set vim_mode on` |
| `output_style` | Output renderer mode | `/config set output_style compact` |
| `spinner_style` | Spinner/progress style | `/config set spinner_style rich` |
| `prompt_cache` | Auto prompt caching for supported providers | `/config set prompt_cache on` |
| `prompt_cache_ttl` | Prompt cache TTL (`5m` or `1h`) | `/config set prompt_cache_ttl 1h` |
| `autorun` | Auto-run tool confirmation behavior | `/config set autorun off` |
| `permissions` | Global permission checks | `/config set permissions on` |
| `plan_mode` | Plan mode state | `/config set plan_mode off` |

### Prompt caching defaults

- `prompt_cache=on`
- `prompt_cache_ttl=5m`
- Optional extended TTL: `/config set prompt_cache_ttl 1h`

See [Prompt Caching Reference](prompt-caching.md) for provider support, thresholds, and behavior.

## See also

- [CLI Reference](cli.md) — CLI flags that override configuration
- [Environment Variables](environment-variables.md) — env var reference
- [Providers Reference](providers.md) — provider configuration
- [Prompt Caching Reference](prompt-caching.md) — cache policy and runtime controls
- [User Guide: Configuration](../user-guide/configuration.md)
- [Configuration (guide)](../user-guide/configuration.md) — tutorial-style guide
