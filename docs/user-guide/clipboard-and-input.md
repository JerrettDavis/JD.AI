---
title: Clipboard & Input
description: Paste detection, ghost text, multi-line input, history browsing, file mentions, and keyboard shortcuts.
---

# Clipboard & Input

JD.AI provides a rich terminal input experience with paste detection, command completion, input history, and multiple ways to feed content into your conversation.

## Clipboard paste detection

When you paste content into JD.AI, it automatically detects and organizes the input:

- **Text blocks** — `[Pasted content #1 1000 chars]`
- **Image data** — `[Pasted Image #1 100kb]`
- **File content** — `[Pasted PDF File #1 2.5kb]`

Pasted content appears as attachments that can be removed individually before sending. This keeps large pastes from cluttering your prompt — you see a summary, and the full content is attached for the AI to read.

### Pasting code blocks

Large code blocks are automatically detected and chunked. JD.AI wraps them as structured attachments so the AI can reference specific sections. This works well for pasting error logs, stack traces, or code snippets from other tools.

## Ghost text completions

JD.AI offers ghost-text tab completions for slash commands. As you type a `/` prefix, suggestions appear in dimmed text. Press <kbd>Tab</kbd> to accept the suggestion.

```text
> /mo          → ghost text shows: del
> /mod         → press Tab → /model
```

This works for all slash commands and their subcommands.

## Multi-line input

JD.AI supports multi-line input for longer prompts. There are several ways to enter multi-line text:

- **Paste** — paste multi-line content directly; JD.AI detects the line breaks.
- **Shift+Enter** — insert a new line without sending (terminal support varies).
- **Backslash continuation** — end a line with `\` to continue on the next line.

Long inputs wrap properly within the terminal width, so you can compose detailed prompts without worrying about formatting.

## Input history

Navigate through your previous inputs with the arrow keys:

| Key | Action |
|-----|--------|
| <kbd>↑</kbd> | Previous input |
| <kbd>↓</kbd> | Next input |

Input history persists within a session. Scroll back through your prompts to re-run or modify earlier requests.

## History browser

Press <kbd>ESC</kbd> twice to open the interactive history browser:

- Browse previous conversation turns
- View token usage per turn
- Roll back to any previous point in the conversation

This is especially useful for undoing a series of changes or returning to a known-good state.

## @ file mentions

Reference files directly in your prompts using `@` mentions:

```text
> look at @src/Auth/AuthService.cs and add input validation
> compare @README.md with the actual installation steps
```

File mentions tell JD.AI exactly which files to focus on, reducing the need for the AI to search.

## ! bash mode

Prefix a line with `!` to execute a shell command directly without going through the AI:

```text
> !dotnet build
> !git status
> !ls -la src/
```

The command output is displayed in your terminal. This is useful for quick checks without invoking the AI.

## Keyboard shortcuts

| Shortcut | Action |
|----------|--------|
| <kbd>Tab</kbd> | Accept ghost-text completion |
| <kbd>↑</kbd> / <kbd>↓</kbd> | Navigate input history |
| <kbd>ESC</kbd> <kbd>ESC</kbd> | Open the interactive history browser |
| <kbd>Ctrl+C</kbd> | Cancel current input or interrupt a running tool |
| <kbd>Ctrl+D</kbd> | Exit JD.AI (same as `/quit`) |

## Tips

- **Paste error messages directly** — JD.AI handles large pastes gracefully and the AI gets the full context.
- **Use @ mentions for precision** — pointing at specific files reduces ambiguity and speeds up responses.
- **Use ! for quick commands** — check build status or git state without round-tripping through the AI.
- **Compact after large pastes** — if you've pasted a lot of content, use `/compact` to reclaim context window space afterward.

## See also

- [Commands](commands.md) — all slash commands
- [Tools](tools.md) — clipboard tools and how they work
- [Sessions & History](sessions.md) — session history and rollback
