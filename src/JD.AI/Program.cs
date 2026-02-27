using JD.AI.Tui;
using JD.AI.Tui.Agent;
using JD.AI.Tui.Agent.Checkpointing;
using JD.AI.Tui.Commands;
using JD.AI.Tui.Providers;
using JD.AI.Tui.Rendering;
using JD.AI.Tui.Tools;
using JD.SemanticKernel.Extensions.Compaction;
using JD.SemanticKernel.Extensions.Skills;
using JD.SemanticKernel.Extensions.Plugins;
using JD.SemanticKernel.Extensions.Hooks;
using Microsoft.SemanticKernel;
using Spectre.Console;

// ──────────────────────────────────────────────────────────
//  jdai — Semantic Kernel TUI Agent
// ──────────────────────────────────────────────────────────

// Parse CLI flags
var skipPermissions = args.Contains("--dangerously-skip-permissions");
var forceUpdateCheck = args.Contains("--force-update-check");
var resumeId = args.SkipWhile(a => !string.Equals(a, "--resume", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();
var isNewSession = args.Contains("--new");

// Fire background update check immediately (non-blocking)
var updateCheckTask = UpdateChecker.CheckAsync(forceUpdateCheck);

AnsiConsole.MarkupLine("[dim]Detecting providers...[/]");

// 1. Build provider registry with all detectors
var detectors = new IProviderDetector[]
{
    new ClaudeCodeDetector(),
    new CopilotDetector(),
    new OllamaDetector(),
};
var registry = new ProviderRegistry(detectors);

// 2. Detect available providers and show status
var providers = await registry.DetectProvidersAsync().ConfigureAwait(false);
foreach (var p in providers)
{
    var icon = p.IsAvailable ? "✅" : "❌";
    AnsiConsole.MarkupLine($"  {icon} [bold]{Markup.Escape(p.Name)}[/]: {Markup.Escape(p.StatusMessage ?? "Unknown")}");
}

var allModels = await registry.GetModelsAsync().ConfigureAwait(false);

if (allModels.Count == 0)
{
    AnsiConsole.MarkupLine("[red]No AI providers available.[/]");
    AnsiConsole.MarkupLine("Ensure at least one of: Claude Code session, GitHub Copilot, or Ollama is running.");
    return 1;
}

// 3. Let user pick a model (or use first available)
ProviderModelInfo selectedModel;
if (allModels.Count == 1)
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
    ChatRenderer.RenderWarning("--dangerously-skip-permissions: ALL tool confirmations disabled.");
}

// Initialize session persistence
var projectPath = Directory.GetCurrentDirectory();
if (!isNewSession)
{
    await session.InitializePersistenceAsync(projectPath, resumeId).ConfigureAwait(false);
    if (resumeId != null && session.SessionInfo != null)
    {
        ChatRenderer.RenderInfo($"Resumed session: {session.SessionInfo.Name ?? session.SessionInfo.Id} ({session.SessionInfo.Turns.Count} turns)");
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
kernel.Plugins.AddFromObject(new MemoryTools(), "memory");

// 7. Load Claude Code skills, plugins, and hooks if available
var skillDirs = new[]
{
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills"),
    Path.Combine(Directory.GetCurrentDirectory(), ".claude", "skills"),
    Path.Combine(Directory.GetCurrentDirectory(), ".jdai", "skills"),
};

foreach (var dir in skillDirs.Where(Directory.Exists))
{
    try
    {
        var builder = Kernel.CreateBuilder();
        JD.SemanticKernel.Extensions.Skills.KernelBuilderExtensions.UseSkills(builder, dir);
        var skillKernel = builder.Build();
        foreach (var plugin in skillKernel.Plugins)
        {
            kernel.Plugins.Add(plugin);
        }

        ChatRenderer.RenderInfo($"  Loaded skills from {dir}");
    }
#pragma warning disable CA1031 // non-fatal
    catch (Exception ex)
    {
        ChatRenderer.RenderWarning($"  Failed to load skills from {dir}: {ex.Message}");
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
            kernel.Plugins.Add(plugin);
        }

        ChatRenderer.RenderInfo($"  Loaded plugins from {dir}");
    }
#pragma warning disable CA1031
    catch (Exception ex)
    {
        ChatRenderer.RenderWarning($"  Failed to load plugins from {dir}: {ex.Message}");
    }
#pragma warning restore CA1031
}

// 8. Add tool confirmation filter
kernel.AutoFunctionInvocationFilters.Add(new ToolConfirmationFilter(session));

// 8b. Load project instructions (JDAI.md, CLAUDE.md, AGENTS.md, etc.)
var instructions = InstructionsLoader.Load();
if (instructions.HasInstructions)
{
    ChatRenderer.RenderInfo($"  Loaded {instructions.Files.Count} instruction file(s)");
}

// 8c. Set up subagent runner and register tools
var subagentRunner = new SubagentRunner(session);
kernel.ImportPluginFromObject(new SubagentTools(subagentRunner), "SubagentTools");

// 8d. Set up checkpoint strategy (stash if git repo, directory otherwise)
ICheckpointStrategy checkpointStrategy = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), ".git"))
    ? new StashCheckpointStrategy()
    : new DirectoryCheckpointStrategy();

// 8e. Register web search tools
kernel.ImportPluginFromObject(new WebSearchTools(), "WebSearchTools");

// 9. Add system prompt
var systemPrompt = """
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

// Append project instructions if found
if (instructions.HasInstructions)
{
    systemPrompt += "\n\n" + instructions.ToSystemPrompt();
}

session.History.AddSystemMessage(systemPrompt);

// 9. Set up slash commands
var commandRouter = new SlashCommandRouter(session, registry, instructions, checkpointStrategy);

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
completionProvider.Register("/checkpoint", "List, restore, or clear checkpoints");
completionProvider.Register("/sandbox", "Show or change sandbox mode");
completionProvider.Register("/quit", "Exit jdai");
completionProvider.Register("/exit", "Exit jdai");
var interactiveInput = new InteractiveInput(completionProvider);

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
ChatRenderer.RenderBanner(
    selectedModel.DisplayName,
    selectedModel.ProviderName,
    allModels.Count);

// 11b. Show update notification if background check completed
var pendingUpdate = await updateCheckTask.ConfigureAwait(false);
if (pendingUpdate is not null)
{
    AnsiConsole.MarkupLine(UpdatePrompter.FormatNotification(pendingUpdate));
    AnsiConsole.WriteLine();
}

// 12. Main interaction loop
var agentLoop = new AgentLoop(session);
var appCts = new CancellationTokenSource();
var lastCtrlCTime = DateTime.MinValue;
var ctrlCWindow = TimeSpan.FromMilliseconds(1500);
TurnInputMonitor? activeTurnMonitor = null;

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // Always suppress default termination

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
    ChatRenderer.RenderUserMessage(input);
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
    ChatRenderer.RenderStatusBar(
        session.CurrentModel?.ProviderName ?? "?",
        session.CurrentModel?.Id ?? "?",
        session.TotalTokens);
}

appCts.Dispose();
return 0;
