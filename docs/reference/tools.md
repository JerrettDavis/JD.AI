---
description: "All built-in tools — file, search, shell, git, web, memory, subagent, think, environment, task, code execution, clipboard, question, diff/patch, batch-edit, usage, encoding/crypto, and Tailscale tools — with parameters and examples."
---

# Tools reference

JD.AI provides a set of built-in tools that the AI agent invokes automatically during conversations. Each tool call is confirmed before execution unless overridden by [`/autorun`](../user-guide/common-workflows.md), [`/permissions`](../user-guide/common-workflows.md), or the `--dangerously-skip-permissions` CLI flag.

Tools are grouped into twenty-seven categories: **File**, **Search**, **Shell**, **Exec/Process Sessions**, **Git**, **Web**, **Web Search**, **Memory**, **Subagent**, **Think**, **Environment**, **Tasks**, **Code Execution**, **Clipboard**, **Questions**, **Diff/Patch**, **Batch Edit**, **Usage Tracking**, **Encoding & Crypto**, **Tailscale Integration**, **Multimodal**, **Code Execution (Sandboxed)**, **Task Management**, **Scheduling**, **Notebook**, **Capability Introspection**, and **Policy/Governance**.

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

## Exec/process session tools

| Function | Description |
|----------|-------------|
| `exec` | Execute a command in foreground or background with process-session tracking. |
| `process` | Manage process sessions via `list`, `poll`, `log`, `write`, `kill`, `clear`, `remove`. |

### Parameters

- **`exec`** — `command` (string), `cwd` (string?, default cwd), `yieldMs` (int, default `250`), `background` (bool, default `false`), `timeoutMs` (int, default `60000`, `0` disables timeout), `pty` (bool, default `false`), `host` (string, currently only `"local"`).
- **`process`** — `action` (string), `id` (string?, required for single-session actions), `input` (string?, for `write`), `yieldMs` (int, poll wait), `maxChars` (int, default `4000`), `force` (bool, for `clear`/`remove`).

### Example

```text
> start tests in background
⚡ Tool: exec(command: "dotnet test", background: true, yieldMs: 500)

> poll that process
⚡ Tool: process(action: "poll", id: "proc-000001", yieldMs: 1000)

> fetch latest logs
⚡ Tool: process(action: "log", id: "proc-000001", maxChars: 8000)
```

## Git tools

| Function | Description |
|----------|-------------|
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

## Think tools

| Function | Description |
|----------|-------------|
| `think` | Scratchpad for reasoning — no side effects. |

The think tool lets the agent plan, reason through trade-offs, or organize multi-step approaches without executing any actions. It simply returns the thought back as a structured note.

### Parameters

- **`think`** — `thought` (string).

### Example

```text
⚡ Tool: think(thought: "The user wants to refactor auth. I should first check the current implementation, then propose changes.")
```

## Environment tools

| Function | Description |
|----------|-------------|
| `get_environment` | Returns OS, architecture, runtime, disk space, and tooling versions. |

### Parameters

- **`get_environment`** — `includeEnvVars` (bool, default `false`). When `true`, includes environment variables with secrets automatically masked.

### Example

```text
> what system am I running on?
⚡ Tool: get_environment(includeEnvVars: false)
```

## Task tools

| Function | Description |
|----------|-------------|
| `create_task` | Create a tracked work item with priority. |
| `list_tasks` | List tasks, optionally filtered by status. |
| `update_task` | Update a task's status, title, or description. |
| `complete_task` | Mark a task as done. |
| `export_tasks` | Export all tasks as JSON. |

### Parameters

- **`create_task`** — `title` (string), `description` (string?, optional), `priority` (`"low"` / `"medium"` / `"high"`, default `"medium"`).
- **`list_tasks`** — `status` (string?, filter: `"pending"`, `"in_progress"`, `"done"`, `"blocked"`).
- **`update_task`** — `id` (string, e.g. `"task-1"`), `status` (string?), `title` (string?), `description` (string?).
- **`complete_task`** — `id` (string).
- **`export_tasks`** — no parameters.

