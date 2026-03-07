using System.Text;
using JD.AI.Core.Commands;
using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Commands;

/// <summary>Lists all running agents and their configuration.</summary>
public sealed class AgentsCommand(
    AgentPoolService pool,
    AgentRouter router) : IChannelCommand
{
    public string Name => "agents";
    public string Description => "Lists all running agents, their models, and routing mappings.";
    public IReadOnlyList<CommandParameter> Parameters => [];

    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var agents = pool.ListAgents();
        var mappings = router.GetMappings();

        var sb = new StringBuilder();
        sb.AppendLine("**Running Agents**");
        sb.AppendLine();

        if (agents.Count == 0)
        {
            sb.AppendLine("No agents are running.");
        }
        else
        {
            foreach (var agent in agents)
            {
                var age = DateTimeOffset.UtcNow - agent.CreatedAt;
                sb.AppendLine($"🤖 **`{agent.Id[..8]}`** — {agent.Provider}/{agent.Model}");
                sb.AppendLine($"   Turns: {agent.TurnCount} | Up: {FormatAge(age)}");

                // Show which channels route to this agent
                var routedChannels = mappings
                    .Where(m => string.Equals(m.Value, agent.Id, StringComparison.Ordinal))
                    .Select(m => m.Key)
                    .ToList();

                if (routedChannels.Count > 0)
                {
                    sb.AppendLine($"   Routes: {string.Join(", ", routedChannels)}");
                }

                sb.AppendLine();
            }
        }

        // Show routing table
        if (mappings.Count > 0)
        {
            sb.AppendLine("**Routing Table:**");
            foreach (var (channelId, agentId) in mappings)
            {
                sb.AppendLine($"• {channelId} → `{agentId[..Math.Min(8, agentId.Length)]}`");
            }
        }

        return Task.FromResult(new CommandResult { Success = true, Content = sb.ToString() });
    }

    private static string FormatAge(TimeSpan ts) =>
        ts.TotalHours >= 1 ? $"{ts.TotalHours:F0}h" : $"{ts.TotalMinutes:F0}m";
}
