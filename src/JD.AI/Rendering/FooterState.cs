using JD.AI.Core.Agents;

namespace JD.AI.Rendering;

/// <summary>
/// A single plugin-provided segment to display in the footer.
/// </summary>
public sealed record PluginSegment(string Key, string Value, int Priority = 0);

/// <summary>
/// Immutable snapshot of all data required to render the TUI footer.
/// </summary>
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
    /// <summary>
    /// Resolves this state into a flat dictionary of named segments.
    /// Keys whose value is null should be hidden by the renderer.
    /// </summary>
    public IDictionary<string, string?> ToSegments()
    {
        var segments = new Dictionary<string, string?>();

        // Always-visible segments
        segments["folder"] = ShortenPath(WorkingDirectory);
        segments["branch"] = GitBranch;
        segments["pr"] = PrLink;
        segments["context"] = FormatContext(ContextTokensUsed, ContextWindowSize);
        segments["provider"] = Provider;
        segments["model"] = Model;
        segments["turns"] = $"turn {TurnCount}";

        // Mode — null when Normal (don't display default)
        segments["mode"] = Mode == PermissionMode.Normal ? null : Mode.ToString();

        // Compact / context-warning — shown when remaining context is below the threshold
        double remainingPercent = ContextWindowSize > 0
            ? (1.0 - (double)ContextTokensUsed / ContextWindowSize) * 100.0
            : 0.0;

        segments["compact"] = remainingPercent < WarnThresholdPercent
            ? $"context {remainingPercent:F0}% remaining"
            : null;

        // Duration — populated externally if enabled
        segments["duration"] = null;

        // Plugin segments
        foreach (var plugin in PluginSegments)
            segments[$"plugin:{plugin.Key}"] = plugin.Value;

        return segments;
    }

    // ── Internal helpers ──────────────────────────────────────────────

    internal static string ShortenPath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) && path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = path[home.Length..];
            // Normalise directory separator so it starts cleanly after ~
            remainder = remainder.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return remainder.Length == 0 ? "~" : $"~/{remainder}";
        }
        return path;
    }

    internal static string FormatContext(long used, long total) =>
        $"{FormatTokenCount(used)}/{FormatTokenCount(total)}";

    internal static string FormatTokenCount(long count)
    {
        const long OneMillion = 1_000_000;
        const long OneThousand = 1_000;

        if (count >= OneMillion)
            return $"{count / (double)OneMillion:F1}M";

        if (count >= OneThousand)
            return $"{count / (double)OneThousand:F1}k";

        return count.ToString();
    }
}
