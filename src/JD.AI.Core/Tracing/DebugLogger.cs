namespace JD.AI.Core.Tracing;

/// <summary>
/// Debug categories for <c>--debug</c> flag filtering.
/// </summary>
[Flags]
public enum DebugCategory
{
    None = 0,
    Tools = 1 << 0,
    Providers = 1 << 1,
    Sessions = 1 << 2,
    Agents = 1 << 3,
    Policies = 1 << 4,
    Memory = 1 << 5,
    All = Tools | Providers | Sessions | Agents | Policies | Memory,
}

/// <summary>
/// Writes category-filtered debug output to stderr when <c>--debug</c> is active.
/// </summary>
public static class DebugLogger
{
    private static volatile DebugCategory _enabledCategories = DebugCategory.None;

    /// <summary>Whether any debug categories are enabled.</summary>
    public static bool IsEnabled => _enabledCategories != DebugCategory.None;

    /// <summary>Enables the specified debug categories.</summary>
    public static void Enable(DebugCategory categories) => _enabledCategories = categories;

    /// <summary>Parses a comma-separated category string (e.g. "tools,providers").</summary>
    public static DebugCategory ParseCategories(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return DebugCategory.All;

        var result = DebugCategory.None;
        foreach (var part in input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<DebugCategory>(part, ignoreCase: true, out var cat))
                result |= cat;
        }

        return result == DebugCategory.None ? DebugCategory.All : result;
    }

    /// <summary>Logs a debug message if the category is enabled.</summary>
    public static void Log(DebugCategory category, string message)
    {
        if ((_enabledCategories & category) == DebugCategory.None) return;
        Console.Error.WriteLine($"[DEBUG {category.ToString().ToLowerInvariant()}] {message}");
    }

    /// <summary>Logs a debug message if the category is enabled, with formatting.</summary>
    public static void Log(DebugCategory category, string format, params object[] args)
    {
        if ((_enabledCategories & category) == DebugCategory.None) return;
        Console.Error.WriteLine(
            string.Concat("[DEBUG ", category.ToString().ToLowerInvariant(), "] ",
                string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args)));
    }
}
