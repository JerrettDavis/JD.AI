# VHS Tape Scripts

These `.tape` files generate terminal screenshots and GIFs for JD.AI documentation using [Charmbracelet VHS](https://github.com/charmbracelet/vhs).

JD.AI docs use two media pipelines:

- **VHS** for realistic terminal recordings and frame captures (`docs/tapes/*.tape`)
- **Playwright** for styled static screenshots and social assets (`docs/scripts/*.mjs`)

## Prerequisites

Install VHS and its dependencies:

```bash
# macOS/Linux
brew install vhs

# Windows
scoop install vhs

# Or via Go
go install github.com/charmbracelet/vhs@latest
```

VHS requires `ttyd` and `ffmpeg` on your PATH.

## Generating screenshots

Run all tapes:

```bash
./docs/tapes/generate.sh
```

Or run individual tapes:

```bash
vhs docs/tapes/demo-startup.tape
```

Generate Playwright-based docs images:

```bash
npm --prefix docs/scripts install
npm --prefix docs/scripts run screenshots
```

Generate Open Graph image:

```bash
npm --prefix docs/scripts run og
```

## Output

Generated files are saved to `docs/images/` and referenced by documentation articles.

## Tape files

| Tape | Output | Description |
|------|--------|-------------|
| `demo-startup.tape` | `demo-startup.gif` | Launch, provider detection, welcome banner |
| `demo-chat.tape` | `demo-chat.gif` | Basic chat interaction with streaming |
| `demo-tools.tape` | `demo-tools.gif` | File editing, grep, shell commands |
| `demo-commands.tape` | `demo-commands.gif` | /help, /model, /providers |
| `demo-subagents.tape` | `demo-subagents.gif` | Spawning explore and task agents |
| `demo-orchestration.tape` | `demo-orchestration.gif` | Team execution with progress panel |
| `demo-sessions.tape` | `demo-sessions.gif` | /save, /sessions, /resume |
| `demo-update.tape` | `demo-update.gif` | Update check and /update |
| `demo-spinner.tape` | `demo-spinner.gif` | /spinner command and style switching |
| `demo-thinking.tape` | `demo-thinking.gif` | Animated thinking indicator and turn metrics |
| `ci-cli-smoke.tape` | `ci-cli-smoke.gif` | CI smoke run for `mcp --help` and `plugin --help` |
