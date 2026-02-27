namespace JD.AI.Tui.Rendering;

/// <summary>
/// A single completion candidate with optional description.
/// </summary>
public sealed record CompletionItem(string Text, string? Description);

/// <summary>
/// Provides command and keyword completion candidates for the interactive input.
/// </summary>
public sealed class CompletionProvider
{
    private readonly List<CompletionItem> _items = [];

    /// <summary>Register a completion candidate.</summary>
    public void Register(string text, string? description = null) =>
        _items.Add(new CompletionItem(text, description));

    /// <summary>
    /// Returns completions matching the given prefix, ordered alphabetically.
    /// Excludes exact matches (no ghost text for a fully typed command).
    /// </summary>
    public IReadOnlyList<CompletionItem> GetCompletions(string prefix) =>
        string.IsNullOrEmpty(prefix)
            ? []
            : _items
                .Where(i => i.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(i.Text, prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.Text, StringComparer.OrdinalIgnoreCase)
                .ToList();
}
