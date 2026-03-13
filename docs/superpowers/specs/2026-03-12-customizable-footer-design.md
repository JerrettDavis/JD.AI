# Customizable Sticky Footer Design

**Date:** 2026-03-12
**Status:** Approved
**UPSS Refs:** `capability.tui-status-bar`, `behavior.status-bar-rendering`

## Problem

JD.AI's terminal UI renders status information (provider, model, tokens) as a transient line after each turn via `ChatRenderer.RenderStatusBar()`. This line scrolls away and is not visible while the user is typing or while the AI is thinking. Additionally, the current rendering pipeline has flickering issues in the spinner/thinking indicator and input area because multiple components write to the terminal independently without coordination.

## Goals

1. A persistent, always-visible status bar fixed at the bottom of the terminal below the input area.
2. Customizable content via a template string with built-in and plugin-contributed segments.
3. Contextual visibility — segments appear/hide based on state (git repo, open PR, context threshold).
4. Eliminate rendering flicker by consolidating all terminal output through a single coordinated rendering pipeline.
5. Claude Code plugin compatibility — honor the `statusline-setup` hook contract.

## Non-Goals

- Full TUI framework (e.g., Terminal.Gui) — Spectre.Console's `Layout` + `Live` is sufficient.
- Mouse interaction with the footer.
- Clickable PR links (terminal dependent, not portable).

## Architecture

### Layout Structure

The terminal is divided into a Spectre `Layout` with three named regions managed by a single `Live` context:

```
┌─────────────────────────────────────────────┐
│              Content Area                   │
│   (messages, tool output, thinking)         │
├─────────────────────────────────────────────┤
│ > user input area                           │
├─────────────────────────────────────────────┤
│ ~/jd-ai │ main │ 12k/200k │ anthropic │ …  │
└─────────────────────────────────────────────┘
```

- **Content area**: Rolling buffer of `IRenderable` objects. All existing renderers (`ChatRenderer`, `TurnProgress`, `DiffRenderer`, `TeamProgressPanel`) produce renderables and hand them to the layout coordinator instead of writing to `AnsiConsole` directly.
- **Input area**: Rendered from a keystroke buffer updated by `InteractiveInput` on the main thread. The layout refresh cycle picks up the current buffer each tick.
- **Footer bar**: Fixed 1–2 lines (configurable). Refreshed on state changes via `Live.Refresh()`.

### Core Components

#### `TerminalLayout`

The single rendering coordinator. Owns the Spectre `Live` context on a dedicated background thread.

- `AppendContent(IRenderable)` — adds to the content buffer
- `SetFooter(IRenderable)` — updates the footer region
- `RefreshInput(string)` — updates the input region from keystroke buffer
- Refresh cadence: throttled to ~60ms to batch updates and eliminate flicker
- Handles terminal resize events by recalculating layout dimensions

#### `FooterBar` : `IRenderable`

Receives a `FooterState` snapshot and renders segments into a single-row Spectre `Table` (no borders, full width, themed background). Segments separated by `│`.

#### `FooterState`

Immutable snapshot of all footer data:

```csharp
record FooterState(
    string WorkingDirectory,
    string? GitBranch,
    string? PrLink,
    long ContextTokensUsed,
    long ContextWindowSize,
    string Provider,
    string Model,
    int TurnCount,
    PermissionMode Mode,
    double WarnThresholdPercent,
    IReadOnlyList<PluginSegment> PluginSegments);
```

#### `FooterStateProvider`

Builds `FooterState` from the current session and environment:

- Reads `AgentSession` for token/context/turn data
- Caches git branch (refreshed every 30s or after git tool calls)
- Caches PR state via `gh pr view` (refreshed every 60s)
- Collects plugin-contributed segments from `StatusLineUpdate` hook results

#### `FooterTemplate`

Parses template strings into a list of `TemplateToken` (literal text or segment reference).

- `{name}` — always rendered
- `{name?}` — conditional, omitted when segment value is null/empty
- Adjacent separators around omitted segments are collapsed (no double `││`)
- `{plugin:key}` — references a plugin-contributed segment
- `{plugins?}` — auto-renders all plugin segments

### Built-in Segments

| Key | Description | Default | Visibility |
|-----|-------------|---------|------------|
| `folder` | Shortened working directory | Visible | Always |
| `branch` | Current git branch | Visible | Auto — only in git repos |
| `pr` | Open PR identifier | Visible | Auto — only if PR exists |
| `context` | Token usage (e.g., `12k/200k`) | Visible | Always |
| `provider` | Provider name | Visible | Always |
| `model` | Model identifier | Visible | Always |
| `turns` | Turn count | Visible | Always |
| `mode` | Permission mode | Hidden | Auto — only when not Normal |
| `compact` | Compression warning | Hidden | Auto — when remaining context < warnThresholdPercent |
| `duration` | Session elapsed time | Hidden | Never (opt-in) |

**Default template:**
```
{folder} │ {branch?} │ {pr?} │ {context} │ {provider} │ {model} │ {turns}
```

