---
title: Tools Reference
description: "All built-in tools — file operations, search, shell, git, web, memory, subagent, think, environment, tasks, code execution, clipboard, questions, diff/patch, batch edit, and usage tracking — with parameters and examples."
---

# Tools Reference

JD.AI provides built-in tools that the AI agent invokes during conversations. Each tool call is confirmed before execution unless overridden by [`/autorun`](commands.md), [`/permissions`](commands.md), or the `--dangerously-skip-permissions` [CLI flag](cli.md).

Tools are grouped into fifteen categories: **File**, **Search**, **Shell**, **Git**, **Web**, **Web Search**, **Memory**, **Subagent**, **Think**, **Environment**, **Tasks**, **Code Execution**, **Clipboard**, **Questions**, **Diff/Patch**, **Batch Edit**, and **Usage Tracking**.

## File tools

| Function | Description |
|---|---|
| `read_file` | Read file contents with an optional line range. |
| `write_file` | Write content to a file, creating it if it does not exist. |
| `edit_file` | Replace exactly one occurrence of `oldStr` with `newStr`. |
| `list_directory` | Produce a tree-like directory listing. |

### Parameters

- **`read_file`** — `path` (string), `startLine` (int?, optional, 1-based), `endLine` (int?, optional, `-1` for EOF).
- **`write_file`** — `path` (string), `content` (string).
- **`edit_file`** — `path` (string), `oldStr` (string), `newStr` (string).
- **`list_directory`** — `path` (string?, defaults to cwd), `maxDepth` (int, default `2`).

### Example

```text
> read the first 20 lines of Program.cs
⚡ Tool: read_file(path: "Program.cs", startLine: 1, endLine: 20)
```

## Search tools

| Function | Description |
|---|---|
| `grep` | Regex search across file contents. |
| `glob` | Find files matching a glob pattern. |

### Parameters

- **`grep`** — `pattern` (string), `path` (string?, default cwd), `glob` (string?, file filter), `context` (int, default `0`), `ignoreCase` (bool, default `false`), `maxResults` (int, default `50`).
- **`glob`** — `pattern` (string, e.g. `**/*.cs`), `path` (string?, default cwd).

### Example

```text
> find all files that reference ILogger
⚡ Tool: grep(pattern: "ILogger", glob: "**/*.cs")
```

## Shell tools

| Function | Description |
|---|---|
| `run_command` | Execute a shell command and capture its output. |

### Parameters

- **`run_command`** — `command` (string), `cwd` (string?, default cwd), `timeoutSeconds` (int, default `60`).

### Example

```text
> run the tests
⚡ Tool: run_command(command: "dotnet test", timeoutSeconds: 120)
```

## Git tools

| Function | Description |
|---|---|
| `git_status` | Show working-tree status. |
| `git_diff` | Show differences between commits, index, or working tree. |
| `git_log` | Display recent commit history. |
| `git_commit` | Stage all changes and create a commit. |
| `git_push` | Push commits to the remote repository. |
| `git_pull` | Pull changes from the remote repository. |
| `git_branch` | List, create, or delete branches. |
| `git_checkout` | Switch branches or restore working tree files. |
| `git_stash` | Stash or restore uncommitted changes. |

### Parameters

- **`git_status`** — `path` (string?, default cwd).
- **`git_diff`** — `target` (string?, e.g. `"main"`, `"--staged"`), `path` (string?).
- **`git_log`** — `count` (int, default `10`), `path` (string?).
- **`git_commit`** — `message` (string), `path` (string?).
- **`git_push`** — `remote` (string, default `"origin"`), `branch` (string?, default current branch), `path` (string?).
- **`git_pull`** — `remote` (string, default `"origin"`), `branch` (string?, default current branch), `path` (string?).
- **`git_branch`** — `name` (string?, omit to list), `delete` (bool, default `false`), `path` (string?).
- **`git_checkout`** — `target` (string, branch/SHA/file), `createNew` (bool, default `false`), `path` (string?).
- **`git_stash`** — `action` (string: `"push"`, `"pop"`, `"list"`, `"drop"`, default `"push"`), `message` (string?, optional), `path` (string?).

### Example

