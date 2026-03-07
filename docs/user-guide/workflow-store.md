---
description: "Publish, discover, and install reusable workflows across teams using the shared workflow store — backed by a local directory or a Git repository."
---

# Shared Workflow Store

The shared workflow store is a centralized catalog for publishing and distributing reusable JD.AI workflows across teams and organizations. Rather than recreating the same multi-step automation for every engineer, a team member publishes a workflow once and anyone with access to the store can browse, install, and run it in their own session.

The store is designed to fit existing Git workflows. When backed by a Git repository, it uses `git pull` before every read and `git commit && git push` on every write, so the catalog stays in sync across machines without any separate service or deployment.

## Quick start

**Step 1 — Configure the store.** Register an `IWorkflowStore` implementation in your host application. For the Git-backed store, provide the repository URL when constructing `GitWorkflowStore`:

```csharp
var workflowStore = new GitWorkflowStore("https://github.com/my-org/jdai-workflows.git");
```

**Step 2 — Browse the shared catalog** to see what workflows are available:

```text
/workflow catalog
```

Output:

```text
Shared Workflow Catalog (3):
  code-review v1.0.0 [review, git] (Team) — alice — Standard code review checklist
  deploy-staging v2.1.0 [deploy, infra] (Organization) — bob — Deploy branch to staging
  onboard-repo v1.0.0 [onboarding] (Public) — alice — First-run setup for new repositories
```

**Step 3 — Install a workflow** to your local catalog:

```text
/workflow install code-review
```

The workflow is downloaded to `~/.jdai/workflows/` and immediately available via `/workflow list`.

**Step 4 — Publish a workflow** you have captured locally:

```text
/workflow publish code-review
```

JD.AI reads the workflow from your local catalog, packages it as `SharedWorkflow` JSON, and pushes it to the store.

## Architecture

### Local catalog

Every JD.AI session maintains a local workflow catalog via `FileWorkflowCatalog`. Workflows are captured automatically when the agent executes a recognized multi-step pattern, or you can export any workflow manually. The local catalog stores definitions at:

```text
~/.jdai/workflows/
  {name}-{version}.json   # e.g. code-review-1.0.json
```

The local catalog is personal — it is not shared unless you publish explicitly.

### Shared workflow store

The shared workflow store (`IWorkflowStore`) sits above the local catalog and provides team-level sharing. There are two concrete backends:

- **`FileWorkflowStore`** — A directory on a shared filesystem (or local machine for single-user setups). Suitable for teams with a network file share.
- **`GitWorkflowStore`** — A Git repository cloned to `~/.jdai/workflow-store/`. Every read pulls, every write commits and pushes.

### Relationship between local catalog and shared store

```text
                           ┌────────────────────────────────┐
                           │   Shared Workflow Store         │
                           │   (IWorkflowStore)              │
                           │                                 │
                           │  FileWorkflowStore              │
                           │    {baseDir}/{name}/{version}   │
                           │                                 │
                           │  GitWorkflowStore               │
                           │    ~/.jdai/workflow-store/      │
                           └────────────┬───────────────────┘
                                        │  publish / install
                    ┌───────────────────▼───────────────────┐
                    │    Local Workflow Catalog               │
                    │    (FileWorkflowCatalog)                │
                    │    ~/.jdai/workflows/                   │
                    └────────────────────────────────────────┘
```

Publishing copies a workflow from the local catalog into the shared store. Installing copies a workflow from the shared store into the local catalog.

## SharedWorkflow model

Every entry in the shared store is a `SharedWorkflow` record serialized as indented camelCase JSON.

### Field reference

