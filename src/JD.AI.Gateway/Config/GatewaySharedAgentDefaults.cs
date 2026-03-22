using JD.AI.Core.Config;

namespace JD.AI.Gateway.Config;

/// <summary>
/// Applies shared gateway agent defaults from ~/.jdai/config.json.
/// </summary>
public static class GatewaySharedAgentDefaults
{
    public static async Task ApplyAsync(
        GatewayConfig config,
        AtomicConfigStore? configStore = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var disposeStore = configStore is null;
        configStore ??= new AtomicConfigStore();

        try
        {
            var pref = await configStore.GetGatewayDefaultAgentAsync(ct).ConfigureAwait(false);
            Apply(config, pref);
        }
        finally
        {
            if (disposeStore)
                configStore.Dispose();
        }
    }

    internal static void Apply(GatewayConfig config, GatewayDefaultAgentConfig preference)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(preference);

        if (string.IsNullOrWhiteSpace(preference.Provider) || string.IsNullOrWhiteSpace(preference.Model))
            return;

        var targetId = ResolveTargetAgentId(config, preference.AgentId);
        if (targetId is null)
            return;

        var target = config.Agents.FirstOrDefault(a =>
            string.Equals(a.Id, targetId, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            target = new AgentDefinition
            {
                Id = targetId,
                AutoSpawn = true,
                SystemPrompt = "You are a helpful AI assistant connected to the JD.AI gateway.",
            };
            config.Agents.Add(target);
        }

        target.Provider = preference.Provider;
        target.Model = preference.Model;

        if (string.IsNullOrWhiteSpace(config.Routing.DefaultAgentId))
            config.Routing.DefaultAgentId = targetId;
    }

    internal static string? ResolveTargetAgentId(GatewayConfig config, string? preferredAgentId)
    {
        if (!string.IsNullOrWhiteSpace(preferredAgentId))
            return preferredAgentId.Trim();

        if (!string.IsNullOrWhiteSpace(config.Routing.DefaultAgentId))
            return config.Routing.DefaultAgentId.Trim();

        var firstConfigured = config.Agents.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Id));
        if (firstConfigured is not null)
            return firstConfigured.Id;

        return "default";
    }
}