### Example

```text
> track the remaining work
⚡ Tool: create_task(title: "Add unit tests for AuthService", priority: "high")
⚡ Tool: create_task(title: "Update README with new endpoints", priority: "medium")
⚡ Tool: list_tasks()
```

## Code execution tools

| Function | Description |
|----------|-------------|
| `execute_code` | Run a code snippet in C#, Python, Node.js, Bash, or PowerShell. |

### Parameters

- **`execute_code`** — `language` (string: `"csharp"`, `"python"`, `"node"`, `"bash"`, `"powershell"`), `code` (string), `timeoutSeconds` (int, default `30`, max `300`).

Temporary files are created in the system temp directory and automatically cleaned up after execution. Processes that exceed the timeout are killed.

### Example

```text
> test this regex in python
⚡ Tool: execute_code(language: "python", code: "import re; print(re.findall(r'\\d+', 'abc 123 def 456'))")
```

## Clipboard tools

| Function | Description |
|----------|-------------|
| `read_clipboard` | Read text from the system clipboard. |
| `write_clipboard` | Write text to the system clipboard. |

Cross-platform support: uses PowerShell/clip on Windows, pbcopy/pbpaste on macOS, and xclip/xsel on Linux.

### Parameters

- **`read_clipboard`** — no parameters.
- **`write_clipboard`** — `text` (string).

### Example

```text
> paste what's on my clipboard
⚡ Tool: read_clipboard()
```

## Question tools

| Function | Description |
|----------|-------------|
| `ask_questions` | Present a structured questionnaire to the user and collect validated answers. |

### Parameters

- **`ask_questions`** — `questionsJson` (string, JSON). The JSON should contain an `AskQuestionsRequest` with `title`, `context`, `questions[]` (each with `key`, `prompt`, `type`, `required`, `options[]`, `validation`), `allowCancel`, and `submitLabel`.

### Supported question types

| Type | Description |
|------|-------------|
| `text` | Free-form text input |
| `confirm` | Yes/no confirmation |
| `singleSelect` | Pick one option from a list |
| `multiSelect` | Pick multiple options from a list |
| `number` | Numeric input with optional min/max bounds |

### Example

```text
⚡ Tool: ask_questions(questionsJson: "{\"title\":\"Project Setup\",\"questions\":[{\"key\":\"name\",\"prompt\":\"Project name?\",\"type\":\"text\",\"required\":true}]}")
```

## Diff and patch tools

Create and apply structured text patches across files. The `create_patch` tool generates unified diff output for review, while `apply_patch` performs atomic multi-file text replacements — if any edit fails validation, no files are modified.

### Functions

- **`create_patch`** — `editsJson` (string, JSON array of `{path, oldText, newText}`). Returns a unified diff showing proposed changes without modifying files.
- **`apply_patch`** — `editsJson` (string, JSON array of `{path, oldText, newText}`). Validates all edits first, then applies atomically. Returns an error if any `oldText` is not found (no files are modified).

### Example

```text
⚡ Tool: create_patch(editsJson: "[{\"path\":\"src/Config.cs\",\"oldText\":\"Timeout = 30\",\"newText\":\"Timeout = 60\"}]")
→ --- a/src/Config.cs
  +++ b/src/Config.cs
  @@ -12,1 +12,1 @@
  -Timeout = 30
  +Timeout = 60
```

## Batch edit tools

Apply multiple text replacements across one or more files in a single atomic operation. All edits are validated before any files are written — if any `oldText` is not found, no files are modified. Multiple edits to the same file are applied sequentially in order.

### Functions

- **`batch_edit_files`** — `editsJson` (string, JSON array of `{path, oldText, newText}`). Groups edits by file, validates all, then writes atomically.

### Example

```text
⚡ Tool: batch_edit_files(editsJson: "[{\"path\":\"src/App.cs\",\"oldText\":\"v1\",\"newText\":\"v2\"},{\"path\":\"src/Config.cs\",\"oldText\":\"old\",\"newText\":\"new\"}]")
→ Applied 2 edit(s) across 2 file(s):
    src/App.cs: 1 edit(s)
    src/Config.cs: 1 edit(s)
```

