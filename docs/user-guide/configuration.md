---
title: Configuration
description: Configure JD.AI with project instructions, default commands, config files, and environment variables.
---

# Configuration

JD.AI works out of the box with sensible defaults. This page covers the main ways to customize it — project instruction files, default commands, config files, and environment variables. For the full configuration reference, see [Configuration Reference](../reference/configuration.md).

## Project instructions (JDAI.md)

`JDAI.md` is a special file that JD.AI reads at the start of every session. Place it in your repository root to declare build commands, code style rules, and project-specific conventions. JD.AI injects its contents into the system prompt so every response respects your project's standards.

### File search order

JD.AI looks for instruction files in this priority order:

1. `JDAI.md` — JD.AI native format
2. `CLAUDE.md` — recognized instruction filename
3. `AGENTS.md` — recognized instruction filename
4. `.github/copilot-instructions.md` — recognized instruction filename
5. `.jdai/instructions.md` — dot-directory variant

All discovered files are merged, with `JDAI.md` taking highest priority when directives overlap.

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

### What to include vs. exclude

| ✅ Include | ❌ Exclude |
|-----------|-----------|
| Build/test commands | Obvious language conventions |
| Code style rules that differ from defaults | Standard patterns the AI already knows |
| Project-specific conventions | Detailed API documentation |
| Architecture decisions | File-by-file descriptions |

### View loaded instructions

```text
/instructions    # Show all loaded instruction content
```

## Setting defaults with `/default`

Persist your preferred provider and model so they're used for all future sessions:

```text
/default provider openai     # Set global default provider
/default model gpt-4o        # Set global default model
/default                     # Show current defaults
```

### Per-project defaults

Override global defaults for a specific project:

```text
/default project provider ollama
/default project model llama3.2:latest
```

Per-project defaults are stored in `.jdai/defaults.json` in the project root.

### Resolution priority

When determining which provider and model to use:

1. **CLI flags** — `--provider` and `--model` passed at launch
2. **Session state** — set during the session via `/model` or `/provider`
3. **Per-project defaults** — `.jdai/defaults.json`
4. **Global defaults** — `~/.jdai/config.json`
5. **First available** — first provider with a valid connection

## Global config file

`~/.jdai/config.json` stores global defaults:

```json
{
  "defaultProvider": "openai",
  "defaultModel": "gpt-4o"
}
```

Edit this file directly or use the `/default` commands.

## Directory structure

JD.AI stores local state in `~/.jdai/`:

```text
~/.jdai/
├── config.json          # Global default provider/model
├── credentials.enc      # Encrypted credential store
├── sessions.db          # SQLite session database
├── update-check.json    # NuGet update cache
├── exports/             # Exported session JSON files
└── models/              # Local GGUF models and registry
    └── registry.json    # Model manifest
```

## Environment variables

| Variable | Description | Default |
|----------|-------------|---------|
| `OLLAMA_ENDPOINT` | Ollama API URL | `http://localhost:11434` |
| `OLLAMA_CHAT_MODEL` | Default Ollama chat model | `llama3.2:latest` |
| `OLLAMA_EMBEDDING_MODEL` | Default embedding model | `all-minilm:latest` |
| `OPENAI_API_KEY` | OpenAI / Codex API key | — |
| `ANTHROPIC_API_KEY` | Anthropic API key | — |
| `JDAI_MODELS_DIR` | Local model storage directory | `~/.jdai/models/` |
| `HF_HOME` | HuggingFace cache directory | `~/.cache/huggingface/` |
| `HF_TOKEN` | HuggingFace API token | — |

See [Provider Setup](provider-setup.md) for the full list of provider-specific environment variables.

### Governance and workflow store

| Variable | Description | Default |
|----------|-------------|---------|
| `JDAI_ORG_CONFIG` | Path to organization policy YAML directory | — |
| `JDAI_WORKFLOW_STORE_URL` | Git remote URL for shared workflow store | — (local file store) |

See [Governance & Policies](governance.md) for the full governance configuration reference, and [Environment Variables](../reference/environment-variables.md) for the complete list.

## Runtime commands

| Command | Description |
|---------|-------------|
| `/autorun` | Toggle auto-approve for tool execution |
| `/permissions` | Toggle all permission checks |
| `/compact` | Force context compaction |
| `/clear` | Clear conversation history |
| `/spinner [style]` | Change the loading animation |
| `/config list` | Show persisted runtime settings |
| `/config get prompt_cache` | Show prompt caching enablement |
| `/config set prompt_cache on|off` | Enable or disable automatic prompt caching |
| `/config set prompt_cache_ttl 5m|1h` | Set prompt cache TTL for supported providers |

Prompt caching defaults:

- `prompt_cache=on`
- `prompt_cache_ttl=5m`

See [Prompt Caching Reference](../reference/prompt-caching.md) for thresholds and provider behavior.

## See also

- [Provider Setup](provider-setup.md) — provider credentials and environment variables
- [Sessions & History](sessions.md) — session storage and persistence
- [Checkpointing](checkpointing.md) — checkpoint strategy configuration
- [Governance & Policies](governance.md) — policy documents, budget limits, tool/provider restrictions
- [Audit Logging](audit-logging.md) — audit sinks and compliance logging
- [Shared Workflow Store](workflow-store.md) — publishing and installing shared workflows
- [Configuration Reference](../reference/configuration.md) — full reference