```text
> push the changes to origin
⚡ Tool: git_push(remote: "origin")

> create a new feature branch
⚡ Tool: git_checkout(target: "feat/new-feature", createNew: true)

> stash my current changes
⚡ Tool: git_stash(action: "push", message: "WIP before refactor")
```

## Web tools

| Function | Description |
|---|---|
| `web_fetch` | Fetch a URL and return its content as readable text. |

### Parameters

- **`web_fetch`** — `url` (string), `maxLength` (int, default `5000`).

## Web Search tools

| Function | Description |
|---|---|
| `web_search` | Search the web for current information. |

### Parameters

- **`web_search`** — `query` (string), `count` (int, default `5`, max `10`).

### Example

```text
> search for the latest .NET 9 breaking changes
⚡ Tool: web_search(query: ".NET 9 breaking changes", count: 5)
```

## Memory tools

| Function | Description |
|---|---|
| `memory_store` | Store text in semantic memory for later retrieval. |
| `memory_search` | Search semantic memory by natural-language query. |
| `memory_forget` | Remove a memory entry by its ID. |

### Parameters

- **`memory_store`** — `text` (string), `category` (string?, optional).
- **`memory_search`** — `query` (string), `maxResults` (int, default `5`).
- **`memory_forget`** — `id` (string).

### Example

```text
> remember that the API key is stored in Azure Key Vault
⚡ Tool: memory_store(text: "API key is stored in Azure Key Vault", category: "architecture")
```

## Subagent tools

| Function | Description |
|---|---|
| `spawn_agent` | Spawn a specialized subagent for a focused task. |
| `spawn_team` | Orchestrate a team of cooperating agents. |
| `query_team_context` | Query the team's shared scratchpad. |

### Parameters

- **`spawn_agent`** — `type` (`explore` / `task` / `plan` / `review` / `general`), `prompt` (string), `mode` (`"single"` or `"multi"`).
- **`spawn_team`** — `strategy` (`sequential` / `fan-out` / `supervisor` / `debate`), `agents` (JSON array), `goal` (string), `multiTurn` (bool).
- **`query_team_context`** — `key` (string — a key name, `"events"`, or `"results"`).

### Example

```text
> review the changes in this PR
⚡ Tool: spawn_agent(type: "review", prompt: "Review the staged changes", mode: "single")
```

## Think tools

| Function | Description |
|---|---|
| `think` | Scratchpad for reasoning — no side effects. |

### Parameters

- **`think`** — `thought` (string).

### Example

```text
⚡ Tool: think(thought: "The user wants to refactor auth. I should first check the current implementation.")
```

## Environment tools

| Function | Description |
|---|---|
| `get_environment` | Returns OS, architecture, runtime, disk space, and tooling versions. |

### Parameters

- **`get_environment`** — `includeEnvVars` (bool, default `false`). When `true`, includes environment variables with secrets masked.

## Task tools

| Function | Description |
|---|---|
| `create_task` | Create a tracked work item with priority. |
| `list_tasks` | List tasks, optionally filtered by status. |
| `update_task` | Update a task's status, title, or description. |
| `complete_task` | Mark a task as done. |
| `export_tasks` | Export all tasks as JSON. |

### Parameters

- **`create_task`** — `title` (string), `description` (string?, optional), `priority` (`"low"` / `"medium"` / `"high"`, default `"medium"`).
- **`list_tasks`** — `status` (string?, filter: `"pending"`, `"in_progress"`, `"done"`, `"blocked"`).
- **`update_task`** — `id` (string), `status` (string?), `title` (string?), `description` (string?).
- **`complete_task`** — `id` (string).
- **`export_tasks`** — no parameters.

### Example

```text
> track the remaining work
⚡ Tool: create_task(title: "Add unit tests for AuthService", priority: "high")
⚡ Tool: list_tasks()
```

## Code execution tools

| Function | Description |
|---|---|
| `execute_code` | Run a code snippet in C#, Python, Node.js, Bash, or PowerShell. |

### Parameters

- **`execute_code`** — `language` (string: `"csharp"`, `"python"`, `"node"`, `"bash"`, `"powershell"`), `code` (string), `timeoutSeconds` (int, default `30`, max `300`).

Temporary files are created in the system temp directory and automatically cleaned up. Processes exceeding the timeout are killed.

