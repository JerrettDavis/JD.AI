using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JD.AI.Agent;
using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Checkpointing;
using JD.AI.Core.Config;
using JD.AI.Core.Governance;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.Mcp;
using JD.AI.Core.Plugins;
using JD.AI.Core.PromptCaching;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Core.Providers.Metadata;
using JD.AI.Core.Providers.ModelSearch;
using JD.AI.Core.Sessions;
using JD.AI.Core.Tools;
using JD.AI.Core.Usage;
using JD.AI.Rendering;
using JD.AI.Workflows;
using JD.AI.Workflows.Store;
using JD.SemanticKernel.Extensions.Mcp;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Spectre.Console;

namespace JD.AI.Commands;

/// <summary>
/// Routes slash commands to their handlers.
/// </summary>
public sealed class SlashCommandRouter : ISlashCommandRouter
{
    private readonly AgentSession _session;
    private readonly IProviderRegistry _registry;
    private readonly ProviderConfigurationManager? _providerConfig;
    private readonly InstructionsResult? _instructions;
    private readonly ICheckpointStrategy? _checkpointStrategy;
    private readonly PluginLoader? _pluginLoader;
    private readonly IPluginLifecycleManager? _pluginManager;
    private readonly IWorkflowCatalog? _workflowCatalog;
    private readonly IWorkflowStore? _workflowStore;
    private readonly WorkflowEmitter _workflowEmitter;
    private readonly IPolicyEvaluator? _policyEvaluator;
    private readonly Action<SpinnerStyle>? _onSpinnerStyleChanged;
    private readonly Func<SpinnerStyle>? _getSpinnerStyle;
    private readonly McpManager _mcpManager;
    private readonly Action<TuiTheme>? _onThemeChanged;
    private readonly Func<TuiTheme>? _getTheme;
    private readonly Action<bool>? _onVimModeChanged;
    private readonly Func<bool>? _getVimMode;
    private readonly Action<OutputStyle>? _onOutputStyleChanged;
    private readonly Func<OutputStyle>? _getOutputStyle;
    private readonly AtomicConfigStore? _configStore;
    private readonly ModelSearchAggregator? _modelSearchAggregator;
    private readonly ModelMetadataProvider? _metadataProvider;
    private readonly IUsageMeter? _usageMeter;
    private readonly Func<string>? _getSkillsStatus;
    private readonly Func<string>? _reloadSkills;

    public SlashCommandRouter(
        AgentSession session,
        IProviderRegistry registry,
        InstructionsResult? instructions = null,
        ICheckpointStrategy? checkpointStrategy = null,
        PluginLoader? pluginLoader = null,
        IPluginLifecycleManager? pluginManager = null,
        IWorkflowCatalog? workflowCatalog = null,
        Func<SpinnerStyle>? getSpinnerStyle = null,
        Action<SpinnerStyle>? onSpinnerStyleChanged = null,
        ProviderConfigurationManager? providerConfig = null,
        McpManager? mcpManager = null,
        AtomicConfigStore? configStore = null,
        ModelSearchAggregator? modelSearchAggregator = null,
        IWorkflowStore? workflowStore = null,
        ModelMetadataProvider? metadataProvider = null,
        Func<TuiTheme>? getTheme = null,
        Action<TuiTheme>? onThemeChanged = null,
        Func<bool>? getVimMode = null,
        Action<bool>? onVimModeChanged = null,
        Func<OutputStyle>? getOutputStyle = null,
        Action<OutputStyle>? onOutputStyleChanged = null,
        IUsageMeter? usageMeter = null,
        IPolicyEvaluator? policyEvaluator = null,
        Func<string>? getSkillsStatus = null,
        Func<string>? reloadSkills = null)
    {
        _session = session;
        _registry = registry;
        _instructions = instructions;
        _checkpointStrategy = checkpointStrategy;
        _pluginLoader = pluginLoader;
        _pluginManager = pluginManager;
        _workflowCatalog = workflowCatalog;
        _workflowStore = workflowStore;
        _workflowEmitter = new WorkflowEmitter();
        _policyEvaluator = policyEvaluator;
        _getSpinnerStyle = getSpinnerStyle;
        _onSpinnerStyleChanged = onSpinnerStyleChanged;
        _providerConfig = providerConfig;
        _mcpManager = mcpManager ?? new McpManager();
        _getTheme = getTheme;
        _onThemeChanged = onThemeChanged;
        _getVimMode = getVimMode;
        _onVimModeChanged = onVimModeChanged;
        _getOutputStyle = getOutputStyle;
        _onOutputStyleChanged = onOutputStyleChanged;
        _configStore = configStore;
        _modelSearchAggregator = modelSearchAggregator;
        _metadataProvider = metadataProvider;
        _usageMeter = usageMeter;
        _getSkillsStatus = getSkillsStatus;
        _reloadSkills = reloadSkills;
    }

    public bool IsSlashCommand(string input) =>
        input.TrimStart().StartsWith('/');

    public async Task<string?> ExecuteAsync(string input, CancellationToken ct = default)
    {
        var parts = input.TrimStart().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "No command provided. Type /help for available commands.";

        if (!SlashCommandCatalog.TryResolveDispatch(parts[0], out var commandId))
            return $"Unknown command: {parts[0]}. Type /help for available commands.";

        var arg = parts.Length > 1 ? parts[1] : null;

        return commandId switch
        {
            SlashCommandId.Help => SlashCommandCatalog.BuildHelpText(),
            SlashCommandId.Models => await ListModelsAsync(ct).ConfigureAwait(false),
            SlashCommandId.Model => await SwitchModelAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.Providers => await ListProvidersAsync(ct).ConfigureAwait(false),
            SlashCommandId.Provider => await HandleProviderCommandAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.Clear => ClearHistory(),
            SlashCommandId.Compact => await CompactAsync(ct).ConfigureAwait(false),
            SlashCommandId.Cost => await GetCostAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.Autorun => ToggleAutoRun(arg),
            SlashCommandId.Permissions => TogglePermissions(arg),
            SlashCommandId.Sessions => await ListSessionsAsync(ct).ConfigureAwait(false),
            SlashCommandId.Resume => await ResumeSessionAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.Name => NameSession(arg),
            SlashCommandId.History => ShowHistory(),
            SlashCommandId.Export => await ExportSessionAsync(ct).ConfigureAwait(false),
            SlashCommandId.Update => await CheckUpdateAsync(ct).ConfigureAwait(false),
            SlashCommandId.Instructions => ShowInstructions(),
            SlashCommandId.Plugins => await HandlePluginsAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.Checkpoint => await HandleCheckpointAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.Sandbox => ShowSandboxInfo(),
            SlashCommandId.Workflow => await HandleWorkflowAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.Spinner => HandleSpinner(arg),
            SlashCommandId.Reasoning => HandleReasoning(arg),
            SlashCommandId.Local => await HandleLocalModelAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.Mcp => await HandleMcpAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.Context => GetContextUsage(),
            SlashCommandId.SystemPrompt => HandleSystemPrompt(arg),
            SlashCommandId.Prompt => await HandlePromptAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.CompactSystemPrompt => await CompactSystemPromptAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.Copy => await CopyLastResponseInstanceAsync().ConfigureAwait(false),
            SlashCommandId.Diff => await ShowDiffAsync(ct).ConfigureAwait(false),
            SlashCommandId.Init => await InitProjectFileAsync(ct).ConfigureAwait(false),
            SlashCommandId.Plan => TogglePlanMode(),
            SlashCommandId.Doctor => await RunDoctorAsync(ct).ConfigureAwait(false),
            SlashCommandId.Fork => await ForkSessionAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.Review => await RunReviewAsync(arg, securityMode: false, ct).ConfigureAwait(false),
            SlashCommandId.SecurityReview => await RunReviewAsync(arg, securityMode: true, ct).ConfigureAwait(false),
            SlashCommandId.Theme => HandleTheme(arg),
            SlashCommandId.Vim => ToggleVimMode(arg),
            SlashCommandId.Stats => await ShowStatsAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.Config => HandleConfig(arg),
            SlashCommandId.Skills => HandleSkills(arg),
            SlashCommandId.Agents => await HandleAgentsAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.Hooks => await HandleHooksAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.Memory => await HandleMemoryAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.OutputStyle => HandleOutputStyle(arg),
            SlashCommandId.Default => await HandleDefaultAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.ModelInfo => await HandleModelInfoAsync(arg, ct).ConfigureAwait(false),
            SlashCommandId.Trace => ShowTrace(arg),
            SlashCommandId.Shortcuts => GetShortcuts(),
            SlashCommandId.Quit => null, // Signal exit
            _ => $"Unknown command: {parts[0]}. Type /help for available commands.",
        };
    }

