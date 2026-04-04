using System.Text;
using JD.AI.Core.Commands;
using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Commands;

/// <summary>Shows current usage statistics (turns, agents, uptime).</summary>
public sealed class UsageCommand(AgentPoolService pool) : IChannelCommand
{
    private static readonly DateTimeOffset StartTime = DateTimeOffset.UtcNow;

    public string Name => "usage";
    public string Description => "Shows current usage statistics (turns, agents, uptime).";
    public IReadOnlyList<CommandParameter> Parameters => [];

    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var agents = pool.ListAgents();
        var uptime = DateTimeOffset.UtcNow - StartTime;
        var totalTurns = agents.Sum(a => a.TurnCount);

        var sb = new StringBuilder();
        sb.AppendLine("**JD.AI Usage Statistics**");
        sb.AppendLine();
        sb.AppendLine($"🕐 **Uptime:** {FormatUptime(uptime)}");
        sb.AppendLine($"🤖 **Active Agents:** {agents.Count}");
        sb.AppendLine($"💬 **Total Turns:** {totalTurns}");

        if (agents.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Per-Agent Breakdown:**");
            foreach (var agent in agents)
            {
                sb.AppendLine($"• `{agent.Id[..8]}` ({agent.Provider}/{agent.Model}) — {agent.TurnCount} turns");
            }
        }

        return Task.FromResult(new CommandResult { Success = true, Content = sb.ToString() });
    }

    internal static string FormatUptime(TimeSpan ts) =>
        ts.Days > 0 ? $"{ts.Days}d {ts.Hours}h {ts.Minutes}m"
        : ts.Hours > 0 ? $"{ts.Hours}h {ts.Minutes}m"
        : $"{ts.Minutes}m {ts.Seconds}s";
}
