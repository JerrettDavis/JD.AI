using JD.AI.Tui.Agent;
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

// 9. Add system prompt
session.History.AddSystemMessage("""
    You are jdai, a helpful AI coding assistant running in a terminal.
    You have access to tools for file operations, code search, shell commands,
    git operations, web fetching, and semantic memory.

    When helping with code tasks:
    - Read relevant files before making changes
    - Use search tools to find code patterns
    - Make minimal, surgical edits
    - Verify changes with builds/tests when appropriate
    - Store important decisions and facts in memory for future recall

    Be concise and direct. Use tools proactively when they'll help answer the question.
    """);

// 9. Set up slash commands
var commandRouter = new SlashCommandRouter(session, registry);

// 10. Render welcome banner
ChatRenderer.RenderBanner(
    selectedModel.DisplayName,
    selectedModel.ProviderName,
    allModels.Count);

// 11. Main interaction loop
var agentLoop = new AgentLoop(session);
using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

while (!cts.IsCancellationRequested)
{
    var input = ChatRenderer.ReadInput();

    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    // Slash command?
    if (commandRouter.IsSlashCommand(input))
    {
        if (input.TrimStart().StartsWith("/quit", StringComparison.OrdinalIgnoreCase) ||
            input.TrimStart().StartsWith("/exit", StringComparison.OrdinalIgnoreCase))
        {
            ChatRenderer.RenderInfo("Goodbye!");
            break;
        }

        var cmdResult = await commandRouter
            .ExecuteAsync(input, cts.Token)
            .ConfigureAwait(false);

        if (cmdResult != null)
        {
            ChatRenderer.RenderInfo(cmdResult);
        }

        continue;
    }

    // Regular chat message → agent loop
    ChatRenderer.RenderUserMessage(input);

    var response = await agentLoop
        .RunTurnAsync(input, cts.Token)
        .ConfigureAwait(false);

    ChatRenderer.RenderAssistantMessage(response);

    // Auto-compaction check
    var estimatedTokens = TokenEstimator.EstimateTokens(session.History);
    if (estimatedTokens > 3000)
    {
        ChatRenderer.RenderInfo("Compacting context...");
        await session.CompactAsync(cts.Token).ConfigureAwait(false);
    }

    // Status bar
    ChatRenderer.RenderStatusBar(
        session.CurrentModel?.ProviderName ?? "?",
        session.CurrentModel?.Id ?? "?",
        session.TotalTokens);
}

return 0;
