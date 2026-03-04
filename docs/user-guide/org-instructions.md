---
description: "Set up organization-level instructions that apply to every JD.AI session across your engineering team, layered above project and user instructions."
---

# Organization Instructions

JD.AI supports a three-level instruction hierarchy — organization, project, and user — that lets teams share coding standards, tooling conventions, and policies without duplicating them in every repository. Organization instructions load first so they apply to every session, while project and user instructions narrow and override them for specific contexts.

When a developer opens JD.AI in any repository, the agent automatically loads org instructions from a configured path before looking for project files. Engineers get the team's conventions without any per-repo setup, and project owners can still override specific directives when a repository has different requirements.

## Quick start

**Step 1 — Create an org config repository.** This is a plain directory (typically a Git repository) that holds your organization's instruction files.

```bash
mkdir jdai-org-config
cd jdai-org-config
git init
```

**Step 2 — Create your org-level instruction file:**

```bash
cat > JDAI.md << 'EOF'
# Engineering Standards

## Build & Test
- Build: `dotnet build` (must produce zero warnings)
- Test: `dotnet test` (must pass before any commit)
- Format: `dotnet format` (run before committing)

## Code Style
- File-scoped namespaces on all new files
- Async/await throughout — never `.Result` or `.Wait()`
- ILogger<T> for logging — never `Console.WriteLine` in production code
- XML doc comments on all public APIs

## Security
- Never commit secrets, API keys, or connection strings
- Use environment variables or Azure Key Vault for credentials
- All HTTP clients must use TLS — no plain HTTP in production
EOF
```

