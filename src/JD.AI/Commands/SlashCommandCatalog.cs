using JD.AI.Rendering;

namespace JD.AI.Commands;

/// <summary>
/// Single source of truth for slash-command completion entries.
/// </summary>
public static class SlashCommandCatalog
{
    public static IReadOnlyList<SlashCommandDescriptor> CompletionEntries { get; } =
    [
        new("/help", "Show available commands"),
        new("/models", "List available models"),
        new("/model", "Switch to a model"),
        new("/model search", "Search for models across all providers"),
        new("/model url", "Pull a model by URL or identifier"),
        new("/providers", "List detected providers"),
        new("/provider", "Manage provider (add|remove|test|list)"),
        new("/provider add", "Configure an API-key provider"),
        new("/provider remove", "Remove provider credentials"),
        new("/provider test", "Test provider connectivity"),
        new("/provider list", "List all providers with status"),
        new("/clear", "Clear chat history"),
        new("/compact", "Force context compaction"),
        new("/cost", "Show token usage"),
        new("/autorun", "Toggle auto-approve for tools"),
        new("/permissions", "Toggle permission checks"),
        new("/sessions", "List recent sessions"),
        new("/resume", "Resume a previous session"),
        new("/name", "Name the current session"),
        new("/history", "Show session turn history"),
        new("/export", "Export current session to JSON"),
        new("/update", "Check for and apply updates"),
        new("/instructions", "Show loaded project instructions"),
        new("/plugins", "List loaded plugins"),
        new("/checkpoint", "List, restore, or clear checkpoints"),
        new("/sandbox", "Show or change sandbox mode"),
        new("/workflow", "Manage workflows (list|show|export|replay|refine)"),
        new("/spinner", "Set progress style (none|minimal|normal|rich|nerdy)"),
        new("/mcp", "Manage MCP servers (list|add|remove|enable|disable)"),
        new("/context", "Show context window usage"),
        new("/copy", "Copy last response to clipboard"),
        new("/diff", "Show uncommitted changes"),
        new("/init", "Initialize JDAI.md project file"),
        new("/plan", "Toggle plan mode (explore only)"),
        new("/doctor", "Run self-diagnostics"),
        new("/fork", "Fork conversation to new session"),
        new("/review", "Review code changes with severity categories"),
        new("/security-review", "Run OWASP/CWE-focused security review"),
        new("/theme", "Set or list terminal themes"),
        new("/vim", "Toggle vim editing mode"),
        new("/stats", "Show session and historical usage statistics"),
        new("/config", "List/get/set command settings"),
        new("/agents", "Manage local agent profiles"),
        new("/hooks", "Manage local hook profiles"),
        new("/memory", "View/edit JDAI.md project memory"),
        new("/output", "Set output rendering style (alias: /output-style)"),
        new("/output-style", "Set output rendering style"),
        new("/default", "Manage default provider/model settings"),
        new("/default provider", "Set global default provider"),
        new("/default model", "Set global default model"),
        new("/default project provider", "Set project default provider"),
        new("/default project model", "Set project default model"),
        new("/quit", "Exit jdai"),
        new("/exit", "Exit jdai"),
    ];

    public static void RegisterCompletions(CompletionProvider completionProvider)
    {
        foreach (var entry in CompletionEntries)
        {
            completionProvider.Register(entry.Command, entry.Description);
        }
    }
}

public sealed record SlashCommandDescriptor(string Command, string Description);
