---
description: "Interactive mode guide for JD.AI: prompt behavior, keyboard shortcuts, vim mode, slash commands, history rollback, steering, and shell/file shortcuts."
---

# Interactive Mode

JD.AI interactive mode is the default `jdai` terminal experience: live prompt editing, slash commands, tool use, streaming responses, and session persistence.

## Start interactive mode

```bash
jdai
```

By default JD.AI:

- Detects providers and models
- Loads project instructions (`JDAI.md`, `CLAUDE.md`, `AGENTS.md`, and related files)
- Opens an interactive prompt

## Input types

You can mix several input styles in the same session.

### 1. Chat prompts

Type a normal instruction and press Enter.

```text
refactor this service to remove duplicated retry logic
```

### 2. Slash commands

Use `/` for built-in command operations.

```text
/help
/model gpt-4o
/review
```

See [Commands Reference](../reference/commands.md) for full command behavior.

### 3. Shell passthrough with `!`

Prefix with `!` to run a shell command directly and inject output into context.

```text
!dotnet test
!git status --short
```

### 4. File mentions with `@`

Reference files inline with `@path` and JD.AI expands them into prompt context.

```text
review @src/JD.AI/Commands/SlashCommandRouter.cs for edge cases
```

### 5. Pasted content attachments

Large or multi-line paste input is collapsed into a visual chip in the prompt and sent as an attachment block when you submit.

## Prompt keyboard shortcuts

| Shortcut | Behavior |
|---|---|
| `Enter` | Submit prompt |
| `Tab` | Accept selected completion |
| `Up` / `Down` | Navigate command history or completion dropdown |
| `Left` / `Right` | Move cursor |
| `Home` / `End` | Jump to start/end of prompt |
| `Backspace` / `Delete` | Delete character (or remove paste chip if selected) |
| `Ctrl+V` | Paste clipboard content |
| `Esc` | Dismiss completion dropdown, or leave insert mode when vim mode is enabled |
| `Esc` then `Esc` (empty prompt) | Open session history viewer |
| `Ctrl+C` | Interrupt current input or cancel active generation |
| `Ctrl+C` then `Ctrl+C` (idle) | Exit app |

## During streaming responses

While JD.AI is generating output, interactive controls stay active:

- Press `Esc` twice to cancel the current turn.
- Press `Ctrl+C` to cancel the current turn.
- Type a follow-up message while output streams and press `Enter` to queue that steering prompt for the next turn.

## History viewer and rollback

Double-press `Esc` on an empty prompt to open the session history viewer.

| Shortcut | Behavior |
|---|---|
| `Up` / `Down` | Move through turns |
| `Enter` | Toggle selected turn details |
| `Ctrl+R` | Roll back session to selected turn |
| `Esc` / `Q` | Close history viewer |

Rollback rewinds persisted session turns and rebuilds active chat context to the selected point.

## Vim mode

Enable with `/vim on` and disable with `/vim off`.

```text
/vim on
```

In normal mode:

| Key | Action |
|---|---|
| `h` `l` | Move left/right |
| `j` `k` | History down/up |
| `0` `$` | Line start/end |
| `w` `b` `e` | Word motions |
| `x` | Delete at cursor |
| `dd` | Clear prompt |
| `yy` `p` | Copy line and paste |
| `gg` `G` | Start/end |
| `i` `a` `I` `A` `o` | Enter insert mode variants |
| `Esc` | Return to normal mode from insert |

## Common interactive workflows

### Review and patch loop

```text
/review
fix the issues and run tests
/review
```

### Security scan loop

```text
/security-review
/security-review --full
```

### Session hygiene loop

```text
/context
/compact
/stats
```

### UX customization loop

```text
/theme nord
/spinner rich
/output-style compact
```

## Related docs

- [Commands Reference](../reference/commands.md)
- [Tools Reference](../reference/tools.md)
- [Common Workflows](common-workflows.md)
- [CLI Reference](../reference/cli.md)

