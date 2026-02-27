namespace JD.AI.Tui.Rendering;

/// <summary>
/// Represents content pasted into the input line that has been collapsed
/// into a visual chip rather than displayed inline.
/// </summary>
public sealed class PastedContent
{
    private static int s_counter;

    public PastedContent(string content, PasteKind kind = PasteKind.Text)
    {
        Id = Interlocked.Increment(ref s_counter);
        RawContent = content;
        Kind = kind;
    }

    public int Id { get; }
    public string RawContent { get; }
    public PasteKind Kind { get; }

    /// <summary>Formatted display label for the chip shown in the input line.</summary>
    public string Label => Kind switch
    {
        PasteKind.Text => FormatTextLabel(),
        PasteKind.Image => $"📷 Pasted Image #{Id} {FormatSize(RawContent.Length)}",
        PasteKind.File => $"📄 Pasted File #{Id} {FormatSize(RawContent.Length)}",
        _ => $"📋 Pasted #{Id}",
    };

    /// <summary>Short tag shown in the input line buffer.</summary>
    public string Chip => $"[{Label}]";

    private string FormatTextLabel()
    {
        var lines = RawContent.Split('\n');
        if (lines.Length > 1)
            return $"📋 Pasted content #{Id} {lines.Length} lines";

        return $"📋 Pasted content #{Id} {FormatCharCount(RawContent.Length)}";
    }

    internal static string FormatCharCount(int chars) => chars switch
    {
        < 1000 => $"{chars} chars",
        < 10_000 => $"{chars / 1000.0:F1}k chars",
        _ => $"{chars / 1000}k chars",
    };

    internal static string FormatSize(int bytes) => bytes switch
    {
        < 1024 => $"{bytes}B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1}KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1}MB",
    };

    /// <summary>Resets the global counter (for testing).</summary>
    internal static void ResetCounter() => s_counter = 0;
}

/// <summary>The type of pasted content.</summary>
public enum PasteKind
{
    Text,
    Image,
    File,
}

/// <summary>
/// The result of an interactive ReadLine call, containing the typed text
/// and any pasted content attachments.
/// </summary>
public sealed class InputResult
{
    public string TypedText { get; init; } = string.Empty;
    public IReadOnlyList<PastedContent> Attachments { get; init; } = [];

    /// <summary>
    /// Assembles the full prompt: typed text with pasted content appended.
    /// </summary>
    public string AssemblePrompt()
    {
        if (Attachments.Count == 0)
            return TypedText;

        var parts = new List<string> { TypedText };
        foreach (var a in Attachments)
        {
            parts.Add($"\n\n--- {a.Label} ---\n{a.RawContent}");
        }

        return string.Join("", parts);
    }
}