## Usage tracking tools

Track token usage and estimated costs for the current session. The agent loop can record usage after each turn, and the `get_usage` tool provides a summary with cost estimates across common model pricing tiers.

### Functions

- **`get_usage`** — No parameters. Returns session token counts (prompt, completion, total), tool call count, turn count, and estimated costs for several model pricing tiers.
- **`reset_usage`** — No parameters. Resets all session usage counters to zero.

## Utilities / Encoding & Crypto tools

Encoding, decoding, hashing, and cryptographic utility tools for common developer operations.

| Function | Description |
|----------|-------------|
| `encode_base64` | Encode text to Base64. |
| `decode_base64` | Decode a Base64-encoded string back to plain text. |
| `encode_url` | URL-encode a string for safe use in URLs and query parameters. |
| `decode_url` | Decode a URL-encoded string back to plain text. |
| `decode_jwt` | Decode a JWT token to inspect its header and payload. |
| `hash_compute` | Compute a cryptographic hash of the input text. |
| `generate_guid` | Generate one or more GUIDs/UUIDs (v4). |

### Parameters

- **`encode_base64`** — `text` (string), `urlSafe` (bool, default `false`). When `urlSafe` is `true`, outputs URL-safe Base64 without padding.
- **`decode_base64`** — `encoded` (string). Handles both standard and URL-safe Base64 automatically.
- **`encode_url`** — `text` (string). Returns percent-encoded string safe for query parameters.
- **`decode_url`** — `encoded` (string). Reverses percent-encoding.
- **`decode_jwt`** — `token` (string). Decodes and pretty-prints the header, payload, and common claims (sub, iss, aud, exp, iat, nbf).
- **`hash_compute`** — `text` (string), `algorithm` (string, default `sha256`). Supported: `sha256`, `sha512`, `sha384`, `sha1`, `md5`.
- **`generate_guid`** — `count` (int, default `1`, max `20`). Generates version 4 UUIDs.

### Examples

```text
> encode this string to base64
⚡ Tool: encode_base64(text: "Hello, World!")

> decode this JWT without verifying the signature
⚡ Tool: decode_jwt(token: "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...")

> hash the password with SHA-256
⚡ Tool: hash_compute(text: "my-secret", algorithm: "sha256")

> generate a unique ID
⚡ Tool: generate_guid(count: 1)
```

> [!WARNING]
> `decode_jwt` **does not verify the token signature**. Use it for inspection and debugging only, not for authentication decisions.

> [!WARNING]
> `MD5` and `SHA1` are cryptographically weak. Avoid using them for security-sensitive workflows — prefer `SHA256` or `SHA512`.

## Tailscale Integration tools

Tools for Tailscale integration: status detection, Tailnet machine discovery, remote orchestration, and credential configuration.

| Function | Description |
|----------|-------------|
| `tailscale_status` | Check Tailscale installation, authentication status, current Tailnet, and local node identity. |
| `tailscale_machines` | List all machines on the Tailnet with name, OS, online status, and addresses. |
| `tailscale_configure` | Configure Tailscale API credentials for machine discovery and remote orchestration. |
| `tailscale_runner_probe` | Check if a `jdai-runner` service is available on a specific Tailnet machine. |
| `tailscale_export` | Export Tailscale configuration, discovered machines, and runner status as JSON. |

### Prerequisites

Tailscale tools require one of the following:

- **Tailscale CLI** installed and authenticated (`tailscale up`) — used for local discovery via `tailscale status --json`.
- **API credentials** configured via `tailscale_configure` — used for remote API-based discovery when the CLI is not available.

### Parameters

