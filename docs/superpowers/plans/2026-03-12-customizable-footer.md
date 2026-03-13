# Customizable Sticky Footer Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a persistent, customizable status bar footer to the TUI with templateable segments, contextual visibility, and plugin extensibility — while eliminating rendering flicker by consolidating all output through Spectre's `Layout` + `Live`.

**Architecture:** Introduce a `TerminalLayout` coordinator that owns a Spectre `Live` context with three regions (content, input, footer). All rendering goes through this coordinator instead of direct `AnsiConsole`/`Console` writes. The footer is a templateable bar with built-in and plugin-contributed segments.

**Tech Stack:** C# / .NET 10, Spectre.Console (Layout, Live, Panel, Table), xUnit + FluentAssertions

**Spec:** `docs/superpowers/specs/2026-03-12-customizable-footer-design.md`
**UPSS:** `specs/capabilities/tui-status-bar.yaml`, `specs/behavior/status-bar-rendering.yaml`

---

## Chunk 1: Configuration and Footer Data Model

Foundation layer — settings, data types, and template engine. No rendering changes yet. All pure logic, fully testable.

### Task 1: FooterSettings Configuration Record

**Files:**
- Create: `src/JD.AI.Core/Config/FooterSettings.cs`
- Modify: `src/JD.AI.Core/Config/TuiSettings.cs:51` (add Footer property)
- Test: `tests/JD.AI.Tests/Config/FooterSettingsTests.cs`

- [ ] **Step 1: Write the failing test for default values**

```csharp
// tests/JD.AI.Tests/Config/FooterSettingsTests.cs
using FluentAssertions;
using JD.AI.Core.Config;

namespace JD.AI.Tests.Config;

public sealed class FooterSettingsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var settings = new FooterSettings();
        settings.Enabled.Should().BeTrue();
        settings.Lines.Should().Be(1);
        settings.Template.Should().Contain("{folder}");
        settings.Template.Should().Contain("{context}");
        settings.WarnThresholdPercent.Should().Be(15);
    }

    [Fact]
    public void Normalize_Null_ReturnsDefaults()
    {
        var result = FooterSettings.Normalize(null);
        result.Should().NotBeNull();
        result.Enabled.Should().BeTrue();
        result.WarnThresholdPercent.Should().Be(15);
    }

    [Fact]
    public void Normalize_ClampsWarnThreshold()
    {
        var settings = new FooterSettings { WarnThresholdPercent = -5 };
        var result = FooterSettings.Normalize(settings);
        result.WarnThresholdPercent.Should().Be(1);
    }

    [Fact]
    public void Normalize_ClampsLines()
    {
        var settings = new FooterSettings { Lines = 10 };
        var result = FooterSettings.Normalize(settings);
        result.Lines.Should().Be(3);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/JD.AI.Tests --filter "FooterSettingsTests" --no-restore -v quiet`
Expected: FAIL — `FooterSettings` type does not exist

- [ ] **Step 3: Write FooterSettings record**

```csharp
// src/JD.AI.Core/Config/FooterSettings.cs
namespace JD.AI.Core.Config;

/// <summary>
/// Configuration for the persistent TUI footer/status bar.
/// </summary>
public sealed record FooterSettings
{
    public const string DefaultTemplate =
        "{folder} │ {branch?} │ {pr?} │ {context} │ {provider} │ {model} │ {turns}";

    /// <summary>Whether the footer is displayed.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Number of footer lines (1–3).</summary>
    public int Lines { get; init; } = 1;

    /// <summary>
    /// Template string. Segments: {name} always shown, {name?} conditional.
    /// Built-in: folder, branch, pr, context, provider, model, turns, mode, compact, duration.
    /// Plugin: {plugin:key}.
    /// </summary>
    public string Template { get; init; } = DefaultTemplate;

    /// <summary>
    /// Percentage of remaining context window at which a warning appears (1–50). Default: 15.
    /// </summary>
    public int WarnThresholdPercent { get; init; } = 15;

    /// <summary>Per-segment visibility overrides. Keys are segment names.</summary>
    public Dictionary<string, SegmentVisibilityOverride> Segments { get; init; } = new();

    public static FooterSettings Normalize(FooterSettings? settings)
    {
        var value = settings ?? new FooterSettings();
        return value with
        {
            Lines = Math.Clamp(value.Lines, 1, 3),
            WarnThresholdPercent = Math.Clamp(value.WarnThresholdPercent, 1, 50),
            Template = string.IsNullOrWhiteSpace(value.Template) ? DefaultTemplate : value.Template,
        };
    }
}

/// <summary>Per-segment visibility override.</summary>
public sealed record SegmentVisibilityOverride
{
    /// <summary>"always", "auto", or "never".</summary>
    public string Visible { get; init; } = "auto";

    /// <summary>Optional per-segment warn percent (overrides global WarnThresholdPercent).</summary>
    public int? WarnPercent { get; init; }
}
```

- [ ] **Step 4: Wire into TuiSettings**

In `src/JD.AI.Core/Config/TuiSettings.cs`, after line 51 add:

```csharp
/// <summary>Footer/status bar settings.</summary>
public FooterSettings Footer { get; init; } = new();
```

