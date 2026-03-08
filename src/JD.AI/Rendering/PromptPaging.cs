using Spectre.Console;

namespace JD.AI.Rendering;

/// <summary>
/// Shared paging behavior for Spectre selection prompts.
/// Keeps the page size within terminal bounds and renders contextual overflow text.
/// </summary>
internal static class PromptPaging
{
    private const int MinimumPageSize = 5;
    private const int DefaultPageSize = 15;
    private const int ReservedVerticalLines = 8;

    public static SelectionPrompt<T> WithAdaptivePaging<T>(
        this SelectionPrompt<T> prompt,
        int preferredPageSize,
        int totalChoices,
        string singularNoun,
        string? pluralNoun = null)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ValidateNouns(singularNoun, pluralNoun);

        var pageSize = ResolvePageSize(preferredPageSize);
        prompt.PageSize(pageSize);
        ApplyOverflowText(prompt, totalChoices, pageSize, singularNoun, pluralNoun, static (p, text) => p.MoreChoicesText(text));
        return prompt;
    }

    public static MultiSelectionPrompt<T> WithAdaptivePaging<T>(
        this MultiSelectionPrompt<T> prompt,
        int preferredPageSize,
        int totalChoices,
        string singularNoun,
        string? pluralNoun = null)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ValidateNouns(singularNoun, pluralNoun);

        var pageSize = ResolvePageSize(preferredPageSize);
        prompt.PageSize(pageSize);
        ApplyOverflowText(prompt, totalChoices, pageSize, singularNoun, pluralNoun, static (p, text) => p.MoreChoicesText(text));
        return prompt;
    }

    private static void ValidateNouns(string singularNoun, string? pluralNoun)
    {
        if (string.IsNullOrWhiteSpace(singularNoun))
            throw new ArgumentException("A singular noun is required.", nameof(singularNoun));

        if (pluralNoun is not null && string.IsNullOrWhiteSpace(pluralNoun))
            throw new ArgumentException("Plural noun cannot be whitespace.", nameof(pluralNoun));
    }

    private static int ResolvePageSize(int preferredPageSize)
    {
        var effectivePreferred = Math.Max(preferredPageSize, MinimumPageSize);
        return Math.Clamp(effectivePreferred, MinimumPageSize, GetTerminalCapacity());
    }

    private static int GetTerminalCapacity()
    {
        if (Console.IsOutputRedirected)
            return DefaultPageSize;

        try
        {
            var windowHeight = Console.WindowHeight;
            if (windowHeight > 0)
                return Math.Max(windowHeight - ReservedVerticalLines, MinimumPageSize);
        }
        catch (IOException)
        {
            // Fall back to default size when terminal dimensions are unavailable.
        }
        catch (PlatformNotSupportedException)
        {
            // Fall back to default size on platforms that do not expose window sizing.
        }
        catch (InvalidOperationException)
        {
            // Fall back to default size when no interactive console is attached.
        }

        return DefaultPageSize;
    }

    private static void ApplyOverflowText<TPrompt>(
        TPrompt prompt,
        int totalChoices,
        int pageSize,
        string singularNoun,
        string? pluralNoun,
        Action<TPrompt, string> setMoreChoicesText)
    {
        if (totalChoices <= pageSize)
            return;

        var hiddenChoices = totalChoices - pageSize;
        var noun = hiddenChoices == 1
            ? singularNoun
            : pluralNoun ?? $"{singularNoun}s";

        var text = $"[dim]({hiddenChoices} more {Markup.Escape(noun)})[/]";
        setMoreChoicesText(prompt, text);
    }
}