    private async Task<IReadOnlyList<ProviderInfo>> DetectProvidersAsync(
        bool forceRefresh,
        CancellationToken ct)
    {
        if (forceRefresh && _registry is ProviderRegistry concrete)
            return await concrete.DetectProvidersAsync(forceRefresh: true, ct).ConfigureAwait(false);

        return await _registry.DetectProvidersAsync(ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ProviderModelInfo>> GetModelsAsync(
        bool forceRefresh,
        CancellationToken ct)
    {
        if (forceRefresh && _registry is ProviderRegistry concrete)
            return await concrete.GetModelsAsync(forceRefresh: true, ct).ConfigureAwait(false);

        return await _registry.GetModelsAsync(ct).ConfigureAwait(false);
    }

    private async Task SwitchModelAndPersistAsync(ProviderModelInfo model, CancellationToken ct)
    {
        _session.SwitchModel(model);
        await PersistProjectDefaultsAsync(model, ct).ConfigureAwait(false);
    }

    private async Task PersistProjectDefaultsAsync(ProviderModelInfo model, CancellationToken ct)
    {
        if (_configStore is null)
            return;

        var projectPath = _session.SessionInfo?.ProjectPath ?? Directory.GetCurrentDirectory();

        try
        {
            await _configStore.SetDefaultProviderAsync(model.ProviderName, projectPath, ct)
                .ConfigureAwait(false);
            await _configStore.SetDefaultModelAsync(model.Id, projectPath, ct)
                .ConfigureAwait(false);
        }
#pragma warning disable CA1031 // preference persistence is best-effort
        catch
#pragma warning restore CA1031
        {
            // Ignore persistence failures during model switches.
        }
    }

    private static string GetShortcuts() => """
        Keyboard Shortcuts:
          Ctrl+C          — Cancel current operation / exit
          Ctrl+L          — Clear screen
          Ctrl+U          — Clear input line
          Ctrl+W          — Delete word backward
          Ctrl+R          — Reverse history search
          Ctrl+V          — Paste from clipboard
          Shift+Tab       — Toggle plan mode
          Alt+T           — Cycle reasoning effort (auto/low/medium/high/max)
          Alt+P           — Cycle through recent models
          Tab             — Accept completion
          Up/Down         — Navigate history / completion dropdown
          Home/End        — Move to start/end of input
          ESC             — Clear input / dismiss completions / vim normal mode
          ESC ESC         — Cancel (at empty prompt)

        Vim Mode (when enabled with /vim):
          i/a/A/I         — Enter insert mode
          ESC             — Return to normal mode
          h/l/w/b/e       — Movement
          0/$             — Start/end of line
          x/dd/dw/D       — Delete operations
          cc/cw/C         — Change operations
          yy/yw/p/P       — Yank and paste
          u               — Undo

        Input Prefixes:
          !<command>      — Execute shell command directly
          @<file>         — Attach file contents to prompt
        """;

    private async Task<string> ListModelsAsync(CancellationToken ct)
    {
        var models = await GetModelsAsync(forceRefresh: true, ct).ConfigureAwait(false);
        if (models.Count == 0)
        {
            return "No models available. Check provider authentication.";
        }

        var selected = ModelPicker.Pick(models, _session.CurrentModel);
        if (selected != null && !string.Equals(selected.Id, _session.CurrentModel?.Id, StringComparison.Ordinal))
        {
            await SwitchModelAndPersistAsync(selected, ct).ConfigureAwait(false);
            return $"Switched to {selected.DisplayName} ({selected.ProviderName})";
        }

        return selected != null
            ? $"Current model: {selected.DisplayName} ({selected.ProviderName})"
            : "Model selection cancelled.";
    }

    private async Task<string> SwitchModelAsync(string? modelId, CancellationToken ct)
    {
        // Route subcommands: /model search <query>, /model url <url>
        if (modelId is not null)
        {
            if (modelId.StartsWith("search ", StringComparison.OrdinalIgnoreCase)
                || string.Equals(modelId, "search", StringComparison.OrdinalIgnoreCase))
            {
                var query = modelId.Length > 7 ? modelId[7..].Trim() : string.Empty;
                return await HandleModelSearchAsync(query, ct).ConfigureAwait(false);
            }

            if (modelId.StartsWith("url ", StringComparison.OrdinalIgnoreCase)
                || string.Equals(modelId, "url", StringComparison.OrdinalIgnoreCase))
            {
                var url = modelId.Length > 4 ? modelId[4..].Trim() : string.Empty;
                return await HandleModelUrlAsync(url, ct).ConfigureAwait(false);
            }
        }

        var models = await GetModelsAsync(forceRefresh: true, ct).ConfigureAwait(false);

        // No argument: show interactive picker
        if (string.IsNullOrWhiteSpace(modelId))
        {
            if (models.Count == 0)
            {
                return "No models available. Check provider authentication.";
            }

            var selected = ModelPicker.Pick(models, _session.CurrentModel);
            if (selected != null)
            {
                await SwitchModelAndPersistAsync(selected, ct).ConfigureAwait(false);
                return $"Switched to {selected.DisplayName} ({selected.ProviderName})";
            }

            return "Model selection cancelled.";
        }

        // With argument: fuzzy match like before
        var model = models.FirstOrDefault(m =>
            m.Id.Contains(modelId, StringComparison.OrdinalIgnoreCase));

        if (model is null)
        {
            return $"Model '{modelId}' not found. Use /models to browse interactively.";
        }

        await SwitchModelAndPersistAsync(model, ct).ConfigureAwait(false);
        return $"Switched to {model.DisplayName} ({model.ProviderName})";
    }

    private async Task<string> HandleModelSearchAsync(string query, CancellationToken ct)
    {
        if (!TryParseModelSearchOptions(query, out var options, out var parseError))
            return parseError;

        var installedModels = await GetModelsAsync(forceRefresh: true, ct).ConfigureAwait(false);
        var installedMatches = installedModels
            .Where(m => MatchesModelSearch(m, options))
            .ToList();

        var installedMap = installedMatches.ToDictionary(
            m => $"{m.ProviderName}|{m.Id}",
            m => m,
            StringComparer.OrdinalIgnoreCase);
        var modelInfoMap = new Dictionary<string, ProviderModelInfo>(installedMap, StringComparer.OrdinalIgnoreCase);

        var results = installedMatches
            .Select(m => new RemoteModelResult(
                m.Id,
                m.DisplayName,
                m.ProviderName,
                null,
                "Installed",
                null,
                m.Capabilities))
            .ToList();
        var resultKeys = new HashSet<string>(
            results.Select(r => $"{r.ProviderName}|{r.Id}"),
            StringComparer.OrdinalIgnoreCase);

        foreach (var catalog in await SearchMetadataCatalogAsync(options, ct).ConfigureAwait(false))
        {
            var key = $"{catalog.Result.ProviderName}|{catalog.Result.Id}";
            if (resultKeys.Add(key))
            {
                results.Add(catalog.Result);
            }

            if (!modelInfoMap.ContainsKey(key))
            {
                modelInfoMap[key] = catalog.ModelInfo;
            }
        }

        if (_modelSearchAggregator is not null && !string.IsNullOrWhiteSpace(options.Query))
        {
            try
            {
                var remoteResults = await _modelSearchAggregator.SearchAllAsync(options.Query, ct).ConfigureAwait(false);
                foreach (var remote in remoteResults)
                {
                    if (!MatchesRemoteSearch(remote, options))
                        continue;

                    var key = $"{remote.ProviderName}|{remote.Id}";
                    if (resultKeys.Add(key))
                    {
                        results.Add(remote);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (results.Count == 0)
                    return $"Search failed: {ex.Message}";
            }
        }

        if (results.Count == 0)
            return $"No models found for '{options.Query}'.";

        results = SortModelSearchResults(results, modelInfoMap, options.Sort);

        // Display results table
        var table = new Table()
            .Border(TableBorder.Rounded);

        var title = $"Search results for '{Markup.Escape(options.Query)}'";
        if (!string.IsNullOrWhiteSpace(options.Provider))
            title += $" | provider={Markup.Escape(options.Provider)}";
        if (!string.IsNullOrWhiteSpace(options.Capability))
            title += $" | cap={Markup.Escape(options.Capability)}";

        table.Title($"[bold]{title}[/]")
            .AddColumn(new TableColumn("[bold]Provider[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Model[/]"))
            .AddColumn(new TableColumn("[bold]Caps[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Ctx[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Cost (in/out)[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Status[/]"));

        foreach (var r in results)
        {
            modelInfoMap.TryGetValue($"{r.ProviderName}|{r.Id}", out var modelInfo);
            var statusMarkup = r.Status switch
            {
                "Installed" => "[green]Installed[/]",
                "Available" or "Pull" => $"[yellow]{Markup.Escape(r.Status)}[/]",
                "Download" => "[blue]Download[/]",
                "Catalog" => "[grey]Catalog[/]",
                _ => Markup.Escape(r.Status),
            };

            table.AddRow(
                Markup.Escape(r.ProviderName),
                Markup.Escape(r.DisplayName),
                r.Capabilities.ToBadge(),
                modelInfo is null ? "-" : modelInfo.ContextWindowTokens.ToString("N0"),
                modelInfo is { HasMetadata: true }
                    ? $"${modelInfo.InputCostPerToken}/{modelInfo.OutputCostPerToken}"
                    : "-",
                statusMarkup);
        }

        AnsiConsole.Write(table);

        // Interactive selection
        var choices = results
            .Select(r => $"{r.ProviderName}: {r.DisplayName}")
            .Append("Cancel")
            .ToList();

        string selection;
        try
        {
            selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select a model to pull:")
                    .WithAdaptivePaging(preferredPageSize: 15, totalChoices: choices.Count, singularNoun: "model")
                    .AddChoices(choices));
        }
        catch (OperationCanceledException)
        {
            return "Model search cancelled.";
        }
        catch (InvalidOperationException)
        {
            return "Model search cancelled.";
        }
        catch (NotSupportedException)
        {
            return "Model search cancelled.";
        }

        if (string.Equals(selection, "Cancel", StringComparison.Ordinal))
            return "Model search cancelled.";

        var selectedIndex = choices.IndexOf(selection);
        var selected = results[selectedIndex];

        if (string.Equals(selected.Status, "Installed", StringComparison.Ordinal))
        {
            // Already installed — switch to it
            var models = await GetModelsAsync(forceRefresh: true, ct).ConfigureAwait(false);
            var match = models.FirstOrDefault(m =>
                m.Id.Contains(selected.Id, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                await SwitchModelAndPersistAsync(match, ct).ConfigureAwait(false);
                return $"Switched to {match.DisplayName} ({match.ProviderName})";
            }

            return $"Model '{selected.DisplayName}' is installed but not found in the provider registry.";
        }

        // Pull the model
        return await PullAndSwitchAsync(selected, ct).ConfigureAwait(false);
    }

    private sealed record ModelSearchOptions(
        string Query,
        string? Provider,
        string? Capability,
        string Sort);

    private sealed record CatalogSearchResult(
        RemoteModelResult Result,
        ProviderModelInfo ModelInfo);

    private static bool TryParseModelSearchOptions(
        string raw,
        out ModelSearchOptions options,
        out string error)
    {
        options = new ModelSearchOptions(string.Empty, null, null, "name");
        error = "Usage: /model search [--provider <name>] [--cap <chat|tools|vision|embeddings|reasoning>] [--sort <name|context|cost|popularity>] <query>";

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var provider = (string?)null;
        var capability = (string?)null;
        var sort = "name";
        var queryParts = new List<string>();

        var tokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (string.Equals(token, "--provider", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= tokens.Length) return false;
                provider = tokens[++i];
                continue;
            }

            if (string.Equals(token, "--cap", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= tokens.Length) return false;
                capability = tokens[++i].ToLowerInvariant();
                continue;
            }

            if (string.Equals(token, "--sort", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= tokens.Length) return false;
                sort = tokens[++i].ToLowerInvariant();
                continue;
            }

            queryParts.Add(token);
        }

        var query = string.Join(' ', queryParts).Trim();
        if (string.IsNullOrWhiteSpace(query))
            return false;

        if (sort is not ("name" or "context" or "cost" or "popularity"))
        {
            error = "Sort must be one of: name, context, cost, popularity.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(capability) &&
            capability is not ("chat" or "tools" or "vision" or "embeddings" or "reasoning"))
        {
            error = "Capability must be one of: chat, tools, vision, embeddings, reasoning.";
            return false;
        }

        options = new ModelSearchOptions(query, provider, capability, sort);
        return true;
    }

    private static bool MatchesModelSearch(ProviderModelInfo model, ModelSearchOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Provider) &&
            !model.ProviderName.Contains(options.Provider, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.Capability) &&
            !MatchesCapability(model.Capabilities, model, options.Capability))
        {
            return false;
        }

        return model.Id.Contains(options.Query, StringComparison.OrdinalIgnoreCase) ||
               model.DisplayName.Contains(options.Query, StringComparison.OrdinalIgnoreCase) ||
               model.ProviderName.Contains(options.Query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesRemoteSearch(RemoteModelResult model, ModelSearchOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Provider) &&
            !model.ProviderName.Contains(options.Provider, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.Capability) &&
            !MatchesCapability(
                model.Capabilities,
                new ProviderModelInfo(model.Id, model.DisplayName, model.ProviderName, Capabilities: model.Capabilities),
                options.Capability))
        {
            return false;
        }

        return model.Id.Contains(options.Query, StringComparison.OrdinalIgnoreCase) ||
               model.DisplayName.Contains(options.Query, StringComparison.OrdinalIgnoreCase) ||
               model.ProviderName.Contains(options.Query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesCapability(
        ModelCapabilities capabilities,
        ProviderModelInfo? model,
        string capability)
    {
        return capability switch
        {
            "chat" => capabilities.HasFlag(ModelCapabilities.Chat),
            "tools" => capabilities.HasFlag(ModelCapabilities.ToolCalling),
            "vision" => capabilities.HasFlag(ModelCapabilities.Vision),
            "embeddings" => capabilities.HasFlag(ModelCapabilities.Embeddings),
            "reasoning" => SupportsReasoningEffort(model),
            _ => true,
        };
    }

    private async Task<IReadOnlyList<CatalogSearchResult>> SearchMetadataCatalogAsync(
        ModelSearchOptions options,
        CancellationToken ct)
    {
        if (_metadataProvider is null || string.IsNullOrWhiteSpace(options.Query))
            return [];

        if (!_metadataProvider.IsLoaded)
        {
            await _metadataProvider.LoadAsync(ct: ct).ConfigureAwait(false);
        }

        var entries = _metadataProvider.GetEntriesSnapshot();
        if (entries.Count == 0)
            return [];

        var results = new List<CatalogSearchResult>();
        foreach (var (key, entry) in entries)
        {
            var providerToken = ResolveCatalogProviderToken(entry, key);
            var providerName = ToDisplayProviderName(providerToken);
            var modelId = StripProviderPrefix(key, providerToken);
            var capabilities = ToCapabilities(entry);

            var candidate = new RemoteModelResult(
                modelId,
                modelId,
                providerName,
                null,
                "Catalog",
                null,
                capabilities);

            if (MatchesRemoteSearch(candidate, options))
            {
                results.Add(new CatalogSearchResult(
                    candidate,
                    new ProviderModelInfo(
                        modelId,
                        modelId,
                        providerName,
                        ContextWindowTokens: entry.MaxInputTokens ?? 128_000,
                        MaxOutputTokens: entry.MaxOutputTokens ?? 16_384,
                        InputCostPerToken: entry.InputCostPerToken ?? 0m,
                        OutputCostPerToken: entry.OutputCostPerToken ?? 0m,
                        HasMetadata: true,
                        Capabilities: capabilities)));
            }
        }

        return results;
    }

    private static string ResolveCatalogProviderToken(ModelMetadataEntry entry, string key)
    {
        if (!string.IsNullOrWhiteSpace(entry.LitellmProvider))
            return entry.LitellmProvider!;

        var slash = key.IndexOf('/', StringComparison.Ordinal);
        return slash > 0 ? key[..slash] : "unknown";
    }

    private static string StripProviderPrefix(string key, string providerToken)
    {
        if (!string.IsNullOrWhiteSpace(providerToken) &&
            key.StartsWith(providerToken + "/", StringComparison.OrdinalIgnoreCase))
        {
            return key[(providerToken.Length + 1)..];
        }

        return key;
    }

    private static string ToDisplayProviderName(string providerToken) => providerToken.ToLowerInvariant() switch
    {
        "openai" => "OpenAI",
        "openrouter" => "OpenRouter",
        "azure" or "azure_ai" => "Azure OpenAI",
        "anthropic" => "Anthropic",
        "gemini" or "google" or "vertex_ai" => "Google Gemini",
        "bedrock" or "bedrock_converse" => "Amazon Bedrock",
        "huggingface" => "HuggingFace",
        "mistral" => "Mistral",
        "ollama" => "Ollama",
        "foundrylocal" or "foundry_local" => "Foundry Local",
        _ => providerToken,
    };

    private static ModelCapabilities ToCapabilities(ModelMetadataEntry entry)
    {
        var caps = ModelCapabilities.Chat;

        if (entry.SupportsFunctionCalling is true)
            caps |= ModelCapabilities.ToolCalling;
        if (entry.SupportsVision is true)
            caps |= ModelCapabilities.Vision;

        return caps;
    }

    private static List<RemoteModelResult> SortModelSearchResults(
        IEnumerable<RemoteModelResult> results,
        Dictionary<string, ProviderModelInfo> modelInfoMap,
        string sort)
    {
        return sort switch
        {
            "context" => results
                .OrderByDescending(r => modelInfoMap.TryGetValue($"{r.ProviderName}|{r.Id}", out var m)
                    ? m.ContextWindowTokens : 0)
                .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            "cost" => results
                .OrderBy(r =>
                {
                    if (!modelInfoMap.TryGetValue($"{r.ProviderName}|{r.Id}", out var model) || !model.HasMetadata)
                        return decimal.MaxValue;
                    return model.InputCostPerToken + model.OutputCostPerToken;
                })
                .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            "popularity" => results
                .OrderByDescending(r => r.Status.Equals("Installed", StringComparison.OrdinalIgnoreCase))
                .ThenBy(r => r.DisplayName.Length)
                .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => results
                .OrderBy(r => r.ProviderName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    private async Task<string> HandleModelUrlAsync(string url, CancellationToken ct)
    {
        if (_modelSearchAggregator is null)
            return "Model search is not available (no search providers configured).";

        if (string.IsNullOrWhiteSpace(url))
            return "Usage: /model url <model-url-or-name>";

        // Auto-detect provider from URL
        string providerName;
        string modelId;

        if (url.Contains("huggingface.co", StringComparison.OrdinalIgnoreCase))
        {
            providerName = "HuggingFace";
            // Extract repo id from URL: https://huggingface.co/org/model → org/model
            var uri = url.Replace("https://huggingface.co/", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("http://huggingface.co/", "", StringComparison.OrdinalIgnoreCase);
            modelId = uri.Split('/', StringSplitOptions.RemoveEmptyEntries) is { Length: >= 2 } segments
                ? $"{segments[0]}/{segments[1]}"
                : uri.TrimEnd('/');
        }
        else if (url.Contains("foundry", StringComparison.OrdinalIgnoreCase))
        {
            providerName = "Foundry Local";
            modelId = url;
        }
        else
        {
            // Default to Ollama (model name format)
            providerName = "Ollama";
            modelId = url;
        }

        var result = new RemoteModelResult(modelId, modelId, providerName, null, "Pull", null);
        return await PullAndSwitchAsync(result, ct).ConfigureAwait(false);
    }

    private async Task<string> PullAndSwitchAsync(RemoteModelResult selected, CancellationToken ct)
    {
        bool pullOk = false;
        string? pullError = null;

        try
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"Pulling {selected.DisplayName}...", async ctx =>
                {
                    var progress = new Progress<string>(msg => ctx.Status($"[dim]{Markup.Escape(msg)}[/]"));
                    pullOk = await PullModelDirectAsync(selected, progress, ct).ConfigureAwait(false);
                }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            pullError = ex.Message;
        }

        if (!pullOk)
            return pullError is not null
                ? $"Failed to pull '{selected.DisplayName}': {pullError}"
                : $"Failed to pull '{selected.DisplayName}'. The provider may not support pulling.";

        // Re-detect providers and find the newly pulled model
        var models = await GetModelsAsync(forceRefresh: true, ct).ConfigureAwait(false);
        var match = models.FirstOrDefault(m =>
            m.Id.Contains(selected.Id, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            await SwitchModelAndPersistAsync(match, ct).ConfigureAwait(false);
            return $"Pulled and switched to {match.DisplayName} ({match.ProviderName})";
        }

        return $"Pulled '{selected.DisplayName}' successfully, but it was not found in the provider registry. You may need to restart.";
    }

    private async Task<bool> PullModelDirectAsync(
        RemoteModelResult model, IProgress<string> progress, CancellationToken ct)
    {
        // Create a temporary provider instance matching the model's provider
        IRemoteModelSearch? searcher = model.ProviderName switch
        {
            "Ollama" => new OllamaModelSearch(new HttpClient()),
            "HuggingFace" => new HuggingFaceModelSearch(new HttpClient()),
            "Foundry Local" => new FoundryLocalModelSearch(),
            _ => null,
        };

        if (searcher is null)
            return false;

        return await searcher.PullAsync(model, progress, ct).ConfigureAwait(false);
    }

    private async Task<string> ListProvidersAsync(CancellationToken ct)
    {
        var providers = await DetectProvidersAsync(forceRefresh: true, ct).ConfigureAwait(false);
        var lines = providers.Select(p =>
        {
            var status = p.IsAvailable ? "✓" : "✗";
            return $"  {status} {p.Name}: {p.StatusMessage}";
        });

        return $"Providers:\n{string.Join('\n', lines)}";
    }

    private string GetCurrentProvider() =>
        _session.CurrentModel is { } m
            ? $"Current: {m.DisplayName} ({m.ProviderName})"
            : "No model selected.";

    private async Task<string> HandleProviderCommandAsync(string? arg, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return await ProviderPickerAsync(ct).ConfigureAwait(false);

        var subParts = arg.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var subCmd = subParts[0].ToUpperInvariant();
        var subArg = subParts.Length > 1 ? subParts[1].Trim() : null;

        return subCmd switch
        {
            "LIST" => await ProviderListAsync(ct).ConfigureAwait(false),
            "ADD" => await ProviderAddAsync(subArg, ct).ConfigureAwait(false),
            "REMOVE" => await ProviderRemoveAsync(subArg, ct).ConfigureAwait(false),
            "TEST" => await ProviderTestAsync(subArg, ct).ConfigureAwait(false),
            _ => "Usage: /provider [list|add <name>|remove <name>|test [name]]",
        };
    }

    private async Task<string> ProviderListAsync(CancellationToken ct)
    {
        var providers = await DetectProvidersAsync(forceRefresh: true, ct).ConfigureAwait(false);
        var activeProviderName = _session.CurrentModel?.ProviderName;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Providers[/]")
            .AddColumn(new TableColumn("[bold]Provider[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Auth[/]"))
            .AddColumn(new TableColumn("[bold]Status[/]"))
            .AddColumn(new TableColumn("[bold]Models[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Endpoint[/]"));

        foreach (var p in providers)
        {
            var isActive = string.Equals(p.Name, activeProviderName, StringComparison.Ordinal);
            var modelCount = p.Models.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var auth = p.Name switch
            {
                "Claude Code" or "GitHub Copilot" or "OpenAI Codex" => "OAuth",
                "Ollama" => "None",
                "Local Models" => "File",
                _ => "API Key",
            };

            string status;
            if (isActive)
                status = "[green]● Active ◄ active[/]";
            else if (p.IsAvailable)
                status = "[yellow]✓ Ready[/]";
            else
                status = "[red]✗ No key[/]";

            var name = isActive
                ? $"[green]{Markup.Escape(p.Name)}[/]"
                : Markup.Escape(p.Name);

            var endpoint = Markup.Escape(p.StatusMessage ?? "-");

            table.AddRow(name, auth, status, modelCount, endpoint);
        }

        AnsiConsole.Write(table);
        return string.Empty;
    }

    private async Task<string> ProviderPickerAsync(CancellationToken ct)
    {
        var providers = await DetectProvidersAsync(forceRefresh: true, ct).ConfigureAwait(false);
        if (providers.Count == 0)
            return "No providers detected. Use /provider add <name> to configure one.";

        var activeProviderName = _session.CurrentModel?.ProviderName;

        var activeChoices = new List<string>();
        var configuredChoices = new List<string>();
        var availableChoices = new List<string>();

        foreach (var p in providers)
        {
            var modelCount = p.Models.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (p.IsAvailable && string.Equals(p.Name, activeProviderName, StringComparison.Ordinal))
            {
                activeChoices.Add($"● {p.Name}  ({modelCount} models)");
            }
            else if (p.IsAvailable)
            {
                configuredChoices.Add($"✓ {p.Name}  ({modelCount} models)");
            }
            else
            {
                availableChoices.Add($"✗ {p.Name}");
            }
        }

        const string configureNew = "+ Configure new provider...";

        var prompt = new SelectionPrompt<string>()
            .Title("[bold]Switch Provider[/]")
            .WithAdaptivePaging(
                preferredPageSize: 15,
                totalChoices: activeChoices.Count + configuredChoices.Count + availableChoices.Count + 1,
                singularNoun: "provider")
            .HighlightStyle(new Style(Color.Aqua, decoration: Decoration.Bold));

        if (activeChoices.Count > 0)
            prompt.AddChoiceGroup("Active", activeChoices);
        if (configuredChoices.Count > 0)
            prompt.AddChoiceGroup("Configured", configuredChoices);
        if (availableChoices.Count > 0)
            prompt.AddChoiceGroup("Available to Configure", availableChoices);

        prompt.AddChoice(configureNew);

        string choice;
        try
        {
            choice = AnsiConsole.Prompt(prompt);
        }
        catch (OperationCanceledException)
        {
            return "Selection cancelled.";
        }
        catch (InvalidOperationException)
        {
            return "Selection cancelled.";
        }

        if (string.Equals(choice, configureNew, StringComparison.Ordinal))
            return await ProviderAddAsync(null, ct).ConfigureAwait(false);

        // Extract provider name from choice (remove status icon and model count suffix)
        var providerName = choice
            .TrimStart('●', '✓', '✗', ' ')
            .Split("  (", StringSplitOptions.None)[0]
            .Trim();

        var selected = providers.FirstOrDefault(p =>
            string.Equals(p.Name, providerName, StringComparison.Ordinal));

        if (selected is null)
            return $"Provider '{providerName}' not found.";

        if (!selected.IsAvailable)
            return await ProviderAddAsync(providerName.ToLowerInvariant(), ct).ConfigureAwait(false);

        if (selected.Models.Count == 0)
            return $"{selected.Name} has no models available.";

        if (selected.Models.Count == 1)
        {
            await SwitchModelAndPersistAsync(selected.Models[0], ct).ConfigureAwait(false);
            return $"Switched to {selected.Models[0].DisplayName} ({selected.Name})";
        }

        // Multiple models — show model picker
        var model = ModelPicker.Pick(selected.Models, _session.CurrentModel);
        if (model is null)
            return "Model selection cancelled.";

        await SwitchModelAndPersistAsync(model, ct).ConfigureAwait(false);
        return $"Switched to {model.DisplayName} ({selected.Name})";
    }

    private async Task<string> ProviderAddAsync(string? providerName, CancellationToken ct)
    {
        if (_providerConfig == null)
            return "Provider configuration not available.";

        if (string.IsNullOrWhiteSpace(providerName))
        {
            return """
                Usage: /provider add <name>
                Available providers: openai, azure-openai, anthropic, google-gemini,
                  mistral, bedrock, huggingface, openrouter, openai-compat
                Example: /provider add openai
                """;
        }

        var name = providerName.Trim().ToLowerInvariant();

        switch (name)
        {
            case "openai":
                AnsiConsole.MarkupLine("[bold]Configure OpenAI[/]");
                var openaiKey = AnsiConsole.Ask<string>("API Key (sk-...):");
                await _providerConfig.SetCredentialAsync("openai", "apikey", openaiKey, ct)
                    .ConfigureAwait(false);
                return "OpenAI configured. Run /providers to verify.";

            case "azure-openai":
                AnsiConsole.MarkupLine("[bold]Configure Azure OpenAI[/]");
                var azureKey = AnsiConsole.Ask<string>("API Key:");
                var azureEndpoint = AnsiConsole.Ask<string>("Endpoint (https://xxx.openai.azure.com):");
                var azureDeployments = AnsiConsole.Ask("Deployments (comma-separated, or blank for defaults):",
                    defaultValue: "");
                await _providerConfig.SetCredentialAsync("azure-openai", "apikey", azureKey, ct)
                    .ConfigureAwait(false);
                await _providerConfig.SetCredentialAsync("azure-openai", "endpoint", azureEndpoint, ct)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(azureDeployments))
                {
                    await _providerConfig.SetCredentialAsync(
                        "azure-openai", "deployments", azureDeployments, ct)
                        .ConfigureAwait(false);
                }

                return "Azure OpenAI configured. Run /providers to verify.";

            case "anthropic":
                AnsiConsole.MarkupLine("[bold]Configure Anthropic[/]");
                var anthropicKey = AnsiConsole.Ask<string>("API Key (sk-ant-...):");
                await _providerConfig.SetCredentialAsync("anthropic", "apikey", anthropicKey, ct)
                    .ConfigureAwait(false);
                return "Anthropic configured. Run /providers to verify.";

            case "google-gemini":
                AnsiConsole.MarkupLine("[bold]Configure Google Gemini[/]");
                var googleKey = AnsiConsole.Ask<string>("API Key:");
                await _providerConfig.SetCredentialAsync("google-gemini", "apikey", googleKey, ct)
                    .ConfigureAwait(false);
                return "Google Gemini configured. Run /providers to verify.";

            case "mistral":
                AnsiConsole.MarkupLine("[bold]Configure Mistral[/]");
                var mistralKey = AnsiConsole.Ask<string>("API Key:");
                await _providerConfig.SetCredentialAsync("mistral", "apikey", mistralKey, ct)
                    .ConfigureAwait(false);
                return "Mistral configured. Run /providers to verify.";

            case "bedrock":
                AnsiConsole.MarkupLine("[bold]Configure AWS Bedrock[/]");
                var awsAccessKey = AnsiConsole.Ask<string>("AWS Access Key ID:");
                var awsSecretKey = AnsiConsole.Ask<string>("AWS Secret Access Key:");
                var awsRegion = AnsiConsole.Ask("AWS Region:", defaultValue: "us-east-1");
                await _providerConfig.SetCredentialAsync("bedrock", "accesskey", awsAccessKey, ct)
                    .ConfigureAwait(false);
                await _providerConfig.SetCredentialAsync("bedrock", "secretkey", awsSecretKey, ct)
                    .ConfigureAwait(false);
                await _providerConfig.SetCredentialAsync("bedrock", "region", awsRegion, ct)
                    .ConfigureAwait(false);
                return "AWS Bedrock configured. Run /providers to verify.";

            case "huggingface":
                AnsiConsole.MarkupLine("[bold]Configure HuggingFace[/]");
                var hfKey = AnsiConsole.Ask<string>("API Key (hf_...):");
                await _providerConfig.SetCredentialAsync("huggingface", "apikey", hfKey, ct)
                    .ConfigureAwait(false);
                return "HuggingFace configured. Run /providers to verify.";

            case "openrouter":
                AnsiConsole.MarkupLine("[bold]Configure OpenRouter[/]");
                var openrouterKey = AnsiConsole.Ask<string>("API Key (sk-or-...):");
                await _providerConfig.SetCredentialAsync("openrouter", "apikey", openrouterKey, ct)
                    .ConfigureAwait(false);
                return "OpenRouter configured. Run /providers to verify.";

            case "openai-compat":
                AnsiConsole.MarkupLine("[bold]Configure OpenAI-Compatible Endpoint[/]");
                var alias = AnsiConsole.Ask<string>("Alias (e.g. groq, together, deepseek):");
                var baseUrl = AnsiConsole.Ask<string>("Base URL (e.g. https://api.groq.com/openai/v1):");
                var compatKey = AnsiConsole.Ask<string>("API Key:");
                await _providerConfig.SetCredentialAsync($"openai-compat:{alias}", "apikey", compatKey, ct)
                    .ConfigureAwait(false);
                await _providerConfig.SetCredentialAsync($"openai-compat:{alias}", "baseurl", baseUrl, ct)
                    .ConfigureAwait(false);
                return $"OpenAI-Compatible endpoint '{alias}' configured. Run /providers to verify.";

            default:
                return $"Unknown provider: {name}. Run /provider add for the list.";
        }
    }

    private async Task<string> ProviderRemoveAsync(string? providerName, CancellationToken ct)
    {
        if (_providerConfig == null)
            return "Provider configuration not available.";

        if (string.IsNullOrWhiteSpace(providerName))
            return "Usage: /provider remove <name>";

        await _providerConfig.RemoveProviderAsync(providerName.Trim().ToLowerInvariant(), ct)
            .ConfigureAwait(false);
        return $"Credentials for '{providerName.Trim()}' removed.";
    }

    private async Task<string> ProviderTestAsync(string? providerName, CancellationToken ct)
    {
        var providers = await DetectProvidersAsync(forceRefresh: true, ct).ConfigureAwait(false);

        IEnumerable<ProviderInfo> toTest = providers;
        if (!string.IsNullOrWhiteSpace(providerName))
        {
            toTest = providers.Where(p =>
                p.Name.Contains(providerName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Provider test results:");
        foreach (var p in toTest)
        {
            var icon = p.IsAvailable ? "✓" : "✗";
            sb.AppendLine($"  {icon} {p.Name}: {p.StatusMessage}");
        }

        return sb.ToString();
    }

    private string ClearHistory()
    {
        _session.ClearHistory();
        return "Chat history cleared.";
    }

    private async Task<string> CompactAsync(CancellationToken ct)
    {
        await _session.CompactAsync(ct).ConfigureAwait(false);
        return "Context compacted.";
    }

    private async Task<string> GetCostAsync(string? arg, CancellationToken ct)
    {
        if (_usageMeter is null)
        {
            var tokens = _session.TotalTokens;
            return $"Token usage: {tokens:N0} total (metering not configured)";
        }

        try
        {
            var sb = new StringBuilder();
            var upper = arg?.ToUpperInvariant().Trim();

            // Determine scope
            UsageSummary usage;
            string scopeLabel;
            if (upper is "--DAY" or "--DAILY")
            {
                var today = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);
                usage = await _usageMeter.GetPeriodUsageAsync(today, DateTimeOffset.UtcNow, ct);
                scopeLabel = "Today";
            }
            else if (upper is "--WEEK" or "--WEEKLY")
            {
                var startOfWeek = new DateTimeOffset(DateTimeOffset.UtcNow.AddDays(-(int)DateTimeOffset.UtcNow.DayOfWeek).Date, TimeSpan.Zero);
                usage = await _usageMeter.GetPeriodUsageAsync(startOfWeek, DateTimeOffset.UtcNow, ct);
                scopeLabel = "This week";
            }
            else if (upper is "--MONTH" or "--MONTHLY")
            {
                var now = DateTimeOffset.UtcNow;
                var startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
                usage = await _usageMeter.GetPeriodUsageAsync(startOfMonth, DateTimeOffset.UtcNow, ct);
                scopeLabel = "This month";
            }
            else if (upper is "--ALL" or "--TOTAL")
            {
                usage = await _usageMeter.GetTotalUsageAsync(ct);
                scopeLabel = "All time";
            }
            else if (upper is "--EXPORT CSV")
            {
                var data = await _usageMeter.ExportAsync(UsageExportFormat.Csv, ct: ct);
                return $"CSV export:\n{data}";
            }
            else if (upper is "--EXPORT JSON")
            {
                var data = await _usageMeter.ExportAsync(UsageExportFormat.Json, ct: ct);
                return $"JSON export:\n{data}";
            }
            else
            {
                // Default: session usage
                var sessionId = _session.SessionInfo?.Id;
                if (sessionId is not null)
                {
                    usage = await _usageMeter.GetSessionUsageAsync(sessionId, ct);
                    scopeLabel = "This session";
                }
                else
                {
                    var tokens = _session.TotalTokens;
                    return $"Token usage: {tokens:N0} total";
                }
            }

            sb.AppendLine($"📊 Usage — {scopeLabel}");
            sb.AppendLine($"  Turns: {usage.TotalTurns:N0}  |  Tool calls: {usage.TotalToolCalls:N0}");
            sb.AppendLine($"  Prompt tokens:     {usage.TotalPromptTokens,12:N0}");
            sb.AppendLine($"  Completion tokens: {usage.TotalCompletionTokens,12:N0}");
            sb.AppendLine($"  Total tokens:      {usage.TotalTokens,12:N0}");
            sb.AppendLine($"  Estimated cost:    ${usage.EstimatedCostUsd:F4}");

            if (usage.ByProvider.Count > 1)
            {
                sb.AppendLine();
                sb.AppendLine("  By provider:");
                foreach (var (pid, breakdown) in usage.ByProvider.OrderByDescending(p => p.Value.TotalTokens))
                {
                    var costStr = breakdown.EstimatedCostUsd > 0
                        ? $"  ~${breakdown.EstimatedCostUsd:F4}"
                        : "  (free)";
                    sb.AppendLine($"    [{pid}] {breakdown.TotalTokens:N0} tokens, {breakdown.Turns} turns{costStr}");
                }
            }

            // Budget status
            var budget = await _usageMeter.CheckBudgetAsync(ct: ct);
            if (budget.LimitUsd.HasValue)
            {
                sb.AppendLine();
                var pct = budget.LimitUsd.Value > 0 ? budget.SpentUsd / budget.LimitUsd.Value * 100 : 0;
                var status = budget.IsExceeded ? "⛔ EXCEEDED" : budget.IsWarning ? "⚠️  WARNING" : "✅ OK";
                sb.AppendLine($"  Budget ({budget.Period}): ${budget.SpentUsd:F2} / ${budget.LimitUsd:F2} ({pct:F0}%) {status}");
            }

            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"Token usage: {_session.TotalTokens:N0} total (metering error: {ex.Message})";
        }
    }

    private string ShowTrace(string? arg)
    {
        var timeline = _session.LastTimeline;
        if (timeline is null || timeline.Entries.Count == 0)
            return "No trace available. Send a message first, then use /trace.";

        // If arg is a number, look up turn by index from session history
        if (arg is not null && int.TryParse(arg, System.Globalization.CultureInfo.InvariantCulture, out _))
            return "Turn-specific traces are recorded per-turn. Currently showing the last turn.";

        var entries = timeline.Entries;
        var total = timeline.TotalDuration;
        var sb = new System.Text.StringBuilder();

        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
            $"  Turn trace ({total.TotalSeconds:F1}s total, {entries.Count} operations)");
        sb.AppendLine("  ────────────────────────────────────────");

        foreach (var entry in entries)
        {
            var prefix = entry.ParentSpanId is null ? "┌" : "├";
            var status = entry.Status switch
            {
                "ok" => "✔",
                "error" => "✗",
                "cancelled" => "⊘",
                "denied" => "⛔",
                _ => "?",
            };

            var dur = entry.Duration.TotalMilliseconds > 0
                ? $"{entry.Duration.TotalMilliseconds,7:F0}ms"
                : "       ";

            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"  {entry.StartTime:HH:mm:ss.fff}  {prefix} {entry.Operation,-40} {dur}  {status}");

            if (entry.ErrorMessage is not null)
                sb.AppendLine($"                    └ {entry.ErrorMessage}");
        }

        // Summary line
        var tokensOut = entries
            .Where(e => e.Attributes.ContainsKey("tokens_out"))
            .Select(e => long.Parse(e.Attributes["tokens_out"], System.Globalization.CultureInfo.InvariantCulture))
            .Sum();
        if (tokensOut > 0)
        {
            sb.AppendLine();
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"  Tokens out: {tokensOut:N0}");
        }

        if (_session.CurrentModel is not null)
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"  Provider: {_session.CurrentModel.ProviderName} | Model: {_session.CurrentModel.Id}");
        }

        return sb.ToString();
    }

    private string ToggleAutoRun(string? arg)
    {
        if (string.Equals(arg, "on", StringComparison.OrdinalIgnoreCase))
        {
            _session.AutoRunEnabled = true;
            return "Auto-run enabled — tools will execute without confirmation.";
        }

        if (string.Equals(arg, "off", StringComparison.OrdinalIgnoreCase))
        {
            _session.AutoRunEnabled = false;
            return "Auto-run disabled — destructive tools will require confirmation.";
        }

        return $"Auto-run is {(_session.AutoRunEnabled ? "on" : "off")}. Usage: /autorun [on|off]";
    }

    private string TogglePermissions(string? arg)
    {
        if (string.Equals(arg, "off", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "false", StringComparison.OrdinalIgnoreCase))
        {
            _session.SkipPermissions = true;
            _session.PermissionMode = PermissionMode.BypassAll;
            return "⚠ Permission checks DISABLED — all tools will run without confirmation.";
        }

        if (string.Equals(arg, "on", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "true", StringComparison.OrdinalIgnoreCase))
        {
            _session.SkipPermissions = false;
            _session.PermissionMode = PermissionMode.Normal;
            return "Permission checks enabled — safety tiers apply.";
        }

        // Named permission modes
        if (string.Equals(arg, "plan", StringComparison.OrdinalIgnoreCase))
        {
            _session.PermissionMode = PermissionMode.Plan;
            _session.SkipPermissions = false;
            return "🔒 Plan mode — read-only tools only. Write and shell operations blocked.";
        }

        if (string.Equals(arg, "acceptEdits", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "accept-edits", StringComparison.OrdinalIgnoreCase))
        {
            _session.PermissionMode = PermissionMode.AcceptEdits;
            _session.SkipPermissions = false;
            return "📝 Accept-edits mode — file writes auto-approved, shell still requires confirmation.";
        }

        if (string.Equals(arg, "normal", StringComparison.OrdinalIgnoreCase))
        {
            _session.PermissionMode = PermissionMode.Normal;
            _session.SkipPermissions = false;
            return "Permission checks enabled — safety tiers apply.";
        }

        return $"Permission mode: {_session.PermissionMode}. Usage: /permissions [on|off|plan|acceptEdits|normal]";
    }

    // ── Session commands ──────────────────────────────────

    private async Task<string> ListSessionsAsync(CancellationToken ct)
    {
        _ = ct; // reserved for future async work
        if (_session.Store == null) return "Session persistence not initialized.";

        var projectHash = _session.SessionInfo?.ProjectHash;
        var sessions = await _session.Store.ListSessionsAsync(projectHash, 15).ConfigureAwait(false);

        if (sessions.Count == 0)
            return "No sessions found.";

        var lines = sessions.Select(s =>
        {
            var name = s.Name ?? "(unnamed)";
            var active = s.IsActive ? " ●" : "";
            var current = string.Equals(s.Id, _session.SessionInfo?.Id, StringComparison.Ordinal) ? " ◄" : "";
            return $"  {s.Id}  {name}{active}{current}  ({s.MessageCount} msgs, {s.UpdatedAt:g})";
        });

        return $"Recent sessions:\n{string.Join('\n', lines)}";
    }

    private async Task<string> ResumeSessionAsync(string? sessionId, CancellationToken ct)
    {
        if (_session.Store == null) return "Session persistence not initialized.";

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            // Show list so user can pick
            return await ListSessionsAsync(ct).ConfigureAwait(false) +
                "\n\nUsage: /resume <session-id>";
        }

        var projectPath = _session.SessionInfo?.ProjectPath ?? Directory.GetCurrentDirectory();
        await _session.InitializePersistenceAsync(projectPath, sessionId).ConfigureAwait(false);

        return _session.SessionInfo != null
            ? $"Resumed session {_session.SessionInfo.Id} ({_session.SessionInfo.Turns.Count} turns restored)"
            : $"Session '{sessionId}' not found.";
    }

    private string NameSession(string? name)
    {
        if (_session.SessionInfo == null)
            return "No active session.";

        if (string.IsNullOrWhiteSpace(name))
            return $"Current session: {_session.SessionInfo.Name ?? "(unnamed)"}. Usage: /name <name>";

        _session.SessionInfo.Name = name;
        return $"Session named: {name}";
    }

    private string ShowHistory()
    {
        if (_session.SessionInfo == null)
            return "No active session.";

        var turns = _session.SessionInfo.Turns;
        if (turns.Count == 0)
            return "No turns in this session.";

        var lines = turns.Select(t =>
        {
            var role = string.Equals(t.Role, "user", StringComparison.Ordinal) ? "👤" : "🤖";
            var preview = (t.Content ?? "").Replace('\n', ' ');
            if (preview.Length > 80)
                preview = string.Concat(preview.AsSpan(0, 77), "...");
            var tools = t.ToolCalls.Count > 0 ? $" [{t.ToolCalls.Count} tools]" : "";
            return $"  {t.TurnIndex}. {role} {preview}{tools}";
        });

        return $"Session history ({turns.Count} turns):\n{string.Join('\n', lines)}";
    }

    private async Task<string> ExportSessionAsync(CancellationToken ct)
    {
        _ = ct;
        if (_session.SessionInfo == null) return "No active session.";

        await _session.ExportSessionAsync().ConfigureAwait(false);
        return $"Session exported to ~/.jdai/projects/{_session.SessionInfo.ProjectHash}/sessions/{_session.SessionInfo.Id}.json";
    }

    private static async Task<string> CheckUpdateAsync(CancellationToken ct)
    {
        var info = await UpdateChecker.CheckAsync(forceCheck: true, ct).ConfigureAwait(false);
        if (info is null)
        {
            return $"jdai is up to date (v{UpdateChecker.GetCurrentVersion()}).";
        }

        var shouldRestart = await UpdatePrompter.PromptAsync(info, ct).ConfigureAwait(false);

        // PromptAsync already rendered detached-launch messaging to the console;
        // return a summary string so the TUI can render it in the message stream.
        return shouldRestart
            ? "Update process started. Exit and restart jdai to apply the update."
            : $"Update available: {info.CurrentVersion} → {info.LatestVersion}";
    }

    // ── New Phase commands ─────────────────────────────────

    private string ShowInstructions() =>
        _instructions?.ToSummary() ?? "No project instructions loaded.";

    private async Task<string> HandlePluginsAsync(string? arg, CancellationToken ct)
    {
        var tokens = (arg ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var action = tokens.Length == 0 ? "list" : tokens[0].ToLowerInvariant();
        var rest = tokens.Length > 1 ? tokens[1] : null;

        if (_pluginManager is null)
        {
            // Fallback for hosts that only provide in-memory runtime loading.
            if (action is "list" && _pluginLoader is not null)
            {
                var runtimePlugins = _pluginLoader.GetAll();
                if (runtimePlugins.Count == 0)
                    return "No plugins loaded.";

                var runtimeLines = runtimePlugins.Select(p =>
                    $"  ✓ {p.Id} ({p.Name}) v{p.Version} (loaded {p.LoadedAt:g})");
                return $"Loaded plugins ({runtimePlugins.Count}):\n{string.Join('\n', runtimeLines)}";
            }

            return "Plugin lifecycle manager not available in this host.";
        }

        return action switch
        {
            "list" => await ListPluginsAsync(ct).ConfigureAwait(false),
            "install" => await InstallPluginAsync(rest, ct).ConfigureAwait(false),
            "enable" => await SetPluginEnabledAsync(rest, enabled: true, ct).ConfigureAwait(false),
            "disable" => await SetPluginEnabledAsync(rest, enabled: false, ct).ConfigureAwait(false),
            "update" => await UpdatePluginAsync(rest, ct).ConfigureAwait(false),
            "uninstall" or "remove" => await UninstallPluginAsync(rest, ct).ConfigureAwait(false),
            "info" => await PluginInfoAsync(rest, ct).ConfigureAwait(false),
            _ => "Usage: /plugins [list|install <source>|enable <id>|disable <id>|update [id]|uninstall <id>|info <id>]",
        };
    }

    private async Task<string> ListPluginsAsync(CancellationToken ct)
    {
        var plugins = await _pluginManager!.ListAsync(ct).ConfigureAwait(false);
        if (plugins.Count == 0)
            return "No plugins installed.";

        var lines = plugins.Select(p =>
            $"  {(p.Enabled ? "✓" : "○")} {p.Id} v{p.Version} ({(p.Loaded ? "loaded" : "not loaded")})");
        return $"Plugins ({plugins.Count}):\n{string.Join('\n', lines)}";
    }

    private async Task<string> InstallPluginAsync(string? source, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "Usage: /plugins install <path-or-url>";

        try
        {
            var plugin = await _pluginManager!.InstallAsync(source, enable: true, ct).ConfigureAwait(false);
            return $"Installed plugin '{plugin.Id}' v{plugin.Version} ({(plugin.Loaded ? "loaded" : "installed")}).";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"Install failed: {ex.Message}";
        }
    }

    private async Task<string> SetPluginEnabledAsync(string? id, bool enabled, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return $"Usage: /plugins {(enabled ? "enable" : "disable")} <id>";

        try
        {
            var plugin = enabled
                ? await _pluginManager!.EnableAsync(id, ct).ConfigureAwait(false)
                : await _pluginManager!.DisableAsync(id, ct).ConfigureAwait(false);
            return $"Plugin '{plugin.Id}' {(enabled ? "enabled" : "disabled")}.";
        }
        catch (InvalidOperationException)
        {
            return $"Plugin '{id}' is not installed.";
        }
    }

    private async Task<string> UninstallPluginAsync(string? id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "Usage: /plugins uninstall <id>";

        var removed = await _pluginManager!.UninstallAsync(id, ct).ConfigureAwait(false);
        return removed
            ? $"Plugin '{id}' uninstalled."
            : $"Plugin '{id}' is not installed.";
    }

    private async Task<string> UpdatePluginAsync(string? id, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                var updated = await _pluginManager!.UpdateAllAsync(ct).ConfigureAwait(false);
                return $"Updated {updated.Count} plugin(s).";
            }

            var plugin = await _pluginManager!.UpdateAsync(id, ct).ConfigureAwait(false);
            return $"Updated plugin '{plugin.Id}' to v{plugin.Version}.";
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"Update failed: {ex.Message}";
        }
    }

    private async Task<string> PluginInfoAsync(string? id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "Usage: /plugins info <id>";

        var plugin = await _pluginManager!.GetAsync(id, ct).ConfigureAwait(false);
        if (plugin is null)
            return $"Plugin '{id}' is not installed.";

        var sb = new StringBuilder();
        sb.AppendLine($"Plugin: {plugin.Name} ({plugin.Id})");
        sb.AppendLine($"Version: {plugin.Version}");
        sb.AppendLine($"Enabled: {(plugin.Enabled ? "yes" : "no")}");
        sb.AppendLine($"Loaded: {(plugin.Loaded ? "yes" : "no")}");
        sb.AppendLine($"Source: {plugin.Source}");
        sb.AppendLine($"Install path: {plugin.InstallPath}");
        if (!string.IsNullOrWhiteSpace(plugin.LastError))
            sb.AppendLine($"Last error: {plugin.LastError}");
        return sb.ToString().TrimEnd();
    }

    private async Task<string> HandleCheckpointAsync(string? arg, CancellationToken ct)
    {
        if (_checkpointStrategy == null)
            return "Checkpointing not configured.";

        var subCmd = arg?.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var action = subCmd is { Length: > 0 } ? subCmd[0].ToUpperInvariant() : "LIST";
        var param = subCmd is { Length: > 1 } ? subCmd[1] : null;

        return action switch
        {
            "LIST" or "" => await ListCheckpointsAsync(ct).ConfigureAwait(false),
            "RESTORE" when param != null => await RestoreCheckpointAsync(param, ct).ConfigureAwait(false),
            "RESTORE" => "Usage: /checkpoint restore <id>",
            "CLEAR" => await ClearCheckpointsAsync(ct).ConfigureAwait(false),
            "CREATE" => await CreateCheckpointAsync(param ?? "manual", ct).ConfigureAwait(false),
            _ => "Usage: /checkpoint [list|create|restore <id>|clear]",
        };
    }

    private async Task<string> ListCheckpointsAsync(CancellationToken ct)
    {
        var checkpoints = await _checkpointStrategy!.ListAsync(ct).ConfigureAwait(false);
        if (checkpoints.Count == 0)
            return "No checkpoints found.";

        var lines = checkpoints.Select(c => $"  {c.Id} — {c.Label} ({c.CreatedAt:g})");
        return $"Checkpoints:\n{string.Join('\n', lines)}";
    }

    private async Task<string> RestoreCheckpointAsync(string id, CancellationToken ct)
    {
        var success = await _checkpointStrategy!.RestoreAsync(id, ct).ConfigureAwait(false);
        return success ? $"Restored checkpoint: {id}" : $"Failed to restore checkpoint '{id}'.";
    }

    private async Task<string> ClearCheckpointsAsync(CancellationToken ct)
    {
        await _checkpointStrategy!.ClearAsync(ct).ConfigureAwait(false);
        return "All checkpoints cleared.";
    }

    private async Task<string> CreateCheckpointAsync(string label, CancellationToken ct)
    {
        var id = await _checkpointStrategy!.CreateAsync(label, ct).ConfigureAwait(false);
        return id != null ? $"Checkpoint created: {id}" : "Nothing to checkpoint (no changes).";
    }

    private static string ShowSandboxInfo() =>
        $"Sandbox modes: none (default), restricted, container.\n" +
        $"Configure via JDAI.md: `sandbox: restricted`";

    // ── Workflow commands ─────────────────────────────────

    private async Task<string> HandleWorkflowAsync(string? arg, CancellationToken ct)
    {
        if (_workflowCatalog is null)
            return "Workflow catalog not configured.";

        var subCmd = arg?.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var action = subCmd is { Length: > 0 } ? subCmd[0].ToUpperInvariant() : "LIST";
        var param = subCmd is { Length: > 1 } ? subCmd[1] : null;

        return action switch
        {
            "LIST" or "" => await ListWorkflowsAsync(ct).ConfigureAwait(false),
            "SHOW" when param is not null => await ShowWorkflowAsync(param, ct).ConfigureAwait(false),
            "SHOW" => "Usage: /workflow show <name>",
            "CREATE" when param is not null => await CreateWorkflowAsync(param, ct).ConfigureAwait(false),
            "CREATE" => "Usage: /workflow create <description>  — Generate a workflow from a natural language description",
            "RENAME" when param is not null => await RenameWorkflowAsync(param, ct).ConfigureAwait(false),
            "RENAME" => "Usage: /workflow rename <old-name> <new-name>",
            "REMOVE" or "DELETE" when param is not null => await RemoveWorkflowAsync(param, ct).ConfigureAwait(false),
            "REMOVE" or "DELETE" => "Usage: /workflow remove <name>",
            "COMPOSE" when param is not null => await ComposeWorkflowsAsync(param, ct).ConfigureAwait(false),
            "COMPOSE" => "Usage: /workflow compose <name> <workflow1> <workflow2> [...]  — Combine workflows",
            "DRY-RUN" or "DRYRUN" when param is not null => await DryRunWorkflowAsync(param, ct).ConfigureAwait(false),
            "DRY-RUN" or "DRYRUN" => "Usage: /workflow dry-run <name>  — Preview workflow execution without running",
            "EXPORT" when param is not null => await ExportWorkflowAsync(param, ct).ConfigureAwait(false),
            "EXPORT" => "Usage: /workflow export <name> [json|csharp|mermaid]",
            "REPLAY" when param is not null => await ReplayWorkflowAsync(param, ct).ConfigureAwait(false),
            "REPLAY" => "Usage: /workflow replay <name> [version]",
            "REFINE" when param is not null => await RefineWorkflowAsync(param, ct).ConfigureAwait(false),
            "REFINE" => "Usage: /workflow refine <name> <feedback>  — e.g. /workflow refine my-workflow Add a validation step after step 2",
            "FROM-HISTORY" or "FROMHISTORY" => await ExtractWorkflowFromHistoryAsync(param, ct).ConfigureAwait(false),
            "CATALOG" => await CatalogSharedWorkflowsAsync(param, ct).ConfigureAwait(false),
            "PUBLISH" when param is not null => await PublishWorkflowAsync(param, ct).ConfigureAwait(false),
            "PUBLISH" => "Usage: /workflow publish <name>",
            "INSTALL" when param is not null => await InstallWorkflowAsync(param, ct).ConfigureAwait(false),
            "INSTALL" => "Usage: /workflow install <name[@version]>",
            "SEARCH" when param is not null => await SearchWorkflowsAsync(param, ct).ConfigureAwait(false),
            "SEARCH" => "Usage: /workflow search <query>",
            "VERSIONS" when param is not null => await ShowWorkflowVersionsAsync(param, ct).ConfigureAwait(false),
            "VERSIONS" => "Usage: /workflow versions <name>",
            _ => "Usage: /workflow [list|show|create|rename|remove|compose|dry-run|export|replay|refine|from-history|catalog|publish|install|search|versions]",
        };
    }

    private async Task<string> ListWorkflowsAsync(CancellationToken ct)
    {
        var workflows = await _workflowCatalog!.ListAsync(ct).ConfigureAwait(false);
        if (workflows.Count == 0)
            return "No workflows in catalog. Workflows are captured automatically during multi-step executions.";

        var lines = workflows.Select(w =>
        {
            var tags = w.Tags.Count > 0 ? $" [{string.Join(", ", w.Tags)}]" : "";
            return $"  {w.Name} v{w.Version}{tags} — {w.Description}";
        });
        return $"Workflows ({workflows.Count}):\n{string.Join('\n', lines)}";
    }

    private async Task<string> ShowWorkflowAsync(string name, CancellationToken ct)
    {
        var workflow = await _workflowCatalog!.GetAsync(name, ct: ct).ConfigureAwait(false);
        if (workflow is null)
            return $"Workflow '{name}' not found.";

        var artifact = _workflowEmitter.Emit(workflow, WorkflowExportFormat.Json);
        return $"Workflow: {workflow.Name} v{workflow.Version}\n{artifact.Content}";
    }

    private async Task<string> RenameWorkflowAsync(string param, CancellationToken ct)
    {
        var parts = param.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return "Usage: /workflow rename <old-name> <new-name>";

        var oldName = parts[0];
        var newName = parts[1];

        var workflow = await _workflowCatalog!.GetAsync(oldName, ct: ct).ConfigureAwait(false);
        if (workflow is null)
            return $"Workflow '{oldName}' not found.";

        workflow.Name = newName;
        await _workflowCatalog.SaveAsync(workflow, ct).ConfigureAwait(false);
        await _workflowCatalog.DeleteAsync(oldName, ct: ct).ConfigureAwait(false);

        return $"Renamed workflow '{oldName}' to '{newName}'.";
    }

    private async Task<string> RemoveWorkflowAsync(string name, CancellationToken ct)
    {
        var deleted = await _workflowCatalog!.DeleteAsync(name.Trim(), ct: ct).ConfigureAwait(false);
        return deleted
            ? $"Removed workflow '{name.Trim()}'."
            : $"Workflow '{name.Trim()}' not found.";
    }

    private async Task<string> ExportWorkflowAsync(string param, CancellationToken ct)
    {
        var parts = param.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var name = parts[0];
        var formatStr = parts.Length > 1 ? parts[1].ToUpperInvariant() : "JSON";

        var format = formatStr switch
        {
            "CSHARP" or "CS" => WorkflowExportFormat.CSharp,
            "MERMAID" => WorkflowExportFormat.Mermaid,
            _ => WorkflowExportFormat.Json,
        };

        var workflow = await _workflowCatalog!.GetAsync(name, ct: ct).ConfigureAwait(false);
        if (workflow is null)
            return $"Workflow '{name}' not found.";

        var artifact = _workflowEmitter.Emit(workflow, format);
        return $"# {workflow.Name} v{workflow.Version} ({format})\n\n{artifact.Content}";
    }

    private async Task<string> ReplayWorkflowAsync(string param, CancellationToken ct)
    {
        var parts = param.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var name = parts[0];
        var version = parts.Length > 1 ? parts[1] : null;

        var workflow = await _workflowCatalog!.GetAsync(name, version, ct).ConfigureAwait(false);
        if (workflow is null)
            return version is not null
                ? $"Workflow '{name}' v{version} not found."
                : $"Workflow '{name}' not found.";

        var steps = FlattenSteps(workflow.Steps, indent: 0);
        return $"Replay plan for {workflow.Name} v{workflow.Version}:\n{steps}\n\n" +
               "(Dry-run mode — pass the prompt to the agent to execute live.)";
    }

    private async Task<string> RefineWorkflowAsync(string param, CancellationToken ct)
    {
        // Parse: /workflow refine <name> <feedback>
        var parts = param.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var name = parts[0];
        var feedback = parts.Length > 1 ? parts[1] : null;

        if (string.IsNullOrWhiteSpace(feedback))
            return $"Usage: /workflow refine {name} <describe what to change>\n" +
                   "Example: /workflow refine my-workflow Add a validation step after cloning";

        var workflow = await _workflowCatalog!.GetAsync(name, ct: ct).ConfigureAwait(false);
        if (workflow is null)
            return $"Workflow '{name}' not found. Use '/workflow list' to see available workflows.";

        if (_session?.Kernel is null)
            return "No active model/kernel. Select a model first with /model.";

        var generator = new WorkflowGenerator();
        var result = await generator.RefineAsync(workflow, feedback, _session.Kernel, ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            return $"⚠️ Refinement failed: {result.Changelog}\n" +
                   (result.RawResponse is not null ? $"Raw response:\n{result.RawResponse[..Math.Min(500, result.RawResponse.Length)]}" : "");
        }

        // Save refined version
        await _workflowCatalog.SaveAsync(result.Workflow, ct).ConfigureAwait(false);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"✅ Refined '{result.Workflow.Name}' → v{result.Workflow.Version}");
        sb.AppendLine($"   Changes: {result.Changelog}");
        sb.AppendLine();
        sb.AppendLine("Updated steps:");
        sb.Append(FlattenSteps(result.Workflow.Steps, 1));
        sb.AppendLine();
        sb.AppendLine($"Use '/workflow show {result.Workflow.Name}' to view JSON, '/workflow dry-run {result.Workflow.Name}' to preview.");
        return sb.ToString().TrimEnd();
    }

