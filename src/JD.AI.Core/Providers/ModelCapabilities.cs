namespace JD.AI.Core.Providers;

/// <summary>
/// Flags describing what a model supports.
/// A model may support one or more capabilities simultaneously.
/// </summary>
[Flags]
public enum ModelCapabilities
{
    /// <summary>No known capabilities (not yet probed).</summary>
    None = 0,

    /// <summary>Conversational text generation.</summary>
    Chat = 1 << 0,

    /// <summary>Function/tool calling via structured schemas.</summary>
    ToolCalling = 1 << 1,

    /// <summary>Image and multi-modal input processing.</summary>
    Vision = 1 << 2,

    /// <summary>Text embedding generation.</summary>
    Embeddings = 1 << 3,
}

/// <summary>
/// Helpers for <see cref="ModelCapabilities"/> display.
/// </summary>
public static class ModelCapabilitiesExtensions
{
    /// <summary>
    /// Returns a compact badge string for display in the TUI.
    /// Examples: "💬🔧" (chat + tools), "💬" (chat only), "💬👁" (chat + vision).
    /// </summary>
    public static string ToBadge(this ModelCapabilities caps)
    {
        if (caps == ModelCapabilities.None)
            return "[dim]?[/]";

        var parts = new System.Text.StringBuilder();
        if (caps.HasFlag(ModelCapabilities.Chat))
            parts.Append("💬");
        if (caps.HasFlag(ModelCapabilities.ToolCalling))
            parts.Append("🔧");
        if (caps.HasFlag(ModelCapabilities.Vision))
            parts.Append("👁");
        if (caps.HasFlag(ModelCapabilities.Embeddings))
            parts.Append("📐");
        return parts.ToString();
    }

    /// <summary>
    /// Returns a human-readable label string (no emoji).
    /// Example: "Chat, Tools" or "Chat".
    /// </summary>
    public static string ToLabel(this ModelCapabilities caps)
    {
        if (caps == ModelCapabilities.None)
            return "Unknown";

        var labels = new List<string>(4);
        if (caps.HasFlag(ModelCapabilities.Chat))
            labels.Add("Chat");
        if (caps.HasFlag(ModelCapabilities.ToolCalling))
            labels.Add("Tools");
        if (caps.HasFlag(ModelCapabilities.Vision))
            labels.Add("Vision");
        if (caps.HasFlag(ModelCapabilities.Embeddings))
            labels.Add("Embeddings");
        return string.Join(", ", labels);
    }
}
