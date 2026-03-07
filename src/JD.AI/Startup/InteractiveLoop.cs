using JD.AI.Agent;
using JD.AI.Commands;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Core.Governance;
using JD.AI.Core.Plugins;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Core.Providers.Metadata;
using JD.AI.Core.Providers.ModelSearch;
using JD.AI.Core.Skills;
using JD.AI.Core.Usage;
using JD.AI.Rendering;
using JD.AI.Workflows;
using JD.AI.Workflows.Store;
using JD.SemanticKernel.Extensions.Compaction;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Spectre.Console;

namespace JD.AI.Startup;

/// <summary>
/// Encapsulates the main TUI interaction loop: input handling, slash commands,
/// agent loop, budget enforcement, auto-compaction, and event hooks.
/// Extracted from Program.cs lines 494-996.
/// </summary>
internal sealed class InteractiveLoop
{
    private readonly AgentSession _session;
    private readonly CliOptions _opts;
    private readonly ProviderModelInfo _selectedModel;
    private readonly IReadOnlyList<ProviderModelInfo> _allModels;
    private readonly Kernel _kernel;
    private readonly ProviderRegistry _registry;
    private readonly ProviderConfigurationManager _providerConfig;
    private readonly AtomicConfigStore _configStore;
    private readonly ModelMetadataProvider _metadataProvider;
    private readonly GovernanceSetup _governance;
    private readonly SkillLifecycleManager _skillLifecycleManager;
    private readonly Action<bool> _refreshSkills;
    private readonly string _systemPrompt;
    private readonly PluginLoader _pluginLoader;
    private readonly IPluginLifecycleManager? _pluginManager;
    private readonly ICostEstimator _costEstimator;

    public InteractiveLoop(
        AgentSession session,
        CliOptions opts,
        ProviderModelInfo selectedModel,
        IReadOnlyList<ProviderModelInfo> allModels,
        Kernel kernel,
        ProviderRegistry registry,
        ProviderConfigurationManager providerConfig,
        AtomicConfigStore configStore,
        ModelMetadataProvider metadataProvider,
        GovernanceSetup governance,
        SkillLifecycleManager skillLifecycleManager,
        Action<bool> refreshSkills,
        string systemPrompt,
        PluginLoader pluginLoader,
        IPluginLifecycleManager? pluginManager,
        ICostEstimator? costEstimator = null)
    {
        _session = session;
        _opts = opts;
        _selectedModel = selectedModel;
        _allModels = allModels;
        _kernel = kernel;
        _registry = registry;
        _providerConfig = providerConfig;
        _configStore = configStore;
        _metadataProvider = metadataProvider;
        _governance = governance;
        _skillLifecycleManager = skillLifecycleManager;
        _refreshSkills = refreshSkills;
        _systemPrompt = systemPrompt;
        _pluginLoader = pluginLoader;
        _pluginManager = pluginManager;
        _costEstimator = costEstimator ?? new DefaultCostEstimator();
    }

