using JD.AI.Core.Commands;
using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Commands;

/// <summary>View or change the agent routing for the current channel.</summary>
public sealed class RouteCommand(
    AgentRouter router,
    AgentPoolService pool) : IChannelCommand
{
    public string Name => "route";
    public string Description => "View or change which agent handles this channel.";
    public IReadOnlyList<CommandParameter> Parameters =>
    [
        new CommandParameter
        {
            Name = "agent",
            Description = "Agent ID or name to route this channel to (e.g., jdai, openclaw)",
            IsRequired = false
        }
    ];

    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var channelId = context.ChannelType;

        if (!context.Arguments.TryGetValue("agent", out var targetAgent) ||
            string.IsNullOrWhiteSpace(targetAgent))
        {
            // Show current route
            var currentAgent = router.GetAgentForChannel(channelId);
            if (currentAgent is null)
            {
                return Task.FromResult(new CommandResult
                {
                    Success = true,
                    Content = $"📡 **{channelId}** — No agent mapped.\nUse `/jdai-route <agent>` to assign one."
                });
            }

            var agentInfo = pool.ListAgents()
                .FirstOrDefault(a => string.Equals(a.Id, currentAgent, StringComparison.Ordinal));

            var detail = agentInfo is not null
                ? $"{agentInfo.Provider}/{agentInfo.Model} (`{agentInfo.Id[..8]}`)"
                : $"`{currentAgent[..Math.Min(8, currentAgent.Length)]}`";

            return Task.FromResult(new CommandResult
            {
                Success = true,
                Content = $"📡 **{channelId}** → {detail}"
            });
        }

        // Reroute: try to find matching agent by ID prefix or provider name
        var agents = pool.ListAgents();
        var match = agents.FirstOrDefault(a =>
            a.Id.StartsWith(targetAgent, StringComparison.OrdinalIgnoreCase) ||
            a.Provider.Contains(targetAgent, StringComparison.OrdinalIgnoreCase) ||
            a.Model.Contains(targetAgent, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            var available = agents.Count > 0
                ? string.Join(", ", agents.Select(a => $"`{a.Id[..8]}` ({a.Provider}/{a.Model})"))
                : "none";
            return Task.FromResult(new CommandResult
            {
                Success = false,
                Content = $"❌ No agent matching **{targetAgent}** found.\nAvailable: {available}"
            });
        }

        router.MapChannel(channelId, match.Id);

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Content = $"✅ **{channelId}** now routes to **{match.Provider}/{match.Model}** (`{match.Id[..8]}`)"
        });
    }
}
