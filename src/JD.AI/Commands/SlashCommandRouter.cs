using JD.AI.Tui.Agent;
using JD.AI.Tui.Providers;

namespace JD.AI.Tui.Commands;

/// <summary>
/// Routes slash commands to their handlers.
/// </summary>
public sealed class SlashCommandRouter : ISlashCommandRouter
{
    private readonly AgentSession _session;
    private readonly IProviderRegistry _registry;

    public SlashCommandRouter(AgentSession session, IProviderRegistry registry)
    {
        _session = session;
        _registry = registry;
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
            "/HELP" => GetHelp(),
            "/MODELS" => await ListModelsAsync(ct).ConfigureAwait(false),
            "/MODEL" => await SwitchModelAsync(arg, ct).ConfigureAwait(false),
            "/PROVIDERS" => await ListProvidersAsync(ct).ConfigureAwait(false),
            "/PROVIDER" => GetCurrentProvider(),
            "/CLEAR" => ClearHistory(),
            "/COMPACT" => await CompactAsync(ct).ConfigureAwait(false),
            "/COST" => GetCost(),
            "/AUTORUN" => ToggleAutoRun(arg),
            "/PERMISSIONS" => TogglePermissions(arg),
            "/QUIT" or "/EXIT" => null, // Signal exit
            _ => $"Unknown command: {parts[0]}. Type /help for available commands.",
        };
    }

    private static string GetHelp() => """
        Available commands:
          /help          — Show this help
          /models        — List available models
          /model <id>    — Switch to a model
          /providers     — List detected providers
          /provider      — Show current provider
          /clear         — Clear chat history
          /compact       — Force context compaction
          /cost          — Show token usage
          /autorun       — Toggle auto-approve for tools
          /permissions   — Toggle permission checks (off = skip all)
          /quit          — Exit jdai
        """;

    private async Task<string> ListModelsAsync(CancellationToken ct)
    {
        var models = await _registry.GetModelsAsync(ct).ConfigureAwait(false);
        if (models.Count == 0)
        {
            return "No models available. Check provider authentication.";
        }

        var lines = models.Select(m =>
        {
            var active = string.Equals(m.Id, _session.CurrentModel?.Id, StringComparison.Ordinal)
                ? " ◄ active" : "";
            return $"  [{m.ProviderName}] {m.Id}{active}";
        });

        return $"Available models:\n{string.Join('\n', lines)}";
    }

    private async Task<string> SwitchModelAsync(string? modelId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return "Usage: /model <model-id>";
        }

        var models = await _registry.GetModelsAsync(ct).ConfigureAwait(false);
        var model = models.FirstOrDefault(m =>
            m.Id.Contains(modelId, StringComparison.OrdinalIgnoreCase));

        if (model is null)
        {
            return $"Model '{modelId}' not found. Use /models to see available models.";
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
}