    private async Task<string> ExtractWorkflowFromHistoryAsync(string? param, CancellationToken ct)
    {
        // Parse: /workflow from-history [N] [--name <name>]
        var turnCount = 10;
        string? name = null;

        if (param is not null)
        {
            var parts = param.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var i in Enumerable.Range(0, parts.Length))
            {
                if (int.TryParse(parts[i], out var n))
                    turnCount = n;
                else if (parts[i].Equals("--name", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
                    name = parts[i + 1];
            }
        }

        if (_session?.History is null || _session.History.Count == 0)
            return "No conversation history to extract from.";

        var messages = _session.History.ToList().AsReadOnly();
        var generator = new WorkflowGenerator();
        var workflow = generator.ExtractFromHistory(messages, turnCount, name);

        if (workflow.Steps.Count == 0)
            return $"No tool calls found in the last {turnCount} turns. Try a larger number.";

        await _workflowCatalog!.SaveAsync(workflow, ct).ConfigureAwait(false);

        var emitter = _workflowEmitter;
        var mermaid = emitter.Emit(workflow, WorkflowExportFormat.Mermaid);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"✅ Extracted workflow '{workflow.Name}' ({workflow.Steps.Count} steps) from last {turnCount} turns");
        if (workflow.Tags.Count > 0)
            sb.AppendLine($"   Tags: {string.Join(", ", workflow.Tags)}");
        sb.AppendLine();
        sb.AppendLine("Steps:");
        sb.Append(FlattenSteps(workflow.Steps, 1));
        sb.AppendLine();
        sb.AppendLine("Diagram:");
        sb.AppendLine(mermaid.Content);
        sb.AppendLine();
        sb.AppendLine($"Use '/workflow refine {workflow.Name} <feedback>' to adjust, '/workflow dry-run {workflow.Name}' to preview.");
        return sb.ToString().TrimEnd();
    }