    public async Task<int> RunAsync()
    {
        // Wire up TUI settings
        var tuiSettings = TuiSettings.Load();
        _session.PromptCachingEnabled = tuiSettings.PromptCacheEnabled;
        _session.PromptCacheTtl = tuiSettings.PromptCacheTtl;
        ChatRenderer.ApplyTheme(tuiSettings.Theme);
        ChatRenderer.SetOutputStyle(tuiSettings.OutputStyle);

        using var spectreOutput = new SpectreAgentOutput(
            tuiSettings.SpinnerStyle,
            _session.CurrentModel?.Id);
        AgentOutput.Current = spectreOutput;

        // Input with command completions
        var completionProvider = new CompletionProvider();
        SlashCommandCatalog.RegisterCompletions(completionProvider);
        var interactiveInput = new InteractiveInput(completionProvider)
        {
            VimModeEnabled = tuiSettings.VimMode,
        };

        // Workflow store and slash command router
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

        var usageMeter = new SqliteUsageMeter(DataDirectories.UsageDb);
        await usageMeter.InitializeAsync().ConfigureAwait(false);
        _session.UsageMeter = usageMeter;

        string GetSkillsStatus() => _skillLifecycleManager.FormatStatusReport();
        string ReloadSkills()
        {
            _refreshSkills(true);
            return _skillLifecycleManager.FormatStatusReport();
        }

        var commandRouter = new SlashCommandRouter(
            _session, _registry, _governance.Instructions, _governance.CheckpointStrategy,
            pluginLoader: _pluginLoader,
            pluginManager: _pluginManager,
            workflowCatalog: workflowCatalog,
            workflowStore: workflowStore,
            getSpinnerStyle: () => spectreOutput.Style,
            onSpinnerStyleChanged: style => spectreOutput.Style = style,
            providerConfig: _providerConfig,
            configStore: _configStore,
            modelSearchAggregator: modelSearchAggregator,
            metadataProvider: _metadataProvider,
            getTheme: () => ChatRenderer.CurrentTheme,
            onThemeChanged: ChatRenderer.ApplyTheme,
            getVimMode: () => interactiveInput.VimModeEnabled,
            onVimModeChanged: enabled => interactiveInput.VimModeEnabled = enabled,
            getOutputStyle: () => ChatRenderer.CurrentOutputStyle,
            onOutputStyleChanged: ChatRenderer.SetOutputStyle,
            usageMeter: usageMeter,
            policyEvaluator: _governance.PolicyEvaluator,
            getSkillsStatus: GetSkillsStatus,
            reloadSkills: ReloadSkills);

        // Event hooks
        WireEventHooks(interactiveInput, _session, _systemPrompt);

        // Welcome banner
        var welcomeSettings = WelcomePanelSettings.Normalize(tuiSettings.Welcome);
        var indicators = welcomeSettings.ShowServices
            ? await WelcomeServiceStatusProbe
                .ProbeSafeAsync(_opts)
                .ConfigureAwait(false)
            : [];

        var shouldFetchMotd = welcomeSettings.ShowMotd
            && !string.IsNullOrWhiteSpace(welcomeSettings.MotdUrl);
        var motd = shouldFetchMotd
            ? await WelcomeMotdProvider
                .TryGetMotdAsync(welcomeSettings)
                .ConfigureAwait(false)
            : null;
        var bannerDetails = new WelcomeBannerDetails(
            WorkingDirectory: Directory.GetCurrentDirectory(),
            Version: WelcomeRuntimeInfo.GetDisplayVersion(),
            Motd: motd);

        ChatRenderer.RenderBanner(
            _selectedModel.DisplayName,
            _selectedModel.ProviderName,
            _allModels.Count,
            indicators,
            bannerDetails,
            welcomeSettings);

        // System prompt budget check
        await CheckSystemPromptBudgetAsync(tuiSettings).ConfigureAwait(false);

        // Main loop
        return await RunMainLoopAsync(
            commandRouter, interactiveInput, spectreOutput).ConfigureAwait(false);
    }

    private static void WireEventHooks(
        InteractiveInput interactiveInput,
        AgentSession session,
        string systemPrompt)
    {
        interactiveInput.OnDoubleEscape += (_, _) =>
        {
            if (session.SessionInfo is not { } si || si.Turns.Count <= 0) return;

            var rollbackIndex = HistoryViewer.Show(si);
            if (rollbackIndex is not { } idx || session.Store == null) return;

            session.Store.DeleteTurnsAfterAsync(si.Id, idx).GetAwaiter().GetResult();
            while (si.Turns.Count > idx + 1)
                si.Turns.RemoveAt(si.Turns.Count - 1);

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
        };

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

        interactiveInput.OnToggleExtendedThinking += (_, _) =>
        {
            ChatRenderer.RenderInfo("Extended thinking is not yet available for this model.");
        };

        interactiveInput.OnCycleModel += (_, _) =>
        {
            ChatRenderer.RenderInfo("Use /model to switch models interactively.");
        };
    }

