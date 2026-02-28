# Checkpointing

Checkpoints save your project state so you can safely roll back if changes go wrong.

## Strategies

### Stash-based (default)
Uses `git stash` to save working tree state before file mutations.
- Fast, lightweight, built into git
- Requires: git repository

### Directory-based
Copies files to `.jdai/checkpoints/` directory.
- Works without git
- Uses more disk space

### Commit-based
Creates checkpoint commits with `[jdai-checkpoint]` prefix.
- Full git history integration
- Can be cleaned up with `git reset`

## Commands
```
/checkpoint list          # Show all checkpoints
/checkpoint restore <id>  # Restore to a checkpoint
/checkpoint clear         # Remove all checkpoints
```

## Automatic checkpointing
Checkpoints are created automatically before file-modifying tool executions (write_file, edit_file, run_command).

## Tips
- Stash-based is recommended for git repos
- Use directory-based for non-git projects
- Clear old checkpoints periodically to save space