In the `Normalize` method, add to the `with` expression:

```csharp
Footer = FooterSettings.Normalize(settings.Footer),
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/JD.AI.Tests --filter "FooterSettingsTests" --no-restore -v quiet`
Expected: PASS (4 tests)

- [ ] **Step 6: Commit**

```bash
git add src/JD.AI.Core/Config/FooterSettings.cs src/JD.AI.Core/Config/TuiSettings.cs tests/JD.AI.Tests/Config/FooterSettingsTests.cs
git commit -m "feat: add FooterSettings configuration record"
```

---

### Task 2: FooterTemplate Parser

**Files:**
- Create: `src/JD.AI/Rendering/FooterTemplate.cs`
- Test: `tests/JD.AI.Tests/Rendering/FooterTemplateTests.cs`

- [ ] **Step 1: Write failing tests for template parsing**

```csharp
// tests/JD.AI.Tests/Rendering/FooterTemplateTests.cs
using FluentAssertions;
using JD.AI.Rendering;

namespace JD.AI.Tests.Rendering;

public sealed class FooterTemplateTests
{
    [Fact]
    public void Parse_ExtractsSegmentTokens()
    {
        var template = FooterTemplate.Parse("{folder} │ {model}");
        template.Tokens.Should().HaveCount(3); // segment, literal, segment
        template.Tokens[0].SegmentKey.Should().Be("folder");
        template.Tokens[1].IsLiteral.Should().BeTrue();
        template.Tokens[2].SegmentKey.Should().Be("model");
    }

    [Fact]
    public void Parse_ConditionalSegment_MarkedOptional()
    {
        var template = FooterTemplate.Parse("{branch?}");
        template.Tokens[0].IsConditional.Should().BeTrue();
        template.Tokens[0].SegmentKey.Should().Be("branch");
    }

    [Fact]
    public void Render_OmitsConditionalWithNullValue()
    {
        var template = FooterTemplate.Parse("{folder} │ {branch?} │ {model}");
        var segments = new Dictionary<string, string?>
        {
            ["folder"] = "~/app",
            ["branch"] = null,
            ["model"] = "claude-sonnet-4-6",
        };

        var result = template.Render(segments);
        result.Should().Be("~/app │ claude-sonnet-4-6");
    }

    [Fact]
    public void Render_IncludesConditionalWithValue()
    {
        var template = FooterTemplate.Parse("{folder} │ {branch?} │ {model}");
        var segments = new Dictionary<string, string?>
        {
            ["folder"] = "~/app",
            ["branch"] = "main",
            ["model"] = "claude-sonnet-4-6",
        };

        var result = template.Render(segments);
        result.Should().Be("~/app │ main │ claude-sonnet-4-6");
    }

    [Fact]
    public void Render_CollapsesAdjacentSeparators()
    {
        var template = FooterTemplate.Parse("{a} │ {b?} │ {c?} │ {d}");
        var segments = new Dictionary<string, string?>
        {
            ["a"] = "A", ["b"] = null, ["c"] = null, ["d"] = "D",
        };

        var result = template.Render(segments);
        result.Should().Be("A │ D");
    }

    [Fact]
    public void Render_UnknownSegmentKey_OmittedSilently()
    {
        var template = FooterTemplate.Parse("{folder} │ {unknown}");
        var segments = new Dictionary<string, string?> { ["folder"] = "~/app" };

        var result = template.Render(segments);
        result.Should().Be("~/app");
    }

    [Fact]
    public void Parse_MalformedTemplate_ReturnsLiteralFallback()
    {
        var template = FooterTemplate.Parse("{unclosed");
        template.Tokens.Should().HaveCount(1);
        template.Tokens[0].IsLiteral.Should().BeTrue();
    }

    [Fact]
    public void Parse_PluginSegment_ExtractsNamespace()
    {
        var template = FooterTemplate.Parse("{plugin:deploy-status}");
        template.Tokens[0].SegmentKey.Should().Be("plugin:deploy-status");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/JD.AI.Tests --filter "FooterTemplateTests" --no-restore -v quiet`
Expected: FAIL — `FooterTemplate` type does not exist

- [ ] **Step 3: Implement FooterTemplate**