    private async Task CheckSystemPromptBudgetAsync(TuiSettings tuiSettings)
    {
        var systemPromptTokens = _session.SystemPromptTokens;
        var contextWindow = _selectedModel.ContextWindowTokens;
        var budgetPercent = tuiSettings.SystemPromptBudgetPercent;
        var budgetTokens = (int)(contextWindow * (budgetPercent / 100.0));
        var compactionMode = tuiSettings.SystemPromptCompaction;

        var shouldCompact = compactionMode == SystemPromptCompaction.Always
            || (compactionMode == SystemPromptCompaction.Auto && systemPromptTokens > budgetTokens);

        if (shouldCompact)
        {
            ChatRenderer.RenderInfo("Compacting system prompt...");
            var newSize = await _session.CompactSystemPromptAsync(budgetTokens).ConfigureAwait(false);
            ChatRenderer.RenderInfo($"System prompt compacted: {systemPromptTokens:N0} → {newSize:N0} tokens.");
        }
        else if (systemPromptTokens > budgetTokens)
        {
            ChatRenderer.RenderSystemPromptWarning(systemPromptTokens, budgetTokens, budgetPercent, contextWindow);
        }
    }

    private async Task<int> RunMainLoopAsync(
        SlashCommandRouter commandRouter,
        InteractiveInput interactiveInput,
        SpectreAgentOutput spectreOutput)
    {
        var agentLoop = new AgentLoop(_session);
        var appCts = new CancellationTokenSource();
        var lastCtrlCTime = DateTime.MinValue;
        var ctrlCWindow = TimeSpan.FromMilliseconds(1500);
        var monitorBox = new TurnMonitorBox();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            var monitor = monitorBox.Value;
            if (monitor != null)
            {
                try
                {
                    AgentOutput.Current.RenderWarning("Cancelling...");
                    monitor.CancelTurn();
                }
#pragma warning disable CA1031
                catch { /* monitor may already be disposed */ }
#pragma warning restore CA1031
                return;
            }

            var now = DateTime.UtcNow;
            if (now - lastCtrlCTime <= ctrlCWindow)
            {
                e.Cancel = false;
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
            _refreshSkills(true);

            var inputResult = ChatRenderer.ReadInputStructured(interactiveInput);
            if (inputResult is null) continue;

            var input = inputResult.AssemblePrompt();
            if (string.IsNullOrWhiteSpace(input)) continue;

            if (inputResult.Attachments.Count > 0)
            {
                foreach (var att in inputResult.Attachments)
                    ChatRenderer.RenderInfo($"  {att.Label}");
            }

            var typedText = inputResult.TypedText;

            // ! bash mode
            if (typedText.StartsWith('!'))
            {
                var bashCmd = typedText[1..].Trim();
                if (!string.IsNullOrEmpty(bashCmd))
                {
                    ChatRenderer.RenderInfo($"$ {bashCmd}");
                    try
                    {
                        var bashResult = await JD.AI.Core.Tools.ShellTools.RunCommandAsync(bashCmd).ConfigureAwait(false);
                        Console.WriteLine(bashResult);
                        _session.History.AddUserMessage($"[Shell command: {bashCmd}]\n{bashResult}");
                    }
#pragma warning disable CA1031
                    catch (Exception ex)
                    {
                        ChatRenderer.RenderWarning($"Command failed: {ex.Message}");
                    }
#pragma warning restore CA1031
                }

                continue;
            }

            // @ file mentions
            if (input.Contains('@'))
            {
                var expanded = FileMentionExpander.Expand(input);
                if (!string.Equals(expanded, input, StringComparison.Ordinal))
                    input = expanded;
            }

            // Slash commands
            if (inputResult.Attachments.Count == 0 && commandRouter.IsSlashCommand(typedText))
            {
                if (typedText.TrimStart().StartsWith("/quit", StringComparison.OrdinalIgnoreCase) ||
                    typedText.TrimStart().StartsWith("/exit", StringComparison.OrdinalIgnoreCase))
                {
                    await _session.CloseSessionAsync().ConfigureAwait(false);
                    ChatRenderer.RenderInfo("Goodbye!");
                    break;
                }

                var cmdResult = await commandRouter
                    .ExecuteAsync(typedText, appCts.Token)
                    .ConfigureAwait(false);
                if (cmdResult != null)
                    ChatRenderer.RenderInfo(cmdResult);
                continue;
            }

            // Regular chat message
            ChatRenderer.DimInputLine(input);
            await RunAgentTurnLoopAsync(
                agentLoop, input, appCts, spectreOutput, monitorBox).ConfigureAwait(false);
        }

        appCts.Dispose();
        return 0;
    }

