using JD.AI.Rendering;

namespace JD.AI.Commands;

/// <summary>
///     Single source of truth for slash-command routing/help/completion metadata.
/// </summary>
public static class SlashCommandCatalog
{
    public static IReadOnlyList<SlashCommandDefinition> Definitions { get; } =
    [
        new(
            SlashCommandId.Help,
            "/help",
            "/help",
            "Show this help",
            "Show available commands"),
        new(
            SlashCommandId.Models,
            "/models",
            "/models",
            "Browse and switch models interactively",
            "List available models"),
        new(
            SlashCommandId.Model,
            "/model",
            "/model [id]",
            "Switch model (interactive picker or by name)",
            "Switch to a model",
            AdditionalCompletions:
            [
                new SlashCommandDescriptor("/model search", "Search for models across all providers"),
                new SlashCommandDescriptor("/model url", "Pull a model by URL or identifier")
            ]),
        new(
            SlashCommandId.Providers,
            "/providers",
            "/providers",
            "List detected providers"),
        new(
            SlashCommandId.Provider,
            "/provider",
            "/provider",
            "Show current provider (subcommands: add, remove, test, list)",
            "Manage provider (add|remove|test|list)",
            AdditionalCompletions:
            [
                new SlashCommandDescriptor("/provider add", "Configure an API-key provider"),
                new SlashCommandDescriptor("/provider remove", "Remove provider credentials"),
                new SlashCommandDescriptor("/provider test", "Test provider connectivity"),
                new SlashCommandDescriptor("/provider list", "List all providers with status")
            ]),
        new(SlashCommandId.Clear, "/clear", "/clear", "Clear chat history"),
        new(SlashCommandId.Compact, "/compact", "/compact", "Force context compaction"),
        new(SlashCommandId.Cost, "/cost", "/cost", "Show token usage"),
        new(SlashCommandId.Autorun, "/autorun", "/autorun", "Toggle auto-approve for tools"),
        new(
            SlashCommandId.Permissions,
            "/permissions",
            "/permissions",
            "Toggle permission checks (off = skip all)"),
        new(SlashCommandId.Sessions, "/sessions", "/sessions", "List recent sessions"),
        new(SlashCommandId.Resume, "/resume", "/resume [id]", "Resume a previous session"),
        new(SlashCommandId.Name, "/name", "/name <name>", "Name the current session"),
        new(SlashCommandId.History, "/history", "/history", "Show session turn history"),
        new(SlashCommandId.Export, "/export", "/export", "Export current session to JSON"),
        new(SlashCommandId.Update, "/update", "/update", "Check for and apply updates"),
        new(
            SlashCommandId.Instructions,
            "/instructions",
            "/instructions",
            "Show loaded project instructions"),
        new(
            SlashCommandId.Plugins,
            "/plugins",
            "/plugins",
            "Manage plugins (list/install/enable/disable/update/uninstall/info)",
            AdditionalCompletions:
            [
                new SlashCommandDescriptor("/plugins install", "Install plugin from path or URL"),
                new SlashCommandDescriptor("/plugins enable", "Enable an installed plugin"),
                new SlashCommandDescriptor("/plugins disable", "Disable an installed plugin"),
                new SlashCommandDescriptor("/plugins update", "Update an installed plugin (or all plugins)"),
                new SlashCommandDescriptor("/plugins uninstall", "Uninstall a plugin"),
                new SlashCommandDescriptor("/plugins info", "Show plugin details")
            ]),
        new(
            SlashCommandId.Checkpoint,
            "/checkpoint",
            "/checkpoint",
            "List, restore, or clear checkpoints"),
        new(SlashCommandId.Sandbox, "/sandbox", "/sandbox", "Show sandbox mode info"),
        new(
            SlashCommandId.Workflow,
            "/workflow",
            "/workflow",
            "Manage workflows (list|show|create|rename|remove|compose|dry-run|export|replay|refine|from-history|catalog|publish|install|search|versions)",
            AdditionalCompletions:
            [
                new SlashCommandDescriptor("/workflow list", "List local workflows"),
                new SlashCommandDescriptor("/workflow show", "Show a workflow"),
                new SlashCommandDescriptor("/workflow create", "Generate a workflow from text"),
                new SlashCommandDescriptor("/workflow rename", "Rename a workflow"),
                new SlashCommandDescriptor("/workflow remove", "Remove a workflow"),
                new SlashCommandDescriptor("/workflow compose", "Compose multiple workflows"),
                new SlashCommandDescriptor("/workflow dry-run", "Preview workflow execution"),
                new SlashCommandDescriptor("/workflow export", "Export a workflow"),
                new SlashCommandDescriptor("/workflow replay", "Replay workflow steps"),
                new SlashCommandDescriptor("/workflow refine", "Refine a workflow using model feedback"),
                new SlashCommandDescriptor("/workflow from-history", "Extract a workflow from session history"),
                new SlashCommandDescriptor("/workflow catalog", "List shared workflow catalog"),
                new SlashCommandDescriptor("/workflow publish", "Publish workflow to shared store"),
                new SlashCommandDescriptor("/workflow install", "Install workflow from shared store"),
                new SlashCommandDescriptor("/workflow search", "Search shared workflows"),
                new SlashCommandDescriptor("/workflow versions", "List shared workflow versions")
            ]),
        new(
            SlashCommandId.Spinner,
            "/spinner",
            "/spinner [style]",
            "Set progress style (none|minimal|normal|rich|nerdy)"),
        new(
            SlashCommandId.Local,
            "/local",
            "/local <cmd>",
            "Manage local models (list|add|scan|remove|search|download)",
            IncludeInCompletion: false),
        new(
            SlashCommandId.Mcp,
            "/mcp",
            "/mcp [cmd]",
            "Manage MCP servers (list|add|remove|enable|disable)"),
        new(SlashCommandId.Context, "/context", "/context", "Show context window usage"),
        new(
            SlashCommandId.CompactSystemPrompt,
            "/compact-system-prompt",
            "/compact-system-prompt [off|auto|always]",
            "Compact system prompt or set mode",
            IncludeInCompletion: false),
        new(SlashCommandId.Copy, "/copy", "/copy", "Copy last response to clipboard"),
        new(SlashCommandId.Diff, "/diff", "/diff", "Show uncommitted changes"),
        new(SlashCommandId.Init, "/init", "/init", "Initialize JDAI.md project file"),
        new(SlashCommandId.Plan, "/plan", "/plan", "Toggle plan mode (explore only)"),
        new(SlashCommandId.Doctor, "/doctor", "/doctor", "Run self-diagnostics"),
        new(SlashCommandId.Fork, "/fork", "/fork [name]", "Fork conversation to new session"),
        new(
            SlashCommandId.Review,
            "/review",
            "/review",
            "Review current changes (or branch diff)"),
        new(
            SlashCommandId.SecurityReview,
            "/security-review",
            "/security-review",
            "OWASP/CWE-focused security analysis"),
        new(SlashCommandId.Theme, "/theme", "/theme [name]", "Set/list terminal themes"),
        new(SlashCommandId.Vim, "/vim", "/vim [on|off]", "Toggle vim editing mode"),
        new(
            SlashCommandId.Stats,
            "/stats",
            "/stats [--history|--daily]",
            "Session and historical usage stats"),
        new(
            SlashCommandId.Config,
            "/config",
            "/config [list|get|set]",
            "Manage persisted command settings",
            "List/get/set command settings"),
        new(
            SlashCommandId.Skills,
            "/skills",
            "/skills [status|reload]",
            "Show managed skill eligibility and refresh",
            "Show or reload managed skills status"),
        new(
            SlashCommandId.Agents,
            "/agents",
            "/agents",
            "Manage local agent profiles"),
        new(SlashCommandId.Hooks, "/hooks", "/hooks", "Manage local hook profiles"),
        new(
            SlashCommandId.Memory,
            "/memory",
            "/memory",
            "View/edit project memory (JDAI.md)"),
        new(
            SlashCommandId.OutputStyle,
            "/output-style",
            "/output-style [style]",
            "Set output format (rich|plain|compact|json)",
            "Set output rendering style",
            Aliases: ["/output"]),
        new(
            SlashCommandId.Default,
            "/default",
            "/default",
            "Manage default provider/model (global & per-project)",
            AdditionalCompletions:
            [
                new SlashCommandDescriptor("/default provider", "Set global default provider"),
                new SlashCommandDescriptor("/default model", "Set global default model"),
                new SlashCommandDescriptor("/default project provider", "Set project default provider"),
                new SlashCommandDescriptor("/default project model", "Set project default model")
            ]),
        new(
            SlashCommandId.ModelInfo,
            "/model-info",
            "/model-info [refresh]",
            "Show model metadata (context, cost, capabilities)",
            AdditionalCompletions:
            [
                new SlashCommandDescriptor("/model-info refresh", "Force-refresh model metadata from LiteLLM")
            ]),
        new(
            SlashCommandId.Trace,
            "/trace",
            "/trace [N]",
            "Show execution timeline for the last turn (or turn N)"),
        new(
            SlashCommandId.Shortcuts,
            "/shortcuts",
            "/shortcuts",
            "List keyboard shortcuts"),
        new(
            SlashCommandId.Quit,
            "/quit",
            "/quit",
            "Exit jdai",
            Aliases: ["/exit"])
    ];

