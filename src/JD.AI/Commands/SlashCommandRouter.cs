using JD.AI.Agent;
using JD.AI.Core.Agents.Checkpointing;
using JD.AI.Core.Plugins;
using JD.AI.Core.Providers;
using JD.AI.Core.Sessions;
using JD.AI.Rendering;
using JD.AI.Workflows;

namespace JD.AI.Commands;

/// <summary>
/// Routes slash commands to their handlers.
/// </summary>
public sealed class SlashCommandRouter : ISlashCommandRouter
{
    private readonly AgentSession _session;
    private readonly IProviderRegistry _registry;
    private readonly InstructionsResult? _instructions;
    private readonly ICheckpointStrategy? _checkpointStrategy;
    private readonly PluginLoader? _pluginLoader;
    private readonly IWorkflowCatalog? _workflowCatalog;
    private readonly WorkflowEmitter _workflowEmitter;

    public SlashCommandRouter(
        AgentSession session,
        IProviderRegistry registry,
        InstructionsResult? instructions = null,
        ICheckpointStrategy? checkpointStrategy = null,
        PluginLoader? pluginLoader = null,
        IWorkflowCatalog? workflowCatalog = null)
    {
        _session = session;
        _registry = registry;
        _instructions = instructions;
        _checkpointStrategy = checkpointStrategy;
        _pluginLoader = pluginLoader;
        _workflowCatalog = workflowCatalog;
        _workflowEmitter = new WorkflowEmitter();
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
            "/PROVIDER" or "/JDAI-PROVIDER" => GetCurrentProvider(),
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
            "/QUIT" or "/EXIT" or "/JDAI-QUIT" or "/JDAI-EXIT" => null, // Signal exit
            _ => $"Unknown command: {parts[0]}. Type /help for available commands.",
        };
    }

    private static string GetHelp() => """
        Available commands (all accept /jdai- prefix, e.g. /jdai-config):
          /help           — Show this help
          /models         — Browse and switch models interactively
          /model [id]     — Switch model (interactive picker or by name)
          /providers      — List detected providers
          /provider       — Show current provider
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
          /workflow       — Manage workflows (list|show|export|replay|refine)
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

    private async Task<string> ListProvidersAsync(CancellationToken ct)
    {
        var providers = await _registry.DetectProvidersAsync(ct).ConfigureAwait(false);
        var lines = providers.Select(p =>
        {
            var status = p.IsAvailable ? "✅" : "❌";
            return $"  {status} {p.Name}: {p.StatusMessage}";
        });

        return $"Providers:\n{string.Join('\n', lines)}";
    }

    private string GetCurrentProvider() =>
        _session.CurrentModel is { } m
            ? $"Current: {m.DisplayName} ({m.ProviderName})"
            : "No model selected.";

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
            _ => "Usage: /workflow [list|show <name>|export <name> [format]|replay <name> [version]|refine <name>]",
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

    private static string FlattenSteps(IEnumerable<AgentStepDefinition> steps, int indent)
    {
        var sb = new System.Text.StringBuilder();
        var pad = new string(' ', indent * 2);
        foreach (var step in steps)
        {
            var prefix = step.Kind switch
            {
                AgentStepKind.Skill => "▶ Skill",
                AgentStepKind.Tool => "🔧 Tool",
                AgentStepKind.Nested => "📦 Nested",
                AgentStepKind.Loop => "🔁 Loop",
                AgentStepKind.Conditional => "❖ If",
                _ => "•",
            };
            sb.AppendLine($"{pad}{prefix}: {step.Name}");
            if (step.SubSteps.Count > 0)
                sb.Append(FlattenSteps(step.SubSteps, indent + 1));
        }

        return sb.ToString();
    }
}