    private async Task RunAgentTurnLoopAsync(
        AgentLoop agentLoop,
        string input,
        CancellationTokenSource appCts,
        SpectreAgentOutput spectreOutput,
        TurnMonitorBox monitorBox)
    {
        string? currentMessage = input;
        var budgetPolicy = _governance.BudgetPolicy;

        while (currentMessage != null && !appCts.IsCancellationRequested)
        {
            // Budget enforcement
            if (budgetPolicy is not null)
            {
                if (budgetPolicy.MaxSessionUsd.HasValue &&
                    _session.SessionSpendUsd >= budgetPolicy.MaxSessionUsd.Value)
                {
                    ChatRenderer.RenderWarning(
                        $"Budget limit (${budgetPolicy.MaxSessionUsd:F2}) reached — spent ${_session.SessionSpendUsd:F2}.");
                    break;
                }

                if (!await _governance.BudgetTracker.IsWithinBudgetAsync(budgetPolicy, appCts.Token).ConfigureAwait(false))
                {
                    var status = await _governance.BudgetTracker.GetStatusAsync(appCts.Token).ConfigureAwait(false);
                    ChatRenderer.RenderWarning(
                        $"Budget exceeded — daily: ${status.TodayUsd:F2}, monthly: ${status.MonthUsd:F2}.");
                    break;
                }
            }

            using var turnMonitor = new TurnInputMonitor(appCts.Token);
            monitorBox.Value = turnMonitor;

            try
            {
                using (_skillLifecycleManager.BeginRunScope())
                {
                    await agentLoop
                        .RunTurnStreamingAsync(currentMessage, turnMonitor.Token)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!appCts.IsCancellationRequested)
            {
                ChatRenderer.RenderWarning("Turn cancelled.");
                break;
            }
            finally
            {
                monitorBox.Value = null;
            }

            // Cost estimation
            if (budgetPolicy is not null && _session.CurrentModel is not null)
            {
                var lastTurn = _session.SessionInfo?.Turns.LastOrDefault();
                if (lastTurn is not null)
                {
                    var estimatedCost = _costEstimator.EstimateTurnCostUsd(
                        _session.CurrentModel,
                        lastTurn.TokensIn,
                        lastTurn.TokensOut);

                    if (estimatedCost > 0m)
                    {
                        _session.SessionSpendUsd += estimatedCost;
                        await _governance.BudgetTracker.RecordSpendAsync(
                                estimatedCost,
                                _session.CurrentModel.ProviderName,
                                appCts.Token)
                            .ConfigureAwait(false);
                    }
                }
            }

            currentMessage = turnMonitor.SteeringMessage;
            if (currentMessage != null)
                ChatRenderer.RenderUserMessage(currentMessage);
        }

        // Auto-compaction
        try
        {
            var freshSettings = TuiSettings.Load();
            if (freshSettings.AutoCompact && freshSettings.CompactThresholdPercent > 0)
            {
                var estimatedTokens = TokenEstimator.EstimateTokens(_session.History);
                var contextWindow = (_session.CurrentModel ?? _selectedModel).ContextWindowTokens;
                var threshold = contextWindow > 0
                    ? (long)(contextWindow * (freshSettings.CompactThresholdPercent / 100.0))
                    : 3000L;
                if (estimatedTokens > threshold)
                {
                    ChatRenderer.RenderInfo("Compacting context...");
                    await _session.CompactAsync(appCts.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (!appCts.IsCancellationRequested)
        {
            // Safe to continue
        }

        // Status bar
        spectreOutput.ModelName = _session.CurrentModel?.Id;
        ChatRenderer.RenderStatusBar(
            _session.CurrentModel?.ProviderName ?? "?",
            _session.CurrentModel?.Id ?? "?",
            _session.TotalTokens);
    }
}

/// <summary>
/// Thread-safe mutable wrapper for <see cref="TurnInputMonitor"/>
/// to replace Volatile.Read/Write with ref locals (incompatible with async).
/// </summary>
internal sealed class TurnMonitorBox
{
    private TurnInputMonitor? _value;

    public TurnInputMonitor? Value
    {
        get => Volatile.Read(ref _value);
        set => Volatile.Write(ref _value, value);
    }
}
