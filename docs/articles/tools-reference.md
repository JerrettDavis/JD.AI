---
description: "All built-in tools — file operations, search, shell, git, web, memory, and subagent spawning — with parameters and examples."
---

# Tools reference

JD.AI provides a set of built-in tools that the AI agent invokes automatically during conversations. Each tool call is confirmed before execution unless overridden by [`/autorun`](common-workflows.md), [`/permissions`](common-workflows.md), or the `--dangerously-skip-permissions` CLI flag.

Tools are grouped into eight categories: **File**, **Search**, **Shell**, **Git**, **Web**, **Web Search**, **Memory**, and **Subagent**.

![Tool execution showing file reading and grep](../images/demo-tools.png)

## File tools

| Function | Description |
|----------|-------------|
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
|----------|-------------|
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
|----------|-------------|
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
|----------|-------------|
| `git_status` | Show working-tree status. |
| `git_diff` | Show differences between commits, index, or working tree. |
| `git_log` | Display recent commit history. |
| `git_commit` | Stage all changes and create a commit. |

### Parameters

- **`git_status`** — `path` (string?, default cwd).
- **`git_diff`** — `target` (string?, e.g. `"main"`, `"--staged"`), `path` (string?).
- **`git_log`** — `count` (int, default `10`), `path` (string?).
- **`git_commit`** — `message` (string), `path` (string?).

### Example

```text
> show me what changed since main
⚡ Tool: git_diff(target: "main")
```

## Web tools

| Function | Description |
|----------|-------------|
| `web_fetch` | Fetch a URL and return its content as readable text. |

### Parameters

- **`web_fetch`** — `url` (string), `maxLength` (int, default `5000`).

## Web Search tools

| Function | Description |
|----------|-------------|
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
|----------|-------------|
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
|----------|-------------|
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

## Tool safety tiers

Every tool belongs to a safety tier that controls how confirmation is handled:

| Tier | Behavior | Tools |
|------|----------|-------|
| **Auto-approve** | Runs without confirmation | `read_file`, `grep`, `glob`, `list_directory`, `git_status`, `git_log`, `memory_search` |
| **Confirm once** | Asks once per session | `web_fetch`, `web_search` |
| **Always confirm** | Asks every invocation | `write_file`, `edit_file`, `run_command`, `git_commit` |

## Controlling tool permissions

Three mechanisms override the default confirmation behavior:

| Mechanism | Scope | Description |
|-----------|-------|-------------|
| `/autorun` | Session | Toggle auto-approve for **all** tools in the current session. |
| `/permissions` | Session | Toggle permission checks entirely — no confirmations at all. |
| `--dangerously-skip-permissions` | Process | CLI flag that disables all permission checks for the lifetime of the process. |

> [!WARNING]
> Disabling confirmations means the agent can write files, run commands, and commit code without asking. Use these overrides only in trusted, automated environments.
