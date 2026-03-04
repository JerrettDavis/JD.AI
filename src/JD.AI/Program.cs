using JD.AI;
using JD.AI.Agent;
using JD.AI.Commands;
using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Checkpointing;
using JD.AI.Core.Agents.Orchestration;
using JD.AI.Core.Channels;
using JD.AI.Core.Config;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using JD.AI.Core.LocalModels;
using JD.AI.Core.Mcp;
using JD.AI.Core.Plugins;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Core.Providers.Metadata;
using JD.AI.Core.Providers.ModelSearch;
using JD.AI.Core.Safety;
using JD.AI.Core.Tools;
using JD.AI.Core.Usage;
using JD.AI.Rendering;
using JD.AI.Tools;
using JD.AI.Workflows;
using JD.AI.Workflows.Store;
using JD.SemanticKernel.Extensions.Compaction;
using JD.SemanticKernel.Extensions.Hooks;
using JD.SemanticKernel.Extensions.Plugins;
using JD.SemanticKernel.Extensions.Skills;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
if (firstNonOptionIndex >= 0 && string.Equals(args[firstNonOptionIndex], "plugin", StringComparison.OrdinalIgnoreCase))
{
    var pluginArgs = args.Skip(firstNonOptionIndex + 1).ToArray();
    return await PluginCliHandler.RunAsync(pluginArgs).ConfigureAwait(false);
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

// Permission mode (plan / acceptEdits / dontAsk / normal)
var permissionModeStr = args.SkipWhile(a => !string.Equals(a, "--permission-mode", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();

// Fallback model chain
var fallbackModelStr = args.SkipWhile(a => !string.Equals(a, "--fallback-model", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();
var fallbackModels = fallbackModelStr?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

// Session management flags
var cliSessionId = args.SkipWhile(a => !string.Equals(a, "--session-id", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();
var forkSession = args.Contains("--fork-session");
var noSessionPersistence = args.Contains("--no-session-persistence");

// Budget limit
var maxBudgetStr = args.SkipWhile(a => !string.Equals(a, "--max-budget-usd", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();
decimal? maxBudgetUsd = decimal.TryParse(maxBudgetStr, System.Globalization.CultureInfo.InvariantCulture, out var mb) ? mb : null;

// Git worktree isolation
var useWorktree = args.Contains("-w") || args.Contains("--worktree");

// JSON schema validation for output
var jsonSchemaArg = args.SkipWhile(a => !string.Equals(a, "--json-schema", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();

// Input format (text or stream-json)
var inputFormat = args.SkipWhile(a => !string.Equals(a, "--input-format", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault() ?? "text";

// Debug logging
var debugMode = args.Contains("--debug");
var debugCategories = debugMode
    ? args.SkipWhile(a => !string.Equals(a, "--debug", StringComparison.OrdinalIgnoreCase))
        .Skip(1).FirstOrDefault()
    : null;
// If --debug value starts with '-' it's a different flag, not categories
if (debugCategories != null && debugCategories.StartsWith('-'))
{
    debugCategories = null;
}

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
var credentialStore = new EncryptedFileStore();
var providerConfig = new ProviderConfigurationManager(credentialStore);

var detectors = new IProviderDetector[]
{
    // OAuth / credential-harvesting providers
    new ClaudeCodeDetector(),
    new CopilotDetector(),
    new OpenAICodexDetector(),
    // Local providers
    new OllamaDetector(),
    new FoundryLocalDetector(),
    new LocalModelDetector(),
    // API key providers
    new OpenAIDetector(providerConfig),
    new AzureOpenAIDetector(providerConfig),
    new AnthropicDetector(providerConfig),
    new GoogleGeminiDetector(providerConfig),
    new MistralDetector(providerConfig),
    new AmazonBedrockDetector(providerConfig),
    new HuggingFaceDetector(providerConfig),
    new OpenAICompatibleDetector(providerConfig),
};
var metadataProvider = new ModelMetadataProvider();
var registry = new ProviderRegistry(detectors, metadataProvider);

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

// 3. Let user pick a model (or use CLI flags / per-project defaults / global defaults / first available)
using var configStore = new AtomicConfigStore();
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
else
{
    // Check per-project then global defaults from config store
    var cfgProjectPath = Directory.GetCurrentDirectory();
    var defaultModel = await configStore.GetDefaultModelAsync(cfgProjectPath).ConfigureAwait(false);
    var defaultProvider = await configStore.GetDefaultProviderAsync(cfgProjectPath).ConfigureAwait(false);

    List<ProviderModelInfo>? defaultCandidates = null;

    if (defaultModel is not null)
    {
        defaultCandidates = allModels.Where(m =>
            m.DisplayName.Contains(defaultModel, StringComparison.OrdinalIgnoreCase) ||
            m.Id.Contains(defaultModel, StringComparison.OrdinalIgnoreCase)).ToList();

        if (defaultProvider is not null)
        {
            defaultCandidates = defaultCandidates.Where(m =>
                m.ProviderName.Contains(defaultProvider, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
    else if (defaultProvider is not null)
    {
        defaultCandidates = allModels.Where(m =>
            m.ProviderName.Contains(defaultProvider, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    if (defaultCandidates is { Count: > 0 })
    {
        selectedModel = defaultCandidates[0];
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

// Apply permission mode
if (permissionModeStr != null)
{
    session.PermissionMode = permissionModeStr.ToUpperInvariant() switch
    {
        "PLAN" => JD.AI.Core.Agents.PermissionMode.Plan,
        "ACCEPTEDITS" => JD.AI.Core.Agents.PermissionMode.AcceptEdits,
        "DONTASK" => JD.AI.Core.Agents.PermissionMode.BypassAll,
        "NORMAL" => JD.AI.Core.Agents.PermissionMode.Normal,
        _ => JD.AI.Core.Agents.PermissionMode.Normal,
    };
    if (!printMode)
    {
        ChatRenderer.RenderInfo($"Permission mode: {session.PermissionMode}");
    }
}

// Apply fallback models
if (fallbackModels.Length > 0)
{
    session.FallbackModels = fallbackModels;
    if (!printMode)
    {
        ChatRenderer.RenderInfo($"Fallback models: {string.Join(" → ", fallbackModels)}");
    }
}

// Apply session persistence flag
if (noSessionPersistence)
{
    session.NoSessionPersistence = true;
}

// Apply budget limit
if (maxBudgetUsd.HasValue)
{
    session.MaxBudgetUsd = maxBudgetUsd;
    if (!printMode) ChatRenderer.RenderInfo($"Budget limit: ${maxBudgetUsd:F2}");
}

// Debug logging
if (debugMode)
{
    session.Verbose = true;
    var parsedCategories = JD.AI.Core.Tracing.DebugLogger.ParseCategories(debugCategories);
    JD.AI.Core.Tracing.DebugLogger.Enable(parsedCategories);
    if (!printMode)
    {
        var cats = debugCategories != null ? $" (categories: {debugCategories})" : "";
        ChatRenderer.RenderInfo($"Debug logging enabled{cats}");
    }
}

// Initialize session persistence
var projectPath = Directory.GetCurrentDirectory();

// --worktree / -w: create git worktree for isolated session
JD.AI.Core.Tools.WorktreeManager? worktreeManager = null;
if (useWorktree)
{
    try
    {
        worktreeManager = new JD.AI.Core.Tools.WorktreeManager(projectPath);
        var wtPath = await worktreeManager.CreateAsync().ConfigureAwait(false);
        projectPath = wtPath;
        Directory.SetCurrentDirectory(wtPath);
        if (!printMode)
        {
            ChatRenderer.RenderInfo($"Worktree created: {wtPath}");
            ChatRenderer.RenderInfo($"  Branch: {worktreeManager.BranchName}");
        }
    }
    catch (Exception ex)
    {
        ChatRenderer.RenderWarning($"Failed to create worktree: {ex.Message}");
        worktreeManager = null;
    }
}
if (noSessionPersistence)
{
    // --no-session-persistence: skip all session I/O
    if (!printMode) ChatRenderer.RenderInfo("Session persistence disabled.");
}
else if (!isNewSession)
{
    // Use explicit --session-id if provided
    if (cliSessionId != null)
    {
        resumeId = cliSessionId;
    }

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
        // Restore last-used model from session's model switch history
        var lastSwitch = session.SessionInfo.ModelSwitchHistory.LastOrDefault();
        if (lastSwitch != null)
        {
            var restored = allModels.FirstOrDefault(m =>
                string.Equals(m.Id, lastSwitch.ModelId, StringComparison.Ordinal) &&
                string.Equals(m.ProviderName, lastSwitch.ProviderName, StringComparison.Ordinal));
            if (restored != null)
            {
                selectedModel = restored;
                kernel = registry.BuildKernel(selectedModel);
                session = new AgentSession(registry, kernel, selectedModel)
                {
                    Store = session.Store,
                    SessionInfo = session.SessionInfo,
                    SkipPermissions = session.SkipPermissions,
                    Verbose = session.Verbose,
                    PermissionMode = session.PermissionMode,
                    FallbackModels = session.FallbackModels,
                    NoSessionPersistence = session.NoSessionPersistence,
                };
                // Re-restore history into the new session's ChatHistory
                foreach (var turn in session.SessionInfo.Turns)
                {
                    if (string.Equals(turn.Role, "user", StringComparison.Ordinal))
                        session.History.AddUserMessage(turn.Content ?? string.Empty);
                    else if (string.Equals(turn.Role, "assistant", StringComparison.Ordinal))
                        session.History.AddAssistantMessage(turn.Content ?? string.Empty);
                }
                if (!printMode) ChatRenderer.RenderInfo($"Restored model: [{restored.ProviderName}] {restored.DisplayName}");
            }
        }
        else if (session.SessionInfo.ModelId != null && session.SessionInfo.ProviderName != null)
        {
            // Fallback: use the session's original model_id/provider_name
            var restored = allModels.FirstOrDefault(m =>
                string.Equals(m.Id, session.SessionInfo.ModelId, StringComparison.Ordinal) &&
                string.Equals(m.ProviderName, session.SessionInfo.ProviderName, StringComparison.Ordinal));
            if (restored != null && !string.Equals(restored.Id, selectedModel.Id, StringComparison.Ordinal))
            {
                selectedModel = restored;
                kernel = registry.BuildKernel(selectedModel);
                session = new AgentSession(registry, kernel, selectedModel)
                {
                    Store = session.Store,
                    SessionInfo = session.SessionInfo,
                    SkipPermissions = session.SkipPermissions,
                    Verbose = session.Verbose,
                    PermissionMode = session.PermissionMode,
                    FallbackModels = session.FallbackModels,
                    NoSessionPersistence = session.NoSessionPersistence,
                };
                foreach (var turn in session.SessionInfo.Turns)
                {
                    if (string.Equals(turn.Role, "user", StringComparison.Ordinal))
                        session.History.AddUserMessage(turn.Content ?? string.Empty);
                    else if (string.Equals(turn.Role, "assistant", StringComparison.Ordinal))
                        session.History.AddAssistantMessage(turn.Content ?? string.Empty);
                }
                if (!printMode) ChatRenderer.RenderInfo($"Restored model: [{restored.ProviderName}] {restored.DisplayName}");
            }
        }
        if (!printMode) ChatRenderer.RenderInfo($"Resumed session: {session.SessionInfo.Name ?? session.SessionInfo.Id} ({session.SessionInfo.Turns.Count} turns)");

        // --fork-session: fork from the resumed session
        if (forkSession)
        {
            await session.ForkSessionAsync("CLI fork").ConfigureAwait(false);
            if (!printMode) ChatRenderer.RenderInfo("Forked session — changes diverge from here.");
        }
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
kernel.Plugins.AddFromType<GitHubTools>("github");
kernel.Plugins.AddFromType<WebTools>("web");
kernel.Plugins.AddFromType<BrowserTools>("browser");
kernel.Plugins.AddFromType<ThinkTools>("think");
kernel.Plugins.AddFromType<EnvironmentTools>("environment");
kernel.Plugins.AddFromType<NotebookTools>("notebook");
kernel.Plugins.AddFromType<ClipboardTools>("clipboard");
kernel.Plugins.AddFromType<DiffTools>("diff");
kernel.Plugins.AddFromType<BatchEditTools>("batchEdit");
kernel.Plugins.AddFromType<MultimodalTools>("multimodal");
kernel.Plugins.AddFromType<ParityDocsTools>("parityDocs");
kernel.Plugins.AddFromType<MigrationTools>("migration");
kernel.Plugins.AddFromType<SkillParityTools>("skillParity");
kernel.Plugins.AddFromObject(new MemoryTools(), "memory");
kernel.Plugins.AddFromObject(new TaskTools(), "tasks");
var usageTools = new UsageTools();
usageTools.SetModel(selectedModel);
kernel.Plugins.AddFromObject(usageTools, "usage");
var capabilityTools = new CapabilityTools(kernel);
kernel.Plugins.AddFromObject(capabilityTools, "capabilities");
kernel.Plugins.AddFromObject(new BenchmarkTools(kernel), "benchmark");
kernel.Plugins.AddFromObject(
    new QuestionTools(req => QuestionnaireSession.Run(req)), "questions");
kernel.Plugins.AddFromObject(new SessionOrchestrationTools(session), "sessions");
kernel.Plugins.AddFromObject(new SchedulerTools(), "scheduler");
kernel.Plugins.AddFromObject(
    new GatewayOpsTools(Environment.GetEnvironmentVariable("JDAI_GATEWAY_URL")), "gateway");

// Channel ops tools — only registered when a channel registry is available
var channelRegistry = new ChannelRegistry();
kernel.Plugins.AddFromObject(new ChannelOpsTools(channelRegistry), "channels");

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

// 7b. Load installed SDK plugins from the JD.AI plugin registry
var pluginLoader = new JD.AI.Core.Plugins.PluginLoader(
    NullLogger<JD.AI.Core.Plugins.PluginLoader>.Instance);
var pluginRegistry = new PluginRegistryStore();
var pluginInstaller = new PluginInstaller(
    new HttpClient(),
    NullLogger<PluginInstaller>.Instance);
var pluginContextFactory = new DelegatePluginContextFactory(
    () => new TerminalPluginContext(kernel));
var pluginManager = new PluginLifecycleManager(
    pluginInstaller,
    pluginRegistry,
    pluginLoader,
    pluginContextFactory,
    NullLogger<PluginLifecycleManager>.Instance);
await pluginManager.LoadEnabledAsync().ConfigureAwait(false);

// 8. Load governance policies, audit, and budget
var policies = PolicyLoader.Load(projectPath);
IPolicyEvaluator? policyEvaluator = null;
if (policies.Count > 0)
{
    var resolvedSpec = PolicyResolver.Resolve(policies);
    policyEvaluator = new PolicyEvaluator(resolvedSpec);
    if (!printMode) ChatRenderer.RenderInfo($"  Loaded {policies.Count} governance policy file(s)");
}

var auditSinks = new List<IAuditSink>();
var auditDir = Path.Combine(DataDirectories.Root, "audit");
using var fileAuditSink = new FileAuditSink(auditDir);
auditSinks.Add(fileAuditSink);

// If policies define additional audit sinks (ES, webhook), add them here
var auditPolicy = policies
    .SelectMany(p => p.Spec.Audit is { } a ? [a] : Array.Empty<AuditPolicy>())
    .FirstOrDefault();
if (auditPolicy is not null)
{
    if (!string.IsNullOrWhiteSpace(auditPolicy.Endpoint) && !string.IsNullOrWhiteSpace(auditPolicy.Index))
        auditSinks.Add(new ElasticsearchAuditSink(
            new HttpClient(), auditPolicy.Endpoint, auditPolicy.Index, auditPolicy.Token));
    if (!string.IsNullOrWhiteSpace(auditPolicy.Url))
        auditSinks.Add(new WebhookAuditSink(new HttpClient(), auditPolicy.Url));
}

var auditService = new AuditService(auditSinks);
session.AuditService = auditService;

using var budgetTracker = new BudgetTracker();

// Construct budget policy from CLI + governance
BudgetPolicy? budgetPolicy = null;
if (maxBudgetUsd.HasValue)
{
    budgetPolicy = new BudgetPolicy { MaxSessionUsd = maxBudgetUsd };
}
// Merge with governance budget policy if present
var governanceBudget = policies
    .SelectMany(p => p.Spec.Budget is { } b ? [b] : Array.Empty<BudgetPolicy>())
    .FirstOrDefault();
if (governanceBudget is not null)
{
    budgetPolicy ??= new BudgetPolicy();
    budgetPolicy.MaxDailyUsd ??= governanceBudget.MaxDailyUsd;
    budgetPolicy.MaxMonthlyUsd ??= governanceBudget.MaxMonthlyUsd;
    budgetPolicy.MaxSessionUsd ??= governanceBudget.MaxSessionUsd;
}

// 8a. Create circuit breaker for tool loop detection
CircuitBreaker? circuitBreaker = null;
{
    var cbPolicy = policies
        .SelectMany(p => p.Spec.CircuitBreaker is { } cb ? [cb] : Array.Empty<CircuitBreakerPolicy>())
        .FirstOrDefault();

    var detector = new ToolLoopDetector(
        windowSize: cbPolicy?.WindowSize ?? 50,
        repetitionWarningThreshold: cbPolicy?.RepetitionWarningThreshold ?? 3,
        repetitionHardStopThreshold: cbPolicy?.RepetitionHardStopThreshold ?? 5,
        pingPongThreshold: cbPolicy?.PingPongThreshold ?? 4);

    circuitBreaker = new CircuitBreaker(
        detector,
        cooldownPeriod: TimeSpan.FromSeconds(cbPolicy?.CooldownSeconds ?? 30),
        hardenedMode: cbPolicy?.Hardened ?? false);
}

// 8b. Add tool confirmation filter with governance + circuit breaker
kernel.AutoFunctionInvocationFilters.Add(
    new ToolConfirmationFilter(session, policyEvaluator, auditService, circuitBreaker));

// 8b2. Register policy tools (needs policyEvaluator + auditService from step 8)
kernel.Plugins.AddFromObject(new PolicyTools(policyEvaluator, auditService), "policy");

// 8c. Load project instructions (JDAI.md, CLAUDE.md, AGENTS.md, etc.)
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
session.PromptCachingEnabled = tuiSettings.PromptCacheEnabled;
session.PromptCacheTtl = tuiSettings.PromptCacheTtl;
ChatRenderer.ApplyTheme(tuiSettings.Theme);
ChatRenderer.SetOutputStyle(tuiSettings.OutputStyle);
using var spectreOutput = new SpectreAgentOutput(
    tuiSettings.SpinnerStyle,
    session.CurrentModel?.Id);
AgentOutput.Current = spectreOutput;

// 10. Build interactive input with command completions
var completionProvider = new CompletionProvider();
SlashCommandCatalog.RegisterCompletions(completionProvider);
var interactiveInput = new InteractiveInput(completionProvider)
{
    VimModeEnabled = tuiSettings.VimMode,
};

// 10a. Set up workflow store and slash commands
var workflowStoreUrl = Environment.GetEnvironmentVariable("JDAI_WORKFLOW_STORE_URL");
IWorkflowStore workflowStore = !string.IsNullOrWhiteSpace(workflowStoreUrl)
    ? new GitWorkflowStore(workflowStoreUrl)
    : new FileWorkflowStore(Path.Combine(DataDirectories.Root, "workflow-store"));

var workflowCatalog = new FileWorkflowCatalog(Path.Combine(DataDirectories.Root, "workflows"));
using var searchHttp = new HttpClient();
var modelSearchAggregator = new ModelSearchAggregator(new IRemoteModelSearch[]
{
    new OllamaModelSearch(searchHttp),
    new HuggingFaceModelSearch(searchHttp),
    new FoundryLocalModelSearch(),
});
// Centralized usage metering
var usageMeter = new SqliteUsageMeter(DataDirectories.UsageDb);
await usageMeter.InitializeAsync();
session.UsageMeter = usageMeter;

var commandRouter = new SlashCommandRouter(
    session, registry, instructions, checkpointStrategy,
    pluginLoader: pluginLoader,
    pluginManager: pluginManager,
    workflowCatalog: workflowCatalog,
    workflowStore: workflowStore,
    getSpinnerStyle: () => spectreOutput.Style,
    onSpinnerStyleChanged: style => spectreOutput.Style = style,
    providerConfig: providerConfig,
    configStore: configStore,
    modelSearchAggregator: modelSearchAggregator,
    metadataProvider: metadataProvider,
    getTheme: () => ChatRenderer.CurrentTheme,
    onThemeChanged: ChatRenderer.ApplyTheme,
    getVimMode: () => interactiveInput.VimModeEnabled,
    onVimModeChanged: enabled => interactiveInput.VimModeEnabled = enabled,
    getOutputStyle: () => ChatRenderer.CurrentOutputStyle,
    onOutputStyleChanged: ChatRenderer.SetOutputStyle,
    usageMeter: usageMeter,
    policyEvaluator: policyEvaluator);

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

// Hook Shift+Tab → cycle permission mode (plan mode toggle)
interactiveInput.OnTogglePlanMode += (_, _) =>
{
    session.PermissionMode = session.PermissionMode switch
    {
        JD.AI.Core.Agents.PermissionMode.Normal => JD.AI.Core.Agents.PermissionMode.Plan,
        JD.AI.Core.Agents.PermissionMode.Plan => JD.AI.Core.Agents.PermissionMode.AcceptEdits,
        JD.AI.Core.Agents.PermissionMode.AcceptEdits => JD.AI.Core.Agents.PermissionMode.Normal,
        _ => JD.AI.Core.Agents.PermissionMode.Normal,
    };
    ChatRenderer.RenderInfo($"Permission mode: {session.PermissionMode}");
};

// Hook Alt+T → toggle extended thinking (future feature placeholder)
interactiveInput.OnToggleExtendedThinking += (_, _) =>
{
    ChatRenderer.RenderInfo("Extended thinking is not yet available for this model.");
};

// Hook Alt+P → cycle through recent models
interactiveInput.OnCycleModel += (_, _) =>
{
    ChatRenderer.RenderInfo("Use /model to switch models interactively.");
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

// 11c. System prompt budget check
if (!printMode)
{
    var systemPromptTokens = session.SystemPromptTokens;
    var contextWindow = selectedModel.ContextWindowTokens;
    var budgetPercent = tuiSettings.SystemPromptBudgetPercent;
    var budgetTokens = (int)(contextWindow * (budgetPercent / 100.0));
    var compactionMode = tuiSettings.SystemPromptCompaction;

    var shouldCompact = compactionMode == SystemPromptCompaction.Always
        || (compactionMode == SystemPromptCompaction.Auto && systemPromptTokens > budgetTokens);

    if (shouldCompact)
    {
        ChatRenderer.RenderInfo("Compacting system prompt...");
        var newSize = await session.CompactSystemPromptAsync(budgetTokens).ConfigureAwait(false);
        ChatRenderer.RenderInfo($"System prompt compacted: {systemPromptTokens:N0} → {newSize:N0} tokens.");
    }
    else if (systemPromptTokens > budgetTokens)
    {
        ChatRenderer.RenderSystemPromptWarning(systemPromptTokens, budgetTokens, budgetPercent, contextWindow);
    }
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

    // JSON schema validation
    if (jsonSchemaArg is not null)
    {
        try
        {
            var schema = JD.AI.Core.Agents.OutputSchemaValidator.LoadSchema(jsonSchemaArg);
            var errors = JD.AI.Core.Agents.OutputSchemaValidator.Validate(lastResponse, schema);
            if (errors.Count > 0)
            {
                // Retry once with schema feedback
                var retryPrompt = JD.AI.Core.Agents.OutputSchemaValidator.GenerateRetryPrompt(errors, schema);
                lastResponse = await printAgentLoop.RunTurnAsync(retryPrompt).ConfigureAwait(false);
                errors = JD.AI.Core.Agents.OutputSchemaValidator.Validate(lastResponse, schema);
                if (errors.Count > 0)
                {
                    Console.Error.WriteLine("Schema validation failed:");
                    foreach (var err in errors)
                        Console.Error.WriteLine($"  - {err}");
                    return JD.AI.Core.Agents.OutputSchemaValidator.SchemaValidationExitCode;
                }

                // Output the corrected response
                Console.Write(lastResponse);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Schema error: {ex.Message}");
            return JD.AI.Core.Agents.OutputSchemaValidator.SchemaValidationExitCode;
        }
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
            AgentOutput.Current.RenderWarning("Cancelling...");
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
    AgentOutput.Current.RenderWarning("Press Ctrl+C again to exit...");
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
        // Budget enforcement: check before each turn
        if (budgetPolicy is not null)
        {
            // Check session-level budget
            if (budgetPolicy.MaxSessionUsd.HasValue &&
                session.SessionSpendUsd >= budgetPolicy.MaxSessionUsd.Value)
            {
                ChatRenderer.RenderWarning(
                    $"Budget limit (${budgetPolicy.MaxSessionUsd:F2}) reached — spent ${session.SessionSpendUsd:F2}.");
                if (printMode) { Environment.ExitCode = 2; }
                break;
            }

            // Check daily/monthly budgets
            if (!await budgetTracker.IsWithinBudgetAsync(budgetPolicy, appCts.Token).ConfigureAwait(false))
            {
                var status = await budgetTracker.GetStatusAsync(appCts.Token).ConfigureAwait(false);
                ChatRenderer.RenderWarning(
                    $"Budget exceeded — daily: ${status.TodayUsd:F2}, monthly: ${status.MonthUsd:F2}.");
                if (printMode) { Environment.ExitCode = 2; }
                break;
            }
        }

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

        // Estimate cost for the turn and track session spend
        if (budgetPolicy is not null && session.CurrentModel is not null)
        {
            // Rough estimate: ~$0.003/1k input, ~$0.015/1k output for mid-tier models
            // Local models (Ollama, Foundry, LlamaSharp) are free
            var provider = session.CurrentModel.ProviderName;
            var isLocal = string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(provider, "Foundry Local", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(provider, "LlamaSharp", StringComparison.OrdinalIgnoreCase);

            if (!isLocal)
            {
                var lastTurn = session.SessionInfo?.Turns.LastOrDefault();
                var tokensOut = lastTurn?.TokensOut ?? 0;
                var estimatedCost = tokensOut * 0.015m / 1000m; // conservative estimate
                session.SessionSpendUsd += estimatedCost;

                await budgetTracker.RecordSpendAsync(estimatedCost, provider, appCts.Token)
                    .ConfigureAwait(false);
            }
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

// Clean up worktree if used
if (worktreeManager is not null)
{
    ChatRenderer.RenderInfo("Cleaning up worktree...");
    await worktreeManager.DisposeAsync().ConfigureAwait(false);
}

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