| Field | Type | Default | Description |
|---|---|---|---|
| `id` | `string` (16-char hex) | Auto-generated | Unique identifier derived from a new GUID. Used for ID-based lookup when a workflow is renamed. |
| `name` | `string` | — | Human-readable workflow name. Used as the primary lookup key and as the directory name in the store. |
| `version` | `string` (semver) | `"1.0.0"` | Semantic version. Multiple versions of the same workflow can coexist in the store. |
| `description` | `string` | `""` | Short summary shown in catalog listings. |
| `author` | `string` | `""` | Author name or email. Populated automatically when publishing from the local catalog. |
| `tags` | `string[]` | `[]` | Arbitrary tags for filtering and search (e.g. `["review", "ci"]`). |
| `requiredTools` | `string[]` | `[]` | Tool names the workflow depends on (e.g. `["git_commit", "run_command"]`). Informational only. |
| `visibility` | `WorkflowVisibility` | `Team` | Access scope. See [Workflow Visibility](#workflow-visibility). |
| `publishedAt` | `DateTimeOffset` | UTC now | Timestamp set at publish time. |
| `definitionJson` | `string` | `""` | The full `AgentWorkflowDefinition` serialized as JSON. |

### Example SharedWorkflow JSON

```json
{
  "id": "a3f1c9e2b7d04e88",
  "name": "code-review",
  "version": "1.2.0",
  "description": "Standard code review: diff, analyze, comment",
  "author": "alice",
  "tags": ["review", "git", "quality"],
  "requiredTools": ["git_diff", "git_commit"],
  "visibility": "Team",
  "publishedAt": "2026-03-01T10:00:00+00:00",
  "definitionJson": "{\"name\":\"code-review\",\"version\":\"1.2.0\", ...}"
}
```

## Storage backends

### File store

`FileWorkflowStore` stores workflows as JSON files under a base directory using a two-level hierarchy:

```text
{baseDir}/
  {name}/
    {version}.json
```

For example:

```text
/shared/jdai-store/
  code-review/
    1.0.0.json
    1.2.0.json
  deploy-staging/
    2.1.0.json
```

Special characters in names and versions are replaced with underscores so file names are safe across all operating systems.

**When to use:** The file store is appropriate for small teams with a shared network drive, for local development and testing, and as a fallback when Git is not available.

### Git store

`GitWorkflowStore` stores workflows inside a Git repository. The repository is cloned to `~/.jdai/workflow-store/` on first use. The layout inside the repo mirrors the file store:

```text
~/.jdai/workflow-store/     ← local clone
  .git/
  code-review/
    1.0.0.json
    1.2.0.json
  deploy-staging/
    2.1.0.json
```

**How operations map to Git commands:**

| Operation | Git activity |
|---|---|
| Catalog / Search / Versions / Get | `git pull --ff-only` then read from local clone |
| Publish | `git pull --ff-only` → write file → `git add` → `git commit` → `git push` |
| Install | `git pull --ff-only` then copy file to `~/.jdai/workflows/` |
| First use (no local clone) | `git clone {repoUrl} workflow-store` |

Commit messages use the format: `publish: {name} v{version}`.

If the pull fails (offline or empty repository), JD.AI falls back to the existing local clone and continues operating. The store is non-blocking for offline use.

**Configure the Git store** by injecting a `GitWorkflowStore` into the application. Both HTTPS and SSH URLs are supported:

```csharp
// HTTPS
var store = new GitWorkflowStore("https://github.com/my-org/jdai-workflows.git");
// SSH
var store = new GitWorkflowStore("git@github.com:my-org/jdai-workflows.git");
```

> [!TIP]
> Use a dedicated repository for workflows rather than adding them to a source code repository. This keeps the catalog clean and makes it easy to grant read access to everyone in the organization.

### Backend comparison

| Feature | File store | Git store |
|---|:-:|:-:|
| Multi-machine sync | Requires shared filesystem | Automatic (pull/push) |
| Version history | Directory listing only | Full Git log |
| Access control | Filesystem permissions | Repository permissions |
| Offline use | Always available | Falls back to local clone |
| Setup complexity | None | Git repository required |
| Best for | Local dev, single-machine | Team and org sharing |

## TUI commands

### `/workflow catalog [tag=<tag>] [author=<author>]`

Browse all workflows in the shared store. The catalog shows the latest version of each workflow with its tags, visibility, author, and description.

```text
/workflow catalog
/workflow catalog tag=review
/workflow catalog author=alice
/workflow catalog tag=deploy author=bob
```

Output example:

```text
Shared Workflow Catalog (3):
  code-review v1.2.0 [review, git] — alice — Standard code review checklist
  deploy-staging v2.1.0 [deploy, infra] (Organization) — bob — Deploy branch to staging
  onboard-repo v1.0.0 [onboarding] (Public) — alice — First-run setup for new repos
```

> [!NOTE]
> The catalog always shows the latest version of each workflow. Use `/workflow versions <name>` to see all published versions.

### `/workflow publish <name>`

Publish a workflow from your local catalog to the shared store.

```text
/workflow publish code-review
```

JD.AI reads the workflow definition from the local catalog, serializes it as a `SharedWorkflow`, and writes it to the store. For the Git store, this performs a pull, commit, and push automatically.

> [!WARNING]
> Publishing overwrites any existing entry with the same name and version. Increment the version before publishing a revised workflow.

### `/workflow install <name>[@version]`

Download a workflow from the shared store to your local catalog at `~/.jdai/workflows/`.

```text
/workflow install code-review            # installs latest version
/workflow install code-review@1.0.0     # installs a specific version
```

The installed file is named `{name}-{version}.json` inside `~/.jdai/workflows/`. After installation the workflow appears in `/workflow list`.

### `/workflow search <query>`

Full-text search across workflow names, descriptions, authors, and tags. Matching is case-insensitive substring search across all versions in the store.

```text
/workflow search deploy
/workflow search "code review"
```

Output example:

```text
Search results for 'deploy' (2):
  deploy-staging v2.1.0 [deploy, infra] — bob — Deploy branch to staging
  deploy-prod v1.0.0 [deploy, infra] — bob — Production deployment with approval gate
```

### `/workflow versions <name>`

List all published versions of a workflow with publish timestamps and authors.

```text
/workflow versions code-review
```

Output example:

```text
Versions of 'code-review' (2):
  v1.0.0 — published 2026-01-15 08:30 UTC by alice
  v1.2.0 — published 2026-02-20 14:05 UTC by alice
```

### Local workflow commands

These commands operate on the local catalog (`~/.jdai/workflows/`) and do not require a configured store:

| Command | Description |
|---|---|
| `/workflow list` | List all workflows in the local catalog. |
| `/workflow show <name>` | Show the full definition JSON for a workflow. |
| `/workflow export <name> [json\|csharp\|mermaid]` | Export a workflow in the requested format. |
| `/workflow replay <name> [version]` | Show the step-by-step execution plan (dry run). |

## CLI equivalents

There are no separate CLI subcommands for workflow store operations. All workflow commands, including store operations, are accessed through the interactive TUI using the `/workflow` slash command.

Start `jdai` in your project directory to access workflows:

```bash
jdai
> /workflow catalog
> /workflow install code-review
> /workflow publish my-workflow
```

For scripting or non-interactive use, pair with print mode:

```bash
jdai -p "/workflow catalog"
jdai -p "/workflow search deploy"
```

## Workflow visibility

Visibility controls who can see and install a workflow. The `visibility` field is stored in every `SharedWorkflow` record and is displayed in catalog listings for non-default levels.

| Level | Meaning |
|---|---|
| `Private` | Only the author can see and install the workflow. |
| `Team` | Team members with access to the store repository can install. This is the default. |
| `Organization` | All members of the organization can install, including read-only store access. |
| `Public` | Anyone with access to the store — including external collaborators — can install. |

> [!NOTE]
> Visibility is enforced at the application level, not the Git repository level. All records in the repository are readable by anyone with repository access. Use repository permissions (branch protection, private repos) to enforce actual access boundaries.

## Versioning

The workflow store uses semantic versioning (`major.minor.patch`). The default version for new workflows is `1.0.0`.

**How the latest version is resolved:**

When you request a workflow by name without specifying a version (`/workflow install code-review`), the store returns the version with the lexicographically highest file name. For semver strings this corresponds to the highest release, provided versions follow the `major.minor.patch` format consistently.

To pin a specific version, use the `@version` syntax:

```text
/workflow install code-review@1.0.0
```

**Version history rules:**

- Publishing a workflow with an existing name and version **overwrites** that version.
- Publishing with a new version **adds** a new entry alongside existing versions.
- `/workflow versions <name>` shows all published versions in ascending order.

> [!TIP]
> For CI/CD pipelines that depend on a specific workflow, always install with an explicit version (`@1.0.0`). Using the latest resolves at install time and can change without notice.

## Configuration

### Environment variables

| Variable | Description | Default |
|---|---|---|
| `JDAI_WORKFLOW_STORE_URL` | Git repository URL (HTTPS or SSH) for the shared workflow store. When set, uses `GitWorkflowStore`; otherwise falls back to a local `FileWorkflowStore` at `~/.jdai/workflow-store/`. | — |

### Local cache location

The Git store clones the remote repository to:

```text
~/.jdai/workflow-store/
```

This directory is managed automatically. Do not edit files in this directory by hand — changes will be overwritten on the next `git pull`.

### Directory structure

```text
~/.jdai/
├── sessions.db              # SQLite session database
├── workflows/               # Local workflow catalog (FileWorkflowCatalog)
│   ├── code-review-1.0.json
│   └── deploy-staging-2.1.json
└── workflow-store/          # Git-backed shared store (GitWorkflowStore)
    ├── .git/
    ├── code-review/
    │   ├── 1.0.0.json
    │   └── 1.2.0.json
    └── deploy-staging/
        └── 2.1.0.json
```

## Best practices

**Tag workflows for discoverability.** Catalog browsing and search both use tags. Use short, consistent tags that describe the domain (`review`, `deploy`, `test`, `ci`, `onboarding`) and the tooling involved (`git`, `dotnet`, `docker`).

**Write clear names and descriptions.** The name is the primary key — keep it lowercase, hyphen-separated, and descriptive. The description appears in every catalog listing; make it a single sentence that answers "what does this workflow do?"

**Pin versions in CI/CD.** Workflows installed for automated pipelines should use explicit versions. The latest resolution can change when a new version is published, breaking reproducible builds.

**Keep workflows small and composable.** A workflow that does one thing well is easier to reuse than one that does many things. Use the `Nested` step kind to compose larger workflows from smaller ones — install the components individually so teams can mix and match.

**Increment versions on breaking changes.** If you change the steps of a published workflow in a way that changes behavior, publish it as a new version rather than overwriting the existing one. Teams pinned to the old version continue to work.

## See also

- [Configuration](configuration.md) — environment variables and data directory layout
- [Common Workflows](common-workflows.md) — practical guides for everyday development tasks
- [Commands Reference](../reference/commands.md) — complete slash command reference
