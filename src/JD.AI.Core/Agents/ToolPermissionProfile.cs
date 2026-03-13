using System.Text.RegularExpressions;

namespace JD.AI.Core.Agents;

/// <summary>
/// In-memory explicit tool permission profile resolved from user/project config.
/// Rules support exact names and glob patterns (<c>*</c>, <c>?</c>).
/// </summary>
public sealed class ToolPermissionProfile
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Simple mutable runtime profile")]
    public List<string> GlobalAllowed { get; } = [];

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Simple mutable runtime profile")]
    public List<string> GlobalDenied { get; } = [];

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Simple mutable runtime profile")]
    public List<string> ProjectAllowed { get; } = [];

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Simple mutable runtime profile")]
    public List<string> ProjectDenied { get; } = [];

    public static ToolPermissionProfile Empty { get; } = new();

    public bool IsExplicitlyAllowed(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return ProjectAllowed.Any(rule => MatchesRule(toolName, rule)) ||
               GlobalAllowed.Any(rule => MatchesRule(toolName, rule));
    }

    public bool IsExplicitlyDenied(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return ProjectDenied.Any(rule => MatchesRule(toolName, rule)) ||
               GlobalDenied.Any(rule => MatchesRule(toolName, rule));
    }

    public void AddAllowed(string toolPattern, bool projectScope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolPattern);
        var list = projectScope ? ProjectAllowed : GlobalAllowed;
        if (!list.Contains(toolPattern, StringComparer.OrdinalIgnoreCase))
            list.Add(toolPattern);
    }

    public void AddDenied(string toolPattern, bool projectScope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolPattern);
        var list = projectScope ? ProjectDenied : GlobalDenied;
        if (!list.Contains(toolPattern, StringComparer.OrdinalIgnoreCase))
            list.Add(toolPattern);
    }

    public static bool MatchesRule(string value, string pattern)
    {
        if (string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase))
            return true;

        if (pattern.IndexOfAny(['*', '?']) < 0)
            return false;

        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + "$";

        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase, RegexTimeout);
    }
}
