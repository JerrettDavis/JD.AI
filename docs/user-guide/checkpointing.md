---
title: Checkpointing
description: Automatic project snapshots before destructive operations — three strategies for safe rollback.
---

# Checkpointing

Checkpoints save your project state so you can safely roll back if changes go wrong. JD.AI creates checkpoints automatically before file-modifying tool executions (`write_file`, `edit_file`, `run_command`) and provides commands to list, restore, and manage them.

## When checkpoints are created

Checkpoints are created automatically before any tool that could modify your project files:

- **`write_file`** — creating or overwriting a file
- **`edit_file`** — replacing text in a file
- **`run_command`** — executing a shell command that might modify files
- **`git_commit`** — committing changes
- **`batch_edit_files`** — multi-file replacements
- **`apply_patch`** — applying a unified diff

This means you always have a restore point before any destructive action.

## Three checkpoint strategies

JD.AI supports three strategies, each suited to different environments.

### Stash-based (default)

Uses `git stash` to save working tree state before file mutations.

- **How it works:** Creates a named git stash entry with a `[jdai-checkpoint]` label.
- **Pros:** Fast, lightweight, built into git, no extra disk space.
- **Cons:** Requires a git repository. Stashes can be accidentally dropped.
- **Requires:** Git repository

```text
# What happens behind the scenes:
git stash push -m "[jdai-checkpoint] Before write_file: src/Auth.cs"
```

### Directory-based

Copies affected files to a `.jdai/checkpoints/` directory in your project root.

- **How it works:** Creates timestamped subdirectories containing copies of files that are about to change.
- **Pros:** Works without git. Simple file-based backup.
- **Cons:** Uses more disk space. Only backs up files that are about to be modified.
- **Requires:** Write access to the project directory

```text
.jdai/checkpoints/
├── 2025-01-15T10-30-00/
│   └── src/Auth.cs
└── 2025-01-15T10-35-00/
    ├── src/Auth.cs
    └── src/Config.cs
```

### Commit-based

Creates actual git commits with a `[jdai-checkpoint]` prefix in the commit message.

- **How it works:** Stages all changes and creates a checkpoint commit before the tool runs.
- **Pros:** Full git history integration. Checkpoints appear in `git log`.
- **Cons:** Creates extra commits that you may want to squash later.
- **Requires:** Git repository

```text
# What appears in git log:
[jdai-checkpoint] Before write_file: src/Auth.cs
[jdai-checkpoint] Before run_command: dotnet test
```

Clean up checkpoint commits with `git rebase -i` or `git reset` when you're done.

## Choosing a strategy

| Scenario | Recommended strategy |
|----------|---------------------|
| Git repository, typical development | **Stash-based** (default) |
| Non-git project or no git available | **Directory-based** |
| Want full commit-level history | **Commit-based** |
| Automated / CI environment | **Commit-based** (easy to reset) |

## Checkpoint commands

### List all checkpoints

```text
/checkpoint list
```

Shows all checkpoints with their ID, timestamp, strategy, and description of what was about to happen.

### Restore to a checkpoint

```text
/checkpoint restore <id>
```

Restores your project to the state captured in the checkpoint. The restore method depends on the strategy:

- **Stash-based:** `git stash pop` the corresponding entry
- **Directory-based:** Copies the backed-up files back to their original locations
- **Commit-based:** `git reset` to the checkpoint commit

### Clear all checkpoints

```text
/checkpoint clear
```

Removes all checkpoints to free disk space and clean up stash entries or checkpoint directories.

## Configuration

### Set the checkpoint strategy in JDAI.md

Add a checkpoint strategy directive to your project's `JDAI.md` file:

```markdown
# Checkpointing
- Strategy: stash (or directory, or commit)
```

### Set via environment or config

You can also configure the strategy in `~/.jdai/config.json`:

```json
{
  "checkpointStrategy": "stash"
}
```

### Disable checkpointing

If you don't want automatic checkpoints (not recommended), you can disable them:

```json
{
  "checkpointStrategy": "none"
}
```

## Tips

- **Stash-based is recommended** for most git repositories — it's fast and doesn't create extra commits.
- **Use directory-based** for non-git projects where you still want rollback safety.
- **Clear old checkpoints periodically** with `/checkpoint clear` to save disk space.
- **Pair with sessions** — combine checkpoints with [session persistence](sessions.md) so you can restore both your conversation and your project state.
- **Review before restoring** — use `/checkpoint list` to see exactly what each checkpoint captured before restoring.

## See also

- [Sessions & History](sessions.md) — conversation persistence and resume
- [Commands](commands.md) — all checkpoint commands
- [Configuration](configuration.md) — global and per-project settings
