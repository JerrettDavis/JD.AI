using JD.AI.Agent;
using JD.AI.Core.Agents.Checkpointing;
using JD.AI.Core.Config;
using JD.AI.Core.Mcp;
using JD.AI.Core.Plugins;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Core.Providers.ModelSearch;
using JD.AI.Core.Sessions;
using JD.AI.Core.Tools;
using JD.AI.Rendering;
using JD.AI.Workflows;
using JD.AI.Workflows.Store;
using JD.SemanticKernel.Extensions.Mcp;
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
    private readonly IWorkflowCatalog? _workflowCatalog;
    private readonly IWorkflowStore? _workflowStore;
    private readonly WorkflowEmitter _workflowEmitter;
    private readonly Action<SpinnerStyle>? _onSpinnerStyleChanged;
    private readonly Func<SpinnerStyle>? _getSpinnerStyle;
    private readonly McpManager _mcpManager;
    private readonly AtomicConfigStore? _configStore;
    private readonly ModelSearchAggregator? _modelSearchAggregator;

    public SlashCommandRouter(
        AgentSession session,
        IProviderRegistry registry,
        InstructionsResult? instructions = null,
        ICheckpointStrategy? checkpointStrategy = null,
        PluginLoader? pluginLoader = null,
        IWorkflowCatalog? workflowCatalog = null,
        Func<SpinnerStyle>? getSpinnerStyle = null,
        Action<SpinnerStyle>? onSpinnerStyleChanged = null,
        ProviderConfigurationManager? providerConfig = null,
        McpManager? mcpManager = null,
        AtomicConfigStore? configStore = null,
        ModelSearchAggregator? modelSearchAggregator = null,
        IWorkflowStore? workflowStore = null)
    {
        _session = session;
        _registry = registry;
        _instructions = instructions;
        _checkpointStrategy = checkpointStrategy;
        _pluginLoader = pluginLoader;
        _workflowCatalog = workflowCatalog;
        _workflowStore = workflowStore;
        _workflowEmitter = new WorkflowEmitter();
        _getSpinnerStyle = getSpinnerStyle;
        _onSpinnerStyleChanged = onSpinnerStyleChanged;
        _providerConfig = providerConfig;
        _mcpManager = mcpManager ?? new McpManager();
        _configStore = configStore;
        _modelSearchAggregator = modelSearchAggregator;
    }

    public bool IsSlashCommand(string input) =>
        input.TrimStart().StartsWith('/');

    public async Task<string?> ExecuteAsync(string input, CancellationToken ct = default)
    {
        var parts = input.TrimStart().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToUpperInvariant();
        var arg = parts.Length > 1 ? parts[1] : null;

        return cmd switch
        {
            "/HELP" or "/JDAI-HELP" => GetHelp(),
            "/MODELS" or "/JDAI-MODELS" => await ListModelsAsync(ct).ConfigureAwait(false),
            "/MODEL" or "/JDAI-MODEL" => await SwitchModelAsync(arg, ct).ConfigureAwait(false),
            "/PROVIDERS" or "/JDAI-PROVIDERS" => await ListProvidersAsync(ct).ConfigureAwait(false),
            "/PROVIDER" or "/JDAI-PROVIDER" => await HandleProviderCommandAsync(arg, ct).ConfigureAwait(false),
            "/CLEAR" or "/JDAI-CLEAR" => ClearHistory(),
            "/COMPACT" or "/JDAI-COMPACT" => await CompactAsync(ct).ConfigureAwait(false),
            "/COST" or "/JDAI-COST" => GetCost(),
            "/AUTORUN" or "/JDAI-AUTORUN" => ToggleAutoRun(arg),
            "/PERMISSIONS" or "/JDAI-PERMISSIONS" => TogglePermissions(arg),
            "/SESSIONS" or "/JDAI-SESSIONS" => await ListSessionsAsync(ct).ConfigureAwait(false),
            "/RESUME" or "/JDAI-RESUME" => await ResumeSessionAsync(arg, ct).ConfigureAwait(false),
            "/NAME" or "/JDAI-NAME" => NameSession(arg),
            "/HISTORY" or "/JDAI-HISTORY" => ShowHistory(),
            "/EXPORT" or "/JDAI-EXPORT" => await ExportSessionAsync(ct).ConfigureAwait(false),
            "/UPDATE" or "/JDAI-UPDATE" => await CheckUpdateAsync(ct).ConfigureAwait(false),
            "/INSTRUCTIONS" or "/JDAI-INSTRUCTIONS" => ShowInstructions(),
            "/PLUGINS" or "/JDAI-PLUGINS" => ShowPlugins(),
            "/CHECKPOINT" or "/JDAI-CHECKPOINT" => await HandleCheckpointAsync(arg, ct).ConfigureAwait(false),
            "/SANDBOX" or "/JDAI-SANDBOX" => ShowSandboxInfo(),
            "/WORKFLOW" or "/JDAI-WORKFLOW" => await HandleWorkflowAsync(arg, ct).ConfigureAwait(false),
            "/SPINNER" or "/JDAI-SPINNER" => HandleSpinner(arg),
            "/LOCAL" or "/JDAI-LOCAL" => await HandleLocalModelAsync(arg, ct).ConfigureAwait(false),
            "/MCP" or "/JDAI-MCP" => await HandleMcpAsync(arg, ct).ConfigureAwait(false),
            "/CONTEXT" or "/JDAI-CONTEXT" => GetContextUsage(),
            "/COMPACT-SYSTEM-PROMPT" or "/JDAI-COMPACT-SYSTEM-PROMPT" => await CompactSystemPromptAsync(arg, ct).ConfigureAwait(false),
            "/COPY" or "/JDAI-COPY" => await CopyLastResponseInstanceAsync().ConfigureAwait(false),
            "/DIFF" or "/JDAI-DIFF" => await ShowDiffAsync(ct).ConfigureAwait(false),
            "/INIT" or "/JDAI-INIT" => await InitProjectFileAsync(ct).ConfigureAwait(false),
            "/PLAN" or "/JDAI-PLAN" => TogglePlanMode(),
            "/DOCTOR" or "/JDAI-DOCTOR" => await RunDoctorAsync(ct).ConfigureAwait(false),
            "/FORK" or "/JDAI-FORK" => await ForkSessionAsync(parts, ct).ConfigureAwait(false),
            "/DEFAULT" or "/JDAI-DEFAULT" => await HandleDefaultAsync(arg, ct).ConfigureAwait(false),
            "/QUIT" or "/EXIT" or "/JDAI-QUIT" or "/JDAI-EXIT" => null, // Signal exit
            _ => $"Unknown command: {parts[0]}. Type /help for available commands.",
        };
    }

    private static string GetHelp() => """
        Available commands (all accept /jdai- prefix, e.g. /jdai-config):
          /help           — Show this help
          /models         — Browse and switch models interactively
          /model [id]     — Switch model (interactive picker or by name)
          /model search   — Search for models across all providers
          /model url      — Pull a model by URL or identifier
          /providers      — List detected providers
          /provider       — Show current provider (subcommands: add, remove, test, list)
          /clear          — Clear chat history
          /compact        — Force context compaction
          /cost           — Show token usage
          /autorun        — Toggle auto-approve for tools
          /permissions    — Toggle permission checks (off = skip all)
          /sessions       — List recent sessions
          /resume [id]    — Resume a previous session
          /name <name>    — Name the current session
          /history        — Show session turn history
          /export         — Export current session to JSON
          /update         — Check for and apply updates
          /instructions   — Show loaded project instructions
          /plugins        — List loaded plugins
          /checkpoint     — List, restore, or clear checkpoints
          /sandbox        — Show sandbox mode info
          /workflow       — Manage workflows (list|show|export|replay|refine|catalog|publish|install|search|versions)
          /spinner [style] — Set progress style (none|minimal|normal|rich|nerdy)
          /local <cmd>    — Manage local models (list|add|scan|remove|search|download)
          /mcp [cmd]      — Manage MCP servers (list|add|remove|enable|disable)
          /context        — Show context window usage
          /compact-system-prompt [off|auto|always] — Compact system prompt or set mode
          /copy           — Copy last response to clipboard
          /diff           — Show uncommitted changes
          /init           — Initialize JDAI.md project file
          /plan           — Toggle plan mode (explore only)
          /doctor         — Run self-diagnostics
          /fork [name]    — Fork conversation to new session
          /default        — Manage default provider/model (global & per-project)
          /quit           — Exit jdai
        """;

    private async Task<string> ListModelsAsync(CancellationToken ct)
    {
        var models = await _registry.GetModelsAsync(ct).ConfigureAwait(false);
        if (models.Count == 0)
        {
            return "No models available. Check provider authentication.";
        }

        var selected = ModelPicker.Pick(models, _session.CurrentModel);
        if (selected != null && !string.Equals(selected.Id, _session.CurrentModel?.Id, StringComparison.Ordinal))
        {
            _session.SwitchModel(selected);
            return $"Switched to {selected.DisplayName} ({selected.ProviderName})";
        }

        return selected != null
            ? $"Current model: {selected.DisplayName} ({selected.ProviderName})"
            : "No model selected.";
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

        var models = await _registry.GetModelsAsync(ct).ConfigureAwait(false);

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
                _session.SwitchModel(selected);
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

        _session.SwitchModel(model);
        return $"Switched to {model.DisplayName} ({model.ProviderName})";
    }

    private async Task<string> HandleModelSearchAsync(string query, CancellationToken ct)
    {
        if (_modelSearchAggregator is null)
            return "Model search is not available (no search providers configured).";

        if (string.IsNullOrWhiteSpace(query))
            return "Usage: /model search <query>";

        IReadOnlyList<RemoteModelResult> results;
        try
        {
            results = await _modelSearchAggregator.SearchAllAsync(query, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"Search failed: {ex.Message}";
        }

        if (results.Count == 0)
            return $"No models found for '{query}'.";

        // Display results table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold]Search results for '{Markup.Escape(query)}'[/]")
            .AddColumn(new TableColumn("[bold]Provider[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Model[/]"))
            .AddColumn(new TableColumn("[bold]Size[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Status[/]"));

        foreach (var r in results)
        {
            var statusMarkup = r.Status switch
            {
                "Installed" => "[green]Installed[/]",
                "Available" or "Pull" => $"[yellow]{Markup.Escape(r.Status)}[/]",
                "Download" => "[blue]Download[/]",
                _ => Markup.Escape(r.Status),
            };

            table.AddRow(
                Markup.Escape(r.ProviderName),
                Markup.Escape(r.DisplayName),
                Markup.Escape(r.Size ?? "-"),
                statusMarkup);
        }

        AnsiConsole.Write(table);

        // Interactive selection
        var choices = results
            .Select(r => $"{r.ProviderName}: {r.DisplayName}")
            .Append("Cancel")
            .ToList();

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a model to pull:")
                .PageSize(15)
                .AddChoices(choices));

        if (string.Equals(selection, "Cancel", StringComparison.Ordinal))
            return "Model search cancelled.";

        var selectedIndex = choices.IndexOf(selection);
        var selected = results[selectedIndex];

        if (string.Equals(selected.Status, "Installed", StringComparison.Ordinal))
        {
            // Already installed — switch to it
            var models = await _registry.GetModelsAsync(ct).ConfigureAwait(false);
            var match = models.FirstOrDefault(m =>
                m.Id.Contains(selected.Id, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                _session.SwitchModel(match);
                return $"Switched to {match.DisplayName} ({match.ProviderName})";
            }

            return $"Model '{selected.DisplayName}' is installed but not found in the provider registry.";
        }

        // Pull the model
        return await PullAndSwitchAsync(selected, ct).ConfigureAwait(false);
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
        var models = await _registry.GetModelsAsync(ct).ConfigureAwait(false);
        var match = models.FirstOrDefault(m =>
            m.Id.Contains(selected.Id, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            _session.SwitchModel(match);
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
        var providers = await _registry.DetectProvidersAsync(ct).ConfigureAwait(false);
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
        var providers = await _registry.DetectProvidersAsync(ct).ConfigureAwait(false);
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
        var providers = await _registry.DetectProvidersAsync(ct).ConfigureAwait(false);
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
            .PageSize(15)
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
            _session.SwitchModel(selected.Models[0]);
            return $"Switched to {selected.Models[0].DisplayName} ({selected.Name})";
        }

        // Multiple models — show model picker
        var model = ModelPicker.Pick(selected.Models, _session.CurrentModel);
        if (model is null)
            return "Model selection cancelled.";

        _session.SwitchModel(model);
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
                  mistral, bedrock, huggingface, openai-compat
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
        var providers = await _registry.DetectProvidersAsync(ct).ConfigureAwait(false);

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

    private string GetCost()
    {
        var tokens = _session.TotalTokens;
        return $"Token usage: {tokens:N0} total";
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
            return "⚠ Permission checks DISABLED — all tools will run without confirmation.";
        }

        if (string.Equals(arg, "on", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "true", StringComparison.OrdinalIgnoreCase))
        {
            _session.SkipPermissions = false;
            return "Permission checks enabled — safety tiers apply.";
        }

        return $"Permission checks are {(_session.SkipPermissions ? "OFF (all skipped)" : "ON")}. Usage: /permissions [on|off]";
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
        return shouldRestart
            ? "Update applied. Please restart jdai."
            : $"Update available: {info.CurrentVersion} → {info.LatestVersion}";
    }

    // ── New Phase commands ─────────────────────────────────

    private string ShowInstructions() =>
        _instructions?.ToSummary() ?? "No project instructions loaded.";

    private string ShowPlugins()
    {
        if (_pluginLoader is null)
            return "Plugin loader not available.";

        var plugins = _pluginLoader.GetAll();
        if (plugins.Count == 0)
            return "No plugins loaded.";

        var lines = plugins.Select(p =>
            $"  ✓ {p.Name} v{p.Version} (loaded {p.LoadedAt:g})");
        return $"Loaded plugins ({plugins.Count}):\n{string.Join('\n', lines)}";
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
            "EXPORT" when param is not null => await ExportWorkflowAsync(param, ct).ConfigureAwait(false),
            "EXPORT" => "Usage: /workflow export <name> [json|csharp|mermaid]",
            "REPLAY" when param is not null => await ReplayWorkflowAsync(param, ct).ConfigureAwait(false),
            "REPLAY" => "Usage: /workflow replay <name> [version]",
            "REFINE" when param is not null => RefineWorkflowInfo(param),
            "REFINE" => "Usage: /workflow refine <name>",
            "CATALOG" => await CatalogSharedWorkflowsAsync(param, ct).ConfigureAwait(false),
            "PUBLISH" when param is not null => await PublishWorkflowAsync(param, ct).ConfigureAwait(false),
            "PUBLISH" => "Usage: /workflow publish <name>",
            "INSTALL" when param is not null => await InstallWorkflowAsync(param, ct).ConfigureAwait(false),
            "INSTALL" => "Usage: /workflow install <name[@version]>",
            "SEARCH" when param is not null => await SearchWorkflowsAsync(param, ct).ConfigureAwait(false),
            "SEARCH" => "Usage: /workflow search <query>",
            "VERSIONS" when param is not null => await ShowWorkflowVersionsAsync(param, ct).ConfigureAwait(false),
            "VERSIONS" => "Usage: /workflow versions <name>",
            _ => "Usage: /workflow [list|show <name>|export <name> [format]|replay <name> [version]|refine <name>|catalog|publish <name>|install <name[@version]>|search <query>|versions <name>]",
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

    private static string RefineWorkflowInfo(string name) =>
        $"To refine '{name}', export it (e.g. /workflow export {name} csharp), " +
        "edit the steps, then save a new version via the agent.";

    // ── Shared workflow store commands ────────────────────────

    private static string WorkflowStoreNotConfigured =>
        "Shared workflow store not configured. Pass --workflow-store or inject an IWorkflowStore to enable.";

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

    private async Task<string> HandleLocalModelAsync(string? arg, CancellationToken ct)
    {
        var parts = (arg ?? "").Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var subCommand = parts.Length > 0 ? parts[0].ToUpperInvariant() : "HELP";
        var subArg = parts.Length > 1 ? parts[1] : null;

        // Find the LocalModelDetector in our registry
        var providers = await _registry.DetectProvidersAsync(ct).ConfigureAwait(false);
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
            lines.AppendLine($"  • {m.Id} — {m.DisplayName}");
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
        return $"Context: [{bar}] {used:N0}/{max:N0} tokens ({pct:F1}%)";
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

            var after = await _session.CompactSystemPromptAsync(budgetTokens, ct).ConfigureAwait(false);
            return before == after
                ? $"System prompt already within budget ({before:N0} tokens ≤ {budgetTokens:N0} budget)."
                : $"System prompt compacted: {before:N0} → {after:N0} tokens (budget: {budgetTokens:N0}).";
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

        var providers = await _registry.DetectProvidersAsync(ct).ConfigureAwait(false);
        var providerList = providers.ToList();
        sb.AppendLine($"Providers: {providerList.Count(p => p.IsAvailable)} available / {providerList.Count} total");

        var allModels = await _registry.GetModelsAsync(ct).ConfigureAwait(false);
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

    private async Task<string> ForkSessionAsync(string[] cmdParts, CancellationToken ct)
    {
        _ = ct;
        if (_session.Store == null || _session.SessionInfo == null)
        {
            return "No active session to fork.";
        }

        var forkName = cmdParts.Length > 1 ? string.Join(' ', cmdParts.Skip(1)) : null;
        var forkedSession = await _session.ForkSessionAsync(forkName).ConfigureAwait(false);
        return $"Forked to new session: {forkedSession?.Id ?? "failed"}";
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