- **`tailscale_status`** — `configDir` (string?, default `~/.jdai`). Returns CLI availability, API credential status, local node hostname, Tailscale IP, and backend state.
- **`tailscale_machines`** — `filter` (string?, `"online"`, `"offline"`, or `"all"`, default `"all"`), `tag` (string?, e.g. `"tag:server"`), `configDir` (string?, default `~/.jdai`).
- **`tailscale_configure`** — `tailnet` (string, e.g. `"example.com"`), `authMethod` (string, `"oauth"` or `"api-key"`), `credential` (string), `clientSecret` (string?, required for OAuth), `configDir` (string?, default `~/.jdai`). Stores credentials to `~/.jdai/tailscale.json`.
- **`tailscale_runner_probe`** — `target` (string, hostname or Tailscale IP), `port` (int, default `18789`), `configDir` (string?, default `~/.jdai`).
- **`tailscale_export`** — `configDir` (string?, default `~/.jdai`). Returns a JSON document with CLI status, API config, machine list, and summary counts.

### Examples

```text
> check tailscale status
⚡ Tool: tailscale_status()

> list all machines on my tailnet
⚡ Tool: tailscale_machines(filter: "online")

> list only machines tagged as servers
⚡ Tool: tailscale_machines(tag: "tag:server")

> probe runner availability on build-agent-01
⚡ Tool: tailscale_runner_probe(target: "build-agent-01")

> export machine inventory for CI
⚡ Tool: tailscale_export()
```

### Common workflow

A typical Tailscale orchestration workflow:

1. **Discover machines** — `tailscale_machines()` to see what's on the Tailnet.
2. **Probe runner availability** — `tailscale_runner_probe(target: "hostname")` for each candidate host.
3. **Export for automation** — `tailscale_export()` to produce JSON suitable for CI pipelines.

### Security guidance

- Store Tailscale API keys and OAuth secrets using environment variables (`TAILSCALE_API_KEY`, `TAILSCALE_OAUTH_CLIENT_ID`, `TAILSCALE_OAUTH_CLIENT_SECRET`) rather than in `~/.jdai/tailscale.json` when possible.
- Use OAuth with least-privilege scopes rather than full API keys.
- Rotate credentials periodically and remove unused API keys from the Tailscale admin console.

## Multimodal tools

| Function | Description |
|----------|-------------|
| `capture_screenshot` | Capture a screenshot of the current screen or a specific window. |
| `analyze_image` | Analyse an image file and describe its contents. |
| `describe_image` | Generate a natural-language description of a provided image path or URL. |

### Parameters

- **`capture_screenshot`** — `target` (string?, `"screen"` or window title, default `"screen"`), `outputPath` (string?, saves to temp if omitted).
- **`analyze_image`** — `path` (string, local file or URL), `prompt` (string?, optional analysis instruction).
- **`describe_image`** — `path` (string, local file or URL), `detail` (string?, `"low"` / `"high"`, default `"high"`).

### Example

```text
> take a screenshot and describe what you see
⚡ Tool: capture_screenshot()
⚡ Tool: analyze_image(path: "/tmp/screenshot.png", prompt: "What UI elements are visible?")
```

## Code execution (sandboxed) tools

Sandboxed process execution with isolated language runtimes. Unlike the inline `execute_code` tool, these runners launch full interpreter processes in a sandboxed working directory with file I/O support and longer timeouts.

| Function | Description |
|----------|-------------|
| `run_python` | Execute a Python script file or inline code in an isolated process. |
| `run_node` | Execute a Node.js script file or inline code in an isolated process. |
| `run_bash` | Execute a bash/shell script in an isolated sandbox. |

### Parameters

- **`run_python`** — `code` (string), `args` (string[]?, optional CLI args), `cwd` (string?, sandbox dir), `timeoutSeconds` (int, default `60`), `packages` (string[]?, pip-install before run).
- **`run_node`** — `code` (string), `args` (string[]?, optional), `cwd` (string?, sandbox dir), `timeoutSeconds` (int, default `60`), `packages` (string[]?, npm-install before run).
- **`run_bash`** — `script` (string), `cwd` (string?, sandbox dir), `timeoutSeconds` (int, default `60`), `env` (object?, extra environment variables).

### Example

```text
> run a data analysis script with pandas
⚡ Tool: run_python(code: "import pandas as pd\ndf = pd.read_csv('data.csv')\nprint(df.describe())", packages: ["pandas"])
```