```csharp
// src/JD.AI/Rendering/FooterTemplate.cs
using System.Text;
using System.Text.RegularExpressions;

namespace JD.AI.Rendering;

/// <summary>
/// Parses and renders footer template strings with segment substitution.
/// </summary>
public sealed partial class FooterTemplate
{
    private static readonly Regex TokenPattern = TokenRegex();

    public IReadOnlyList<TemplateToken> Tokens { get; }

    private FooterTemplate(IReadOnlyList<TemplateToken> tokens) => Tokens = tokens;

    public static FooterTemplate Parse(string template)
    {
        var tokens = new List<TemplateToken>();
        var lastEnd = 0;

        foreach (Match match in TokenPattern.Matches(template))
        {
            if (match.Index > lastEnd)
                tokens.Add(TemplateToken.Literal(template[lastEnd..match.Index]));

            var key = match.Groups["key"].Value;
            var conditional = match.Groups["cond"].Success;
            tokens.Add(TemplateToken.Segment(key, conditional));
            lastEnd = match.Index + match.Length;
        }

        if (lastEnd < template.Length)
            tokens.Add(TemplateToken.Literal(template[lastEnd..]));

        // If no segments found (malformed), treat entire string as literal
        if (tokens.Count > 0 && tokens.All(t => t.IsLiteral))
            return new FooterTemplate(tokens);

        return new FooterTemplate(tokens);
    }

    public string Render(IReadOnlyDictionary<string, string?> segments)
    {
        // First pass: resolve all tokens
        var resolved = new List<(bool IsLiteral, string Text, bool WasOmitted)>();

        foreach (var token in Tokens)
        {
            if (token.IsLiteral)
            {
                resolved.Add((true, token.LiteralText!, false));
            }
            else
            {
                segments.TryGetValue(token.SegmentKey!, out var value);
                var hasValue = !string.IsNullOrEmpty(value);

                if (!hasValue && (token.IsConditional || !segments.ContainsKey(token.SegmentKey!)))
                {
                    resolved.Add((false, "", true));
                }
                else
                {
                    resolved.Add((false, value ?? "", false));
                }
            }
        }

        // Second pass: collapse separators adjacent to omitted segments
        var sb = new StringBuilder();
        for (var i = 0; i < resolved.Count; i++)
        {
            var (isLiteral, text, wasOmitted) = resolved[i];

            if (wasOmitted)
                continue;

            if (isLiteral)
            {
                // Skip separator if previous non-literal was omitted or next non-literal is omitted
                var prevSegmentOmitted = PrevSegmentOmitted(resolved, i);
                var nextSegmentOmitted = NextSegmentOmitted(resolved, i);

                if (prevSegmentOmitted || nextSegmentOmitted)
                {
                    // Check if this is a separator (only whitespace and │)
                    if (IsSeparator(text))
                        continue;
                }

                sb.Append(text);
            }
            else
            {
                sb.Append(text);
            }
        }

        return sb.ToString().Trim();
    }

    private static bool PrevSegmentOmitted(
        List<(bool IsLiteral, string Text, bool WasOmitted)> resolved, int literalIndex)
    {
        for (var i = literalIndex - 1; i >= 0; i--)
        {
            if (!resolved[i].IsLiteral) return resolved[i].WasOmitted;
        }
        return true; // No previous segment = start of line
    }

    private static bool NextSegmentOmitted(
        List<(bool IsLiteral, string Text, bool WasOmitted)> resolved, int literalIndex)
    {
        for (var i = literalIndex + 1; i < resolved.Count; i++)
        {
            if (!resolved[i].IsLiteral) return resolved[i].WasOmitted;
        }
        return true; // No next segment = end of line
    }

    private static bool IsSeparator(string text) =>
        text.Trim() is "" or "│" or "|" or "·" or "•" or "-" or "—";

    [GeneratedRegex(@"\{(?<key>[a-zA-Z][a-zA-Z0-9:_-]*)(?<cond>\?)?\}")]
    private static partial Regex TokenRegex();
}

/// <summary>A parsed token from a footer template — either a literal or a segment reference.</summary>
public sealed record TemplateToken
{
    public bool IsLiteral { get; private init; }
    public string? LiteralText { get; private init; }
    public string? SegmentKey { get; private init; }
    public bool IsConditional { get; private init; }

    public static TemplateToken Literal(string text) => new() { IsLiteral = true, LiteralText = text };
    public static TemplateToken Segment(string key, bool conditional) =>
        new() { IsLiteral = false, SegmentKey = key, IsConditional = conditional };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/JD.AI.Tests --filter "FooterTemplateTests" --no-restore -v quiet`
Expected: PASS (8 tests)

- [ ] **Step 5: Commit**

```bash
git add src/JD.AI/Rendering/FooterTemplate.cs tests/JD.AI.Tests/Rendering/FooterTemplateTests.cs
git commit -m "feat: add FooterTemplate parser with conditional segment support"
```

---

### Task 3: FooterState Data Record