**Default always-visible bar:**
```
~/projects/jd-ai │ 12k/200k │ anthropic │ claude-sonnet-4-6 │ turn 5
```

**With contextual segments active:**
```
~/projects/jd-ai │ main │ PR #350 │ 185k/200k ⚠ │ anthropic │ claude-sonnet-4-6 │ turn 5
```

### Configuration

Added to `TuiSettings` in `~/.jdai/tui-settings.json`:

```json
{
  "footer": {
    "enabled": true,
    "lines": 1,
    "template": "{folder} │ {branch?} │ {pr?} │ {context} │ {provider} │ {model} │ {turns}",
    "warnThresholdPercent": 15,
    "segments": {
      "mode": { "visible": "auto" },
      "compact": { "visible": "auto", "warnPercent": 15 },
      "duration": { "visible": "never" }
    }
  }
}
```

**Segment visibility modes:** `"always"`, `"auto"` (contextual rules), `"never"`

**Threshold precedence:** The per-segment `warnPercent` (e.g., `segments.compact.warnPercent`) overrides the top-level `footer.warnThresholdPercent` for that segment only. If unset, the segment inherits the top-level value.

**Claude Code compatibility:** JD.AI reads `~/.claude/settings.json` status line configuration as a fallback (existing parity mechanism). Plugins register footer segments via the `StatusLineUpdate` hook event (JD.AI's equivalent of Claude Code's `statusline-setup` hook — same contract, JD.AI naming). JD.AI's own config takes precedence when both exist.

### Plugin/Hook Integration

Plugins contribute footer segments via a `StatusLineUpdate` hook event (matching Claude Code's contract):

- Plugins return `{ key, value, position?, priority? }`
- `FooterStateProvider` collects and merges plugin segments into `FooterState.PluginSegments`
- Plugin segments are referenced in templates as `{plugin:key}`
- Plugin segment keys cannot override built-in keys

### Rendering Pipeline Migration

This is a significant refactor. The migration is phased:

1. **Phase 1 — Layout coordinator**: Introduce `TerminalLayout` with `Live` context. Wire `ChatRenderer` to produce `IRenderable` objects through the layout. Move `TurnProgress` to a layout-managed component. This phase alone fixes spinner flicker.
2. **Phase 2 — Input migration**: Migrate `InteractiveInput` to render through the layout instead of writing directly to the console. Fixes input flicker.
3. **Phase 3 — Footer**: Add `FooterBar`, `FooterState`, `FooterStateProvider`, `FooterTemplate`. Wire configuration and plugin hooks. Deprecate `RenderStatusBar()` and `RenderModeBar()`.
4. **Phase 4 — Cleanup**: Remove deprecated rendering methods. Update all tests.

### Error Handling

- Template parse failures fall back to the default template with a warning log.
- Git/PR cache refresh failures are swallowed (stale data displayed, not missing data).
- Plugin segment hook errors are logged and the segment is omitted.
- Footer rendering must complete within 2ms to avoid impacting the 60ms refresh cadence.

## Testing Strategy

- **`FooterTemplateTests`**: Parse templates, resolve segments, conditional omission, separator cleanup, malformed templates.
- **`FooterStateProviderTests`**: Build state from mock session, git/PR caching behavior, plugin segment collection.
- **`FooterBarTests`**: Render snapshots via Spectre `TestConsole` to verify output format.
- **`TerminalLayoutTests`**: Content append ordering, refresh throttling, resize handling.
- **`TuiSettings` integration**: Footer config JSON round-trip serialization.
- **Existing test suites**: All current rendering tests must pass through the migration.

## Files Created/Modified

### New Files
- `src/JD.AI/Rendering/TerminalLayout.cs`
- `src/JD.AI/Rendering/FooterBar.cs`
- `src/JD.AI/Rendering/FooterState.cs`
- `src/JD.AI/Rendering/FooterStateProvider.cs`
- `src/JD.AI/Rendering/FooterTemplate.cs`
- `tests/JD.AI.Tests/Rendering/FooterTemplateTests.cs`
- `tests/JD.AI.Tests/Rendering/FooterBarTests.cs`
- `tests/JD.AI.Tests/Rendering/FooterStateProviderTests.cs`
- `tests/JD.AI.Tests/Rendering/TerminalLayoutTests.cs`
- `specs/capabilities/tui-status-bar.yaml`
- `specs/behavior/status-bar-rendering.yaml`

### Modified Files
- `src/JD.AI/Rendering/ChatRenderer.cs` — produce `IRenderable` instead of direct writes
- `src/JD.AI/Rendering/SpectreAgentOutput.cs` — route through `TerminalLayout`
- `src/JD.AI/Rendering/TurnProgress.cs` — stateful component, no cursor manipulation
- `src/JD.AI/Rendering/InteractiveInput.cs` — keystroke buffer, no direct console writes
- `src/JD.AI/Startup/InteractiveLoop.cs` — initialize `TerminalLayout`, remove `RenderStatusBar`/`RenderModeBar` calls
- `src/JD.AI.Core/Config/TuiSettings.cs` — add `FooterSettings` section