## Task management tools

Higher-level project task management with assignment, ownership, and due-date tracking. Complements the basic task-tracking tools with team-oriented workflow features.

| Function | Description |
|----------|-------------|
| `assign_task` | Assign a task to a team member or agent. |
| `set_due_date` | Set or update a due date on an existing task. |
| `list_assigned_tasks` | List tasks assigned to a specific owner. |
| `get_task_summary` | Return a project summary: counts by status, overdue items, and upcoming deadlines. |

### Parameters

- **`assign_task`** — `id` (string), `owner` (string, username or agent name).
- **`set_due_date`** — `id` (string), `dueDate` (string, ISO 8601 date).
- **`list_assigned_tasks`** — `owner` (string), `status` (string?, filter).
- **`get_task_summary`** — no parameters.

### Example

```text
> assign the auth task to alice with a deadline
⚡ Tool: assign_task(id: "task-3", owner: "alice")
⚡ Tool: set_due_date(id: "task-3", dueDate: "2025-08-01")
```

## Scheduler tools

Cron-style job scheduling for one-time and recurring tasks. Jobs are persisted to the agent's store and survive restarts.

| Function | Description |
|----------|-------------|
| `schedule_job` | Schedule a command or workflow to run at a specified time or on a cron expression. |
| `list_jobs` | List all scheduled jobs with their next run time and status. |
| `cancel_job` | Cancel a scheduled job by ID. |
| `get_job_status` | Retrieve the last execution status and output of a job. |

### Parameters

- **`schedule_job`** — `name` (string), `command` (string), `schedule` (string, ISO 8601 datetime or cron expression like `"0 9 * * 1-5"`), `recurring` (bool, default `false`), `description` (string?, optional).
- **`list_jobs`** — `status` (string?, `"pending"`, `"running"`, `"completed"`, `"failed"`, or `"all"`, default `"all"`).
- **`cancel_job`** — `id` (string).
- **`get_job_status`** — `id` (string).

### Example

```text
> run the nightly build every weekday at 2am
⚡ Tool: schedule_job(name: "nightly-build", command: "dotnet build", schedule: "0 2 * * 1-5", recurring: true)

> list all scheduled jobs
⚡ Tool: list_jobs()
```

## Notebook tools

Jupyter notebook integration for cell-by-cell execution, variable inspection, and output capture.

| Function | Description |
|----------|-------------|
| `notebook_open` | Open a Jupyter notebook file and load its cells into the current session. |
| `notebook_run_cell` | Execute a specific cell by index and return its output. |
| `notebook_run_all` | Execute all cells in order and return a summary of outputs. |
| `notebook_get_vars` | Inspect the current kernel variable namespace. |
| `notebook_export` | Export the notebook with all outputs to a file (`.ipynb` or `.html`). |

### Parameters

- **`notebook_open`** — `path` (string, `.ipynb` file path).
- **`notebook_run_cell`** — `index` (int, 0-based cell index), `code` (string?, override cell source), `timeoutSeconds` (int, default `30`).
- **`notebook_run_all`** — `timeoutSeconds` (int per cell, default `30`), `stopOnError` (bool, default `true`).
- **`notebook_get_vars`** — `filter` (string?, regex pattern to filter variable names).
- **`notebook_export`** — `path` (string, output file path), `format` (string, `"ipynb"` or `"html"`, default `"ipynb"`).

### Example

```text
> open the analysis notebook and run all cells
⚡ Tool: notebook_open(path: "analysis/explore.ipynb")
⚡ Tool: notebook_run_all(stopOnError: false)
```

## Capability introspection tools

Query model and runtime capabilities such as context window size, vision support, and tool-calling availability.

| Function | Description |
|----------|-------------|
| `get_capabilities` | Return the capabilities of the currently active model. |
| `check_vision_support` | Check whether the active model supports image/vision input. |
| `get_context_window` | Return the maximum context window size (tokens) for the active model. |

### Parameters