    // DispatchMap must be declared after Definitions (C# initializes static fields in declaration order)
    private static readonly Dictionary<string, SlashCommandId> DispatchMap = BuildDispatchMap();

    public static IReadOnlyList<SlashCommandHelpEntry> HelpEntries { get; } =
        Definitions.Select(static d => new SlashCommandHelpEntry(d.HelpSignature, d.HelpDescription)).ToList();

    public static IReadOnlyList<SlashCommandDescriptor> CompletionEntries { get; } = BuildCompletionEntries();

    public static string BuildHelpText()
    {
        var maxUsageLength = HelpEntries.Max(static h => h.Usage.Length);
        var lines = HelpEntries.Select(h => $"  {h.Usage.PadRight(maxUsageLength)} — {h.Description}");
        return "Available commands (all accept /jdai- prefix, e.g. /jdai-config):\n" +
               string.Join('\n', lines);
    }

    public static bool TryResolveDispatch(string commandToken, out SlashCommandId id) =>
        DispatchMap.TryGetValue(NormalizeDispatchToken(commandToken), out id);

    private static string NormalizeDispatchToken(string commandToken)
    {
        if (string.IsNullOrWhiteSpace(commandToken))
            return string.Empty;

        var normalized = commandToken.Trim();
        if (!normalized.StartsWith('/'))
            normalized = "/" + normalized;

        if (normalized.StartsWith("/jdai-", StringComparison.OrdinalIgnoreCase))
            normalized = "/" + normalized[6..];

        return normalized.ToUpperInvariant();
    }