**Files:**
- Create: `src/JD.AI/Rendering/FooterState.cs`
- Test: `tests/JD.AI.Tests/Rendering/FooterStateTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/JD.AI.Tests/Rendering/FooterStateTests.cs
using FluentAssertions;
using JD.AI.Rendering;

namespace JD.AI.Tests.Rendering;

public sealed class FooterStateTests
{
    [Fact]
    public void ToSegments_IncludesAlwaysVisibleKeys()
    {
        var state = CreateDefault();
        var segments = state.ToSegments();

        segments.Should().ContainKey("folder");
        segments.Should().ContainKey("context");
        segments.Should().ContainKey("provider");
        segments.Should().ContainKey("model");
        segments.Should().ContainKey("turns");
    }

    [Fact]
    public void ToSegments_BranchNull_WhenNotInGitRepo()
    {
        var state = CreateDefault() with { GitBranch = null };
        var segments = state.ToSegments();

        segments["branch"].Should().BeNull();
    }

    [Fact]
    public void ToSegments_ContextFormat_ShowsHumanReadable()
    {
        var state = CreateDefault() with
        {
            ContextTokensUsed = 12_000,
            ContextWindowSize = 200_000,
        };
        var segments = state.ToSegments();

        segments["context"].Should().Be("12.0k/200.0k");
    }

    [Fact]
    public void ToSegments_ContextWarning_WhenBelowThreshold()
    {
        var state = CreateDefault() with
        {
            ContextTokensUsed = 180_000,
            ContextWindowSize = 200_000,
            WarnThresholdPercent = 15,
        };
        var segments = state.ToSegments();

        // 10% remaining < 15% threshold
        segments["compact"].Should().NotBeNull();
    }

    [Fact]
    public void ToSegments_NoCompactWarning_WhenAboveThreshold()
    {
        var state = CreateDefault() with
        {
            ContextTokensUsed = 100_000,
            ContextWindowSize = 200_000,
            WarnThresholdPercent = 15,
        };
        var segments = state.ToSegments();

        segments["compact"].Should().BeNull();
    }

    [Fact]
    public void FolderPath_ShortenedWithTilde()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var state = CreateDefault() with { WorkingDirectory = Path.Combine(home, "projects", "app") };
        var segments = state.ToSegments();

        segments["folder"].Should().StartWith("~");
    }

    private static FooterState CreateDefault() => new(
        WorkingDirectory: "/home/user/projects/app",
        GitBranch: "main",
        PrLink: null,
        ContextTokensUsed: 5_000,
        ContextWindowSize: 200_000,
        Provider: "anthropic",
        Model: "claude-sonnet-4-6",
        TurnCount: 3,
        Mode: Core.Agents.PermissionMode.Normal,
        WarnThresholdPercent: 15,
        PluginSegments: []);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/JD.AI.Tests --filter "FooterStateTests" --no-restore -v quiet`
Expected: FAIL — `FooterState` type does not exist

- [ ] **Step 3: Implement FooterState**

```csharp
// src/JD.AI/Rendering/FooterState.cs
using JD.AI.Core.Agents;

namespace JD.AI.Rendering;

/// <summary>Immutable snapshot of all data needed to render the footer bar.</summary>
public sealed record FooterState(
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
    IReadOnlyList<PluginSegment> PluginSegments)
{
    /// <summary>Resolve all segments to a dictionary for template rendering.</summary>
    public Dictionary<string, string?> ToSegments()
    {
        var segments = new Dictionary<string, string?>
        {
            ["folder"] = ShortenPath(WorkingDirectory),
            ["branch"] = GitBranch,
            ["pr"] = PrLink,
            ["context"] = FormatContext(ContextTokensUsed, ContextWindowSize),
            ["provider"] = Provider,
            ["model"] = Model,
            ["turns"] = $"turn {TurnCount}",
            ["mode"] = Mode == PermissionMode.Normal ? null : Mode.ToString(),
            ["compact"] = GetCompactWarning(),
            ["duration"] = null, // Populated externally if enabled
        };

        foreach (var plugin in PluginSegments)
            segments[$"plugin:{plugin.Key}"] = plugin.Value;

        return segments;
    }

    private string? GetCompactWarning()
    {
        if (ContextWindowSize <= 0) return null;
        var remainingPercent = 100.0 * (ContextWindowSize - ContextTokensUsed) / ContextWindowSize;
        return remainingPercent < WarnThresholdPercent ? $"{remainingPercent:F0}% left" : null;
    }

    internal static string ShortenPath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) &&
            path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
        {
            return "~" + path[home.Length..].Replace('\\', '/');
        }

        return path;
    }

    internal static string FormatContext(long used, long total)
    {
        if (total <= 0) return $"{FormatTokenCount(used)}";
        return $"{FormatTokenCount(used)}/{FormatTokenCount(total)}";
    }

    private static string FormatTokenCount(long count) => count switch
    {
        >= 1_000_000 => $"{count / 1_000_000.0:F1}M",
        >= 1_000 => $"{count / 1_000.0:F1}k",
        _ => count.ToString(),
    };
}

/// <summary>A segment contributed by a plugin via the StatusLineUpdate hook.</summary>
public sealed record PluginSegment(string Key, string Value, int Priority = 0);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/JD.AI.Tests --filter "FooterStateTests" --no-restore -v quiet`
Expected: PASS (6 tests)

- [ ] **Step 5: Commit**

```bash
git add src/JD.AI/Rendering/FooterState.cs tests/JD.AI.Tests/Rendering/FooterStateTests.cs
git commit -m "feat: add FooterState record with segment resolution"
```

---

## Chunk 2: Footer Rendering

Build the visual footer bar component using the template engine and state from Chunk 1.

### Task 4: FooterBar Renderable

