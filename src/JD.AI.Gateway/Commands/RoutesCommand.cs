using System.Text;
using JD.AI.Core.Commands;
using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Commands;

/// <summary>Lists all channel-to-agent routing mappings.</summary>
public sealed class RoutesCommand(
    AgentRouter router,
    AgentPoolService pool) : IChannelCommand
{
    public string Name => "routes";
    public string Description => "Lists all channel-to-agent routing mappings.";
    public IReadOnlyList<CommandParameter> Parameters => [];

    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var mappings = router.GetMappings();
        var agents = pool.ListAgents();

        var sb = new StringBuilder();
        sb.AppendLine("**Routing Table**");
        sb.AppendLine();

        if (mappings.Count == 0)
        {
            sb.AppendLine("No routes configured.");
        }
        else
        {
            foreach (var (channelId, agentId) in mappings)
            {
                var agentInfo = agents.FirstOrDefault(a =>
                    string.Equals(a.Id, agentId, StringComparison.Ordinal));

                var detail = agentInfo is not null
                    ? $"{agentInfo.Provider}/{agentInfo.Model} (`{agentInfo.Id[..8]}`)"
                    : $"`{agentId[..Math.Min(8, agentId.Length)]}`";

                sb.AppendLine($"📡 **{channelId}** → {detail}");
            }
        }

        return Task.FromResult(new CommandResult { Success = true, Content = sb.ToString() });
    }
}