- **`get_capabilities`** — no parameters. Returns a capability map: `contextWindow`, `vision`, `toolCalling`, `streaming`, `json_mode`.
- **`check_vision_support`** — no parameters. Returns `true` / `false`.
- **`get_context_window`** — no parameters. Returns token count as an integer.

### Example

```text
> what can the current model do?
⚡ Tool: get_capabilities()
→ { contextWindow: 200000, vision: true, toolCalling: true, streaming: true }
```

## Policy and governance tools

Check governance policies, validate tool permissions, and enforce budget constraints before executing operations.

| Function | Description |
|----------|-------------|
| `check_policy` | Evaluate whether a proposed action is permitted under the active governance policy. |
| `validate_permission` | Check whether the current session has permission to invoke a specific tool. |
| `check_budget` | Compare estimated operation cost against the configured session budget. |

### Parameters

- **`check_policy`** — `action` (string, description of the proposed action), `context` (string?, optional extra context).
- **`validate_permission`** — `tool` (string, tool function name), `args` (object?, the intended arguments).
- **`check_budget`** — `estimatedTokens` (int), `model` (string?, defaults to active model).

### Example

```text
> check if I'm allowed to push to main
⚡ Tool: check_policy(action: "git push to main branch")

> verify budget before a large summarization run
⚡ Tool: check_budget(estimatedTokens: 50000)
```

## OpenClaw compatibility aliases

JD.AI also exposes OpenClaw-style aliases so external tool contracts can map cleanly to native tools:

| Alias | Canonical JD.AI tool |
|-------|----------------------|
| `bash` | `run_command` |
| `read` | `read_file` |
| `write` | `write_file` |
| `edit` | `edit_file` |
| `ls` | `list_directory` |
| `webfetch` | `web_fetch` |
| `websearch` | `web_search` |
| `todo_read` | `list_tasks` |
| `todo_write` | `update_task` / task mutations |
| `exec` | `run_command` semantics + managed process sessions |
| `process` | native process session control |

### Shared compatibility envelope parameters

The aliases support additional optional parameters:

- `summary` — return a compact response.
- `maxResultChars` — hard-cap output length.
- `noContext` — exclude actual tool output from model/session context.
- `noStream` — compatibility flag (currently no effect on tool output handling).
- `timeoutMs` — timeout override for alias tools that support execution timeouts.

## Tool safety tiers

Every tool belongs to a safety tier that controls how confirmation is handled:

| Tier | Behavior | Tools |
|------|----------|-------|
| **Auto-approve** | Runs without confirmation | `read_file`, `grep`, `glob`, `list_directory`, `git_status`, `git_diff`, `git_log`, `git_branch`, `memory_search`, `web_fetch`, `ask_questions`, `think`, `get_environment`, `list_tasks`, `export_tasks`, `read_clipboard`, `get_usage`, `create_patch`, `encode_base64`, `decode_base64`, `encode_url`, `decode_url`, `decode_jwt`, `hash_compute`, `generate_guid`, `tailscale_status`, `tailscale_machines`, `tailscale_export` |
| **Confirm once** | Asks once per session | `write_file`, `edit_file`, `git_commit`, `git_push`, `git_pull`, `git_checkout`, `git_stash`, `memory_store`, `memory_forget`, `create_task`, `update_task`, `complete_task`, `write_clipboard`, `spawn_agent`, `spawn_team`, `apply_patch`, `batch_edit_files`, `reset_usage`, `tailscale_configure`, `tailscale_runner_probe` |
| **Always confirm** | Asks every invocation | `run_command`, `web_search`, `execute_code` |

## Controlling tool permissions

Three mechanisms override the default confirmation behavior:

| Mechanism | Scope | Description |
|-----------|-------|-------------|
| `/autorun` | Session | Toggle auto-approve for **all** tools in the current session. |
| `/permissions` | Session | Toggle permission checks entirely — no confirmations at all. |
| `--dangerously-skip-permissions` | Process | CLI flag that disables all permission checks for the lifetime of the process. |

> [!WARNING]
> Disabling confirmations means the agent can write files, run commands, and commit code without asking. Use these overrides only in trusted, automated environments.