**Files:**
- Create: `src/JD.AI/Rendering/FooterBar.cs`
- Test: `tests/JD.AI.Tests/Rendering/FooterBarTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/JD.AI.Tests/Rendering/FooterBarTests.cs
using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Rendering;
using Spectre.Console;
using Spectre.Console.Testing;

namespace JD.AI.Tests.Rendering;

public sealed class FooterBarTests
{
    [Fact]
    public void Render_ContainsAllDefaultSegments()
    {
        var state = CreateState();
        var bar = new FooterBar(FooterSettings.DefaultTemplate);
        var console = new TestConsole();

        console.Write(bar.ToRenderable(state));
        var output = console.Output;

        output.Should().Contain("~/app");
        output.Should().Contain("anthropic");
        output.Should().Contain("claude-sonnet-4-6");
        output.Should().Contain("5.0k/200.0k");
        output.Should().Contain("turn 3");
    }

    [Fact]
    public void Render_OmitsBranch_WhenNull()
    {
        var state = CreateState() with { GitBranch = null };
        var bar = new FooterBar(FooterSettings.DefaultTemplate);
        var console = new TestConsole();

        console.Write(bar.ToRenderable(state));
        var output = console.Output;

        output.Should().NotContain("main");
    }

    [Fact]
    public void Render_ShowsBranch_WhenPresent()
    {
        var state = CreateState();
        var bar = new FooterBar(FooterSettings.DefaultTemplate);
        var console = new TestConsole();

        console.Write(bar.ToRenderable(state));
        var output = console.Output;

        output.Should().Contain("main");
    }

    [Fact]
    public void Render_Disabled_ReturnsEmpty()
    {
        var bar = new FooterBar(FooterSettings.DefaultTemplate, enabled: false);
        var renderable = bar.ToRenderable(CreateState());

        var console = new TestConsole();
        console.Write(renderable);
        console.Output.Trim().Should().BeEmpty();
    }

    private static FooterState CreateState() => new(
        WorkingDirectory: "/home/user/app",
        GitBranch: "main",
        PrLink: null,
        ContextTokensUsed: 5_000,
        ContextWindowSize: 200_000,
        Provider: "anthropic",
        Model: "claude-sonnet-4-6",
        TurnCount: 3,
        Mode: PermissionMode.Normal,
        WarnThresholdPercent: 15,
        PluginSegments: []);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/JD.AI.Tests --filter "FooterBarTests" --no-restore -v quiet`
Expected: FAIL — `FooterBar` type does not exist

- [ ] **Step 3: Implement FooterBar**

```csharp
// src/JD.AI/Rendering/FooterBar.cs
using Spectre.Console;
using Spectre.Console.Rendering;

namespace JD.AI.Rendering;

/// <summary>
/// Renders a footer status bar from a FooterState snapshot using a parsed template.
/// </summary>
public sealed class FooterBar
{
    private readonly FooterTemplate _template;
    private readonly bool _enabled;

    public FooterBar(string templateString, bool enabled = true)
    {
        _template = FooterTemplate.Parse(templateString);
        _enabled = enabled;
    }

    /// <summary>
    /// Produce a Spectre IRenderable for the current state.
    /// </summary>
    public IRenderable ToRenderable(FooterState state)
    {
        if (!_enabled)
            return new Text("");

        var segments = state.ToSegments();
        var rendered = _template.Render(segments);

        // Pad to full width with grey background
        var width = System.Console.WindowWidth;
        var padded = rendered.Length < width
            ? rendered.PadRight(width)
            : rendered[..width];

        return new Markup($"[on grey]{Markup.Escape(padded)}[/]");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/JD.AI.Tests --filter "FooterBarTests" --no-restore -v quiet`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add src/JD.AI/Rendering/FooterBar.cs tests/JD.AI.Tests/Rendering/FooterBarTests.cs
git commit -m "feat: add FooterBar renderable component"
```

---

### Task 5: FooterStateProvider

**Files:**
- Create: `src/JD.AI/Rendering/FooterStateProvider.cs`
- Test: `tests/JD.AI.Tests/Rendering/FooterStateProviderTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/JD.AI.Tests/Rendering/FooterStateProviderTests.cs
using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Rendering;

namespace JD.AI.Tests.Rendering;

public sealed class FooterStateProviderTests
{
    [Fact]
    public void BuildState_PopulatesRequiredFields()
    {
        var provider = new FooterStateProvider("/home/user/app");
        provider.Update(
            provider: "anthropic",
            model: "claude-sonnet-4-6",
            tokensUsed: 5_000,
            contextWindow: 200_000,
            turnCount: 3,
            mode: PermissionMode.Normal,
            warnThresholdPercent: 15);

        var state = provider.CurrentState;

        state.WorkingDirectory.Should().Be("/home/user/app");
        state.Provider.Should().Be("anthropic");
        state.Model.Should().Be("claude-sonnet-4-6");
        state.ContextTokensUsed.Should().Be(5_000);
        state.TurnCount.Should().Be(3);
    }

    [Fact]
    public void SetGitInfo_UpdatesBranchAndPr()
    {
        var provider = new FooterStateProvider("/tmp/test");
        provider.SetGitInfo("feature/foo", "PR #42");

        var state = provider.CurrentState;
        state.GitBranch.Should().Be("feature/foo");
        state.PrLink.Should().Be("PR #42");
    }

    [Fact]
    public void SetGitInfo_NullClearsBranch()
    {
        var provider = new FooterStateProvider("/tmp/test");
        provider.SetGitInfo("main", null);
        provider.SetGitInfo(null, null);

        provider.CurrentState.GitBranch.Should().BeNull();
    }

