# Commands Reference

Slash commands are typed at the `>` prompt prefixed with `/`. Type `/help` to see all commands.

![Slash commands help output](../images/demo-commands-help.png)

## Model & Provider Management

### `/help`

Show all available commands with descriptions.

### `/models`

List all available models across all detected providers. Shows model ID, provider name, and current selection.

### `/model <id>`

Switch to a different model. The model ID can be partial — JD.AI fuzzy-matches.

```text
/model gpt-4o
/model claude-sonnet
/model llama3.2
```

### `/providers`

List all detected AI providers with their connection status and available model count.

```text
Detecting providers...
  ✅ Claude Code: Authenticated — 1 model(s)
  ✅ GitHub Copilot: Authenticated — 3 model(s)
  ❌ Ollama: Not available
```

### `/provider`

Show the currently active provider and model.

## Context Management

### `/clear`

Clear the entire conversation history. Starts fresh while keeping the same session.

### `/compact`

Force context compaction — summarizes the conversation to free up context window space. Use when conversations get long.

### `/cost`

Show token usage statistics for the current session: prompt tokens, completion tokens, and total cost.

## Safety Controls

### `/autorun`

Toggle auto-approve mode for tool execution. When enabled, tools run without confirmation prompts.

> [!WARNING]
> Use with caution — tools can modify files and run commands.

### `/permissions`

Toggle permission checks entirely. When disabled, all tools execute without any confirmation.

> [!WARNING]
> Equivalent to `--dangerously-skip-permissions` for the current session.

## Session Management

### `/sessions`

List recent sessions with ID, name, project path, and turn count. Sessions are stored in `~/.jdai/sessions.db`.

### `/resume [id]`

Resume a previous session. Without an ID, shows the list to choose from. With an ID, loads that specific session.

```text
/resume
/resume abc123
```

### `/name <name>`

Name the current session for easy recall.

```text
/name feature-authentication
```

### `/history`

Show the turn-by-turn history of the current session with role, token counts, and timestamps. Supports interactive rollback (double-ESC).

### `/export`

Export the current session to a JSON file. Saved to `~/.jdai/exports/`.

## Project & Environment

### `/update`

Check for new versions of JD.AI on NuGet and optionally apply the update.

### `/instructions`

Show all loaded project instructions (from `JDAI.md`, `CLAUDE.md`, `AGENTS.md`, etc.).

### `/checkpoint`

Manage git checkpoints for safe rollback:

```text
/checkpoint list          # Show all checkpoints
/checkpoint restore <id>  # Restore to a checkpoint
/checkpoint clear         # Remove all checkpoints
```

### `/sandbox`

Show current sandbox/execution mode information.

### `/quit` or `/exit`

Exit JD.AI. Unsaved sessions are auto-saved.

## Quick Reference

| Command | Description |
|---|---|
| `/help` | Show help |
| `/models` | List models |
| `/model <id>` | Switch model |
| `/providers` | List providers |
| `/provider` | Show current provider |
| `/clear` | Clear conversation |
| `/compact` | Compress context |
| `/cost` | Token usage |
| `/autorun` | Toggle auto-approve |
| `/permissions` | Toggle confirmations |
| `/sessions` | List sessions |
| `/resume [id]` | Resume session |
| `/name <name>` | Name session |
| `/history` | Show history |
| `/export` | Export to JSON |
| `/update` | Check for updates |
| `/instructions` | Show instructions |
| `/checkpoint` | Manage checkpoints |
| `/sandbox` | Sandbox info |
| `/quit` | Exit |