**Step 3 — Point JD.AI to the org config repository.** Set the environment variable (or write the path to the config file — see [Configuration](#configuration)):

```bash
export JDAI_ORG_CONFIG=/path/to/jdai-org-config
```

**Step 4 — Verify that instructions loaded.** In any JD.AI session:

```text
/instructions
```

Org instructions appear first in the output, labeled with the `org:` prefix:

```text
Loaded instructions:
  ✓ org:JDAI.md (/path/to/jdai-org-config/JDAI.md, 412 chars)
  ✓ JDAI.md (/my-project/JDAI.md, 238 chars)
```

## Instruction hierarchy

JD.AI loads and merges instructions from three levels. All discovered files are concatenated and injected into the system prompt in this order:

```text
  1. Organization   ← org:JDAI.md, org:CLAUDE.md, etc.  (loaded first)
  2. Project        ← JDAI.md, CLAUDE.md, etc.           (per-repository)
  3. User           ← loaded from session context        (personal overrides)
```

**Concatenation, not conflict resolution.** All files are appended sequentially. The AI sees all instruction content. If a project instruction contradicts an org instruction, both are present in the system prompt and the AI applies judgment — it generally honors the more specific (project-level) directive.

**Precedence in practice.** Because org instructions appear first and project instructions follow, the AI treats project-level instructions as refinements of org standards rather than replacements. For hard requirements ("never commit secrets"), place them in org instructions so they always appear regardless of project content.

### Diagram

```text
System Prompt
═══════════════════════════════════════════
# Project Instructions (org:JDAI.md)       ← Organization: broad standards
{org instruction content}

---

# Project Instructions (JDAI.md)           ← Project: specific conventions
{project instruction content}
═══════════════════════════════════════════
```

## Setting up organization instructions

### Repository structure

An org config repository is a plain directory. JD.AI scans it for the same file names it looks for in project directories:

```text
jdai-org-config/
├── JDAI.md                         # primary org instructions (loaded first)
├── CLAUDE.md                       # loaded if JDAI.md not present
├── AGENTS.md                       # loaded if neither above present
├── .github/
│   └── copilot-instructions.md     # Copilot-compatible fallback
└── .jdai/
    └── instructions.md             # dot-directory variant
```

You can maintain multiple files if different tools need different formats. All discovered files are loaded.

### Pointing JD.AI at the org config

There are two mechanisms, checked in order:

**1. `JDAI_ORG_CONFIG` environment variable** (highest priority):

```bash
export JDAI_ORG_CONFIG=/path/to/jdai-org-config
```

Set this in your shell profile (`.bashrc`, `.zshrc`) or in your organization's standard dev environment setup script.

**2. `~/.jdai/org-config-path` file** (used when the env var is not set):

```bash
echo "/path/to/jdai-org-config" > ~/.jdai/org-config-path
```

Writing the path to this file persists the setting across terminal sessions without requiring a shell profile change.

### Complete example org instruction file

```markdown
# Acme Engineering Standards

## Build & Test
- Build: `dotnet build Acme.slnx` (zero warnings required)
- Test: `dotnet test --filter "Category!=Integration"` before every commit
- Integration tests: `dotnet test --filter "Category=Integration"` on CI only
- Format: `dotnet format Acme.slnx` — run before opening a PR

## Code Style
- File-scoped namespaces on all new files
- Primary constructors for simple types (C# 12+)
- Async/await throughout — never `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`
- `ILogger<T>` for logging — never `Console.WriteLine` in production code
- `ArgumentNullException.ThrowIfNull` for null checks in public APIs
- XML doc comments on all public APIs (`<summary>` minimum)

## Architecture Decisions
- All database access goes through repository interfaces — no direct EF calls in handlers
- MediatR for CQRS — commands and queries separated into Commands/ and Queries/
- All HTTP calls use `IHttpClientFactory` — no raw `new HttpClient()`
- Feature flags via `IFeatureManager` (Azure App Configuration)

## Git Conventions
- Conventional commits: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`
- PR branches: `feature/`, `fix/`, `chore/`, `hotfix/`
- Squash merge to main — no merge commits
- Always rebase on main before requesting review

## Security
- Never commit secrets, API keys, or connection strings
- Use Azure Key Vault for production credentials; user-secrets for local dev
- All external HTTP calls must use HTTPS — no plain HTTP
- Input validation on all public endpoints using FluentValidation

## Approved AI Providers
- Claude Code (primary)
- GitHub Copilot (secondary)
- Local models via LLamaSharp for offline work
- Do not use unapproved providers on systems with access to production data
```

## Configuration

### Environment variables

| Variable | Description | Default |
|---|---|---|
| `JDAI_ORG_CONFIG` | Path to the org config directory. Checked first. Must point to an existing directory or it is ignored. | — |

### `~/.jdai/org-config-path` file

When `JDAI_ORG_CONFIG` is not set, JD.AI reads the path from `~/.jdai/org-config-path`. The file must contain a single line with the absolute path to the org config directory.

```text
~/.jdai/org-config-path
```

Contents:

```text
/home/alice/work/jdai-org-config
```

### Resolution order

```text
1. JDAI_ORG_CONFIG environment variable
      ↓ (if not set or directory does not exist)
2. ~/.jdai/org-config-path file
      ↓ (if file does not exist or path is invalid)
3. No org instructions loaded — project behavior unchanged
```

If the configured path does not exist on disk, JD.AI silently skips org instructions and continues with project-level loading. No error is raised.

## File discovery

JD.AI scans the org config directory for instruction files using the same priority list as project instructions:

| Priority | File name | Notes |
|:-:|---|---|
| 1 | `JDAI.md` | JD.AI native format — loaded first |
| 2 | `CLAUDE.md` | Claude Code compatibility |
| 3 | `AGENTS.md` | Codex CLI / OpenAI Agents SDK compatibility |
| 4 | `.github/copilot-instructions.md` | GitHub Copilot compatibility |
| 5 | `.jdai/instructions.md` | Dot-directory variant |

All files that exist and are non-empty are loaded. Files whose content is entirely whitespace are skipped.

**`org:` prefix.** Every file loaded from the org config path is identified in the loaded file list with an `org:` prefix. For example, `JDAI.md` from the org config appears as `org:JDAI.md` and `CLAUDE.md` appears as `org:CLAUDE.md`. This makes it straightforward to distinguish org-level files from project-level files in the `/instructions` output and in the system prompt.

**Include directives.** Org instruction files support the same `include:` directive as project files:

```markdown
# Acme Engineering Standards

include: sections/code-style.md
include: sections/security-policy.md
include: sections/approved-tools.md
```

Relative paths are resolved from the directory containing the instruction file. Absolute paths are used as-is. If an included file does not exist, a `<!-- include not found: path -->` comment is inserted in its place.

## Viewing loaded instructions

The `/instructions` command shows all loaded instruction files with their source path and character count:

```text
/instructions
```

Output when both org and project instructions are loaded:

```text
Loaded instructions:
  ✓ org:JDAI.md (/home/alice/work/jdai-org-config/JDAI.md, 1024 chars)
  ✓ org:CLAUDE.md (/home/alice/work/jdai-org-config/CLAUDE.md, 256 chars)
  ✓ JDAI.md (/my-project/JDAI.md, 412 chars)
```

Org instructions are always listed first. Files labeled `org:` come from the org config path.

> [!NOTE]
> The system prompt concatenates all files in the order shown. Org instructions appear before project instructions so the AI reads org-level context first.

## Best practices

**Keep org instructions general.** Org files should contain standards that apply to every engineer on every project: coding conventions, tooling requirements, security rules, and approved providers. Avoid project-specific details in org instructions — they belong in each repository's own `JDAI.md`.

**Use project instructions for project-specific details.** Build commands, deployment procedures, database migration steps, and module-specific notes belong in the project's `JDAI.md`. The org instructions set the baseline; the project narrows it.

**Do not duplicate.** If a convention is in the org instructions, do not repeat it in every project's `JDAI.md`. The AI sees both. Duplication adds noise without improving adherence.

**Use include directives for modular org instructions.** For large engineering organizations with many teams, split the org instructions into focused topic files and use `include:` to compose them:

```text
jdai-org-config/
├── JDAI.md                   # top-level: include: directives only
├── sections/
│   ├── code-style.md
│   ├── security.md
│   ├── git-conventions.md
│   └── approved-providers.md
```

```markdown
# Acme Engineering Standards

include: sections/code-style.md
include: sections/security.md
include: sections/git-conventions.md
include: sections/approved-providers.md
```

This makes it easier to update individual policies without editing a single large file and reduces merge conflicts when multiple teams contribute.

**Version the org config repository.** Because org instructions affect every developer's JD.AI session, treat changes the same way you would treat changes to shared tooling — review them, document them in commit messages, and communicate changes to engineers.

## Example org instructions

The following is a complete, production-quality org instruction file for an engineering organization using .NET and Azure:

```markdown
# Contoso Engineering — JD.AI Standards

## Build & Test
- Build: `dotnet build src/Contoso.slnx` (zero warnings, zero errors)
- Unit tests: `dotnet test --filter "Category=Unit"` — always run before committing
- Integration tests: `dotnet test --filter "Category=Integration"` — CI only
- Format: `dotnet format src/Contoso.slnx` — run before pushing
- Minimum code coverage: 80% on new code

## Code Style
- C# 12+ features: primary constructors, collection expressions, file-scoped namespaces
- Async/await everywhere — never `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`
- `ILogger<T>` injection for all logging — `Console.WriteLine` only in CLI tools
- `ArgumentNullException.ThrowIfNull` for public API null guards
- XML doc on all `public` and `protected` members — `<summary>` is mandatory
- Nullable reference types enabled on all new projects

## Architecture
- CQRS via MediatR: commands in `Commands/`, queries in `Queries/`
- No EF Core calls outside of repository implementations
- `IHttpClientFactory` for all outbound HTTP — no `new HttpClient()`
- All feature flags use `IFeatureManager` (Azure App Configuration)
- Domain events published via MassTransit on Azure Service Bus

## Git Conventions
- Conventional commits: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`, `perf:`
- PR branches: `feature/`, `fix/`, `chore/`, `hotfix/`, `release/`
- Squash merge to main — no merge commits in main history
- Rebase on main before requesting review
- PR description: include what changed, why, and how to test

## Security
- Secrets in Azure Key Vault (prod) or `dotnet user-secrets` (local) — never in source
- All external HTTP calls use HTTPS — HTTP is rejected by policy
- Input validation on all public endpoints using FluentValidation
- SQL queries use parameterized statements or EF Core — no string interpolation in queries
- Dependency audit: `dotnet list package --vulnerable` must be clean before release

## Approved AI Providers
- Claude Code — primary, approved for all code on all systems
- GitHub Copilot — approved for all code on all systems
- Ollama (local) — approved for offline and privacy-sensitive work
- LLamaSharp (local) — approved for air-gapped environments
- Do not use unapproved cloud providers on systems with access to production data or PII
```

## See also

- [Configuration](configuration.md) — environment variables, data directories, and instruction file format
- [Commands Reference](commands-reference.md) — `/instructions` and other slash commands
- [Common Workflows](common-workflows.md) — practical development guides
