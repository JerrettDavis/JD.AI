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
├── sessions.db          # SQLite session database
├── update-check.json    # NuGet update cache
└── exports/             # Exported session JSON files
```

## Skills, plugins, and hooks

JD.AI loads Claude Code extensions from standard locations:

- `~/.claude/skills/` — Personal skills
- `~/.claude/plugins/` — Personal plugins
- `.claude/skills/` — Project skills
- `.claude/plugins/` — Project plugins

These extensions are registered as Semantic Kernel functions and filters at startup.

See [Skills and Plugins](skills-and-plugins.md) for details.

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

## Environment variables

| Variable | Description | Default |
|---|---|---|
| `OLLAMA_ENDPOINT` | Ollama API URL | `http://localhost:11434` |
| `OLLAMA_CHAT_MODEL` | Default Ollama chat model | `llama3.2:latest` |
| `OLLAMA_EMBEDDING_MODEL` | Default embedding model | `all-minilm:latest` |
| `OPENAI_API_KEY` | OpenAI / Codex API key (if not using CLI auth) | — |
| `CODEX_TOKEN` | Codex CLI access token override | — |
| `JDAI_MODELS_DIR` | Local model storage and registry directory | `~/.jdai/models/` |
| `HF_HOME` | HuggingFace cache directory (for local model scanning) | `~/.cache/huggingface/` |
| `HF_TOKEN` | HuggingFace API token for authenticated access | — |
