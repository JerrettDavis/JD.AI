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

## Organization instructions

JD.AI supports organization-level instruction files that apply across all projects. Org instructions are loaded before project instructions and establish baseline conventions for your team.

Set up an org config repository and point JD.AI to it:

```bash
export JDAI_ORG_CONFIG=/path/to/org-config-repo
```

See [Organization Instructions](org-instructions.md) for the full guide.

## Governance and policies

For teams and organizations, JD.AI supports YAML-defined policies that control tool access, provider restrictions, budget limits, data redaction, and audit logging. Policies are scoped hierarchically and enforced at tool invocation time.

See [Governance & Policies](governance.md) and [Audit Logging](audit-logging.md) for details.

## Directory structure

JD.AI stores local state in `~/.jdai/`:

```text
~/.jdai/
├── sessions.db          # SQLite session database
├── budget.json          # Budget tracking (daily/monthly spend)
├── org-config-path      # Path to org config repo (optional)
├── update-check.json    # NuGet update cache
├── audit/               # Audit logs (daily-rotated JSONL)
│   └── audit-2026-03-03.jsonl
├── exports/             # Exported session JSON files
├── models/              # Local GGUF models and registry
│   └── registry.json    # Model manifest
├── policies/            # User-level policy YAML files
│   └── security.yaml
├── workflows/           # Local workflow definitions
└── workflow-store/      # Git-backed shared workflow cache
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

### System prompt budget

Large system prompts (from JDAI.md, org instructions, skills, plugins, and appended prompts) can consume a significant portion of the context window, leaving less room for conversation. JD.AI can detect when the system prompt exceeds a configurable budget and optionally compact it automatically.

**Commands:**

```text
/compact-system-prompt           # Compact the system prompt now
/compact-system-prompt off       # Never auto-compact (default)
/compact-system-prompt auto      # Compact when system prompt exceeds budget
/compact-system-prompt always    # Always compact system prompt at startup
```

**TUI settings** (persisted to `~/.jdai/tui-settings.json`):

| Setting | Description | Default |
|---|---|---|
| `systemPromptBudgetPercent` | Maximum percentage of context window the system prompt should use (0–100) | `20` |
| `systemPromptCompaction` | Compaction mode: `off`, `auto`, or `always` | `off` |

**Budget calculation:** The budget is `contextWindow × (systemPromptBudgetPercent / 100)`. For example, with a 200k context window and 20% budget, the system prompt budget is 40k tokens. When the system prompt exceeds this budget, JD.AI either shows a warning (when compaction is `off`) or automatically compacts the prompt (when `auto` or `always`).

Each model carries its own context window size. Claude models use 200k; GPT-4o and Ollama models default to 128k. The `/context` command reflects the actual context window of the current model.

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
| `JDAI_ORG_CONFIG` | Path to organization config repo for org-level instructions | — |
| `JDAI_WORKFLOW_STORE_REPO` | Git repo URL for shared workflow store | — |
| `JDAI_DATA_DIR` | Override root data directory (all JD.AI state) | `~/.jdai/` |

## See also

- [Governance & Policies](governance.md) — Tool restrictions, provider policies, budget limits
- [Audit Logging](audit-logging.md) — Audit event sinks and compliance logging
- [Shared Workflow Store](workflow-store.md) — Share and discover workflows across teams
- [Organization Instructions](org-instructions.md) — Org-wide instruction files