    private static List<SlashCommandDescriptor> BuildCompletionEntries()
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in Definitions)
        {
            if (!definition.IncludeInCompletion)
                continue;

            entries[definition.Command] = definition.CompletionDescription;
            foreach (var alias in definition.Aliases) entries[alias] = definition.CompletionDescription;

            foreach (var additional in definition.AdditionalCompletions)
                entries[additional.Command] = additional.Description;
        }

        return entries.Select(static kvp => new SlashCommandDescriptor(kvp.Key, kvp.Value)).ToList();
    }

    private static Dictionary<string, SlashCommandId> BuildDispatchMap()
    {
        var dispatch = new Dictionary<string, SlashCommandId>(StringComparer.Ordinal);
        foreach (var definition in Definitions)
        {
            RegisterDispatchToken(dispatch, definition.Command, definition.Id);
            foreach (var alias in definition.Aliases) RegisterDispatchToken(dispatch, alias, definition.Id);
        }

        return dispatch;
    }

    private static void RegisterDispatchToken(
        IDictionary<string, SlashCommandId> dispatch,
        string token,
        SlashCommandId id)
    {
        var normalized = NormalizeDispatchToken(token);
        if (!dispatch.TryAdd(normalized, id))
            throw new InvalidOperationException($"Duplicate slash-command token detected: {token}");
    }

    public static void RegisterCompletions(CompletionProvider completionProvider)
    {
        foreach (var entry in CompletionEntries) completionProvider.Register(entry.Command, entry.Description);
    }
}

public sealed record SlashCommandDescriptor(string Command, string Description);

public sealed record SlashCommandHelpEntry(string Usage, string Description);

public sealed class SlashCommandDefinition
{
    public SlashCommandDefinition(
        SlashCommandId Id,
        string Command,
        string HelpSignature,
        string HelpDescription,
        string? CompletionDescription = null,
        bool IncludeInCompletion = true,
        IReadOnlyList<string>? Aliases = null,
        IReadOnlyList<SlashCommandDescriptor>? AdditionalCompletions = null)
    {
        this.Id = Id;
        this.Command = Command;
        this.HelpSignature = HelpSignature;
        this.HelpDescription = HelpDescription;
        this.CompletionDescription = CompletionDescription ?? HelpDescription;
        this.IncludeInCompletion = IncludeInCompletion;
        this.Aliases = Aliases ?? [];
        this.AdditionalCompletions = AdditionalCompletions ?? [];
    }

    public SlashCommandId Id { get; }
    public string Command { get; }
    public string HelpSignature { get; }
    public string HelpDescription { get; }
    public string CompletionDescription { get; }
    public bool IncludeInCompletion { get; }
    public IReadOnlyList<string> Aliases { get; }
    public IReadOnlyList<SlashCommandDescriptor> AdditionalCompletions { get; }
}

public enum SlashCommandId
{
    Help,
    Models,
    Model,
    Providers,
    Provider,
    Clear,
    Compact,
    Cost,
    Autorun,
    Permissions,
    Sessions,
    Resume,
    Name,
    History,
    Export,
    Update,
    Instructions,
    Plugins,
    Checkpoint,
    Sandbox,
    Workflow,
    Spinner,
    Local,
    Mcp,
    Context,
    CompactSystemPrompt,
    Copy,
    Diff,
    Init,
    Plan,
    Doctor,
    Fork,
    Review,
    SecurityReview,
    Theme,
    Vim,
    Stats,
    Config,
    Skills,
    Agents,
    Hooks,
    Memory,
    OutputStyle,
    Default,
    ModelInfo,
    Trace,
    Shortcuts,
    Quit
}
