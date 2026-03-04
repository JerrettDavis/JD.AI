using JD.AI;
using JD.AI.Agent;
using JD.AI.Commands;
using JD.AI.Core.Agents.Checkpointing;
using JD.AI.Core.Agents.Orchestration;
using JD.AI.Core.Config;
using JD.AI.Core.LocalModels;
using JD.AI.Core.Mcp;
using JD.AI.Core.Providers;
using JD.AI.Rendering;
using JD.AI.Tools;
using JD.AI.Workflows;
using JD.SemanticKernel.Extensions.Compaction;
using JD.SemanticKernel.Extensions.Hooks;
using JD.SemanticKernel.Extensions.Plugins;
using JD.SemanticKernel.Extensions.Skills;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Spectre.Console;

// ──────────────────────────────────────────────────────────
//  jdai — Semantic Kernel TUI Agent
// ──────────────────────────────────────────────────────────

// Ensure console uses UTF-8 so Unicode glyphs (Braille spinners, box-drawing,
// checkmarks, etc.) render correctly on all platforms.
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

// Parse CLI flags
var skipPermissions = args.Contains("--dangerously-skip-permissions");
var forceUpdateCheck = args.Contains("--force-update-check");
var resumeId = args.SkipWhile(a => !string.Equals(a, "--resume", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();
var isNewSession = args.Contains("--new");
var cliModel = args.SkipWhile(a => !string.Equals(a, "--model", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();
var cliProvider = args.SkipWhile(a => !string.Equals(a, "--provider", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();
var gatewayMode = args.Contains("--gateway");
var gatewayPort = args.SkipWhile(a => !string.Equals(a, "--gateway-port", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();

// Handle 'mcp' subcommand early (before provider detection).
// Allow global options (starting with '-') to appear before the 'mcp' subcommand,
// e.g. `jdai --debug mcp list` works the same as `jdai mcp list`.
var firstNonOptionIndex = Array.FindIndex(args, a => !a.StartsWith('-'));
if (firstNonOptionIndex >= 0 && string.Equals(args[firstNonOptionIndex], "mcp", StringComparison.OrdinalIgnoreCase))
{
    var mcpArgs = args.Skip(firstNonOptionIndex + 1).ToArray();
    return await McpCliHandler.RunAsync(mcpArgs).ConfigureAwait(false);
}
// Print mode: non-interactive, query → stdout → exit
var printMode = args.Contains("-p") || args.Contains("--print");
var printQuery = printMode
    ? args.SkipWhile(a => !string.Equals(a, "-p", StringComparison.OrdinalIgnoreCase) &&
                          !string.Equals(a, "--print", StringComparison.OrdinalIgnoreCase))
        .Skip(1).FirstOrDefault()
    : null;

// Continue most recent session
var continueSession = args.Contains("-c") || args.Contains("--continue");

// System prompt overrides
var systemPromptOverride = args.SkipWhile(a => !string.Equals(a, "--system-prompt", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();
var appendSystemPrompt = args.SkipWhile(a => !string.Equals(a, "--append-system-prompt", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();
var systemPromptFile = args.SkipWhile(a => !string.Equals(a, "--system-prompt-file", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();
var appendSystemPromptFile = args.SkipWhile(a => !string.Equals(a, "--append-system-prompt-file", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();

// Output format for print mode
var outputFormat = args.SkipWhile(a => !string.Equals(a, "--output-format", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault() ?? "text";

// Max turns limit
var maxTurnsStr = args.SkipWhile(a => !string.Equals(a, "--max-turns", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();
int? maxTurns = int.TryParse(maxTurnsStr, out var mt) ? mt : null;

// Verbose mode
var verboseMode = args.Contains("--verbose");

// Additional working directories
var addDirs = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    if (string.Equals(args[i], "--add-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        addDirs.Add(args[++i]);
    }
}

// Tool filtering
var allowedTools = args.SkipWhile(a => !string.Equals(a, "--allowedTools", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault()?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var disallowedTools = args.SkipWhile(a => !string.Equals(a, "--disallowedTools", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault()?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

// Read piped stdin if available (e.g. `cat file | jdai -p "query"`)
string? pipedInput = null;
if (Console.IsInputRedirected)
{
    pipedInput = await Console.In.ReadToEndAsync().ConfigureAwait(false);
}

// --gateway: start the Gateway as an embedded ASP.NET host alongside the TUI
Microsoft.AspNetCore.Builder.WebApplication? gatewayHost = null;
if (gatewayMode)
{
    var port = gatewayPort ?? "5100";
    var gwBuilder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(["--urls", $"http://localhost:{port}"]);
    gwBuilder.Logging.SetMinimumLevel(LogLevel.Warning);

    var gwApp = gwBuilder.Build();
    gwApp.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));
    gwApp.MapGet("/ready", () => Results.Ok(new { Status = "Ready" }));

    gatewayHost = gwApp;
    _ = gwApp.StartAsync();
    if (!printMode)
    {
        AnsiConsole.MarkupLine($"[dim]Gateway started on http://localhost:{port}[/]");
    }
}

// Fire background update check immediately (non-blocking)
var updateCheckTask = UpdateChecker.CheckAsync(forceUpdateCheck);

if (!printMode)
{
    AnsiConsole.MarkupLine("[dim]Detecting providers...[/]");
}

// 1. Build provider registry with all detectors
var detectors = new IProviderDetector[]
{
    new ClaudeCodeDetector(),
    new CopilotDetector(),
    new OpenAICodexDetector(),
    new OllamaDetector(),
    new FoundryLocalDetector(),
    new LocalModelDetector(),
};
var registry = new ProviderRegistry(detectors);

// 2. Detect available providers and show status
var providers = await registry.DetectProvidersAsync().ConfigureAwait(false);
if (!printMode)
{
    foreach (var p in providers)
    {
        var icon = p.IsAvailable ? "[green]✓[/]" : "[red]✗[/]";
        AnsiConsole.MarkupLine($"  {icon} [bold]{Markup.Escape(p.Name)}[/]: {Markup.Escape(p.StatusMessage ?? "Unknown")}");
    }
}

var allModels = await registry.GetModelsAsync().ConfigureAwait(false);

if (allModels.Count == 0)
{
    Console.Error.WriteLine("No AI providers available.");
    return 1;
}

// 3. Let user pick a model (or use CLI flags / first available)
ProviderModelInfo selectedModel;
if (cliModel != null)
{
    // --model flag: match by display name or model ID (case-insensitive)
    var candidates = allModels.Where(m =>
        m.DisplayName.Contains(cliModel, StringComparison.OrdinalIgnoreCase) ||
        m.Id.Contains(cliModel, StringComparison.OrdinalIgnoreCase)).ToList();

    if (cliProvider != null)
    {
        candidates = candidates.Where(m =>
            m.ProviderName.Contains(cliProvider, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    if (candidates.Count == 0)
    {
        AnsiConsole.MarkupLine($"[red]No model matching '{Markup.Escape(cliModel)}' found.[/]");
        return 1;
    }

    selectedModel = candidates[0];
}
else if (cliProvider != null)
{
    var candidates = allModels.Where(m =>
        m.ProviderName.Contains(cliProvider, StringComparison.OrdinalIgnoreCase)).ToList();

    if (candidates.Count == 0)
    {
        AnsiConsole.MarkupLine($"[red]No models from provider '{Markup.Escape(cliProvider)}' found.[/]");
        return 1;
    }

    selectedModel = candidates.Count == 1 || printMode
        ? candidates[0]
        : AnsiConsole.Prompt(
            new SelectionPrompt<ProviderModelInfo>()
                .Title("Select a model:")
                .PageSize(15)
                .UseConverter(m => Markup.Escape($"[{m.ProviderName}] {m.DisplayName}"))
                .AddChoices(candidates));
}
else if (allModels.Count == 1 || printMode)
{
    selectedModel = allModels[0];
}
else
{
    selectedModel = AnsiConsole.Prompt(
        new SelectionPrompt<ProviderModelInfo>()
            .Title("Select a model:")
            .PageSize(15)
            .UseConverter(m => Markup.Escape($"[{m.ProviderName}] {m.DisplayName}"))
            .AddChoices(allModels));
}

// 4. Build initial kernel with the selected model
var kernel = registry.BuildKernel(selectedModel);

// 5. Create agent session
var session = new AgentSession(registry, kernel, selectedModel);

// Apply CLI flags
if (skipPermissions)
{
    session.SkipPermissions = true;
    if (!printMode) ChatRenderer.RenderWarning("--dangerously-skip-permissions: ALL tool confirmations disabled.");
}
if (verboseMode)
{
    session.Verbose = true;
}

// Initialize session persistence
var projectPath = Directory.GetCurrentDirectory();
if (!isNewSession)
{
    // --continue: auto-resume the most recent session for this project
    if (continueSession && resumeId == null)
    {
        await session.InitializePersistenceAsync(projectPath).ConfigureAwait(false);
        if (session.Store != null)
        {
            var projectHash = JD.AI.Core.Sessions.ProjectHasher.Hash(projectPath);
            var recentSessions = await session.Store.ListSessionsAsync(projectHash, 1).ConfigureAwait(false);
            if (recentSessions.Count > 0)
            {
                resumeId = recentSessions[0].Id;
            }
        }
    }

    await session.InitializePersistenceAsync(projectPath, resumeId).ConfigureAwait(false);
    if (resumeId != null && session.SessionInfo != null)
    {
        if (!printMode) ChatRenderer.RenderInfo($"Resumed session: {session.SessionInfo.Name ?? session.SessionInfo.Id} ({session.SessionInfo.Turns.Count} turns)");
    }
}
else
{
    await session.InitializePersistenceAsync(projectPath).ConfigureAwait(false);
}

// 6. Register built-in tools
kernel.Plugins.AddFromType<FileTools>("file");
kernel.Plugins.AddFromType<SearchTools>("search");
kernel.Plugins.AddFromType<ShellTools>("shell");
kernel.Plugins.AddFromType<GitTools>("git");
kernel.Plugins.AddFromType<WebTools>("web");
kernel.Plugins.AddFromType<ThinkTools>("think");
kernel.Plugins.AddFromType<EnvironmentTools>("environment");
kernel.Plugins.AddFromType<NotebookTools>("notebook");
kernel.Plugins.AddFromType<ClipboardTools>("clipboard");
kernel.Plugins.AddFromType<DiffTools>("diff");
kernel.Plugins.AddFromType<BatchEditTools>("batchEdit");
kernel.Plugins.AddFromObject(new MemoryTools(), "memory");
kernel.Plugins.AddFromObject(new TaskTools(), "tasks");
var usageTools = new UsageTools();
kernel.Plugins.AddFromObject(usageTools, "usage");
kernel.Plugins.AddFromObject(
    new QuestionTools(req => QuestionnaireSession.Run(req)), "questions");

// 7. Load Claude Code skills, plugins, and hooks if available
var skillDirs = new[]
{
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills"),
    Path.Combine(Directory.GetCurrentDirectory(), ".claude", "skills"),
    Path.Combine(Directory.GetCurrentDirectory(), ".jdai", "skills"),
};

var skillIndex = 0;
foreach (var dir in skillDirs.Where(Directory.Exists))
{
    try
    {
        // Each directory gets a unique plugin name to avoid duplicate key errors
        var suffix = skillIndex == 0 ? "" : $"_{skillIndex}";
        var pluginName = $"Skills{suffix}";
        skillIndex++;

        var builder = Kernel.CreateBuilder();
        JD.SemanticKernel.Extensions.Skills.KernelBuilderExtensions.UseSkills(
            builder, dir, opts => opts.PluginName = pluginName);
        var skillKernel = builder.Build();
        foreach (var plugin in skillKernel.Plugins)
        {
            if (kernel.Plugins.TryGetPlugin(plugin.Name, out _))
            {
                if (!printMode) ChatRenderer.RenderWarning($"  Skipped duplicate skill plugin '{plugin.Name}' from {dir}");
                continue;
            }

            kernel.Plugins.Add(plugin);
        }

        if (!printMode) ChatRenderer.RenderInfo($"  Loaded skills from {dir}");
    }
#pragma warning disable CA1031 // non-fatal
    catch (Exception ex)
    {
        if (!printMode) ChatRenderer.RenderWarning($"  Failed to load skills from {dir}: {ex.Message}");
    }
#pragma warning restore CA1031
}

var pluginDirs = new[]
{
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "plugins"),
    Path.Combine(Directory.GetCurrentDirectory(), ".claude", "plugins"),
    Path.Combine(Directory.GetCurrentDirectory(), ".jdai", "plugins"),
};

foreach (var dir in pluginDirs.Where(Directory.Exists))
{
    try
    {
        var builder = Kernel.CreateBuilder();
        JD.SemanticKernel.Extensions.Plugins.KernelBuilderExtensions.UseAllPlugins(builder, dir);
        var pluginKernel = builder.Build();
        foreach (var plugin in pluginKernel.Plugins)
        {
            if (kernel.Plugins.TryGetPlugin(plugin.Name, out _))
            {
                if (!printMode) ChatRenderer.RenderWarning($"  Skipped duplicate plugin '{plugin.Name}' from {dir}");
                continue;
            }

            kernel.Plugins.Add(plugin);
        }

        if (!printMode) ChatRenderer.RenderInfo($"  Loaded plugins from {dir}");
    }
#pragma warning disable CA1031
    catch (Exception ex)
    {
        if (!printMode) ChatRenderer.RenderWarning($"  Failed to load plugins from {dir}: {ex.Message}");
    }
#pragma warning restore CA1031
}

// 8. Add tool confirmation filter
kernel.AutoFunctionInvocationFilters.Add(new ToolConfirmationFilter(session));

// 8b. Load project instructions (JDAI.md, CLAUDE.md, AGENTS.md, etc.)
var instructions = InstructionsLoader.Load();
if (instructions.HasInstructions)
{
    if (!printMode) ChatRenderer.RenderInfo($"  Loaded {instructions.Files.Count} instruction file(s)");
}

// 8c. Set up subagent runner and register tools
var orchestrator = new TeamOrchestrator(session);
kernel.ImportPluginFromObject(new SubagentTools(orchestrator), "SubagentTools");

// 8d. Set up checkpoint strategy (stash if git repo, directory otherwise)
ICheckpointStrategy checkpointStrategy = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), ".git"))
    ? new StashCheckpointStrategy()
    : new DirectoryCheckpointStrategy();

// 8e. Register web search tools
kernel.ImportPluginFromObject(new WebSearchTools(), "WebSearchTools");

// 8f. Tool filtering (--allowedTools / --disallowedTools)
if (allowedTools is { Length: > 0 })
{
    var allowed = new HashSet<string>(allowedTools, StringComparer.OrdinalIgnoreCase);
    var toRemove = kernel.Plugins
        .SelectMany(p => p.Select(f => (Plugin: p, Function: f)))
        .Where(pf => !allowed.Contains(pf.Function.Name) && !allowed.Contains($"{pf.Plugin.Name}-{pf.Function.Name}"))
        .Select(pf => pf.Plugin.Name)
        .Distinct()
        .ToList();
    foreach (var name in toRemove)
    {
        if (!allowed.Contains(name))
            kernel.Plugins.Remove(kernel.Plugins[name]);
    }
}
if (disallowedTools is { Length: > 0 })
{
    var disallowed = new HashSet<string>(disallowedTools, StringComparer.OrdinalIgnoreCase);
    var toRemove = kernel.Plugins.Where(p => disallowed.Contains(p.Name)).Select(p => p.Name).ToList();
    foreach (var name in toRemove)
    {
        kernel.Plugins.Remove(kernel.Plugins[name]);
    }
}

// 9. Build system prompt
string systemPrompt;
if (systemPromptOverride != null)
{
    systemPrompt = systemPromptOverride;
}
else if (systemPromptFile != null && File.Exists(systemPromptFile))
{
    systemPrompt = await File.ReadAllTextAsync(systemPromptFile).ConfigureAwait(false);
}
else
{
    systemPrompt = """
        You are jdai, a helpful AI coding assistant running in a terminal.
        You have access to tools for file operations, code search, shell commands,
        git operations, web fetching, web search, semantic memory, and subagents.

        When helping with code tasks:
        - Read relevant files before making changes
        - Use search tools to find code patterns
        - Make minimal, surgical edits
        - Verify changes with builds/tests when appropriate
        - Store important decisions and facts in memory for future recall
        - Use subagents for specialized work (explore for analysis, task for commands, plan for planning, review for code review)

        Be concise and direct. Use tools proactively when they'll help answer the question.
        """;

    // Append project instructions
    if (instructions.HasInstructions)
    {
        systemPrompt += "\n\n" + instructions.ToSystemPrompt();
    }
}

// Append additional prompt text
if (appendSystemPrompt != null)
{
    systemPrompt += "\n\n" + appendSystemPrompt;
}
if (appendSystemPromptFile != null && File.Exists(appendSystemPromptFile))
{
    systemPrompt += "\n\n" + await File.ReadAllTextAsync(appendSystemPromptFile).ConfigureAwait(false);
}

// Plan mode injection
if (session.PlanMode)
{
    systemPrompt += "\n\nYou are in plan mode. DO NOT make changes to files. Only read, explore, and plan.";
}

session.History.AddSystemMessage(systemPrompt);

// 9. Wire up SpectreAgentOutput so streaming renders in the TUI
var tuiSettings = TuiSettings.Load();
ChatRenderer.ApplyTheme(tuiSettings.Theme);
ChatRenderer.SetOutputStyle(tuiSettings.OutputStyle);
using var spectreOutput = new SpectreAgentOutput(
    tuiSettings.SpinnerStyle,
    session.CurrentModel?.Id);
AgentOutput.Current = spectreOutput;

// 10. Build interactive input with command completions
var completionProvider = new CompletionProvider();
completionProvider.Register("/help", "Show available commands");
completionProvider.Register("/models", "List available models");
completionProvider.Register("/model", "Switch to a model");
completionProvider.Register("/providers", "List detected providers");
completionProvider.Register("/provider", "Show current provider");
completionProvider.Register("/clear", "Clear chat history");
completionProvider.Register("/compact", "Force context compaction");
completionProvider.Register("/cost", "Show token usage");
completionProvider.Register("/autorun", "Toggle auto-approve for tools");
completionProvider.Register("/permissions", "Toggle permission checks");
completionProvider.Register("/sessions", "List recent sessions");
completionProvider.Register("/resume", "Resume a previous session");
completionProvider.Register("/name", "Name the current session");
completionProvider.Register("/history", "Show session turn history");
completionProvider.Register("/export", "Export current session to JSON");
completionProvider.Register("/update", "Check for and apply updates");
completionProvider.Register("/instructions", "Show loaded project instructions");
completionProvider.Register("/plugins", "List loaded plugins");
completionProvider.Register("/checkpoint", "List, restore, or clear checkpoints");
completionProvider.Register("/sandbox", "Show or change sandbox mode");
completionProvider.Register("/workflow", "Manage workflows (list|show|export|replay|refine)");
completionProvider.Register("/spinner", "Set progress style (none|minimal|normal|rich|nerdy)");
completionProvider.Register("/mcp", "Manage MCP servers (list|add|remove|enable|disable)");
completionProvider.Register("/context", "Show context window usage");
completionProvider.Register("/copy", "Copy last response to clipboard");
completionProvider.Register("/diff", "Show uncommitted changes");
completionProvider.Register("/init", "Initialize JDAI.md project file");
completionProvider.Register("/plan", "Toggle plan mode (explore only)");
completionProvider.Register("/doctor", "Run self-diagnostics");
completionProvider.Register("/fork", "Fork conversation to new session");
completionProvider.Register("/review", "Review code changes with severity categories");
completionProvider.Register("/security-review", "Run OWASP/CWE-focused security review");
completionProvider.Register("/theme", "Set or list terminal themes");
completionProvider.Register("/vim", "Toggle vim editing mode");
completionProvider.Register("/stats", "Show session and historical usage statistics");
completionProvider.Register("/config", "List/get/set command settings");
completionProvider.Register("/agents", "Manage local agent profiles");
completionProvider.Register("/hooks", "Manage local hook profiles");
completionProvider.Register("/memory", "View/edit JDAI.md project memory");
completionProvider.Register("/output-style", "Set output rendering style");
completionProvider.Register("/quit", "Exit jdai");
completionProvider.Register("/exit", "Exit jdai");
var interactiveInput = new InteractiveInput(completionProvider)
{
    VimModeEnabled = tuiSettings.VimMode,
};

// 10a. Set up slash commands
var workflowCatalog = new FileWorkflowCatalog(Path.Combine(DataDirectories.Root, "workflows"));
var commandRouter = new SlashCommandRouter(
    session, registry, instructions, checkpointStrategy,
    workflowCatalog: workflowCatalog,
    getSpinnerStyle: () => spectreOutput.Style,
    onSpinnerStyleChanged: style => spectreOutput.Style = style,
    getTheme: () => ChatRenderer.CurrentTheme,
    onThemeChanged: ChatRenderer.ApplyTheme,
    getVimMode: () => interactiveInput.VimModeEnabled,
    onVimModeChanged: enabled => interactiveInput.VimModeEnabled = enabled,
    getOutputStyle: () => ChatRenderer.CurrentOutputStyle,
    onOutputStyleChanged: ChatRenderer.SetOutputStyle);

// Hook double-ESC at empty prompt → open history viewer
interactiveInput.OnDoubleEscape += (sender, e) =>
{
    if (session.SessionInfo is { } si && si.Turns.Count > 0)
    {
        var rollbackIndex = HistoryViewer.Show(si);
        if (rollbackIndex is { } idx && session.Store != null)
        {
            session.Store.DeleteTurnsAfterAsync(si.Id, idx).GetAwaiter().GetResult();
            // Remove from in-memory turns too
            while (si.Turns.Count > idx + 1)
                si.Turns.RemoveAt(si.Turns.Count - 1);

            // Rebuild chat history
            session.History.Clear();
            session.History.AddSystemMessage(systemPrompt);
            foreach (var t in si.Turns)
            {
                if (string.Equals(t.Role, "user", StringComparison.Ordinal))
                    session.History.AddUserMessage(t.Content ?? string.Empty);
                else if (string.Equals(t.Role, "assistant", StringComparison.Ordinal))
                    session.History.AddAssistantMessage(t.Content ?? string.Empty);
            }
            ChatRenderer.RenderInfo($"Rolled back to turn {idx}. Context restored.");
        }
    }
};

// 11. Render welcome banner
if (!printMode)
{
    ChatRenderer.RenderBanner(
        selectedModel.DisplayName,
        selectedModel.ProviderName,
        allModels.Count);
}

// 11b. Show update notification if background check completed
var pendingUpdate = await updateCheckTask.ConfigureAwait(false);
if (pendingUpdate is not null && !printMode)
{
    AnsiConsole.MarkupLine(UpdatePrompter.FormatNotification(pendingUpdate));
    AnsiConsole.WriteLine();
}

// Print mode: non-interactive execution
if (printMode)
{
    var query = new System.Text.StringBuilder();
    if (pipedInput != null)
    {
        query.AppendLine(pipedInput);
        query.AppendLine("---");
    }
    if (printQuery != null)
    {
        query.Append(printQuery);
    }
    else if (pipedInput == null)
    {
        Console.Error.WriteLine("Error: --print requires a query argument or piped input.");
        return 1;
    }

    var printAgentLoop = new AgentLoop(session);
    var turnCount = 0;
    string lastResponse = "";
    string? currentPrintMessage = query.ToString();

    while (currentPrintMessage != null)
    {
        turnCount++;
        if (maxTurns.HasValue && turnCount > maxTurns.Value)
        {
            Console.Error.WriteLine($"Error: max turns ({maxTurns.Value}) exceeded.");
            return 1;
        }

        lastResponse = await printAgentLoop.RunTurnAsync(currentPrintMessage).ConfigureAwait(false);
        currentPrintMessage = null;
    }

    if (string.Equals(outputFormat, "json", StringComparison.OrdinalIgnoreCase))
    {
        var jsonResult = new
        {
            result = lastResponse,
            model = selectedModel.Id,
            provider = selectedModel.ProviderName,
            turns = turnCount,
        };
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(jsonResult,
            JsonOptions.Indented));
    }
    else
    {
        Console.Write(lastResponse);
    }

    return 0;
}

// 12. Main interaction loop
var agentLoop = new AgentLoop(session);
var appCts = new CancellationTokenSource();
var lastCtrlCTime = DateTime.MinValue;
var ctrlCWindow = TimeSpan.FromMilliseconds(1500);
TurnInputMonitor? activeTurnMonitor = null;

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // Suppress default termination by default

    // If there's an active turn, cancel it (like single ESC)
    var monitor = Volatile.Read(ref activeTurnMonitor);
    if (monitor != null)
    {
        try
        {
            ChatRenderer.RenderWarning("Cancelling...");
            monitor.CancelTurn();
        }
#pragma warning disable CA1031 // catch broad — best effort during signal handler
        catch { /* monitor may already be disposed */ }
#pragma warning restore CA1031
        return;
    }

    // No active turn — double-tap Ctrl+C to exit
    var now = DateTime.UtcNow;
    if (now - lastCtrlCTime <= ctrlCWindow)
    {
        e.Cancel = false; // Let the OS terminate — input read is blocking
        try { appCts.Cancel(); }
#pragma warning disable CA1031
        catch { /* already disposed/cancelled */ }
#pragma warning restore CA1031
        return;
    }

    lastCtrlCTime = now;
    Console.WriteLine();
    ChatRenderer.RenderWarning("Press Ctrl+C again to exit...");
};

while (!appCts.IsCancellationRequested)
{
    var inputResult = ChatRenderer.ReadInputStructured(interactiveInput);

    if (inputResult is null)
    {
        continue;
    }

    var input = inputResult.AssemblePrompt();

    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    // Show attachment summary if any were pasted
    if (inputResult.Attachments.Count > 0)
    {
        foreach (var att in inputResult.Attachments)
        {
            ChatRenderer.RenderInfo($"  {att.Label}");
        }
    }

    // Slash command? (only check the typed text, not assembled)
    var typedText = inputResult.TypedText;

    // ! bash mode: run command directly, add output to context
    if (typedText.StartsWith('!'))
    {
        var bashCmd = typedText[1..].Trim();
        if (!string.IsNullOrEmpty(bashCmd))
        {
            ChatRenderer.RenderInfo($"$ {bashCmd}");
            try
            {
                var bashResult = await ShellTools.RunCommandAsync(bashCmd).ConfigureAwait(false);
                Console.WriteLine(bashResult);
                session.History.AddUserMessage($"[Shell command: {bashCmd}]\n{bashResult}");
            }
#pragma warning disable CA1031 // non-fatal shell error
            catch (Exception ex)
            {
                ChatRenderer.RenderWarning($"Command failed: {ex.Message}");
            }
#pragma warning restore CA1031
        }
        continue;
    }

    // @ file mentions: inject file contents into the message
    if (input.Contains('@'))
    {
        var expanded = FileMentionExpander.Expand(input);
        if (!string.Equals(expanded, input, StringComparison.Ordinal))
        {
            input = expanded;
        }
    }

    if (inputResult.Attachments.Count == 0 && commandRouter.IsSlashCommand(typedText))
    {
        if (typedText.TrimStart().StartsWith("/quit", StringComparison.OrdinalIgnoreCase) ||
            typedText.TrimStart().StartsWith("/exit", StringComparison.OrdinalIgnoreCase))
        {
            await session.CloseSessionAsync().ConfigureAwait(false);
            ChatRenderer.RenderInfo("Goodbye!");
            break;
        }

        var cmdResult = await commandRouter
            .ExecuteAsync(typedText, appCts.Token)
            .ConfigureAwait(false);

        if (cmdResult != null)
        {
            ChatRenderer.RenderInfo(cmdResult);
        }

        continue;
    }

    // Regular chat message → streaming agent loop with steering
    ChatRenderer.DimInputLine(input);
    string? currentMessage = input;

    while (currentMessage != null && !appCts.IsCancellationRequested)
    {
        using var turnMonitor = new TurnInputMonitor(appCts.Token);
        Volatile.Write(ref activeTurnMonitor, turnMonitor);

        try
        {
            await agentLoop
                .RunTurnStreamingAsync(currentMessage, turnMonitor.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!appCts.IsCancellationRequested)
        {
            ChatRenderer.RenderWarning("Turn cancelled.");
            break;
        }
        finally
        {
            Volatile.Write(ref activeTurnMonitor, null);
        }

        // Check for queued steering message
        currentMessage = turnMonitor.SteeringMessage;
        if (currentMessage != null)
        {
            ChatRenderer.RenderUserMessage(currentMessage);
        }
    }

    // Auto-compaction check
    try
    {
        var estimatedTokens = TokenEstimator.EstimateTokens(session.History);
        if (estimatedTokens > 3000)
        {
            ChatRenderer.RenderInfo("Compacting context...");
            await session.CompactAsync(appCts.Token).ConfigureAwait(false);
        }
    }
    catch (OperationCanceledException) when (!appCts.IsCancellationRequested)
    {
        // Turn was cancelled during compaction — safe to continue
    }

    // Status bar
    spectreOutput.ModelName = session.CurrentModel?.Id;
    ChatRenderer.RenderStatusBar(
        session.CurrentModel?.ProviderName ?? "?",
        session.CurrentModel?.Id ?? "?",
        session.TotalTokens);
}

appCts.Dispose();

if (gatewayHost is not null)
{
    await gatewayHost.StopAsync().ConfigureAwait(false);
    (gatewayHost as IDisposable)?.Dispose();
}

return 0;

/// <summary>Cached JSON serializer options to satisfy CA1869.</summary>
#pragma warning disable MA0047 // file-scoped types in top-level programs have no namespace
file static class JsonOptions
#pragma warning restore MA0047
{
    public static readonly System.Text.Json.JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
    };
}
