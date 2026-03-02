using System.Text;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Commands;

/// <summary>Shows agent and channel health status.</summary>
public sealed class StatusCommand(
    AgentPoolService pool,
    IChannelRegistry channels) : IChannelCommand
{
    public string Name => "status";
    public string Description => "Shows agent and channel health status.";
    public IReadOnlyList<CommandParameter> Parameters => [];

    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("**JD.AI System Status**");
        sb.AppendLine();

        // Channel status
        sb.AppendLine("**Channels:**");
        foreach (var ch in channels.Channels)
        {
            var status = ch.IsConnected ? "🟢 Connected" : "🔴 Disconnected";
            sb.AppendLine($"• **{ch.DisplayName}** ({ch.ChannelType}) — {status}");
        }

        // Agent status
        var agents = pool.ListAgents();
        sb.AppendLine();
        sb.AppendLine("**Agents:**");
        if (agents.Count == 0)
        {
            sb.AppendLine("• No agents running.");
        }
        else
        {
            foreach (var agent in agents)
            {
                var age = DateTimeOffset.UtcNow - agent.CreatedAt;
                sb.AppendLine($"• `{agent.Id[..8]}` — {agent.Provider}/{agent.Model} — {agent.TurnCount} turns — up {FormatAge(age)}");
            }
        }

        return Task.FromResult(new CommandResult { Success = true, Content = sb.ToString() });
    }

    private static string FormatAge(TimeSpan ts) =>
        ts.TotalHours >= 1 ? $"{ts.TotalHours:F0}h" : $"{ts.TotalMinutes:F0}m";
}
