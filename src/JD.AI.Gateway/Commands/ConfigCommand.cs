using System.Text;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using JD.AI.Core.Providers;
using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Commands;

/// <summary>Prints a full configuration summary for the gateway.</summary>
public sealed class ConfigCommand(
    AgentRouter router,
    AgentPoolService pool,
    IChannelRegistry channels,
    IProviderRegistry registry) : IChannelCommand
{
    public string Name => "config";
    public string Description => "Prints provider, route, agent, and channel configuration.";
    public IReadOnlyList<CommandParameter> Parameters => [];

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var agents = pool.ListAgents();
        var mappings = router.GetMappings();
        var allChannels = channels.Channels;
        var providers = await registry.DetectProvidersAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("**JD.AI Configuration**");
        sb.AppendLine();

        // Providers
        sb.AppendLine("**Providers:**");
        foreach (var p in providers)
        {
            var status = p.IsAvailable ? "🟢" : "🔴";
            sb.AppendLine($"  {status} {p.Name} — {p.Models.Count} model(s)");
        }

        sb.AppendLine();

        // Agents
        sb.AppendLine("**Agents:**");
        if (agents.Count == 0)
        {
            sb.AppendLine("  (none running)");
        }
        else
        {
            foreach (var a in agents)
            {
                var age = DateTimeOffset.UtcNow - a.CreatedAt;
                sb.AppendLine($"  🤖 `{a.Id[..8]}` — {a.Provider}/{a.Model} — {a.TurnCount} turns — up {FormatAge(age)}");
            }
        }

        sb.AppendLine();

        // Routes
        sb.AppendLine("**Routes:**");
        if (mappings.Count == 0)
        {
            sb.AppendLine("  (no routes configured)");
        }
        else
        {
            foreach (var (ch, agentId) in mappings)
            {
                var agentInfo = agents.FirstOrDefault(a =>
                    string.Equals(a.Id, agentId, StringComparison.Ordinal));
                var detail = agentInfo is not null
                    ? $"{agentInfo.Provider}/{agentInfo.Model}"
                    : agentId[..Math.Min(8, agentId.Length)];
                sb.AppendLine($"  📡 {ch} → {detail}");
            }
        }

        sb.AppendLine();

        // Channels
        sb.AppendLine("**Channels:**");
        if (allChannels.Count == 0)
        {
            sb.AppendLine("  (none registered)");
        }
        else
        {
            foreach (var ch in allChannels)
            {
                var status = ch.IsConnected ? "🟢" : "🔴";
                sb.AppendLine($"  {status} {ch.DisplayName} ({ch.ChannelType})");
            }
        }

        return new CommandResult { Success = true, Content = sb.ToString() };
    }

    private static string FormatAge(TimeSpan ts) =>
        ts.TotalHours >= 1 ? $"{ts.TotalHours:F0}h" : $"{ts.TotalMinutes:F0}m";
}