### Example

```text
> test this regex in python
⚡ Tool: execute_code(language: "python", code: "import re; print(re.findall(r'\\d+', 'abc 123 def 456'))")
```

## Clipboard tools

| Function | Description |
|---|---|
| `read_clipboard` | Read text from the system clipboard. |
| `write_clipboard` | Write text to the system clipboard. |

Cross-platform: PowerShell/clip on Windows, pbcopy/pbpaste on macOS, xclip/xsel on Linux.

### Parameters

- **`read_clipboard`** — no parameters.
- **`write_clipboard`** — `text` (string).

## Question tools

| Function | Description |
|---|---|
| `ask_questions` | Present a structured questionnaire and collect validated answers. |

### Parameters

- **`ask_questions`** — `questionsJson` (string, JSON). Contains `title`, `context`, `questions[]` (each with `key`, `prompt`, `type`, `required`, `options[]`, `validation`), `allowCancel`, `submitLabel`.

### Supported question types

| Type | Description |
|---|---|
| `text` | Free-form text input |
| `confirm` | Yes/no confirmation |
| `singleSelect` | Pick one option from a list |
| `multiSelect` | Pick multiple options from a list |
| `number` | Numeric input with optional min/max bounds |

## Diff and patch tools

| Function | Description |
|---|---|
| `create_patch` | Generate unified diff output for review without modifying files. |
| `apply_patch` | Perform atomic multi-file text replacements. If any edit fails, no files are modified. |

### Parameters

- **`create_patch`** — `editsJson` (string, JSON array of `{path, oldText, newText}`).
- **`apply_patch`** — `editsJson` (string, JSON array of `{path, oldText, newText}`).

### Example

```text
⚡ Tool: create_patch(editsJson: "[{\"path\":\"src/Config.cs\",\"oldText\":\"Timeout = 30\",\"newText\":\"Timeout = 60\"}]")
```

## Batch edit tools

| Function | Description |
|---|---|
| `batch_edit_files` | Apply multiple text replacements across files in a single atomic operation. |

### Parameters

- **`batch_edit_files`** — `editsJson` (string, JSON array of `{path, oldText, newText}`). Groups edits by file, validates all, then writes atomically.

## Usage tracking tools

| Function | Description |
|---|---|
| `get_usage` | Returns session token counts, tool call count, turn count, and cost estimates. |
| `reset_usage` | Resets all session usage counters to zero. |

### Parameters

- **`get_usage`** — no parameters.
- **`reset_usage`** — no parameters.

## Tool safety tiers

Every tool belongs to a safety tier that controls confirmation behavior:

| Tier | Behavior | Tools |
|---|---|---|
| **Auto-approve** | Runs without confirmation | `read_file`, `grep`, `glob`, `list_directory`, `git_status`, `git_diff`, `git_log`, `git_branch`, `memory_search`, `web_fetch`, `ask_questions`, `think`, `get_environment`, `list_tasks`, `export_tasks`, `read_clipboard`, `get_usage`, `create_patch` |
| **Confirm once** | Asks once per session | `write_file`, `edit_file`, `git_commit`, `git_push`, `git_pull`, `git_checkout`, `git_stash`, `memory_store`, `memory_forget`, `create_task`, `update_task`, `complete_task`, `write_clipboard`, `spawn_agent`, `spawn_team`, `apply_patch`, `batch_edit_files`, `reset_usage` |
| **Always confirm** | Asks every invocation | `run_command`, `web_search`, `execute_code` |

## Controlling tool permissions

| Mechanism | Scope | Description |
|---|---|---|
| `/autorun` | Session | Toggle auto-approve for **all** tools in the current session. |
| `/permissions` | Session | Toggle permission checks entirely — no confirmations at all. |
| `--dangerously-skip-permissions` | Process | [CLI flag](cli.md) that disables all permission checks for the lifetime of the process. |

> [!WARNING]
> Disabling confirmations means the agent can write files, run commands, and commit code without asking. Use these overrides only in trusted, automated environments.

## See also

- [Commands Reference](commands.md) — interactive slash commands
- [CLI Reference](cli.md) — command-line flags
- [Providers Reference](providers.md) — AI provider configuration
- [User Guide: Tools](../user-guide/tools.md)