    [Fact]
    public void AddPluginSegment_AppearsInState()
    {
        var provider = new FooterStateProvider("/tmp/test");
        provider.AddPluginSegment("deploy", "prod-v2.1");

        var state = provider.CurrentState;
        state.PluginSegments.Should().ContainSingle(s => s.Key == "deploy");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/JD.AI.Tests --filter "FooterStateProviderTests" --no-restore -v quiet`
Expected: FAIL — `FooterStateProvider` type does not exist

- [ ] **Step 3: Implement FooterStateProvider**

```csharp
// src/JD.AI/Rendering/FooterStateProvider.cs
using JD.AI.Core.Agents;

namespace JD.AI.Rendering;

/// <summary>
/// Collects session, git, and plugin data to build FooterState snapshots.
/// Thread-safe — called from both the main loop and background refresh timers.
/// </summary>
public sealed class FooterStateProvider
{
    private readonly string _workingDirectory;
    private readonly object _lock = new();

    private string _provider = "?";
    private string _model = "?";
    private long _tokensUsed;
    private long _contextWindow;
    private int _turnCount;
    private PermissionMode _mode;
    private double _warnThresholdPercent = 15;
    private string? _gitBranch;
    private string? _prLink;
    private readonly List<PluginSegment> _pluginSegments = [];

    public FooterStateProvider(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }

    /// <summary>Update session-level data (called after each turn).</summary>
    public void Update(
        string provider, string model,
        long tokensUsed, long contextWindow,
        int turnCount, PermissionMode mode,
        double warnThresholdPercent)
    {
        lock (_lock)
        {
            _provider = provider;
            _model = model;
            _tokensUsed = tokensUsed;
            _contextWindow = contextWindow;
            _turnCount = turnCount;
            _mode = mode;
            _warnThresholdPercent = warnThresholdPercent;
        }
    }

    /// <summary>Update git branch and PR info (called from background refresh).</summary>
    public void SetGitInfo(string? branch, string? prLink)
    {
        lock (_lock)
        {
            _gitBranch = branch;
            _prLink = prLink;
        }
    }

    /// <summary>Add or update a plugin-contributed segment.</summary>
    public void AddPluginSegment(string key, string value, int priority = 0)
    {
        lock (_lock)
        {
            _pluginSegments.RemoveAll(s => s.Key == key);
            _pluginSegments.Add(new PluginSegment(key, value, priority));
        }
    }

    /// <summary>Build an immutable snapshot of the current state.</summary>
    public FooterState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return new FooterState(
                    WorkingDirectory: _workingDirectory,
                    GitBranch: _gitBranch,
                    PrLink: _prLink,
                    ContextTokensUsed: _tokensUsed,
                    ContextWindowSize: _contextWindow,
                    Provider: _provider,
                    Model: _model,
                    TurnCount: _turnCount,
                    Mode: _mode,
                    WarnThresholdPercent: _warnThresholdPercent,
                    PluginSegments: _pluginSegments.ToList());
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/JD.AI.Tests --filter "FooterStateProviderTests" --no-restore -v quiet`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add src/JD.AI/Rendering/FooterStateProvider.cs tests/JD.AI.Tests/Rendering/FooterStateProviderTests.cs
git commit -m "feat: add FooterStateProvider for collecting footer data"
```

---

## Chunk 3: Wire Footer into InteractiveLoop

Integrate the footer components into the existing TUI without the full `Live` rendering migration yet. This gets the footer visible as a first deliverable. The full `TerminalLayout`/`Live` refactor is a separate follow-up.

### Task 6: Wire Footer into InteractiveLoop

**Files:**
- Modify: `src/JD.AI/Startup/InteractiveLoop.cs:291-526`
- Modify: `src/JD.AI/Rendering/ChatRenderer.cs:342-355`

- [ ] **Step 1: Add FooterStateProvider field to InteractiveLoop**

In `src/JD.AI/Startup/InteractiveLoop.cs`, locate the class fields section (near the top of the class). Add:

```csharp
private FooterStateProvider? _footerStateProvider;
private FooterBar? _footerBar;
```

Add `using JD.AI.Rendering;` to the usings if not already present.

- [ ] **Step 2: Initialize footer in RunAsync (before the main loop)**

In the `RunAsync` method, after TuiSettings is loaded and before the main loop starts, add initialization:

```csharp
// Initialize footer
var footerSettings = tuiSettings.Footer;
if (footerSettings.Enabled)
{
    _footerStateProvider = new FooterStateProvider(Directory.GetCurrentDirectory());
    _footerBar = new FooterBar(footerSettings.Template);
}
```

- [ ] **Step 3: Update RenderStatusBar call to use footer**

In `RunAgentTurnLoopAsync` (around line 520-525), replace the `ChatRenderer.RenderStatusBar(...)` call with:

```csharp
// Update footer state and render
if (_footerStateProvider is not null && _footerBar is not null)
{
    _footerStateProvider.Update(
        provider: _session.CurrentModel?.ProviderName ?? "?",
        model: _session.CurrentModel?.Id ?? "?",
        tokensUsed: _session.TotalTokens,
        contextWindow: _session.CurrentModel?.ContextWindowTokens ?? 0,
        turnCount: _session.SessionInfo?.Turns.Count ?? 0,
        mode: _session.PermissionMode,
        warnThresholdPercent: tuiSettings.Footer.WarnThresholdPercent);

    var renderable = _footerBar.ToRenderable(_footerStateProvider.CurrentState);
    AnsiConsole.Write(renderable);
    AnsiConsole.WriteLine();
}
else
{
    ChatRenderer.RenderStatusBar(
        _session.CurrentModel?.ProviderName ?? "?",
        _session.CurrentModel?.Id ?? "?",
        _session.TotalTokens);
}
```

Note: `tuiSettings` needs to be accessible here. If it's a local variable in `RunAsync`, either promote it to a field or pass the `FooterSettings` through. Check the existing pattern for how `_session` is accessed.

- [ ] **Step 4: Start git info background refresh**

After footer initialization, start a background timer to refresh git info:

```csharp
if (_footerStateProvider is not null)
{
    _ = Task.Run(async () =>
    {
        while (!appCts.IsCancellationRequested)
        {
            try
            {
                var branch = await GetGitBranchAsync();
                var pr = await GetPrLinkAsync();
                _footerStateProvider.SetGitInfo(branch, pr);
            }
            catch { /* swallow — stale data is fine */ }

            await Task.Delay(TimeSpan.FromSeconds(30), appCts.Token);
        }
    }, appCts.Token);
}
```

Add helper methods:

```csharp
private static async Task<string?> GetGitBranchAsync()
{
    try
    {
        var result = await Core.Infrastructure.ProcessExecutor.RunAsync(
            "git", "rev-parse --abbrev-ref HEAD",
            Directory.GetCurrentDirectory(),
            TimeSpan.FromSeconds(2));
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
    }
    catch { return null; }
}

private static async Task<string?> GetPrLinkAsync()
{
    try
    {
        var result = await Core.Infrastructure.ProcessExecutor.RunAsync(
            "gh", "pr view --json number --jq .number",
            Directory.GetCurrentDirectory(),
            TimeSpan.FromSeconds(3));
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput)
            ? $"PR #{result.StandardOutput.Trim()}"
            : null;
    }
    catch { return null; }
}
```

- [ ] **Step 5: Build and verify no compilation errors**

Run: `dotnet build src/JD.AI/JD.AI.csproj --no-restore -v quiet`
Expected: Build succeeded

- [ ] **Step 6: Run existing tests to verify no regressions**

Run: `dotnet test tests/JD.AI.Tests --no-restore -v quiet`
Expected: All existing tests pass

- [ ] **Step 7: Commit**

```bash
git add src/JD.AI/Startup/InteractiveLoop.cs
git commit -m "feat: wire footer bar into interactive loop with git background refresh"
```

---

### Task 7: Render Footer After Mode Bar (Always Visible)

**Files:**
- Modify: `src/JD.AI/Startup/InteractiveLoop.cs:335-341`

- [ ] **Step 1: Add footer rendering before each input prompt**

In `RunMainLoopAsync`, after `ChatRenderer.RenderModeBar(_session.PermissionMode);` (line 339), add footer rendering so it appears before each prompt:

```csharp
ChatRenderer.RenderModeBar(_session.PermissionMode);

// Render footer bar before prompt
if (_footerStateProvider is not null && _footerBar is not null)
{
    var renderable = _footerBar.ToRenderable(_footerStateProvider.CurrentState);
    AnsiConsole.Write(renderable);
    AnsiConsole.WriteLine();
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build src/JD.AI/JD.AI.csproj --no-restore -v quiet`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/JD.AI/Startup/InteractiveLoop.cs
git commit -m "feat: render footer bar before each input prompt"
```

---

### Task 8: Manual Smoke Test

- [ ] **Step 1: Run the TUI and verify the footer appears**

Run: `dotnet run --project src/JD.AI -- --help` (or start an interactive session)

Verify:
- Footer bar appears with grey background below mode bar
- Shows folder, context, provider, model, turns
- Git branch appears if in a git repo
- No visual artifacts or double rendering

- [ ] **Step 2: Run full test suite**

Run: `dotnet test --no-restore -v quiet`
Expected: All tests pass, no regressions

- [ ] **Step 3: Commit any adjustments**

If adjustments were needed during smoke testing, commit them:

```bash
git add -u
git commit -m "fix: adjust footer rendering from smoke test"
```

---

## Chunk 4: Full Test Coverage and Edge Cases

### Task 9: Integration Tests

**Files:**
- Create: `tests/JD.AI.Tests/Rendering/FooterIntegrationTests.cs`

- [ ] **Step 1: Write integration tests covering the full render pipeline**

```csharp
// tests/JD.AI.Tests/Rendering/FooterIntegrationTests.cs
using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Rendering;
using Spectre.Console.Testing;

namespace JD.AI.Tests.Rendering;

/// <summary>
/// End-to-end tests: FooterStateProvider -> FooterState -> FooterTemplate -> FooterBar -> rendered output.
/// </summary>
public sealed class FooterIntegrationTests
{
    [Fact]
    public void FullPipeline_DefaultTemplate_RendersCorrectly()
    {
        var provider = new FooterStateProvider("/home/user/project");
        provider.Update("anthropic", "claude-sonnet-4-6", 12_000, 200_000, 5,
            PermissionMode.Normal, 15);
        provider.SetGitInfo("main", null);

        var bar = new FooterBar(FooterSettings.DefaultTemplate);
        var console = new TestConsole();
        console.Write(bar.ToRenderable(provider.CurrentState));

        var output = console.Output;
        output.Should().Contain("main");
        output.Should().Contain("12.0k/200.0k");
        output.Should().Contain("anthropic");
        output.Should().Contain("turn 5");
    }

    [Fact]
    public void FullPipeline_ContextWarning_AppearsAtThreshold()
    {
        var provider = new FooterStateProvider("/tmp/test");
        provider.Update("anthropic", "claude-sonnet-4-6", 180_000, 200_000, 10,
            PermissionMode.Normal, 15);

        var bar = new FooterBar("{context} │ {compact?}");
        var console = new TestConsole();
        console.Write(bar.ToRenderable(provider.CurrentState));

        console.Output.Should().Contain("left");
    }

    [Fact]
    public void FullPipeline_PluginSegment_RenderedInTemplate()
    {
        var provider = new FooterStateProvider("/tmp/test");
        provider.Update("anthropic", "claude-sonnet-4-6", 1_000, 200_000, 1,
            PermissionMode.Normal, 15);
        provider.AddPluginSegment("deploy", "prod-v3");

        var bar = new FooterBar("{model} │ {plugin:deploy?}");
        var console = new TestConsole();
        console.Write(bar.ToRenderable(provider.CurrentState));

        console.Output.Should().Contain("prod-v3");
    }

    [Fact]
    public void FullPipeline_CustomTemplate_RespectsOrder()
    {
        var provider = new FooterStateProvider("/tmp/test");
        provider.Update("openai", "gpt-4o", 50_000, 128_000, 7,
            PermissionMode.Plan, 15);

        var bar = new FooterBar("{model} │ {turns} │ {mode?}");
        var console = new TestConsole();
        console.Write(bar.ToRenderable(provider.CurrentState));

        var output = console.Output;
        output.Should().Contain("gpt-4o");
        output.Should().Contain("turn 7");
        output.Should().Contain("Plan");
    }

    [Fact]
    public void FullPipeline_DisabledFooter_RendersNothing()
    {
        var provider = new FooterStateProvider("/tmp/test");
        provider.Update("anthropic", "claude-sonnet-4-6", 1_000, 200_000, 1,
            PermissionMode.Normal, 15);

        var bar = new FooterBar(FooterSettings.DefaultTemplate, enabled: false);
        var console = new TestConsole();
        console.Write(bar.ToRenderable(provider.CurrentState));

        console.Output.Trim().Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run all new tests**

Run: `dotnet test tests/JD.AI.Tests --filter "Footer" --no-restore -v quiet`
Expected: All pass

- [ ] **Step 3: Run full test suite for regressions**

Run: `dotnet test tests/JD.AI.Tests --no-restore -v quiet`
Expected: All pass, no regressions

- [ ] **Step 4: Commit**

```bash
git add tests/JD.AI.Tests/Rendering/FooterIntegrationTests.cs
git commit -m "test: add end-to-end footer rendering integration tests"
```

---

### Task 10: TuiSettings Round-Trip Test

**Files:**
- Modify: `tests/JD.AI.Tests/Config/FooterSettingsTests.cs`

- [ ] **Step 1: Add JSON round-trip test**

Append to the existing `FooterSettingsTests`:

```csharp
[Fact]
public void TuiSettings_FooterConfig_RoundTrips()
{
    var settings = new TuiSettings
    {
        Footer = new FooterSettings
        {
            Enabled = true,
            Lines = 2,
            Template = "{model} │ {turns}",
            WarnThresholdPercent = 10,
        },
    };

    var json = System.Text.Json.JsonSerializer.Serialize(settings, JD.AI.Core.Infrastructure.JsonDefaults.Options);
    var deserialized = System.Text.Json.JsonSerializer.Deserialize<TuiSettings>(json, JD.AI.Core.Infrastructure.JsonDefaults.Options);

    deserialized!.Footer.Enabled.Should().BeTrue();
    deserialized.Footer.Lines.Should().Be(2);
    deserialized.Footer.Template.Should().Be("{model} │ {turns}");
    deserialized.Footer.WarnThresholdPercent.Should().Be(10);
}
```

- [ ] **Step 2: Run test**

Run: `dotnet test tests/JD.AI.Tests --filter "TuiSettings_FooterConfig_RoundTrips" --no-restore -v quiet`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add tests/JD.AI.Tests/Config/FooterSettingsTests.cs
git commit -m "test: add TuiSettings footer config JSON round-trip test"
```

---

### Task 11: Final Cleanup and Documentation

- [ ] **Step 1: Run full test suite one final time**

Run: `dotnet test --no-restore -v quiet`
Expected: All tests pass

- [ ] **Step 2: Verify all new files are committed**

Run: `git status`
Expected: Clean working tree

- [ ] **Step 3: Create a summary commit if any loose changes remain**

```bash
git add -u
git commit -m "chore: final cleanup for customizable footer feature"
```
