---
title: "Output Styles and Themes"
description: "The four JD.AI output styles (Rich, Plain, Compact, JSON) and the ten built-in color themes — how to switch between them and when to use each."
---

# Output Styles and Themes

JD.AI supports four output styles that control how responses are rendered in the terminal, and ten built-in color themes that adjust the palette used by Rich and Compact modes. The right combination depends on whether you are interacting with the assistant directly, scripting with its output, or consuming responses programmatically.

## Output Styles at a Glance

| Style | Markdown | Colors | ANSI codes | Best for |
|-------|----------|--------|------------|----------|
| **Rich** (default) | Rendered | Full | Yes | Daily interactive use |
| **Compact** | Rendered | Full | Yes | Dense terminal workflows |
| **Plain** | Raw text | None | No | Scripting, piping, CI |
| **JSON** | Raw text (in envelope) | None | No | Automation, tooling |

## Rich (Default)

Rich mode provides the full Spectre.Console rendering experience. Every AI response is parsed as Markdown and rendered with:

- Formatted headings (H1–H6), bold, italic, and strikethrough
- Syntax-highlighted fenced code blocks for [10 supported languages](tui-rendering.md#syntax-highlighting)
- Formatted tables with column alignment
- Nested ordered and unordered lists
- Blockquotes, horizontal rules, and inline code spans
- Auto-detected diff rendering with red/green/cyan coloring
- Bold-cyan colorization for slash-command mentions in prose
- Spinners and progress indicators during generation
- A live streaming byte counter (`◆ 1.2 KB…`) that clears when generation completes

Rich mode is intended for everyday use at an interactive terminal. Because it emits ANSI escape codes and terminal control sequences, it is not suitable for piping to tools that do not understand them.

See [TUI Rendering](tui-rendering.md) for a complete reference of all supported Markdown elements and rendering behaviors.

## Compact

Compact mode is like Rich mode but without decorative borders and panels. Markdown is still parsed and rendered, colors are still applied, and syntax highlighting still works. However, the surrounding chrome — bordered code-block panels, decorative header lines — is omitted, resulting in denser output that fits more content on screen.

Use Compact mode when you want formatted output but prefer a tighter layout, for example on a small terminal window or when working through a long multi-step task where screen real estate matters.

## Plain

Plain mode writes the AI response as raw text to standard output. No Markdown parsing, no ANSI color codes, no terminal control sequences. The output is exactly what would be sent to a log file.

Use Plain mode when:

- Piping JD.AI output to another command (`jdai ... | grep`, `jdai ... > output.txt`)
- Running JD.AI in a CI pipeline or automated script
- Consuming output in a tool or editor that does not handle ANSI codes
- Comparing responses programmatically where formatting would add noise

The streaming byte counter is also suppressed in Plain mode — tokens are written directly to standard output as they arrive, so you see partial output in real time without any cursor-control sequences.

## JSON

JSON mode wraps each message in a machine-readable JSON envelope. This makes it straightforward to parse JD.AI output in scripts, log aggregators, or custom tooling without fragile text parsing.

Each response is emitted as a JSON object on standard output. The envelope includes metadata such as the model name, token counts, and the raw response text:

```json
{
  "role": "assistant",
  "content": "Here is the answer to your question...",
  "model": "gpt-4o",
  "usage": {
    "promptTokens": 142,
    "completionTokens": 87,
    "totalTokens": 229
  }
}
```

Use JSON mode when building integrations that consume JD.AI responses downstream — for example, a shell script that checks the response for a specific phrase, a log pipeline that indexes responses, or a wrapper application.

## Switching Output Styles

### Slash Command (Interactive)

At the `>` prompt, run:

```text
/output-style rich
/output-style plain
/output-style compact
/output-style json
```

The style takes effect immediately for the next response.

### CLI Flag

Pass `--output` (or its alias `-o`) when launching JD.AI:

```bash
jdai --output plain
jdai --output json
jdai --output compact
```

### Configuration File

Set the style persistently in `~/.jdai/config.json`:

```json
{
  "outputStyle": "rich"
}
```

Valid values are `rich`, `plain`, `compact`, and `json`. The CLI flag overrides the config file; the `/output-style` command overrides both for the duration of the session.

## Themes

Themes control the color palette used in Rich and Compact output modes. They affect prompt color, header color, and the colors used for informational, warning, and error messages. Code syntax highlighting colors are also derived from the active theme.

### Built-In Themes

JD.AI ships with ten built-in themes:

| Theme name | Character | Palette |
|------------|-----------|---------|
| `DefaultDark` | Dark background | Blue accent, white foreground — the default theme |
| `Monokai` | Dark background | Warm green/yellow/orange accents inspired by the classic editor theme |
| `SolarizedDark` | Dark background | Muted blue-green accents on a dark olive base |
| `SolarizedLight` | Light background | Same solarized palette adapted for light terminal backgrounds |
| `Nord` | Dark background | Arctic blue-grey palette with cool, desaturated accents |
| `Dracula` | Dark background | Purple/pink/cyan accents on a near-black background |
| `OneDark` | Dark background | Atom One Dark–style muted purple and blue-green |
| `CatppuccinMocha` | Dark background | Pastel lavender/pink/teal on a deep mocha base |
| `Gruvbox` | Dark background | Warm retro amber/green on a dark brown base |
| `HighContrast` | Dark background | Maximum contrast white-on-black with bright accent colors; accessibility-focused |

### Switching Themes

At the `>` prompt:

```text
/theme DefaultDark
/theme Monokai
/theme SolarizedDark
/theme SolarizedLight
/theme Nord
/theme Dracula
/theme OneDark
/theme CatppuccinMocha
/theme Gruvbox
/theme HighContrast
```

The theme is applied immediately — the next response uses the new palette.

### Persisting a Theme

Set the theme in `~/.jdai/config.json`:

```json
{
  "outputStyle": "rich",
  "theme": "Nord"
}
```

### Choosing a Theme

- **DefaultDark** — safe choice for any dark-background terminal; well-tested readability
- **SolarizedLight** — use if your terminal background is light/white
- **HighContrast** — use if you need maximum legibility or are using a screen reader that works with visible text contrast
- **Nord / Dracula / OneDark** — popular editor themes that many developers find familiar
- **CatppuccinMocha / Gruvbox / Monokai** — distinctive palettes with strong aesthetic identity

> **Note:** Themes only apply to Rich and Compact output modes. Plain and JSON modes produce uncolored output regardless of the active theme.

## Configuration Reference

The following keys in `~/.jdai/config.json` control output and theming:

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `outputStyle` | string | `"rich"` | Output style: `rich`, `plain`, `compact`, or `json` |
| `theme` | string | `"DefaultDark"` | Color theme name (see table above) |

Full example:

```json
{
  "outputStyle": "rich",
  "theme": "CatppuccinMocha"
}
```

## Same Response in Different Styles

To illustrate the difference between modes, consider the same AI response about a code snippet:

**Rich / Compact** — rendered Markdown:

```
┌─────────────────────────────┐
│ csharp                      │
│ var x = kernel.InvokeAsync( │
│     prompt);                │
└─────────────────────────────┘

Use **await** to get the result.
```

**Plain** — raw text:

```
```csharp
var x = kernel.InvokeAsync(prompt);
```

Use **await** to get the result.
```

**JSON** — machine-readable envelope:

```json
{
  "role": "assistant",
  "content": "```csharp\nvar x = kernel.InvokeAsync(prompt);\n```\n\nUse **await** to get the result.",
  "model": "gpt-4o",
  "usage": { "promptTokens": 48, "completionTokens": 32, "totalTokens": 80 }
}
```

## Related Topics

- [TUI Rendering](tui-rendering.md) — Detailed reference for Markdown, syntax highlighting, and diff rendering in Rich mode
- [Commands](commands.md) — Full slash-command reference including `/output-style` and `/theme`
- [Configuration](configuration.md) — All configuration file options
