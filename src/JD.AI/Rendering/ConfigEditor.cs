using System.Diagnostics.CodeAnalysis;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Core.PromptCaching;
using Spectre.Console;

namespace JD.AI.Rendering;

/// <summary>
/// Interactive full-screen configuration editor using Spectre.Console prompts.
/// Uses type-appropriate controls: SelectionPrompt for enums/booleans, TextPrompt
/// for validated integers and free-form strings, MultiSelectionPrompt for grouped booleans.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class ConfigEditor
{
    // ── Entry model ─────────────────────────────────────────────────────

    private sealed record ConfigEntry(string Id, string Label, string CurrentValue)
    {
        public static readonly ConfigEntry Done = new("__done__", "✓  Done", "");
        public static readonly ConfigEntry WelcomePanel = new("__welcome__", "Welcome panel…", "");

        public bool IsDone => string.Equals(Id, "__done__", StringComparison.Ordinal);
        public bool IsWelcomePanel => string.Equals(Id, "__welcome__", StringComparison.Ordinal);
    }

    // ── Public entry point ───────────────────────────────────────────────

    /// <summary>
    /// Opens the interactive config editor. Loops until the user selects "Done".
    /// Returns a human-readable summary of changes made during the session.
    /// </summary>
    public static string Edit(
        Func<TuiSettings> loadSettings,
        Action<TuiSettings> saveSettings,
        AgentSession session,
        Action<TuiTheme>? onThemeChanged = null,
        Func<TuiTheme>? getTheme = null,
        Action<bool>? onVimModeChanged = null,
        Func<bool>? getVimMode = null,
        Action<OutputStyle>? onOutputStyleChanged = null,
        Func<OutputStyle>? getOutputStyle = null,
        Action<SpinnerStyle>? onSpinnerStyleChanged = null,
        Func<SpinnerStyle>? getSpinnerStyle = null)
    {
        var changes = new List<string>();

        // Pre-flight: fall back gracefully in non-interactive terminals
        if (!AnsiConsole.Profile.Capabilities.Interactive)
            return "No changes.";

        while (true)
        {
            var settings = loadSettings();
            var welcome = WelcomePanelSettings.Normalize(settings.Welcome);

            // Build the live-value entry list
            var theme = getTheme?.Invoke() ?? settings.Theme;
            var vim = getVimMode?.Invoke() ?? settings.VimMode;
            var output = getOutputStyle?.Invoke() ?? settings.OutputStyle;
            var spinner = getSpinnerStyle?.Invoke() ?? settings.SpinnerStyle;

            var displayEntries = new[]
            {
                new ConfigEntry("theme",        "theme              ", ThemeToken(theme)),
                new ConfigEntry("output_style", "output_style       ", output.ToString().ToLowerInvariant()),
                new ConfigEntry("spinner_style","spinner_style      ", spinner.ToString().ToLowerInvariant()),
                new ConfigEntry("vim_mode",     "vim_mode           ", OnOff(vim)),
            };

            var cacheEntries = new[]
            {
                new ConfigEntry("prompt_cache",     "prompt_cache       ", OnOff(session.PromptCachingEnabled)),
                new ConfigEntry("prompt_cache_ttl", "prompt_cache_ttl   ", PromptCachePolicy.ToToken(session.PromptCacheTtl)),
            };

            var sysPromptEntries = new[]
            {
                new ConfigEntry("sys_prompt_compaction", "sys_prompt_compaction", settings.SystemPromptCompaction.ToString().ToLowerInvariant()),
                new ConfigEntry("sys_prompt_budget",     "sys_prompt_budget    ", $"{settings.SystemPromptBudgetPercent}%"),
            };

            var compactEntries = new[]
            {
                new ConfigEntry("compact_auto",      "compact_auto       ", OnOff(settings.AutoCompact)),
                new ConfigEntry("compact_threshold", "compact_threshold  ", $"{settings.CompactThresholdPercent}%"),
            };

            var sessionEntries = new[]
            {
                new ConfigEntry("autorun",      "autorun            ", OnOff(session.AutoRunEnabled)),
                new ConfigEntry("permissions",  "permissions        ", OnOff(!session.SkipPermissions)),
                new ConfigEntry("plan_mode",    "plan_mode          ", OnOff(session.PlanMode)),
            };

            var welcomeCount = CountEnabledWelcome(welcome);
            var welcomeSummary = $"{welcomeCount}/5 enabled";
            var welcomePanelEntry = ConfigEntry.WelcomePanel with { CurrentValue = welcomeSummary };

            var motdEntries = new[]
            {
                new ConfigEntry("motd_url",        "motd_url           ", welcome.MotdUrl ?? "(none)"),
                new ConfigEntry("motd_timeout_ms",  "motd_timeout_ms    ", $"{welcome.MotdTimeoutMs}ms"),
                new ConfigEntry("motd_max_length",  "motd_max_length    ", $"{welcome.MotdMaxLength} chars"),
            };

            // Selection prompt
            var prompt = new SelectionPrompt<ConfigEntry>()
                .Title("[bold]Configuration Editor[/] [dim](↑/↓ navigate · Enter select)[/]")
                .PageSize(25)
                .HighlightStyle(new Style(Color.Aqua, decoration: Decoration.Bold))
                .UseConverter(e => e.IsDone
                    ? $"[green]{Markup.Escape(e.Label)}[/]"
                    : $"[dim]{Markup.Escape(e.Label)}[/]  [bold]{Markup.Escape(e.CurrentValue)}[/]")
                .AddChoices(ConfigEntry.Done)
                .AddChoiceGroup(new ConfigEntry("__grp_display__", "── Display ──", ""), displayEntries)
                .AddChoiceGroup(new ConfigEntry("__grp_cache__", "── Prompt Caching ──", ""), cacheEntries)
                .AddChoiceGroup(new ConfigEntry("__grp_sysprompt__", "── System Prompt ──", ""), sysPromptEntries)
                .AddChoiceGroup(new ConfigEntry("__grp_compact__", "── Context Compaction ──", ""), compactEntries)
                .AddChoiceGroup(new ConfigEntry("__grp_session__", "── Session Behavior ──", ""), sessionEntries)
                .AddChoiceGroup(new ConfigEntry("__grp_welcome__", "── Welcome Panel ──", ""), [welcomePanelEntry, .. motdEntries]);

            ConfigEntry selected;
            try
            {
                selected = AnsiConsole.Prompt(prompt);
            }
            catch (OperationCanceledException)
            {
                break; // User cancelled
            }
            catch (InvalidOperationException)
            {
                break; // Non-interactive terminal
            }

            if (selected.IsDone)
                break;

            if (selected.IsWelcomePanel)
            {
                EditWelcomePanel(settings, saveSettings, welcome, changes);
                continue;
            }

            var change = EditSetting(
                selected.Id,
                settings,
                saveSettings,
                session,
                onThemeChanged,
                onVimModeChanged,
                onOutputStyleChanged,
                onSpinnerStyleChanged);

            if (change is not null)
                changes.Add(change);
        }

        return changes.Count == 0
            ? "No changes."
            : $"Changes applied:\n{string.Join("\n", changes.Select(c => $"  {c}"))}";
    }

    // ── Welcome panel multi-select ───────────────────────────────────────

    private static void EditWelcomePanel(
        TuiSettings settings,
        Action<TuiSettings> saveSettings,
        WelcomePanelSettings welcome,
        List<string> changes)
    {
        var allOptions = new[]
        {
            ("Model summary",    "model_summary"),
            ("Services status",  "services"),
            ("Working directory","cwd"),
            ("Version",          "version"),
            ("MoTD",             "motd"),
        };

        var prompt = new MultiSelectionPrompt<(string Label, string Id)>()
            .Title("[bold]Welcome Panel[/] [dim](Space toggle · Enter confirm · a all · n none)[/]")
            .PageSize(10)
            .InstructionsText("[dim]<space> toggle · <enter> confirm[/]")
            .UseConverter(o => o.Label)
            .AddChoices(allOptions);

        // Pre-select currently enabled items
        if (welcome.ShowModelSummary) prompt.Select(allOptions[0]);
        if (welcome.ShowServices)     prompt.Select(allOptions[1]);
        if (welcome.ShowWorkingDirectory) prompt.Select(allOptions[2]);
        if (welcome.ShowVersion)      prompt.Select(allOptions[3]);
        if (welcome.ShowMotd)         prompt.Select(allOptions[4]);

        IList<(string Label, string Id)> selected;
        try
        {
            selected = AnsiConsole.Prompt(prompt);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        var selectedIds = selected.Select(o => o.Id).ToHashSet();
        var updated = welcome with
        {
            ShowModelSummary    = selectedIds.Contains("model_summary"),
            ShowServices        = selectedIds.Contains("services"),
            ShowWorkingDirectory = selectedIds.Contains("cwd"),
            ShowVersion         = selectedIds.Contains("version"),
            ShowMotd            = selectedIds.Contains("motd"),
        };

        saveSettings(settings with { Welcome = updated });
        changes.Add($"welcome panel → {CountEnabledWelcome(updated)}/5 enabled");
    }

    // ── Individual setting editor ────────────────────────────────────────

    private static string? EditSetting(
        string id,
        TuiSettings settings,
        Action<TuiSettings> saveSettings,
        AgentSession session,
        Action<TuiTheme>? onThemeChanged,
        Action<bool>? onVimModeChanged,
        Action<OutputStyle>? onOutputStyleChanged,
        Action<SpinnerStyle>? onSpinnerStyleChanged)
    {
        var welcome = WelcomePanelSettings.Normalize(settings.Welcome);

        switch (id)
        {
            case "theme":
            {
                var themes = Enum.GetValues<TuiTheme>();
                if (!TrySelectOne("Select theme", themes, ThemeToken, settings.Theme, out var pick))
                    return null;
                onThemeChanged?.Invoke(pick);
                saveSettings(settings with { Theme = pick });
                return $"theme = {ThemeToken(pick)}";
            }

            case "output_style":
            {
                // Exclude Json: session-only, confusing as a persistent default
                var styles = Enum.GetValues<OutputStyle>().Where(s => s != OutputStyle.Json).ToArray();
                if (!TrySelectOne("Select output style", styles, s => s.ToString().ToLowerInvariant(), settings.OutputStyle, out var pick))
                    return null;
                onOutputStyleChanged?.Invoke(pick);
                saveSettings(settings with { OutputStyle = pick });
                return $"output_style = {pick.ToString().ToLowerInvariant()}";
            }

            case "spinner_style":
            {
                var styles = Enum.GetValues<SpinnerStyle>();
                if (!TrySelectOne("Select spinner style", styles, SpinnerDesc, settings.SpinnerStyle, out var pick))
                    return null;
                onSpinnerStyleChanged?.Invoke(pick);
                saveSettings(settings with { SpinnerStyle = pick });
                return $"spinner_style = {pick.ToString().ToLowerInvariant()}";
            }

            case "vim_mode":
            {
                if (!TrySelectBool("vim_mode", settings.VimMode, out var pick))
                    return null;
                onVimModeChanged?.Invoke(pick);
                saveSettings(settings with { VimMode = pick });
                return $"vim_mode = {OnOff(pick)}";
            }

            case "prompt_cache":
            {
                if (!TrySelectBool("prompt_cache", session.PromptCachingEnabled, out var pick))
                    return null;
                session.PromptCachingEnabled = pick;
                saveSettings(settings with { PromptCacheEnabled = pick });
                return $"prompt_cache = {OnOff(pick)}";
            }

            case "prompt_cache_ttl":
            {
                var options = new[] { ("5m — 5 minutes", PromptCacheTtl.FiveMinutes), ("1h — 1 hour", PromptCacheTtl.OneHour) };
                var defaultOpt = options.First(o => o.Item2 == session.PromptCacheTtl);
                if (!TrySelectOne("Select prompt cache TTL", options, o => o.Item1, defaultOpt, out var pick))
                    return null;
                session.PromptCacheTtl = pick.Item2;
                saveSettings(settings with { PromptCacheTtl = pick.Item2 });
                return $"prompt_cache_ttl = {PromptCachePolicy.ToToken(pick.Item2)}";
            }

            case "sys_prompt_compaction":
            {
                var modes = Enum.GetValues<SystemPromptCompaction>();
                if (!TrySelectOne("Select system prompt compaction", modes,
                        m => m switch
                        {
                            SystemPromptCompaction.Off    => "off    — never compact system prompt",
                            SystemPromptCompaction.Auto   => "auto   — compact when over budget",
                            SystemPromptCompaction.Always => "always — compact at every startup",
                            _ => m.ToString().ToLowerInvariant(),
                        },
                        settings.SystemPromptCompaction, out var pick))
                    return null;
                saveSettings(settings with { SystemPromptCompaction = pick });
                return $"sys_prompt_compaction = {pick.ToString().ToLowerInvariant()}";
            }

            case "sys_prompt_budget":
            {
                var pct = PromptInt("System prompt budget [dim](0–100%)[/]", settings.SystemPromptBudgetPercent, 0, 100);
                if (pct is null) return null;
                saveSettings(settings with { SystemPromptBudgetPercent = pct.Value });
                return $"sys_prompt_budget = {pct.Value}%";
            }

            case "compact_auto":
            {
                if (!TrySelectBool("compact_auto", settings.AutoCompact, out var pick))
                    return null;
                saveSettings(settings with { AutoCompact = pick });
                return $"compact_auto = {OnOff(pick)}";
            }

            case "compact_threshold":
            {
                var pct = PromptInt("Compaction threshold [dim](1–100%)[/]", settings.CompactThresholdPercent, 1, 100);
                if (pct is null) return null;
                saveSettings(settings with { CompactThresholdPercent = pct.Value });
                return $"compact_threshold = {pct.Value}%";
            }

            case "autorun":
            {
                if (!TrySelectBool("autorun", session.AutoRunEnabled, out var pick))
                    return null;
                session.AutoRunEnabled = pick;
                return $"autorun = {OnOff(pick)}";
            }

            case "permissions":
            {
                var current = !session.SkipPermissions;
                if (!TrySelectBool("permissions", current, out var pick))
                    return null;
                session.SkipPermissions = !pick;
                return $"permissions = {OnOff(pick)}";
            }

            case "plan_mode":
            {
                if (!TrySelectBool("plan_mode", session.PlanMode, out var pick))
                    return null;
                session.PlanMode = pick;
                return $"plan_mode = {OnOff(pick)}";
            }

            case "motd_url":
            {
                var url = PromptString("MoTD URL [dim](enter blank to clear)[/]", welcome.MotdUrl ?? "");
                if (url is null) return null;
                var cleaned = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
                saveSettings(settings with { Welcome = welcome with { MotdUrl = cleaned } });
                return $"motd_url = {cleaned ?? "(none)"}";
            }

            case "motd_timeout_ms":
            {
                var ms = PromptInt("MoTD timeout [dim](100–5000 ms)[/]", welcome.MotdTimeoutMs, 100, 5000);
                if (ms is null) return null;
                saveSettings(settings with { Welcome = welcome with { MotdTimeoutMs = ms.Value } });
                return $"motd_timeout_ms = {ms.Value}ms";
            }

            case "motd_max_length":
            {
                var len = PromptInt("MoTD max length [dim](40–1000 chars)[/]", welcome.MotdMaxLength, 40, 1000);
                if (len is null) return null;
                saveSettings(settings with { Welcome = welcome with { MotdMaxLength = len.Value } });
                return $"motd_max_length = {len.Value}";
            }

            default:
                return null;
        }
    }

    // ── Reusable prompt helpers ──────────────────────────────────────────

    /// <summary>
    /// Shows a selection prompt. Returns false (and leaves <paramref name="selected"/> unchanged) if cancelled.
    /// </summary>
    private static bool TrySelectOne<T>(string title, IEnumerable<T> choices, Func<T, string> label, T current, out T selected)
        where T : notnull
    {
        var list = choices.ToList();
        var prompt = new SelectionPrompt<T>()
            .Title($"[bold]{Markup.Escape(title)}[/]")
            .PageSize(15)
            .HighlightStyle(new Style(Color.Aqua, decoration: Decoration.Bold))
            .UseConverter(item =>
            {
                var l = label(item);
                var active = EqualityComparer<T>.Default.Equals(item, current) ? "  [dim]◄ current[/]" : "";
                return $"{Markup.Escape(l)}{active}";
            })
            .AddChoices(list);

        try
        {
            selected = AnsiConsole.Prompt(prompt);
            return true;
        }
        catch (InvalidOperationException)
        {
            selected = current;
            return false;
        }
    }

    private static bool TrySelectBool(string label, bool current, out bool result)
    {
        var options = new[] { ("on", true), ("off", false) };
        if (!TrySelectOne($"Set {label}", options, o => o.Item1, options.First(o => o.Item2 == current), out var pick))
        {
            result = current;
            return false;
        }
        result = pick.Item2;
        return true;
    }

    private static int? PromptInt(string title, int current, int min, int max)
    {
        try
        {
            return AnsiConsole.Prompt(
                new TextPrompt<int>($"[bold]{title}[/] [dim](current: {current})[/]")
                    .DefaultValue(current)
                    .Validate(v => v >= min && v <= max
                        ? ValidationResult.Success()
                        : ValidationResult.Error($"[red]Must be between {min} and {max}[/]")));
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string? PromptString(string title, string current)
    {
        try
        {
            return AnsiConsole.Prompt(
                new TextPrompt<string>($"[bold]{title}[/]")
                    .DefaultValue(current)
                    .AllowEmpty());
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    // ── Utilities ────────────────────────────────────────────────────────

    private static string OnOff(bool value) => value ? "on" : "off";

    private static int CountEnabledWelcome(WelcomePanelSettings w) =>
        (w.ShowModelSummary ? 1 : 0) +
        (w.ShowServices     ? 1 : 0) +
        (w.ShowWorkingDirectory ? 1 : 0) +
        (w.ShowVersion      ? 1 : 0) +
        (w.ShowMotd         ? 1 : 0);

    private static string ThemeToken(TuiTheme theme) => theme switch
    {
        TuiTheme.DefaultDark    => "default-dark",
        TuiTheme.SolarizedDark  => "solarized-dark",
        TuiTheme.SolarizedLight => "solarized-light",
        TuiTheme.OneDark        => "one-dark",
        TuiTheme.CatppuccinMocha => "catppuccin-mocha",
        TuiTheme.HighContrast   => "high-contrast",
        _ => theme.ToString().ToLowerInvariant(),
    };

    private static string SpinnerDesc(SpinnerStyle s) => s switch
    {
        SpinnerStyle.None    => "none    — no animation",
        SpinnerStyle.Minimal => "minimal — dot + elapsed time",
        SpinnerStyle.Normal  => "normal  — braille spinner (default)",
        SpinnerStyle.Rich    => "rich    — spinner + progress bar + stats",
        SpinnerStyle.Nerdy   => "nerdy   — all stats including TTFT",
        _ => s.ToString().ToLowerInvariant(),
    };
}