    // ── Shared workflow store commands ────────────────────────

    private static string WorkflowStoreNotConfigured =>
        "Shared workflow store not configured. Inject an IWorkflowStore implementation to enable store commands.";

    private async Task<string> CatalogSharedWorkflowsAsync(string? param, CancellationToken ct)
    {
        if (_workflowStore is null)
            return WorkflowStoreNotConfigured;

        // Parse optional tag= or author= filters from param
        string? tag = null;
        string? author = null;
        if (param is not null)
        {
            foreach (var part in param.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.StartsWith("tag=", StringComparison.OrdinalIgnoreCase))
                    tag = part[4..];
                else if (part.StartsWith("author=", StringComparison.OrdinalIgnoreCase))
                    author = part[7..];
            }
        }

        var workflows = await _workflowStore.CatalogAsync(tag, author, ct).ConfigureAwait(false);
        if (workflows.Count == 0)
            return "No shared workflows in the store catalog.";

        var lines = workflows.Select(w =>
        {
            var tags = w.Tags.Count > 0 ? $" [{string.Join(", ", w.Tags)}]" : "";
            var vis = w.Visibility != WorkflowVisibility.Team ? $" ({w.Visibility})" : "";
            return $"  {w.Name} v{w.Version}{tags}{vis} — {w.Author} — {w.Description}";
        });
        return $"Shared Workflow Catalog ({workflows.Count}):\n{string.Join('\n', lines)}";
    }

    private async Task<string> PublishWorkflowAsync(string name, CancellationToken ct)
    {
        if (_workflowStore is null)
            return WorkflowStoreNotConfigured;

        if (_workflowCatalog is null)
            return "Workflow catalog not configured.";

        // Enforce workflow publish RBAC policy
        if (_policyEvaluator is not null)
        {
            var ctx = new PolicyContext(UserId: Environment.UserName);
            var rbacResult = _policyEvaluator.EvaluateWorkflowPublish(ctx);
            if (rbacResult.Decision == PolicyDecision.Deny)
                return $"⛔ Publish denied: {rbacResult.Reason}";
        }

        var definition = await _workflowCatalog.GetAsync(name, ct: ct).ConfigureAwait(false);
        if (definition is null)
            return $"Workflow '{name}' not found in local catalog.";

        var artifact = _workflowEmitter.Emit(definition, WorkflowExportFormat.Json);

        var shared = new SharedWorkflow
        {
            Name = definition.Name,
            Version = definition.Version,
            Description = definition.Description,
            Author = Environment.UserName,
            PublishedAt = DateTimeOffset.UtcNow,
            Tags = definition.Tags,
            DefinitionJson = artifact.Content,
        };

        await _workflowStore.PublishAsync(shared, ct).ConfigureAwait(false);
        return $"Published '{definition.Name}' v{definition.Version} to shared store.";
    }

    private async Task<string> InstallWorkflowAsync(string param, CancellationToken ct)
    {
        if (_workflowStore is null)
            return WorkflowStoreNotConfigured;

        // Parse name[@version]
        string nameOrId;
        string? version = null;
        var atIndex = param.IndexOf('@');
        if (atIndex >= 0)
        {
            nameOrId = param[..atIndex].Trim();
            version = param[(atIndex + 1)..].Trim();
        }
        else
        {
            nameOrId = param.Trim();
        }

        var localDir = Path.Combine(DataDirectories.Root, "workflows");

        var installed = await _workflowStore.InstallAsync(nameOrId, version, localDir, ct)
            .ConfigureAwait(false);

        return installed
            ? $"Installed '{nameOrId}'{(version is not null ? $" v{version}" : "")} to {localDir}"
            : $"Workflow '{nameOrId}'{(version is not null ? $" v{version}" : "")} not found in store.";
    }

    private async Task<string> SearchWorkflowsAsync(string query, CancellationToken ct)
    {
        if (_workflowStore is null)
            return WorkflowStoreNotConfigured;

        var results = await _workflowStore.SearchAsync(query, ct).ConfigureAwait(false);
        if (results.Count == 0)
            return $"No workflows found matching '{query}'.";

        var lines = results.Select(w =>
        {
            var tags = w.Tags.Count > 0 ? $" [{string.Join(", ", w.Tags)}]" : "";
            return $"  {w.Name} v{w.Version}{tags} — {w.Author} — {w.Description}";
        });
        return $"Search results for '{query}' ({results.Count}):\n{string.Join('\n', lines)}";
    }

    private async Task<string> ShowWorkflowVersionsAsync(string name, CancellationToken ct)
    {
        if (_workflowStore is null)
            return WorkflowStoreNotConfigured;

        var versions = await _workflowStore.VersionsAsync(name, ct).ConfigureAwait(false);
        if (versions.Count == 0)
            return $"No versions found for workflow '{name}'.";

        var lines = versions.Select(w =>
            $"  v{w.Version} — published {w.PublishedAt:yyyy-MM-dd HH:mm} UTC by {w.Author}");
        return $"Versions of '{name}' ({versions.Count}):\n{string.Join('\n', lines)}";
    }

    private static string FlattenSteps(IEnumerable<AgentStepDefinition> steps, int indent)
    {
        var sb = new System.Text.StringBuilder();
        var pad = new string(' ', indent * 2);
        foreach (var step in steps)
        {
            var prefix = step.Kind switch
            {
                AgentStepKind.Skill => "▶ Skill",
                AgentStepKind.Tool => "» Tool",
                AgentStepKind.Nested => "» Nested",
                AgentStepKind.Loop => "↻ Loop",
                AgentStepKind.Conditional => "❖ If",
                _ => "•",
            };
            sb.AppendLine($"{pad}{prefix}: {step.Name}");
            if (step.SubSteps.Count > 0)
                sb.Append(FlattenSteps(step.SubSteps, indent + 1));
        }

        return sb.ToString();
    }

    private async Task<string> CreateWorkflowAsync(string description, CancellationToken ct)
    {
        // Parse optional --name flag: /workflow create --name my-wf Build, test, deploy
        string? name = null;
        var desc = description;
        if (desc.StartsWith("--name ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = desc.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                name = parts[1];
                desc = parts[2];
            }
        }

        // Collect available tools for the LLM prompt
        HashSet<string>? availableTools = null;
        if (_session?.Kernel is not null)
        {
            availableTools = [];
            foreach (var plugin in _session.Kernel.Plugins)
                foreach (var fn in plugin)
                    availableTools.Add($"{plugin.Name}-{fn.Name}");
        }

        var generator = new WorkflowGenerator();
        var result = await generator.GenerateAsync(desc, _session!.Kernel, name, availableTools, ct)
            .ConfigureAwait(false);

        var workflow = result.Workflow;

        if (!result.Success)
        {
            return $"⚠ LLM generation failed — used heuristic fallback.\n" +
                   $"  Reason: {result.Changelog}\n" +
                   $"  Use '/workflow refine {workflow.Name} <feedback>' to improve.";
        }

        // Dry-run validation before saving
        var dryRun = generator.DryRun(workflow, availableTools);
        if (dryRun.MissingTools.Count > 0)
        {
            var warning = new System.Text.StringBuilder();
            warning.AppendLine($"⚠ Generated workflow '{workflow.Name}' references {dryRun.MissingTools.Count} unavailable tool(s); not saved.");
            warning.AppendLine($"  Missing: {string.Join(", ", dryRun.MissingTools)}");
            warning.AppendLine();
            warning.AppendLine("Preview:");
            warning.Append(FlattenSteps(workflow.Steps, 1));
            warning.AppendLine();
            warning.AppendLine("Refine the description and run /workflow create again.");
            return warning.ToString().TrimEnd();
        }

        await _workflowCatalog!.SaveAsync(workflow, ct).ConfigureAwait(false);

        var emitter = _workflowEmitter;
        var mermaid = emitter.Emit(workflow, WorkflowExportFormat.Mermaid);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"✅ Created workflow '{workflow.Name}' v{workflow.Version} ({workflow.Steps.Count} steps)");
        if (workflow.Tags.Count > 0)
            sb.AppendLine($"   Tags: {string.Join(", ", workflow.Tags)}");
        sb.AppendLine();
        sb.AppendLine("Steps:");
        sb.Append(FlattenSteps(workflow.Steps, 1));
        sb.AppendLine();
        sb.AppendLine("Diagram:");
        sb.AppendLine(mermaid.Content);
        sb.AppendLine();
        sb.AppendLine($"Use '/workflow show {workflow.Name}' to view JSON, '/workflow dry-run {workflow.Name}' to preview execution.");
        return sb.ToString().TrimEnd();
    }

    private async Task<string> DryRunWorkflowAsync(string name, CancellationToken ct)
    {
        var workflow = await _workflowCatalog!.GetAsync(name, ct: ct).ConfigureAwait(false);
        if (workflow is null)
            return $"Workflow '{name}' not found. Use '/workflow list' to see available workflows.";

        // Collect available tool names from the kernel
        HashSet<string>? availableTools = null;
        if (_session?.Kernel is not null)
        {
            availableTools = [];
            foreach (var plugin in _session.Kernel.Plugins)
            {
                foreach (var fn in plugin)
                    availableTools.Add($"{plugin.Name}-{fn.Name}");
            }
        }

        var generator = new WorkflowGenerator();
        var result = generator.DryRun(workflow, availableTools);
        return WorkflowGenerator.FormatDryRun(result);
    }

    private async Task<string> ComposeWorkflowsAsync(string param, CancellationToken ct)
    {
        var parts = param.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
            return "Usage: /workflow compose <name> <workflow1> <workflow2> [...]";

        var compositeName = parts[0];
        var workflowNames = parts[1..];
        var workflows = new List<AgentWorkflowDefinition>();
        var missing = new List<string>();

        foreach (var wfName in workflowNames)
        {
            var wf = await _workflowCatalog!.GetAsync(wfName, ct: ct).ConfigureAwait(false);
            if (wf is null)
                missing.Add(wfName);
            else
                workflows.Add(wf);
        }

        if (missing.Count > 0)
            return $"Workflows not found: {string.Join(", ", missing)}. Use '/workflow list' to see available.";

        var generator = new WorkflowGenerator();
        var composite = generator.Compose(compositeName, workflows);
        await _workflowCatalog!.SaveAsync(composite, ct).ConfigureAwait(false);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"✅ Composed '{compositeName}' from {workflows.Count} workflows:");
        foreach (var wf in workflows)
            sb.AppendLine($"  → {wf.Name} ({wf.Steps.Count} steps)");
        sb.AppendLine($"\nTotal steps: {composite.Steps.Sum(s => CountStepsRecursive(s))}");
        sb.AppendLine($"Use '/workflow dry-run {compositeName}' to preview execution.");
        return sb.ToString().TrimEnd();
    }

    private static int CountStepsRecursive(AgentStepDefinition step)
    {
        var count = 1;
        foreach (var sub in step.SubSteps)
            count += CountStepsRecursive(sub);
        return count;
    }

    // ── Spinner/progress style ──────────────────────────────

    private string HandleSpinner(string? arg)
    {
        if (_onSpinnerStyleChanged is null || _getSpinnerStyle is null)
            return "Spinner style is not configurable in this context.";

        if (string.IsNullOrWhiteSpace(arg))
        {
            var current = _getSpinnerStyle();
            var styles = string.Join(", ", Enum.GetNames<SpinnerStyle>()
                .Select(s => s.ToLowerInvariant()));
            return $"Current spinner style: {current.ToString().ToLowerInvariant()}\n" +
                   $"Available: {styles}\n" +
                   "Usage: /spinner <style>";
        }

        if (!Enum.TryParse<SpinnerStyle>(arg.Trim(), ignoreCase: true, out var style))
        {
            var styles = string.Join(", ", Enum.GetNames<SpinnerStyle>()
                .Select(s => s.ToLowerInvariant()));
            return $"Unknown style: '{arg}'. Available: {styles}";
        }

        _onSpinnerStyleChanged(style);

        // Persist to settings file
        var settings = new TuiSettings { SpinnerStyle = style };
        try { settings.Save(); }
#pragma warning disable CA1031 // Best-effort save
        catch { /* non-critical — persist is best-effort */ }
#pragma warning restore CA1031

        return $"Spinner style set to: {style.ToString().ToLowerInvariant()}";
    }

    private string HandleReasoning(string? arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
        {
            var level = ToReasoningToken(_session.ReasoningEffortOverride);
            var support = SupportsReasoningEffort(_session.CurrentModel)
                ? "supported by current model/provider"
                : "may be ignored by current model/provider";
            return $"Reasoning effort: {level} ({support}). Usage: /reasoning [auto|none|low|medium|high|max]";
        }

        var token = arg.Trim().ToLowerInvariant();
        ReasoningEffort? selected = token switch
        {
            "auto" => null,
            "none" => ReasoningEffort.None,
            "low" => ReasoningEffort.Low,
            "medium" => ReasoningEffort.Medium,
            "high" => ReasoningEffort.High,
            "max" => ReasoningEffort.Max,
            _ => null,
        };

        if (selected is null && !string.Equals(token, "auto", StringComparison.Ordinal))
            return "Usage: /reasoning [auto|none|low|medium|high|max]";

        _session.ReasoningEffortOverride = selected;

        var warning = !SupportsReasoningEffort(_session.CurrentModel) && selected is not null
            ? "\n⚠ Current model may ignore this setting."
            : string.Empty;

        return $"Reasoning effort set to: {ToReasoningToken(selected)}.{warning}";
    }

    private static string ToReasoningToken(ReasoningEffort? effort) =>
        effort?.ToString().ToLowerInvariant() ?? "auto";

    internal static bool SupportsReasoningEffort(ProviderModelInfo? model)
    {
        if (model is null)
            return false;

        var provider = model.ProviderName.ToLowerInvariant();
        var id = model.Id.ToLowerInvariant();

        if (provider.Contains("openai", StringComparison.Ordinal) ||
            provider.Contains("anthropic", StringComparison.Ordinal) ||
            provider.Contains("claude", StringComparison.Ordinal) ||
            provider.Contains("gemini", StringComparison.Ordinal) ||
            provider.Contains("google", StringComparison.Ordinal))
        {
            return true;
        }

        return id.StartsWith("o1", StringComparison.Ordinal) ||
               id.StartsWith("o3", StringComparison.Ordinal) ||
               id.StartsWith("o4", StringComparison.Ordinal) ||
               id.Contains("reasoning", StringComparison.Ordinal) ||
               id.Contains("qwq", StringComparison.Ordinal) ||
               id.Contains("deepseek-r1", StringComparison.Ordinal) ||
               id.Contains("grok-3-mini", StringComparison.Ordinal);
    }

    private async Task<string> HandleLocalModelAsync(string? arg, CancellationToken ct)
    {
        var parts = (arg ?? "").Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var subCommand = parts.Length > 0 ? parts[0].ToUpperInvariant() : "HELP";
        var subArg = parts.Length > 1 ? parts[1] : null;

        // Find the LocalModelDetector in our registry
        var providers = await DetectProvidersAsync(forceRefresh: true, ct).ConfigureAwait(false);
        var localProvider = providers.FirstOrDefault(p =>
            string.Equals(p.Name, "Local", StringComparison.OrdinalIgnoreCase));

        return subCommand switch
        {
            "LIST" => FormatLocalModelList(localProvider),
            "ADD" => await AddLocalModelAsync(subArg, ct).ConfigureAwait(false),
            "SCAN" => await ScanLocalModelsAsync(subArg, ct).ConfigureAwait(false),
            "REMOVE" => RemoveLocalModel(subArg),
            "SEARCH" => await SearchHuggingFaceAsync(subArg, ct).ConfigureAwait(false),
            "DOWNLOAD" => await DownloadModelAsync(subArg, ct).ConfigureAwait(false),
            _ => """
                /local commands:
                  /local list              — List registered local models
                  /local add <path>        — Register a GGUF file or directory
                  /local scan [dir]        — Scan directory for GGUF files
                  /local remove <id>       — Remove a model from the registry
                  /local search <query>    — Search HuggingFace for GGUF models
                  /local download <repo>   — Download a model from HuggingFace
                """,
        };
    }

    private static string FormatLocalModelList(Core.Providers.ProviderInfo? localProvider)
    {
        if (localProvider is null || !localProvider.IsAvailable || localProvider.Models.Count == 0)
            return "No local models registered. Use /local add <path> or /local download <repo>.";

        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"Local models ({localProvider.Models.Count}):");
        foreach (var m in localProvider.Models)
        {
            lines.AppendLine($"  {m.Capabilities.ToBadge()} {m.Id} — {m.DisplayName}");
        }

        return lines.ToString().TrimEnd();
    }

    private async Task<string> AddLocalModelAsync(string? path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "Usage: /local add <path-to-gguf-file-or-directory>";

        var detector = FindLocalDetector();
        if (detector is null) return "Local model provider not available.";

        if (Directory.Exists(path))
        {
            await detector.Registry.ScanDirectoryAsync(path, ct).ConfigureAwait(false);
        }
        else if (File.Exists(path))
        {
            await detector.Registry.AddFileAsync(path, ct).ConfigureAwait(false);
        }
        else
        {
            return $"Path not found: {path}";
        }

        await detector.Registry.SaveAsync(ct).ConfigureAwait(false);
        return $"Added models from: {path}. Use /models to select one.";
    }

    private async Task<string> ScanLocalModelsAsync(string? dir, CancellationToken ct)
    {
        var detector = FindLocalDetector();
        if (detector is null) return "Local model provider not available.";

        await detector.Registry.ScanDirectoryAsync(dir, ct).ConfigureAwait(false);
        await detector.Registry.SaveAsync(ct).ConfigureAwait(false);
        return $"Scanned {dir ?? detector.Registry.ModelsDirectory}. Found {detector.Registry.Models.Count} model(s).";
    }

    private string RemoveLocalModel(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "Usage: /local remove <model-id>";

        var detector = FindLocalDetector();
        if (detector is null) return "Local model provider not available.";

        return detector.Registry.Remove(id)
            ? $"Removed model: {id}"
            : $"Model not found: {id}";
    }

    private static async Task<string> SearchHuggingFaceAsync(string? query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Usage: /local search <query> (e.g., /local search llama 7b)";

        var source = new Core.LocalModels.Sources.HuggingFaceModelSource(string.Empty);
        var results = await source.SearchAsync(query, limit: 10, ct: ct).ConfigureAwait(false);

        if (results.Count == 0)
            return "No GGUF models found on HuggingFace for that query.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"HuggingFace GGUF models for '{query}':");
        foreach (var r in results)
        {
            sb.AppendLine($"  • {r.Id ?? r.ModelId} ({r.Downloads:N0} downloads)");
        }

        sb.AppendLine("Use /local download <repo-id> to download.");
        return sb.ToString().TrimEnd();
    }

    private async Task<string> DownloadModelAsync(string? repoId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(repoId))
            return "Usage: /local download <repo-id> (e.g., /local download TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF)";

        var detector = FindLocalDetector();
        if (detector is null) return "Local model provider not available.";

        var downloader = new Core.LocalModels.ModelDownloader(detector.Registry.ModelsDirectory);

        var model = await downloader.DownloadFromHuggingFaceAsync(repoId, ct: ct).ConfigureAwait(false);
        detector.Registry.Add(model);
        await detector.Registry.SaveAsync(ct).ConfigureAwait(false);

        return $"Downloaded: {model.DisplayName} ({model.FileSizeBytes / (1024.0 * 1024.0):F1} MB). Use /models to select it.";
    }

    private Core.LocalModels.LocalModelDetector? FindLocalDetector()
    {
        if (_registry is ProviderRegistry pr)
        {
            return pr.GetDetector("Local") as Core.LocalModels.LocalModelDetector;
        }

        return null;
    }

    // ── /mcp ─────────────────────────────────────────────────────────────────

    private async Task<string> HandleMcpAsync(string? arg, CancellationToken ct)
    {
        var parts = arg?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries) ?? [];
        var sub = parts.Length > 0 ? parts[0].ToLowerInvariant() : "list";
        var rest = parts.Length > 1 ? parts[1] : null;

        return sub switch
        {
            "list" => await McpListAsync(ct).ConfigureAwait(false),
            "add" => await McpAddAsync(rest, ct).ConfigureAwait(false),
            "remove" => await McpRemoveAsync(rest, ct).ConfigureAwait(false),
            "enable" => await McpSetEnabledAsync(rest, true, ct).ConfigureAwait(false),
            "disable" => await McpSetEnabledAsync(rest, false, ct).ConfigureAwait(false),
            _ => McpHelp(),
        };
    }

    private async Task<string> McpListAsync(CancellationToken ct)
    {
        var servers = await _mcpManager.GetAllServersAsync(ct).ConfigureAwait(false);

        if (servers.Count == 0)
        {
            return """
                No MCP servers configured.
                Add one with: /mcp add <name> --transport stdio --command <cmd> [--args <arg1> <arg2>]
                           or: /mcp add <name> --transport http <url>
                """;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"MCP servers ({servers.Count}):");
        sb.AppendLine();

        // Group by scope+source; order Project → User → BuiltIn (higher precedence first)
        var groups = servers
            .GroupBy(s => (s.Scope, s.SourceProvider, s.SourcePath))
            .OrderByDescending(g => ScopePriority(g.Key.Scope));

        foreach (var group in groups)
        {
            var (scope, provider, path) = group.Key;
            var label = scope switch
            {
                McpScope.Project => $"Project MCPs ({path ?? provider})",
                McpScope.BuiltIn => "Built-in MCPs (always available)",
                _ => $"User MCPs ({path ?? provider})",
            };
            sb.AppendLine($"  {label}");

            foreach (var s in group)
            {
                var status = _mcpManager.GetStatus(s.Name);
                // If disabled, always show the disabled icon/state regardless of cached status.
                var displayIcon = s.IsEnabled ? status.Icon : "○";
                var displayState = s.IsEnabled
                    ? status.State.ToString().ToLowerInvariant()
                    : "disabled";
                sb.AppendLine($"    {s.Name} · {displayIcon} {displayState}");
            }

            sb.AppendLine();
        }

        sb.Append("Use /mcp add|remove|enable|disable for management.");
        return sb.ToString();
    }

    private async Task<string> McpAddAsync(string? args, CancellationToken ct)
    {
        // Usage: add <name> --transport stdio --command <cmd> [--args arg1 arg2...]
        //        add <name> --transport http <url>
        if (string.IsNullOrWhiteSpace(args))
            return McpAddUsage();

        var tokens = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 3)
            return McpAddUsage();

        var name = tokens[0];
        var transportIdx = Array.FindIndex(tokens, t =>
            string.Equals(t, "--transport", StringComparison.OrdinalIgnoreCase));

        if (transportIdx < 0 || transportIdx + 1 >= tokens.Length)
            return McpAddUsage();

        var transportStr = tokens[transportIdx + 1];
        McpServerDefinition server;

        if (string.Equals(transportStr, "http", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(transportStr, "https", StringComparison.OrdinalIgnoreCase))
        {
            var urlIdx = transportIdx + 2;
            if (urlIdx >= tokens.Length)
                return "Usage: /mcp add <name> --transport http <url>";

            var url = tokens[urlIdx];
            server = new McpServerDefinition(
                name: name,
                displayName: name,
                transport: McpTransportType.Http,
                scope: McpScope.User,
                sourceProvider: "JD.AI",
                sourcePath: null,
                url: Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl) ? parsedUrl : null,
                command: null,
                args: null,
                env: null,
                isEnabled: true);
        }
        else if (string.Equals(transportStr, "stdio", StringComparison.OrdinalIgnoreCase))
        {
            var cmdIdx = Array.FindIndex(tokens, t =>
                string.Equals(t, "--command", StringComparison.OrdinalIgnoreCase));

            if (cmdIdx < 0 || cmdIdx + 1 >= tokens.Length)
                return "Usage: /mcp add <name> --transport stdio --command <cmd> [--args arg1 arg2...]";

            var command = tokens[cmdIdx + 1];

            var argStartIdx = Array.FindIndex(tokens, t =>
                string.Equals(t, "--args", StringComparison.OrdinalIgnoreCase));

            var serverArgs = argStartIdx >= 0
                ? tokens[(argStartIdx + 1)..].ToList()
                : (IReadOnlyList<string>)[];

            server = new McpServerDefinition(
                name: name,
                displayName: name,
                transport: McpTransportType.Stdio,
                scope: McpScope.User,
                sourceProvider: "JD.AI",
                sourcePath: null,
                url: null,
                command: command,
                args: serverArgs,
                env: null,
                isEnabled: true);
        }
        else
        {
            return $"Unknown transport '{transportStr}'. Use stdio or http.";
        }

        await _mcpManager.AddOrUpdateAsync(server, ct).ConfigureAwait(false);
        return $"Added MCP server '{name}' ({transportStr}).";
    }

    private async Task<string> McpRemoveAsync(string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Usage: /mcp remove <name>";

        await _mcpManager.RemoveAsync(name.Trim(), ct).ConfigureAwait(false);
        return $"Removed MCP server '{name.Trim()}' (if it existed in JD.AI config).";
    }

    private async Task<string> McpSetEnabledAsync(string? name, bool enabled, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return $"Usage: /mcp {(enabled ? "enable" : "disable")} <name>";

        await _mcpManager.SetEnabledAsync(name.Trim(), enabled, ct).ConfigureAwait(false);
        return $"MCP server '{name.Trim()}' {(enabled ? "enabled" : "disabled")}.";
    }

    private static string McpHelp() => """
        /mcp commands:
          /mcp list                    — List all configured MCP servers
          /mcp add <name> --transport stdio --command <cmd> [--args ...]
                                       — Add a stdio MCP server
          /mcp add <name> --transport http <url>
                                       — Add an HTTP MCP server
          /mcp remove <name>           — Remove a JD.AI-managed MCP server
          /mcp enable <name>           — Enable a JD.AI-managed MCP server
          /mcp disable <name>          — Disable a JD.AI-managed MCP server
        """;

    private static string McpAddUsage() => """
        Usage:
          /mcp add <name> --transport stdio --command <cmd> [--args arg1 arg2...]
          /mcp add <name> --transport http <url>
        """;

    private static int ScopePriority(McpScope scope) => scope switch
    {
        McpScope.BuiltIn => 0,
        McpScope.User => 1,
        McpScope.Project => 2,
        _ => -1,
    };
    // ── New parity commands ─────────────────────────────────

    private string GetContextUsage()
    {
        var used = JD.SemanticKernel.Extensions.Compaction.TokenEstimator.EstimateTokens(_session.History);
        var max = _session.CurrentModel?.ContextWindowTokens ?? 128_000;
        var pct = (double)used / max * 100;
        var filledCount = (int)(pct / 2);
        if (filledCount > 50) filledCount = 50;
        var bar = new string('█', filledCount) + new string('░', 50 - filledCount);
        return $"Context: [{bar}] {used:N0}/{max:N0} tokens ({pct:F1}%)\nUse /prompt to inspect message-level context.";
    }

    private string HandleSystemPrompt(string? arg)
    {
        _session.CaptureOriginalSystemPromptIfUnset();
        var current = GetCurrentSystemPromptText();

        if (string.IsNullOrWhiteSpace(arg))
        {
            if (string.IsNullOrWhiteSpace(current))
                return "No system prompt is currently loaded.";

            return $"System prompt ({_session.SystemPromptTokens:N0} tokens):\n\n{current}";
        }

        var trimmed = arg.Trim();
        if (string.Equals(trimmed, "reset", StringComparison.OrdinalIgnoreCase))
        {
            if (!_session.TryResetSystemPrompt())
                return "No original startup system prompt is available to reset.";

            return $"System prompt reset to original startup text ({_session.SystemPromptTokens:N0} tokens).";
        }

        if (string.Equals(trimmed, "edit", StringComparison.OrdinalIgnoreCase))
        {
            return EditSystemPromptInEditor();
        }

        const string appendPrefix = "append ";
        if (trimmed.StartsWith(appendPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var text = trimmed[appendPrefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(text))
                return "Usage: /system-prompt append <text>";

            var before = _session.SystemPromptTokens;
            var next = string.IsNullOrWhiteSpace(current) ? text : $"{current}\n\n{text}";
            _session.ReplaceSystemPrompt(next);
            return FormatSystemPromptUpdateResult("Appended to", before, _session.SystemPromptTokens);
        }

        const string prependPrefix = "prepend ";
        if (trimmed.StartsWith(prependPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var text = trimmed[prependPrefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(text))
                return "Usage: /system-prompt prepend <text>";

            var before = _session.SystemPromptTokens;
            var next = string.IsNullOrWhiteSpace(current) ? text : $"{text}\n\n{current}";
            _session.ReplaceSystemPrompt(next);
            return FormatSystemPromptUpdateResult("Prepended to", before, _session.SystemPromptTokens);
        }

        const string replacePrefix = "replace ";
        if (trimmed.StartsWith(replacePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var target = trimmed[replacePrefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(target))
                return "Usage: /system-prompt replace <file-path|text>";

            var cleanedTarget = target.Trim().Trim('"');
            var replacement = File.Exists(cleanedTarget)
                ? File.ReadAllText(cleanedTarget)
                : target;

            if (string.IsNullOrWhiteSpace(replacement))
                return "Replacement system prompt cannot be empty.";

            var before = _session.SystemPromptTokens;
            _session.ReplaceSystemPrompt(replacement);
            return FormatSystemPromptUpdateResult("Replaced", before, _session.SystemPromptTokens);
        }

        return "Usage: /system-prompt [append <text>|prepend <text>|replace <file-path|text>|reset|edit]";
    }

    private async Task<string> HandlePromptAsync(string? arg, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return RenderPromptView(full: false);

        var trimmed = arg.Trim();
        if (string.Equals(trimmed, "--full", StringComparison.OrdinalIgnoreCase))
            return RenderPromptView(full: true);

        const string dropPrefix = "drop ";
        if (trimmed.StartsWith(dropPrefix, StringComparison.OrdinalIgnoreCase))
            return DropPromptMessages(trimmed[dropPrefix.Length..].Trim());

        const string injectPrefix = "inject ";
        if (trimmed.StartsWith(injectPrefix, StringComparison.OrdinalIgnoreCase))
            return InjectPromptMessage(trimmed[injectPrefix.Length..].Trim());

        if (string.Equals(trimmed, "export", StringComparison.OrdinalIgnoreCase))
            return await ExportPromptAsync(null, ct).ConfigureAwait(false);

        const string exportPrefix = "export ";
        if (trimmed.StartsWith(exportPrefix, StringComparison.OrdinalIgnoreCase))
            return await ExportPromptAsync(trimmed[exportPrefix.Length..].Trim(), ct).ConfigureAwait(false);

        return "Usage: /prompt [--full|drop <n[-m]>|inject [--system] <text>|export [path]]";
    }

    private string RenderPromptView(bool full)
    {
        var sb = new StringBuilder();
        var systemTokens = _session.SystemPromptTokens;
        sb.AppendLine($"System Prompt: {systemTokens:N0} tokens (use /system-prompt to view/edit)");
        sb.AppendLine(new string('─', 24));

        var indexed = GetIndexedPromptMessages();
        if (indexed.Count == 0)
        {
            sb.AppendLine("No non-system messages in context.");
        }
        else
        {
            foreach (var entry in indexed)
            {
                var role = entry.Message.Role.ToString().ToLowerInvariant();
                var content = entry.Message.Content ?? string.Empty;
                var msgTokens = JD.SemanticKernel.Extensions.Compaction.TokenEstimator.EstimateTokens(content);

                if (full)
                {
                    sb.AppendLine($"#{entry.DisplayIndex} [{role}] ({msgTokens:N0} tokens)");
                    sb.AppendLine(string.IsNullOrWhiteSpace(content) ? "(empty)" : content);
                    sb.AppendLine();
                    continue;
                }

                var preview = BuildSingleLinePreview(content, 80);
                sb.AppendLine($"#{entry.DisplayIndex} [{role}] {preview} ({msgTokens:N0} tokens)");
            }
        }

        var totalTokens = JD.SemanticKernel.Extensions.Compaction.TokenEstimator.EstimateTokens(_session.History);
        var contextWindow = _session.CurrentModel?.ContextWindowTokens ?? 128_000;
        var pct = contextWindow > 0 ? (100.0 * totalTokens / contextWindow) : 0;
        sb.AppendLine(new string('─', 24));
        sb.AppendLine($"Total: {totalTokens:N0} / {contextWindow:N0} tokens ({pct:F1}%)");
        return sb.ToString().TrimEnd();
    }

    private string DropPromptMessages(string indexSpec)
    {
        if (!TryParseDisplayRange(indexSpec, out var start, out var end))
            return "Usage: /prompt drop <n> or /prompt drop <n-m>";

        var indexed = GetIndexedPromptMessages();
        if (indexed.Count == 0)
            return "No non-system messages available to drop.";

        if (start < 1 || end < start || end > indexed.Count)
            return $"Range must be between 1 and {indexed.Count}.";

        var toRemove = indexed
            .Where(e => e.DisplayIndex >= start && e.DisplayIndex <= end)
            .Select(e => e.HistoryIndex)
            .OrderByDescending(i => i)
            .ToList();

        var before = JD.SemanticKernel.Extensions.Compaction.TokenEstimator.EstimateTokens(_session.History);
        foreach (var historyIndex in toRemove)
            _session.History.RemoveAt(historyIndex);
        var after = JD.SemanticKernel.Extensions.Compaction.TokenEstimator.EstimateTokens(_session.History);

        var dropped = end - start + 1;
        var range = start == end ? $"#{start}" : $"#{start}-#{end}";
        return $"Dropped {dropped} context message(s) ({range}). Tokens: {before:N0} -> {after:N0}.";
    }

    private string InjectPromptMessage(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return "Usage: /prompt inject [--system] <text>";

        const string systemPrefix = "--system ";
        if (payload.StartsWith(systemPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var text = payload[systemPrefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(text))
                return "Usage: /prompt inject --system <text>";

            _session.CaptureOriginalSystemPromptIfUnset();
            var current = GetCurrentSystemPromptText();
            var next = string.IsNullOrWhiteSpace(current) ? text : $"{current}\n\n{text}";
            var before = _session.SystemPromptTokens;
            _session.ReplaceSystemPrompt(next);
            return $"Injected system context. System prompt tokens: {before:N0} -> {_session.SystemPromptTokens:N0}.";
        }

        _session.History.AddUserMessage(payload);
        var index = GetIndexedPromptMessages().Count;
        return $"Injected user context as message #{index}.";
    }

    private async Task<string> ExportPromptAsync(string? path, CancellationToken ct)
    {
        var targetPath = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(Directory.GetCurrentDirectory(), $"context-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md")
            : path.Trim().Trim('"');

        var fullPath = Path.GetFullPath(targetPath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var markdown = BuildPromptExportMarkdown();
        await File.WriteAllTextAsync(fullPath, markdown, ct).ConfigureAwait(false);
        return $"Prompt context exported to {fullPath}";
    }

    private string BuildPromptExportMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Prompt Context Export");
        sb.AppendLine();
        sb.AppendLine($"- Generated (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- Model: {_session.CurrentModel?.Id ?? "(unknown)"}");
        sb.AppendLine();

        for (var i = 0; i < _session.History.Count; i++)
        {
            var msg = _session.History[i];
            var role = msg.Role.ToString().ToLowerInvariant();
            var content = msg.Content ?? string.Empty;
            var tokens = JD.SemanticKernel.Extensions.Compaction.TokenEstimator.EstimateTokens(content);

            sb.AppendLine($"## {i + 1}. {role} ({tokens:N0} tokens)");
            sb.AppendLine();
            sb.AppendLine(string.IsNullOrWhiteSpace(content) ? "(empty)" : content);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private string? GetCurrentSystemPromptText() =>
        _session.History.FirstOrDefault(m => m.Role == AuthorRole.System)?.Content;

    private static string FormatSystemPromptUpdateResult(string action, int beforeTokens, int afterTokens)
    {
        var delta = afterTokens - beforeTokens;
        var deltaText = delta == 0 ? "0" : delta > 0 ? $"+{delta:N0}" : $"{delta:N0}";
        return $"{action} system prompt. Before: {beforeTokens:N0} tokens; After: {afterTokens:N0} tokens ({deltaText}).";
    }

    private string EditSystemPromptInEditor()
    {
        var current = GetCurrentSystemPromptText();
        if (string.IsNullOrWhiteSpace(current))
            return "No system prompt is currently loaded.";

        var editorSpec = Environment.GetEnvironmentVariable("EDITOR");
        if (string.IsNullOrWhiteSpace(editorSpec))
            editorSpec = OperatingSystem.IsWindows() ? "notepad" : "vi";

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"jdai-system-prompt-{Guid.NewGuid():N}.md");
        File.WriteAllText(tempPath, current);

        try
        {
            var parts = editorSpec
                .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
                return "Unable to launch editor: EDITOR is empty.";

            var executable = parts[0];
            var args = parts.Length > 1
                ? $"{parts[1]} \"{tempPath}\""
                : $"\"{tempPath}\"";

            using var process = Process.Start(new ProcessStartInfo(executable, args)
            {
                UseShellExecute = false,
            });

            if (process is null)
                return $"Unable to launch editor '{editorSpec}'.";

            process.WaitForExit();
            if (process.ExitCode != 0)
                return $"Editor exited with code {process.ExitCode}. No changes applied.";

            var edited = File.ReadAllText(tempPath);
            if (string.IsNullOrWhiteSpace(edited))
                return "Edited system prompt is empty. No changes applied.";

            if (string.Equals(edited, current, StringComparison.Ordinal))
                return "System prompt unchanged.";

            var before = _session.SystemPromptTokens;
            _session.ReplaceSystemPrompt(edited);
            return FormatSystemPromptUpdateResult("Updated", before, _session.SystemPromptTokens);
        }
        catch (Exception ex)
        {
            return $"Failed to edit system prompt: {ex.Message}";
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
#pragma warning disable CA1031
            catch
#pragma warning restore CA1031
            {
                // Best effort cleanup for temp file.
            }
        }
    }

    private List<(int DisplayIndex, int HistoryIndex, ChatMessageContent Message)> GetIndexedPromptMessages()
    {
        var result = new List<(int DisplayIndex, int HistoryIndex, ChatMessageContent Message)>();
        var displayIndex = 1;
        for (var i = 0; i < _session.History.Count; i++)
        {
            var msg = _session.History[i];
            if (msg.Role == AuthorRole.System)
                continue;

            result.Add((displayIndex, i, msg));
            displayIndex++;
        }

        return result;
    }

    private static string BuildSingleLinePreview(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "(empty)";

        var singleLine = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace('\n', ' ')
            .Trim();

        if (singleLine.Length <= maxChars)
            return singleLine;

        return string.Concat(singleLine.AsSpan(0, maxChars - 3), "...");
    }

    private static bool TryParseDisplayRange(string input, out int start, out int end)
    {
        start = 0;
        end = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        var dash = trimmed.IndexOf('-');
        if (dash < 0)
        {
            if (!int.TryParse(trimmed, out start))
                return false;
            end = start;
            return true;
        }

        var left = trimmed[..dash].Trim();
        var right = trimmed[(dash + 1)..].Trim();
        if (!int.TryParse(left, out start) || !int.TryParse(right, out end))
            return false;
        return true;
    }

    private async Task<string> CompactSystemPromptAsync(string? arg, CancellationToken ct)
    {
        // No arg: compact now (one-shot)
        if (string.IsNullOrWhiteSpace(arg))
        {
            var contextWindow = _session.CurrentModel?.ContextWindowTokens ?? 128_000;
            var settings = TuiSettings.Load();
            var budgetTokens = (int)(contextWindow * (settings.SystemPromptBudgetPercent / 100.0));
            var before = _session.SystemPromptTokens;

            if (before == 0)
                return "No system prompt to compact.";

            _session.CaptureOriginalSystemPromptIfUnset();
            var after = await _session.CompactSystemPromptAsync(budgetTokens, ct).ConfigureAwait(false);
            return before == after
                ? $"System prompt already within budget ({before:N0} tokens ≤ {budgetTokens:N0} budget).\nUse /system-prompt to inspect the current prompt."
                : $"System prompt compacted: {before:N0} → {after:N0} tokens (budget: {budgetTokens:N0}).\nUse /system-prompt to inspect the current prompt.";
        }

        // off/auto/always: persist setting
        if (!Enum.TryParse<SystemPromptCompaction>(arg.Trim(), ignoreCase: true, out var mode))
        {
            return $"Unknown mode: '{arg}'. Available: off, auto, always";
        }

        var current = TuiSettings.Load();
        var updated = current with { SystemPromptCompaction = mode };
        try { updated.Save(); }
#pragma warning disable CA1031
        catch { /* best-effort persist */ }
#pragma warning restore CA1031

        return $"System prompt compaction set to: {mode.ToString().ToLowerInvariant()}";
    }

    private async Task<string> CopyLastResponseInstanceAsync()
    {
        var lastAssistant = _session.History
            .Where(m => m.Role == Microsoft.SemanticKernel.ChatCompletion.AuthorRole.Assistant)
            .LastOrDefault();
        if (lastAssistant != null)
        {
            var text = lastAssistant.Content ?? "";
            await ClipboardTools.WriteClipboardAsync(text).ConfigureAwait(false);
            return $"Copied {text.Length} characters to clipboard.";
        }

        return "No assistant response to copy.";
    }

    private static async Task<string?> ShowDiffAsync(CancellationToken ct)
    {
        _ = ct;
        var diffOutput = await ShellTools.RunCommandAsync("git diff").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(diffOutput) || diffOutput.Contains("Exit code: 1", StringComparison.Ordinal))
        {
            diffOutput = await ShellTools.RunCommandAsync("git diff --cached").ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(diffOutput) || string.Equals(diffOutput.Trim(), "Exit code: 0", StringComparison.Ordinal))
        {
            return "No uncommitted changes.";
        }

        return diffOutput;
    }

    private static async Task<string> InitProjectFileAsync(CancellationToken ct)
    {
        _ = ct;
        var jdaiPath = Path.Combine(Directory.GetCurrentDirectory(), "JDAI.md");
        if (File.Exists(jdaiPath))
        {
            return $"JDAI.md already exists at {jdaiPath}";
        }

        var template = """
            # Project Instructions

            <!-- jdai reads this file to understand your project. -->

            ## Conventions

            -

            ## Architecture

            -

            ## Testing

            -
            """;
        await File.WriteAllTextAsync(jdaiPath, template, ct).ConfigureAwait(false);
        return $"Created {jdaiPath} — edit it to guide jdai.";
    }

    private string TogglePlanMode()
    {
        _session.PlanMode = !_session.PlanMode;
        return _session.PlanMode
            ? "Plan mode ON — jdai will explore and plan without making changes."
            : "Plan mode OFF — normal mode restored.";
    }

    private async Task<string> RunDoctorAsync(CancellationToken ct)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== jdai Doctor ===");
        sb.AppendLine($"Version: {typeof(SlashCommandRouter).Assembly.GetName().Version}");
        sb.AppendLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        sb.AppendLine($"CWD: {Directory.GetCurrentDirectory()}");

        var providers = await DetectProvidersAsync(forceRefresh: true, ct).ConfigureAwait(false);
        var providerList = providers.ToList();
        sb.AppendLine($"Providers: {providerList.Count(p => p.IsAvailable)} available / {providerList.Count} total");

        var allModels = await GetModelsAsync(forceRefresh: true, ct).ConfigureAwait(false);
        sb.AppendLine($"Models: {allModels.Count}");
        sb.AppendLine($"Current: {_session.CurrentModel?.ProviderName ?? "?"} / {_session.CurrentModel?.Id ?? "?"}");
        sb.AppendLine($"Plugins: {_session.Kernel.Plugins.Count}");
        sb.AppendLine($"Tools: {_session.Kernel.Plugins.SelectMany(p => p).Count()}");
        sb.AppendLine($"Instructions: {(_instructions?.HasInstructions == true ? $"{_instructions.Files.Count} file(s)" : "none")}");
        sb.AppendLine($"Session: {_session.SessionInfo?.Id ?? "none"}");

        try
        {
            var gitVer = await ShellTools.RunCommandAsync("git --version").ConfigureAwait(false);
            sb.AppendLine($"Git: {gitVer.Trim()}");
        }
#pragma warning disable CA1031
        catch { sb.AppendLine("Git: not found"); }
#pragma warning restore CA1031

        try
        {
            var dotnetVer = await ShellTools.RunCommandAsync("dotnet --version").ConfigureAwait(false);
            sb.AppendLine($".NET CLI: {dotnetVer.Trim()}");
        }
#pragma warning disable CA1031
        catch { sb.AppendLine(".NET CLI: not found"); }
#pragma warning restore CA1031

        return sb.ToString();
    }

    private async Task<string> ForkSessionAsync(string? arg, CancellationToken ct)
    {
        _ = ct;
        if (_session.Store == null || _session.SessionInfo == null)
        {
            return "No active session to fork.";
        }

        var forkName = string.IsNullOrWhiteSpace(arg) ? null : arg;
        var forkedSession = await _session.ForkSessionAsync(forkName).ConfigureAwait(false);
        return $"Forked to new session: {forkedSession?.Id ?? "failed"}";
    }

    private async Task<string> HandleModelInfoAsync(string? arg, CancellationToken ct)
    {
        var model = _session.CurrentModel;
        if (model is null)
            return "No model selected.";

        if (string.Equals(arg?.Trim(), "refresh", StringComparison.OrdinalIgnoreCase)
            && _metadataProvider is not null)
        {
            await _metadataProvider.LoadAsync(forceRefresh: true, ct).ConfigureAwait(false);
            var refreshed = _metadataProvider.Enrich([model]);
            if (refreshed.Count > 0 && refreshed[0].HasMetadata)
            {
                model = refreshed[0];
                _session.UpdateModelMetadata(model);
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Model Info ===");
        sb.AppendLine($"  Name:           {model.DisplayName}");
        sb.AppendLine($"  Provider:       {model.ProviderName}");
        sb.AppendLine($"  ID:             {model.Id}");
        sb.AppendLine($"  Context window: {model.ContextWindowTokens:N0} tokens");
        sb.AppendLine($"  Max output:     {model.MaxOutputTokens:N0} tokens");

        if (model.HasMetadata)
        {
            sb.AppendLine($"  Input cost:     ${model.InputCostPerToken}/token");
            sb.AppendLine($"  Output cost:    ${model.OutputCostPerToken}/token");
            sb.AppendLine($"  Source:         LiteLLM catalog");
        }
        else
        {
            sb.AppendLine("  Cost data:      Not available (using defaults)");
        }

        if (_metadataProvider is not null)
        {
            sb.AppendLine($"  Catalog size:   {_metadataProvider.EntryCount:N0} models");
            if (_metadataProvider.LastFetched is { } fetched)
                sb.AppendLine($"  Last fetched:   {fetched:yyyy-MM-dd HH:mm} UTC");
        }

        return sb.ToString();
    }

    // ── /review, /security-review ──────────────────────────

    private sealed record ReviewRequest(
        bool SecurityMode,
        bool FullScan,
        string? Branch,
        string? Target);

    private sealed record SecurityFinding(
        string Severity,
        string Cwe,
        string Title,
        string File,
        int Line,
        string Summary,
        string Recommendation);

    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Options;

    private async Task<string> RunReviewAsync(string? arg, bool securityMode, CancellationToken ct)
    {
        try
        {
            var request = await ParseReviewArgsAsync(arg, securityMode, ct).ConfigureAwait(false);
            if (request is null)
            {
                return securityMode
                    ? "Usage: /security-review [--full] [--branch <name> --target <name>]"
                    : "Usage: /review [--branch <name> --target <name>]";
            }

            if (request.SecurityMode)
                return await RunSecurityReviewAsync(request, ct).ConfigureAwait(false);

            var (diff, files) = await GetReviewDiffAndFilesAsync(request, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(diff))
                return "No changes to review.";

            var fileContext = await BuildFileContextAsync(files, ct).ConfigureAwait(false);
            var trimmedDiff = TrimTo(diff, 50_000);

            var prompt = $$"""
            Review the following code changes and return findings in exactly this format:

            ## Critical
            - <file:line> <issue and impact>

            ## Warning
            - <file:line> <issue and impact>

            ## Suggestions
            - <file:line> <improvement suggestion>

            Rules:
            - Focus on correctness, regressions, reliability, security, and maintainability.
            - Include file paths and line numbers when possible.
            - If no items for a section, write "- None."
            - Keep each bullet concise.

            Diff:
            ```diff
            {{trimmedDiff}}
            ```

            Additional file context:
            {{fileContext}}
            """;

            var reviewed = await RunModelAnalysisAsync(prompt, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(reviewed))
                return "Review completed, but no findings were returned.";

            return request.Branch is not null
                ? $"Reviewing `{request.Branch}` against `{request.Target}`\n\n{reviewed}"
                : $"Reviewing uncommitted changes\n\n{reviewed}";
        }
        catch (Exception ex)
        {
            return $"Review failed: {ex.Message}";
        }
    }

    private async Task<string> RunSecurityReviewAsync(ReviewRequest request, CancellationToken ct)
    {
        var files = await GetSecurityTargetFilesAsync(request, ct).ConfigureAwait(false);
        if (files.Count == 0)
            return "No files found for security scan.";

        var findings = new List<SecurityFinding>();
        foreach (var file in files.Where(IsSourceLikeFile))
        {
            if (!File.Exists(file)) continue;
            var text = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);

            AddSecurityFindings(findings, file, text);
        }

        var bySeverity = findings
            .GroupBy(f => f.Severity)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var critical = bySeverity.GetValueOrDefault("critical")?.Count ?? 0;
        var warning = bySeverity.GetValueOrDefault("warning")?.Count ?? 0;
        var info = bySeverity.GetValueOrDefault("info")?.Count ?? 0;

        var sb = new StringBuilder();
        sb.AppendLine(request.FullScan
            ? "Security scan across tracked repository files (OWASP/CWE heuristics)"
            : "Security scan of current changes (OWASP/CWE heuristics)");

        if (findings.Count == 0)
        {
            sb.AppendLine("No obvious security findings from heuristic checks.");
            return sb.ToString().TrimEnd();
        }

        foreach (var severity in new[] { "critical", "warning", "info" })
        {
            if (!bySeverity.TryGetValue(severity, out var items) || items.Count == 0)
                continue;

            var label = severity switch
            {
                "critical" => "Critical",
                "warning" => "Warning",
                _ => "Info",
            };

            sb.AppendLine();
            sb.AppendLine($"## {label}");
            foreach (var finding in items.Take(40))
            {
                sb.AppendLine(
                    $"- {finding.Cwe} {finding.Title} — {finding.File}:{finding.Line} | {finding.Summary} | Fix: {finding.Recommendation}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Summary: {critical} critical, {warning} warning, {info} info");
        return sb.ToString().TrimEnd();
    }

    private static void AddSecurityFindings(List<SecurityFinding> findings, string file, string text)
    {
        AddPatternFindings(
            findings, file, text,
            severity: "critical",
            cwe: "CWE-89",
            title: "Potential SQL Injection",
            pattern: @"(?im)(select|insert|update|delete)\s+.+\+.+",
            summary: "String concatenation appears in SQL statement construction.",
            recommendation: "Use parameterized queries or ORM parameters.");

        AddPatternFindings(
            findings, file, text,
            severity: "warning",
            cwe: "CWE-798",
            title: "Hard-coded Credential Pattern",
            pattern: @"(?im)\b(api[_-]?key|secret|password|token)\b\s*[:=]\s*[""'][^""'\r\n]{8,}[""']",
            summary: "Credential-like literal appears hard-coded.",
            recommendation: "Move secrets to environment variables or secret manager.");

        AddPatternFindings(
            findings, file, text,
            severity: "warning",
            cwe: "CWE-327",
            title: "Weak Cryptographic Hash",
            pattern: @"(?im)\b(MD5|SHA1)\b",
            summary: "Weak hash algorithm detected.",
            recommendation: "Use SHA-256+ or approved cryptographic primitives.");

        AddPatternFindings(
            findings, file, text,
            severity: "warning",
            cwe: "CWE-78",
            title: "Potential Command Injection",
            pattern: @"(?im)ProcessStartInfo\s*\{[^}]*Arguments\s*=\s*\$""",
            summary: "Interpolated shell arguments may include untrusted input.",
            recommendation: "Validate inputs and avoid shell interpolation when possible.");

        AddPatternFindings(
            findings, file, text,
            severity: "info",
            cwe: "CWE-22",
            title: "Potential Path Traversal Risk",
            pattern: @"(?im)Path\.Combine\([^)]*(input|path|filename|user)",
            summary: "Path combines potentially user-controlled values.",
            recommendation: "Normalize and validate paths before filesystem access.");
    }

    private static void AddPatternFindings(
        List<SecurityFinding> findings,
        string file,
        string text,
        string severity,
        string cwe,
        string title,
        string pattern,
        string summary,
        string recommendation)
    {
        var matches = Regex.Matches(text, pattern, RegexOptions.CultureInvariant);
        foreach (Match match in matches.Cast<Match>().Take(5))
        {
            var line = 1 + text.AsSpan(0, match.Index).Count('\n');
            findings.Add(new SecurityFinding(
                Severity: severity,
                Cwe: cwe,
                Title: title,
                File: file.Replace('\\', '/'),
                Line: line,
                Summary: summary,
                Recommendation: recommendation));
        }
    }

    private async Task<ReviewRequest?> ParseReviewArgsAsync(string? arg, bool securityMode, CancellationToken ct)
    {
        var tokens = (arg ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        string? branch = null;
        string? target = null;
        var full = false;

        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (string.Equals(token, "--full", StringComparison.OrdinalIgnoreCase))
            {
                full = true;
                continue;
            }

            if (string.Equals(token, "--branch", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= tokens.Length) return null;
                branch = tokens[++i];
                continue;
            }

            if (string.Equals(token, "--target", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= tokens.Length) return null;
                target = tokens[++i];
            }
        }

        if (target is not null && branch is null)
        {
            branch = await GetCurrentBranchAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(branch))
                throw new InvalidOperationException(
                    "Unable to determine current git branch for --target comparison.");
        }

        if (branch is not null && target is null)
            target = "main";

        return new ReviewRequest(securityMode, full, branch, target);
    }

    private async Task<(string Diff, IReadOnlyList<string> Files)> GetReviewDiffAndFilesAsync(
        ReviewRequest request, CancellationToken ct)
    {
        if (request.Branch is not null && request.Target is not null)
        {
            var args = $"{request.Target}...{request.Branch}";
            var diffResult = await RunGitCommandAsync($"diff {args}", ct).ConfigureAwait(false);
            EnsureGitCommandSucceeded($"diff {args}", diffResult);
            var filesOutput = await RunGitCommandAsync($"diff --name-only {args}", ct).ConfigureAwait(false);
            EnsureGitCommandSucceeded($"diff --name-only {args}", filesOutput);
            return (diffResult.StdOut, SplitLines(filesOutput.StdOut));
        }

        var unstaged = await RunGitCommandAsync("diff", ct).ConfigureAwait(false);
        EnsureGitCommandSucceeded("diff", unstaged);
        var staged = await RunGitCommandAsync("diff --cached", ct).ConfigureAwait(false);
        EnsureGitCommandSucceeded("diff --cached", staged);
        var diff = $"{unstaged.StdOut}\n{staged.StdOut}".Trim();

        var unstagedFiles = await RunGitCommandAsync("diff --name-only", ct).ConfigureAwait(false);
        EnsureGitCommandSucceeded("diff --name-only", unstagedFiles);
        var stagedFiles = await RunGitCommandAsync("diff --cached --name-only", ct).ConfigureAwait(false);
        EnsureGitCommandSucceeded("diff --cached --name-only", stagedFiles);
        var untrackedFiles = await RunGitCommandAsync("ls-files --others --exclude-standard", ct).ConfigureAwait(false);
        EnsureGitCommandSucceeded("ls-files --others --exclude-standard", untrackedFiles);

        var files = SplitLines($"{unstagedFiles.StdOut}\n{stagedFiles.StdOut}\n{untrackedFiles.StdOut}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (diff, files);
    }

    private async Task<IReadOnlyList<string>> GetSecurityTargetFilesAsync(ReviewRequest request, CancellationToken ct)
    {
        if (request.FullScan)
        {
            var all = await RunGitCommandAsync("ls-files", ct).ConfigureAwait(false);
            EnsureGitCommandSucceeded("ls-files", all);
            return SplitLines(all.StdOut);
        }

        var (_, files) = await GetReviewDiffAndFilesAsync(request, ct).ConfigureAwait(false);
        return files;
    }

    private async Task<string> BuildFileContextAsync(IReadOnlyList<string> files, CancellationToken ct)
    {
        if (files.Count == 0)
            return "No changed files available.";

        var sb = new StringBuilder();
        foreach (var file in files.Where(IsSourceLikeFile).Take(8))
        {
            if (!File.Exists(file)) continue;
            var content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            sb.AppendLine($"### {file.Replace('\\', '/')}");
            sb.AppendLine("```");
            sb.AppendLine(TrimTo(content, 5_000));
            sb.AppendLine("```");
            sb.AppendLine();
        }

        return sb.Length == 0 ? "No readable text files found." : sb.ToString().TrimEnd();
    }

    private async Task<string> RunModelAnalysisAsync(string prompt, CancellationToken ct)
    {
        var chat = _session.Kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage("""
            You are a principal engineer doing a high-signal code review.
            Prioritize bugs, regressions, security issues, and maintainability risks.
            Be concise and concrete.
            """);
        history.AddUserMessage(prompt);

        var settings = new OpenAIPromptExecutionSettings
        {
            ModelId = _session.CurrentModel?.Id,
            MaxTokens = 2200,
            Temperature = 0.1,
        };
        AgentLoop.ApplyReasoningEffort(
            settings,
            _session.CurrentModel,
            _session.ReasoningEffortOverride);
        PromptCachePolicy.Apply(
            settings,
            _session.CurrentModel,
            history,
            _session.PromptCachingEnabled,
            _session.PromptCacheTtl);

        var result = await chat.GetChatMessageContentAsync(
            history,
            settings,
            _session.Kernel,
            ct).ConfigureAwait(false);

        return result.Content ?? string.Empty;
    }

    private static bool IsSourceLikeFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".cs" or ".csx" or ".md" or ".json" or ".yaml" or ".yml" or ".xml" or ".ts" or ".tsx" or ".js" or ".jsx" or ".py" or ".go" or ".java" or ".cpp" or ".c" or ".h" or ".hpp" or ".rb" or ".rs" or ".sql" or ".ps1" or ".sh" or ".txt";
    }

    private static string TrimTo(string value, int maxChars) =>
        value.Length <= maxChars
            ? value
            : value[..maxChars] + "\n... [truncated]";

    private static List<string> SplitLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

    private static void EnsureGitCommandSucceeded(
        string command,
        (int ExitCode, string StdOut, string StdErr) result)
    {
        if (result.ExitCode == 0)
            return;

        throw new InvalidOperationException(FormatGitError(command, result));
    }

    private static string FormatGitError(
        string command,
        (int ExitCode, string StdOut, string StdErr) result)
    {
        var error = string.IsNullOrWhiteSpace(result.StdErr)
            ? "No error output."
            : result.StdErr.Trim();
        return $"`git {command}` failed (exit {result.ExitCode}): {error}";
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunGitCommandAsync(
        string args, CancellationToken ct)
    {
        try
        {
            var result = await ProcessExecutor.RunAsync(
                "git", $"--no-pager {args}",
                workingDirectory: Directory.GetCurrentDirectory(),
                cancellationToken: ct).ConfigureAwait(false);

            return (result.ExitCode, result.StandardOutput, result.StandardError);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return (-1, string.Empty, $"Failed to run git: {ex.Message}");
        }
    }

    private static async Task<string?> GetCurrentBranchAsync(CancellationToken ct)
    {
        var result = await RunGitCommandAsync("rev-parse --abbrev-ref HEAD", ct).ConfigureAwait(false);
        if (result.ExitCode != 0)
            return null;

        return result.StdOut.Trim();
    }

    // ── /theme, /vim, /output-style, /config ───────────────

    private static readonly IReadOnlyDictionary<string, TuiTheme> ThemeAliases =
        new Dictionary<string, TuiTheme>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = TuiTheme.DefaultDark,
            ["default-dark"] = TuiTheme.DefaultDark,
            ["monokai"] = TuiTheme.Monokai,
            ["solarized-dark"] = TuiTheme.SolarizedDark,
            ["solarized-light"] = TuiTheme.SolarizedLight,
            ["nord"] = TuiTheme.Nord,
            ["dracula"] = TuiTheme.Dracula,
            ["one-dark"] = TuiTheme.OneDark,
            ["catppuccin"] = TuiTheme.CatppuccinMocha,
            ["catppuccin-mocha"] = TuiTheme.CatppuccinMocha,
            ["gruvbox"] = TuiTheme.Gruvbox,
            ["high-contrast"] = TuiTheme.HighContrast,
        };

    private static readonly IReadOnlyDictionary<string, OutputStyle> OutputStyleAliases =
        new Dictionary<string, OutputStyle>(StringComparer.OrdinalIgnoreCase)
        {
            ["rich"] = OutputStyle.Rich,
            ["plain"] = OutputStyle.Plain,
            ["compact"] = OutputStyle.Compact,
            ["json"] = OutputStyle.Json,
        };

    private string HandleTheme(string? arg)
    {
        if (_getTheme is null || _onThemeChanged is null)
            return "Theme switching is not configurable in this context.";

        if (string.IsNullOrWhiteSpace(arg))
        {
            var current = _getTheme();
            var available = string.Join(", ", ThemeAliases.Keys.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(k => k));
            return $"Current theme: {ToThemeToken(current)}\nAvailable: {available}\nUsage: /theme <name>";
        }

        var token = arg.Trim();
        if (!ThemeAliases.TryGetValue(token, out var selected))
            return $"Unknown theme '{token}'. Run /theme to list available themes.";

        _onThemeChanged(selected);
        SaveSettings(TuiSettings.Load() with { Theme = selected });
        return $"Theme set to {ToThemeToken(selected)}.";
    }

    private string ToggleVimMode(string? arg)
    {
        if (_getVimMode is null || _onVimModeChanged is null)
            return "Vim mode is not configurable in this context.";

        bool enabled;
        if (string.IsNullOrWhiteSpace(arg))
        {
            enabled = !_getVimMode();
        }
        else if (TryParseOnOff(arg.Trim(), out var parsed))
        {
            enabled = parsed;
        }
        else
        {
            return $"Vim mode is {(_getVimMode() ? "ON" : "OFF")}. Usage: /vim [on|off]";
        }

        _onVimModeChanged(enabled);
        SaveSettings(TuiSettings.Load() with { VimMode = enabled });
        return enabled
            ? "Vim mode: ON (ESC normal mode, i/a/I/A to enter insert mode)"
            : "Vim mode: OFF (standard editing restored)";
    }

    private string HandleOutputStyle(string? arg)
    {
        if (_getOutputStyle is null || _onOutputStyleChanged is null)
            return "Output style is not configurable in this context.";

        if (string.IsNullOrWhiteSpace(arg))
        {
            var current = _getOutputStyle();
            var available = string.Join(", ", OutputStyleAliases.Keys.OrderBy(k => k));
            return $"Current output style: {current.ToString().ToLowerInvariant()}\nAvailable: {available}\nUsage: /output-style <style>";
        }

        var token = arg.Trim();
        if (!OutputStyleAliases.TryGetValue(token, out var style))
            return $"Unknown output style '{token}'. Run /output-style to list options.";

        _onOutputStyleChanged(style);
        SaveSettings(TuiSettings.Load() with { OutputStyle = style });
        if (style == OutputStyle.Json)
            return "Output style set to json for this session only.";
        return $"Output style set to {style.ToString().ToLowerInvariant()}.";
    }

    private string HandleConfig(string? arg)
    {
        var settings = TuiSettings.Load();
        var parts = (arg ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var action = parts.Length == 0 ? "edit" : parts[0].ToLowerInvariant();
        var rest = parts.Length > 1 ? parts[1] : null;

        // Fall back to list in non-interactive (non-ANSI) terminals
        if (string.Equals(action, "edit", StringComparison.Ordinal) &&
            !AnsiConsole.Profile.Capabilities.Ansi)
            action = "list";

        return action switch
        {
            "edit" => ConfigEditor.Edit(
                TuiSettings.Load,
                SaveSettings,
                _session,
                _onThemeChanged,
                _getTheme,
                _onVimModeChanged,
                _getVimMode,
                _onOutputStyleChanged,
                _getOutputStyle,
                _onSpinnerStyleChanged,
                _getSpinnerStyle),
            "list" => FormatConfig(settings),
            "get" => GetConfigValue(rest, settings),
            "set" => SetConfigValue(rest, settings),
            _ => "Usage: /config [edit|list|get <key>|set <key> <value>]",
        };
    }

    private string HandleSkills(string? arg)
    {
        if (_getSkillsStatus is null)
            return "Skills lifecycle manager not initialized.";

        var token = (arg ?? "status").Trim();
        if (string.IsNullOrWhiteSpace(token) || string.Equals(token, "status", StringComparison.OrdinalIgnoreCase))
            return _getSkillsStatus();

        if (string.Equals(token, "reload", StringComparison.OrdinalIgnoreCase))
            return _reloadSkills?.Invoke() ?? "Skills reload is not available.";

        return "Usage: /skills [status|reload]";
    }

    private string FormatConfig(TuiSettings settings)
    {
        var theme = _getTheme?.Invoke() ?? settings.Theme;
        var vim = _getVimMode?.Invoke() ?? settings.VimMode;
        var output = _getOutputStyle?.Invoke() ?? settings.OutputStyle;
        var spinner = _getSpinnerStyle?.Invoke() ?? settings.SpinnerStyle;
        var welcome = WelcomePanelSettings.Normalize(settings.Welcome);
        var motdUrl = string.IsNullOrWhiteSpace(welcome.MotdUrl) ? "(none)" : welcome.MotdUrl;

        return $$"""
            Configuration:
              theme: {{ToThemeToken(theme)}}
              vim_mode: {{vim.ToString().ToLowerInvariant()}}
              output_style: {{output.ToString().ToLowerInvariant()}}
              spinner_style: {{spinner.ToString().ToLowerInvariant()}}
              prompt_cache: {{_session.PromptCachingEnabled.ToString().ToLowerInvariant()}}
              prompt_cache_ttl: {{PromptCachePolicy.ToToken(_session.PromptCacheTtl)}}
              sys_prompt_compaction: {{settings.SystemPromptCompaction.ToString().ToLowerInvariant()}}
              sys_prompt_budget: {{settings.SystemPromptBudgetPercent}}
              compact_auto: {{settings.AutoCompact.ToString().ToLowerInvariant()}}
              compact_threshold: {{settings.CompactThresholdPercent}}
              autorun: {{_session.AutoRunEnabled.ToString().ToLowerInvariant()}}
              permissions: {{(!_session.SkipPermissions).ToString().ToLowerInvariant()}}
              plan_mode: {{_session.PlanMode.ToString().ToLowerInvariant()}}
              welcome_model_summary: {{welcome.ShowModelSummary.ToString().ToLowerInvariant()}}
              welcome_services: {{welcome.ShowServices.ToString().ToLowerInvariant()}}
              welcome_cwd: {{welcome.ShowWorkingDirectory.ToString().ToLowerInvariant()}}
              welcome_version: {{welcome.ShowVersion.ToString().ToLowerInvariant()}}
              welcome_motd: {{welcome.ShowMotd.ToString().ToLowerInvariant()}}
              welcome_motd_url: {{motdUrl}}
              welcome_motd_timeout_ms: {{welcome.MotdTimeoutMs}}
              welcome_motd_max_length: {{welcome.MotdMaxLength}}

            Usage:
              /config          — interactive editor
              /config list     — show all settings
              /config get <key>
              /config set <key> <value>
            """;
    }

    private string GetConfigValue(string? key, TuiSettings settings)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "Usage: /config get <key>";

        var token = NormalizeConfigKey(key);
        var welcome = WelcomePanelSettings.Normalize(settings.Welcome);
        return token switch
        {
            "theme" => $"theme={ToThemeToken(_getTheme?.Invoke() ?? settings.Theme)}",
            "vim_mode" => $"vim_mode={(_getVimMode?.Invoke() ?? settings.VimMode).ToString().ToLowerInvariant()}",
            "output_style" => $"output_style={(_getOutputStyle?.Invoke() ?? settings.OutputStyle).ToString().ToLowerInvariant()}",
            "spinner_style" => $"spinner_style={(_getSpinnerStyle?.Invoke() ?? settings.SpinnerStyle).ToString().ToLowerInvariant()}",
            "prompt_cache" => $"prompt_cache={_session.PromptCachingEnabled.ToString().ToLowerInvariant()}",
            "prompt_cache_ttl" => $"prompt_cache_ttl={PromptCachePolicy.ToToken(_session.PromptCacheTtl)}",
            "sys_prompt_compaction" => $"sys_prompt_compaction={settings.SystemPromptCompaction.ToString().ToLowerInvariant()}",
            "sys_prompt_budget" => $"sys_prompt_budget={settings.SystemPromptBudgetPercent}",
            "compact_auto" => $"compact_auto={settings.AutoCompact.ToString().ToLowerInvariant()}",
            "compact_threshold" => $"compact_threshold={settings.CompactThresholdPercent}",
            "autorun" => $"autorun={_session.AutoRunEnabled.ToString().ToLowerInvariant()}",
            "permissions" => $"permissions={(!_session.SkipPermissions).ToString().ToLowerInvariant()}",
            "plan_mode" => $"plan_mode={_session.PlanMode.ToString().ToLowerInvariant()}",
            "welcome_model_summary" => $"welcome_model_summary={welcome.ShowModelSummary.ToString().ToLowerInvariant()}",
            "welcome_services" => $"welcome_services={welcome.ShowServices.ToString().ToLowerInvariant()}",
            "welcome_cwd" => $"welcome_cwd={welcome.ShowWorkingDirectory.ToString().ToLowerInvariant()}",
            "welcome_version" => $"welcome_version={welcome.ShowVersion.ToString().ToLowerInvariant()}",
            "welcome_motd" => $"welcome_motd={welcome.ShowMotd.ToString().ToLowerInvariant()}",
            "welcome_motd_url" => $"welcome_motd_url={welcome.MotdUrl ?? "(none)"}",
            "welcome_motd_timeout_ms" => $"welcome_motd_timeout_ms={welcome.MotdTimeoutMs}",
            "welcome_motd_max_length" => $"welcome_motd_max_length={welcome.MotdMaxLength}",
            _ => $"Unknown config key '{key}'.",
        };
    }

    private string SetConfigValue(string? keyValue, TuiSettings settings)
    {
        if (string.IsNullOrWhiteSpace(keyValue))
            return "Usage: /config set <key> <value>";

        var parts = keyValue.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return "Usage: /config set <key> <value>";

        var key = NormalizeConfigKey(parts[0]);
        var value = parts[1].Trim();
        var welcome = WelcomePanelSettings.Normalize(settings.Welcome);

        switch (key)
        {
            case "theme":
                if (!ThemeAliases.TryGetValue(value, out var theme))
                    return $"Unknown theme '{value}'.";
                _onThemeChanged?.Invoke(theme);
                SaveSettings(settings with { Theme = theme });
                return $"theme={ToThemeToken(theme)}";

            case "vim_mode":
                if (!TryParseOnOff(value, out var vimEnabled))
                    return "vim_mode expects on/off.";
                _onVimModeChanged?.Invoke(vimEnabled);
                SaveSettings(settings with { VimMode = vimEnabled });
                return $"vim_mode={vimEnabled.ToString().ToLowerInvariant()}";

            case "output_style":
                if (!OutputStyleAliases.TryGetValue(value, out var outputStyle))
                    return $"Unknown output_style '{value}'.";
                _onOutputStyleChanged?.Invoke(outputStyle);
                SaveSettings(settings with { OutputStyle = outputStyle });
                if (outputStyle == OutputStyle.Json)
                    return "output_style=json (session-only)";
                return $"output_style={outputStyle.ToString().ToLowerInvariant()}";

            case "spinner_style":
                if (!Enum.TryParse<SpinnerStyle>(value, true, out var spinner))
                    return "Unknown spinner_style. Use none|minimal|normal|rich|nerdy.";
                _onSpinnerStyleChanged?.Invoke(spinner);
                SaveSettings(settings with { SpinnerStyle = spinner });
                return $"spinner_style={spinner.ToString().ToLowerInvariant()}";

            case "prompt_cache":
                if (!TryParseOnOff(value, out var promptCacheEnabled))
                    return "prompt_cache expects on/off.";
                _session.PromptCachingEnabled = promptCacheEnabled;
                SaveSettings(settings with { PromptCacheEnabled = promptCacheEnabled });
                return $"prompt_cache={promptCacheEnabled.ToString().ToLowerInvariant()}";

            case "prompt_cache_ttl":
                if (!PromptCachePolicy.TryParseTtl(value, out var ttl))
                    return "prompt_cache_ttl expects 5m or 1h.";
                _session.PromptCacheTtl = ttl;
                SaveSettings(settings with { PromptCacheTtl = ttl });
                return $"prompt_cache_ttl={PromptCachePolicy.ToToken(ttl)}";

            case "compact_auto":
                if (!TryParseOnOff(value, out var compactAuto))
                    return "compact_auto expects on/off.";
                SaveSettings(settings with { AutoCompact = compactAuto });
                return $"compact_auto={compactAuto.ToString().ToLowerInvariant()}";

            case "compact_threshold":
                if (!int.TryParse(value, out var pct) || pct < 0 || pct > 100)
                    return "compact_threshold expects a number from 0 to 100.";
                SaveSettings(settings with { CompactThresholdPercent = pct });
                return $"compact_threshold={pct}";

            case "sys_prompt_compaction":
                if (!Enum.TryParse<SystemPromptCompaction>(value, ignoreCase: true, out var compactionMode))
                    return "sys_prompt_compaction expects: off, auto, always.";
                SaveSettings(settings with { SystemPromptCompaction = compactionMode });
                return $"sys_prompt_compaction={compactionMode.ToString().ToLowerInvariant()}";

            case "sys_prompt_budget":
                if (!int.TryParse(value, out var budget) || budget < 0 || budget > 100)
                    return "sys_prompt_budget expects a number from 0 to 100.";
                SaveSettings(settings with { SystemPromptBudgetPercent = budget });
                return $"sys_prompt_budget={budget}";

            case "autorun":
                if (!TryParseOnOff(value, out var autorun))
                    return "autorun expects on/off.";
                _session.AutoRunEnabled = autorun;
                return $"autorun={autorun.ToString().ToLowerInvariant()}";

            case "permissions":
                if (!TryParseOnOff(value, out var permissionsOn))
                    return "permissions expects on/off.";
                _session.SkipPermissions = !permissionsOn;
                return $"permissions={permissionsOn.ToString().ToLowerInvariant()}";

            case "plan_mode":
                if (!TryParseOnOff(value, out var plan))
                    return "plan_mode expects on/off.";
                _session.PlanMode = plan;
                return $"plan_mode={plan.ToString().ToLowerInvariant()}";

            case "welcome_model_summary":
                if (!TryParseOnOff(value, out var showModelSummary))
                    return "welcome_model_summary expects on/off.";
                SaveSettings(settings with { Welcome = welcome with { ShowModelSummary = showModelSummary } });
                return $"welcome_model_summary={showModelSummary.ToString().ToLowerInvariant()}";

            case "welcome_services":
                if (!TryParseOnOff(value, out var showServices))
                    return "welcome_services expects on/off.";
                SaveSettings(settings with { Welcome = welcome with { ShowServices = showServices } });
                return $"welcome_services={showServices.ToString().ToLowerInvariant()}";

            case "welcome_cwd":
                if (!TryParseOnOff(value, out var showCwd))
                    return "welcome_cwd expects on/off.";
                SaveSettings(settings with { Welcome = welcome with { ShowWorkingDirectory = showCwd } });
                return $"welcome_cwd={showCwd.ToString().ToLowerInvariant()}";

            case "welcome_version":
                if (!TryParseOnOff(value, out var showVersion))
                    return "welcome_version expects on/off.";
                SaveSettings(settings with { Welcome = welcome with { ShowVersion = showVersion } });
                return $"welcome_version={showVersion.ToString().ToLowerInvariant()}";

            case "welcome_motd":
                if (!TryParseOnOff(value, out var showMotd))
                    return "welcome_motd expects on/off.";
                SaveSettings(settings with { Welcome = welcome with { ShowMotd = showMotd } });
                return $"welcome_motd={showMotd.ToString().ToLowerInvariant()}";

            case "welcome_motd_url":
                var motdUrl = value.Equals("none", StringComparison.OrdinalIgnoreCase)
                              || value.Equals("off", StringComparison.OrdinalIgnoreCase)
                              || value.Equals("-", StringComparison.Ordinal)
                    ? null
                    : value;
                SaveSettings(settings with { Welcome = welcome with { MotdUrl = motdUrl } });
                return $"welcome_motd_url={motdUrl ?? "(none)"}";

            case "welcome_motd_timeout_ms":
                if (!int.TryParse(value, out var motdTimeout) || motdTimeout < 100 || motdTimeout > 5000)
                    return "welcome_motd_timeout_ms expects a number from 100 to 5000.";
                SaveSettings(settings with { Welcome = welcome with { MotdTimeoutMs = motdTimeout } });
                return $"welcome_motd_timeout_ms={motdTimeout}";

            case "welcome_motd_max_length":
                if (!int.TryParse(value, out var motdMax) || motdMax < 40 || motdMax > 1000)
                    return "welcome_motd_max_length expects a number from 40 to 1000.";
                SaveSettings(settings with { Welcome = welcome with { MotdMaxLength = motdMax } });
                return $"welcome_motd_max_length={motdMax}";

            default:
                return $"Unknown config key '{parts[0]}'.";
        }
    }

    private static string NormalizeConfigKey(string key) =>
        key.Trim().ToLowerInvariant().Replace('-', '_').Replace('.', '_');

    private static bool TryParseOnOff(string value, out bool enabled)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "on":
            case "true":
            case "1":
            case "yes":
                enabled = true;
                return true;
            case "off":
            case "false":
            case "0":
            case "no":
                enabled = false;
                return true;
            default:
                enabled = false;
                return false;
        }
    }

    private static string ToThemeToken(TuiTheme theme) => theme switch
    {
        TuiTheme.DefaultDark => "default-dark",
        TuiTheme.SolarizedDark => "solarized-dark",
        TuiTheme.SolarizedLight => "solarized-light",
        TuiTheme.OneDark => "one-dark",
        TuiTheme.CatppuccinMocha => "catppuccin-mocha",
        TuiTheme.HighContrast => "high-contrast",
        _ => theme.ToString().ToLowerInvariant(),
    };

    private static void SaveSettings(TuiSettings settings)
    {
        try
        {
            settings.Save();
        }
#pragma warning disable CA1031
        catch { }
#pragma warning restore CA1031
    }

    // ── /stats ──────────────────────────────────────────────

    private async Task<string> ShowStatsAsync(string? arg, CancellationToken ct)
    {
        var token = (arg ?? string.Empty).Trim();
        if (string.Equals(token, "--history", StringComparison.OrdinalIgnoreCase))
            return await ShowHistoryStatsAsync(ct).ConfigureAwait(false);

        if (string.Equals(token, "--daily", StringComparison.OrdinalIgnoreCase))
            return await ShowDailyStatsAsync(ct).ConfigureAwait(false);

        return ShowSessionStats();
    }

    private string ShowSessionStats()
    {
        var session = _session.SessionInfo;
        if (session is null)
            return $"Session stats unavailable. Current token estimate: {_session.TotalTokens:N0}.";

        var turns = session.Turns;
        var first = turns.FirstOrDefault()?.CreatedAt;
        var last = turns.LastOrDefault()?.CreatedAt;
        var duration = first.HasValue && last.HasValue
            ? last.Value - first.Value
            : TimeSpan.Zero;

        var providerTotals = turns
            .Where(t => !string.IsNullOrWhiteSpace(t.ProviderName))
            .GroupBy(t => t.ProviderName!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Provider = g.Key,
                Tokens = g.Sum(t => t.TokensIn + t.TokensOut),
            })
            .OrderByDescending(x => x.Tokens)
            .ToList();

        var toolTotals = turns
            .SelectMany(t => t.ToolCalls)
            .GroupBy(tc => tc.ToolName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Tool = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Session Stats");
        sb.AppendLine($"Turns: {turns.Count} | Duration: {FormatDuration(duration)} | Tokens: {session.TotalTokens:N0}");

        if (providerTotals.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Provider breakdown:");
            var total = Math.Max(1L, providerTotals.Sum(p => p.Tokens));
            foreach (var p in providerTotals)
            {
                var pct = (double)p.Tokens / total;
                sb.AppendLine($"  {p.Provider,-12} {BuildBar(pct, 20)} {(pct * 100):F0}% ({p.Tokens:N0})");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Tool usage:");
        if (toolTotals.Count == 0)
        {
            sb.AppendLine("  No tool calls recorded.");
        }
        else
        {
            var max = toolTotals.Max(t => t.Count);
            foreach (var tool in toolTotals)
            {
                var pct = max == 0 ? 0 : (double)tool.Count / max;
                sb.AppendLine($"  {tool.Tool,-14} {BuildBar(pct, 12, '▓', '░')} {tool.Count} calls");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private async Task<string> ShowHistoryStatsAsync(CancellationToken ct)
    {
        if (_session.Store is null)
            return "History stats unavailable: session persistence not initialized.";

        _ = ct;
        var sessions = await _session.Store.ListSessionsAsync(limit: 200).ConfigureAwait(false);
        if (sessions.Count == 0)
            return "No historical sessions found.";

        var totalTokens = sessions.Sum(s => s.TotalTokens);
        var totalMessages = sessions.Sum(s => s.MessageCount);
        var active = sessions.Count(s => s.IsActive);

        var providerTotals = sessions
            .Where(s => !string.IsNullOrWhiteSpace(s.ProviderName))
            .GroupBy(s => s.ProviderName!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Provider = g.Key, Tokens = g.Sum(s => s.TotalTokens) })
            .OrderByDescending(x => x.Tokens)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"History Stats ({sessions.Count} sessions)");
        sb.AppendLine($"Total tokens: {totalTokens:N0}");
        sb.AppendLine($"Total messages: {totalMessages:N0}");
        sb.AppendLine($"Active sessions: {active}");
        sb.AppendLine();

        if (providerTotals.Count > 0)
        {
            var max = Math.Max(1L, providerTotals.Max(p => p.Tokens));
            sb.AppendLine("Provider totals:");
            foreach (var p in providerTotals)
            {
                var pct = (double)p.Tokens / max;
                sb.AppendLine($"  {p.Provider,-12} {BuildBar(pct, 16)} {p.Tokens:N0}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private async Task<string> ShowDailyStatsAsync(CancellationToken ct)
    {
        if (_session.Store is null)
            return "Daily stats unavailable: session persistence not initialized.";

        _ = ct;
        var sessions = await _session.Store.ListSessionsAsync(limit: 500).ConfigureAwait(false);
        if (sessions.Count == 0)
            return "No sessions available for daily stats.";

        var byDay = sessions
            .GroupBy(s => s.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .ToList();

        var max = Math.Max(1L, byDay.Max(g => g.Sum(s => s.TotalTokens)));
        var sb = new StringBuilder();
        sb.AppendLine("Daily Usage");
        foreach (var day in byDay)
        {
            var tokens = day.Sum(s => s.TotalTokens);
            var pct = (double)tokens / max;
            sb.AppendLine($"  {day.Key:yyyy-MM-dd} {BuildBar(pct, 18)} {tokens:N0} tokens ({day.Count()} sessions)");
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildBar(double ratio, int width, char fill = '█', char empty = '░')
    {
        ratio = Math.Clamp(ratio, 0, 1);
        var filled = (int)Math.Round(ratio * width, MidpointRounding.AwayFromZero);
        return new string(fill, filled) + new string(empty, Math.Max(0, width - filled));
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 60)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        return $"{duration.TotalSeconds:F0}s";
    }

    // ── /agents and /hooks ─────────────────────────────────

    private sealed class AgentProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = "General-purpose assistant";
        public string Provider { get; set; } = "default";
        public string Model { get; set; } = "default";
        public List<string> Tools { get; set; } = [];
        public bool Enabled { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    private sealed class HookProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Event { get; set; } = "post-tool";
        public string Command { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    private static string AgentsPath => Path.Combine(DataDirectories.Root, "agents.json");
    private static string HooksPath => Path.Combine(DataDirectories.Root, "hooks.json");

    private async Task<string> HandleAgentsAsync(string? arg, CancellationToken ct)
    {
        var profiles = await LoadAgentsAsync(ct).ConfigureAwait(false);
        var tokens = (arg ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var action = tokens.Length == 0 ? "list" : tokens[0].ToLowerInvariant();
        var rest = tokens.Length > 1 ? tokens[1] : null;

        switch (action)
        {
            case "list":
                if (profiles.Count == 0)
                    return "No agent profiles configured. Create one with: /agents create <name>";
                return "Agent profiles:\n" + string.Join('\n', profiles.Select(p =>
                    $"  - {p.Name} ({(p.Enabled ? "enabled" : "disabled")}) — {p.Description} | {p.Provider}/{p.Model}"));

            case "create":
                if (string.IsNullOrWhiteSpace(rest))
                    return "Usage: /agents create <name>";
                if (profiles.Any(p => string.Equals(p.Name, rest, StringComparison.OrdinalIgnoreCase)))
                    return $"Agent '{rest}' already exists.";
                profiles.Add(new AgentProfile { Name = rest.Trim() });
                await SaveAgentsAsync(profiles, ct).ConfigureAwait(false);
                return $"Created agent profile '{rest.Trim()}'.";

            case "delete":
                if (string.IsNullOrWhiteSpace(rest))
                    return "Usage: /agents delete <name>";
                profiles.RemoveAll(p => string.Equals(p.Name, rest, StringComparison.OrdinalIgnoreCase));
                await SaveAgentsAsync(profiles, ct).ConfigureAwait(false);
                return $"Deleted agent profile '{rest.Trim()}' (if it existed).";

            case "set":
                return await SetAgentFieldAsync(rest, profiles, ct).ConfigureAwait(false);

            default:
                return "Usage: /agents [list|create <name>|delete <name>|set <name> <field> <value>]";
        }
    }

    private async Task<string> SetAgentFieldAsync(
        string? rest,
        List<AgentProfile> profiles,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rest))
            return "Usage: /agents set <name> <field> <value>";

        var parts = rest.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return "Usage: /agents set <name> <field> <value>";

        var name = parts[0];
        var field = parts[1].ToLowerInvariant();
        var value = parts[2];

        var profile = profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
            return $"Agent '{name}' not found.";

        switch (field)
        {
            case "description":
                profile.Description = value;
                break;
            case "provider":
                profile.Provider = value;
                break;
            case "model":
                profile.Model = value;
                break;
            case "tools":
                profile.Tools = value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
                break;
            case "enabled":
                if (!TryParseOnOff(value, out var enabled))
                    return "enabled expects on/off.";
                profile.Enabled = enabled;
                break;
            default:
                return "Supported fields: description, provider, model, tools, enabled";
        }

        profile.UpdatedAt = DateTime.UtcNow;
        await SaveAgentsAsync(profiles, ct).ConfigureAwait(false);
        return $"Updated agent '{profile.Name}' ({field}).";
    }

    private async Task<string> HandleHooksAsync(string? arg, CancellationToken ct)
    {
        var hooks = await LoadHooksAsync(ct).ConfigureAwait(false);
        var tokens = (arg ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var action = tokens.Length == 0 ? "list" : tokens[0].ToLowerInvariant();
        var rest = tokens.Length > 1 ? tokens[1] : null;

        switch (action)
        {
            case "list":
                if (hooks.Count == 0)
                    return "No hooks configured. Create one with: /hooks create <name>";
                return "Hooks:\n" + string.Join('\n', hooks.Select(h =>
                    $"  - {h.Name} ({(h.Enabled ? "enabled" : "disabled")}) [{h.Event}] {h.Command}"));

            case "create":
                if (string.IsNullOrWhiteSpace(rest))
                    return "Usage: /hooks create <name>";
                if (hooks.Any(h => string.Equals(h.Name, rest, StringComparison.OrdinalIgnoreCase)))
                    return $"Hook '{rest}' already exists.";
                hooks.Add(new HookProfile { Name = rest.Trim(), Command = "echo hook", Event = "post-tool" });
                await SaveHooksAsync(hooks, ct).ConfigureAwait(false);
                return $"Created hook '{rest.Trim()}'.";

            case "delete":
                if (string.IsNullOrWhiteSpace(rest))
                    return "Usage: /hooks delete <name>";
                hooks.RemoveAll(h => string.Equals(h.Name, rest, StringComparison.OrdinalIgnoreCase));
                await SaveHooksAsync(hooks, ct).ConfigureAwait(false);
                return $"Deleted hook '{rest.Trim()}' (if it existed).";

            case "toggle":
                if (string.IsNullOrWhiteSpace(rest))
                    return "Usage: /hooks toggle <name>";
                var target = hooks.FirstOrDefault(h => string.Equals(h.Name, rest, StringComparison.OrdinalIgnoreCase));
                if (target is null) return $"Hook '{rest}' not found.";
                target.Enabled = !target.Enabled;
                target.UpdatedAt = DateTime.UtcNow;
                await SaveHooksAsync(hooks, ct).ConfigureAwait(false);
                return $"Hook '{target.Name}' {(target.Enabled ? "enabled" : "disabled")}.";

            case "set":
                return await SetHookFieldAsync(rest, hooks, ct).ConfigureAwait(false);

            default:
                return "Usage: /hooks [list|create <name>|delete <name>|toggle <name>|set <name> <field> <value>]";
        }
    }

    private async Task<string> SetHookFieldAsync(
        string? rest,
        List<HookProfile> hooks,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rest))
            return "Usage: /hooks set <name> <field> <value>";

        var parts = rest.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return "Usage: /hooks set <name> <field> <value>";

        var name = parts[0];
        var field = parts[1].ToLowerInvariant();
        var value = parts[2];
        var hook = hooks.FirstOrDefault(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase));
        if (hook is null)
            return $"Hook '{name}' not found.";

        switch (field)
        {
            case "event":
                hook.Event = value;
                break;
            case "command":
                hook.Command = value;
                break;
            case "enabled":
                if (!TryParseOnOff(value, out var enabled))
                    return "enabled expects on/off.";
                hook.Enabled = enabled;
                break;
            default:
                return "Supported fields: event, command, enabled";
        }

        hook.UpdatedAt = DateTime.UtcNow;
        await SaveHooksAsync(hooks, ct).ConfigureAwait(false);
        return $"Updated hook '{hook.Name}' ({field}).";
    }

    private static async Task<List<AgentProfile>> LoadAgentsAsync(CancellationToken ct)
    {
        if (!File.Exists(AgentsPath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(AgentsPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<AgentProfile>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static async Task SaveAgentsAsync(List<AgentProfile> profiles, CancellationToken ct)
    {
        Directory.CreateDirectory(DataDirectories.Root);
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        await File.WriteAllTextAsync(AgentsPath, json, ct).ConfigureAwait(false);
    }

    private static async Task<List<HookProfile>> LoadHooksAsync(CancellationToken ct)
    {
        if (!File.Exists(HooksPath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(HooksPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<HookProfile>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static async Task SaveHooksAsync(List<HookProfile> hooks, CancellationToken ct)
    {
        Directory.CreateDirectory(DataDirectories.Root);
        var json = JsonSerializer.Serialize(hooks, JsonOptions);
        await File.WriteAllTextAsync(HooksPath, json, ct).ConfigureAwait(false);
    }

    // ── /memory ─────────────────────────────────────────────

    private static string MemoryFilePath => Path.Combine(Directory.GetCurrentDirectory(), "JDAI.md");

    private static async Task<string> HandleMemoryAsync(string? arg, CancellationToken ct)
    {
        var tokens = (arg ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var action = tokens.Length == 0 ? "show" : tokens[0].ToLowerInvariant();
        var rest = tokens.Length > 1 ? tokens[1] : null;

        switch (action)
        {
            case "show":
            case "view":
                if (!File.Exists(MemoryFilePath))
                    return "JDAI.md not found. Create one with /init or /memory reset.";
                var content = await File.ReadAllTextAsync(MemoryFilePath, ct).ConfigureAwait(false);
                return $"Project memory ({MemoryFilePath}):\n\n{TrimTo(content, 12_000)}";

            case "edit":
            case "set":
                if (string.IsNullOrWhiteSpace(rest))
                    return "Usage: /memory edit <new content>";
                await File.WriteAllTextAsync(MemoryFilePath, rest, ct).ConfigureAwait(false);
                return $"Updated {MemoryFilePath}.";

            case "append":
                if (string.IsNullOrWhiteSpace(rest))
                    return "Usage: /memory append <text>";
                await File.AppendAllTextAsync(MemoryFilePath, Environment.NewLine + rest, ct).ConfigureAwait(false);
                return $"Appended to {MemoryFilePath}.";

            case "reset":
                var template = """
                    # Project Instructions

                    ## Conventions
                    -

                    ## Architecture
                    -

                    ## Testing
                    -
                    """;
                await File.WriteAllTextAsync(MemoryFilePath, template, ct).ConfigureAwait(false);
                return $"Reset {MemoryFilePath} to template.";

            default:
                return "Usage: /memory [show|edit <text>|append <text>|reset]";
        }
    }

    private async Task<string> HandleDefaultAsync(string? arg, CancellationToken ct)
    {
        if (_configStore is null)
        {
            return "Default settings not available (config store not initialized).";
        }

        var projectPath = _session.SessionInfo?.ProjectPath ?? Directory.GetCurrentDirectory();

        // No arguments → show current defaults
        if (string.IsNullOrWhiteSpace(arg))
        {
            var config = await _configStore.ReadAsync(ct).ConfigureAwait(false);
            var globalProvider = config.Defaults.Provider ?? "(not set)";
            var globalModel = config.Defaults.Model ?? "(not set)";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Global defaults:");
            sb.AppendLine($"  Provider: {globalProvider}");
            sb.AppendLine($"  Model:    {globalModel}");

            if (config.ProjectDefaults.TryGetValue(projectPath, out var proj))
            {
                sb.AppendLine();
                sb.AppendLine($"Project defaults ({projectPath}):");
                sb.AppendLine($"  Provider: {proj.Provider ?? "(not set)"}");
                sb.AppendLine($"  Model:    {proj.Model ?? "(not set)"}");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine($"No project defaults for {projectPath}.");
            }

            return sb.ToString().TrimEnd();
        }

        var tokens = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // /default project provider <name> | /default project model <id>
        if (tokens.Length >= 3
            && string.Equals(tokens[0], "project", StringComparison.OrdinalIgnoreCase))
        {
            var subCmd = tokens[1];
            var value = string.Join(' ', tokens.Skip(2));

            if (string.Equals(subCmd, "provider", StringComparison.OrdinalIgnoreCase))
            {
                await _configStore.SetDefaultProviderAsync(value, projectPath, ct).ConfigureAwait(false);
                return $"Project default provider set to '{value}' for {projectPath}.";
            }

            if (string.Equals(subCmd, "model", StringComparison.OrdinalIgnoreCase))
            {
                await _configStore.SetDefaultModelAsync(value, projectPath, ct).ConfigureAwait(false);
                return $"Project default model set to '{value}' for {projectPath}.";
            }

            return "Usage: /default project provider <name> | /default project model <id>";
        }

        // /default provider <name> | /default model <id>
        if (tokens.Length >= 2)
        {
            var subCmd = tokens[0];
            var value = string.Join(' ', tokens.Skip(1));

            if (string.Equals(subCmd, "provider", StringComparison.OrdinalIgnoreCase))
            {
                await _configStore.SetDefaultProviderAsync(value, ct: ct).ConfigureAwait(false);
                return $"Global default provider set to '{value}'.";
            }

            if (string.Equals(subCmd, "model", StringComparison.OrdinalIgnoreCase))
            {
                await _configStore.SetDefaultModelAsync(value, ct: ct).ConfigureAwait(false);
                return $"Global default model set to '{value}'.";
            }
        }

        return """
            Usage:
              /default                        — Show current defaults
              /default provider <name>        — Set global default provider
              /default model <id>             — Set global default model
              /default project provider <name> — Set project default provider
              /default project model <id>     — Set project default model
            """;
    }
}
