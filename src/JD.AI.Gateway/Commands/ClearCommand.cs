using JD.AI.Core.Commands;
using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Commands;

/// <summary>Clears conversation history for an agent.</summary>
public sealed class ClearCommand(AgentPoolService pool) : IChannelCommand
{
    public string Name => "clear";
    public string Description => "Clears conversation history for an agent.";
    public IReadOnlyList<CommandParameter> Parameters =>
    [
        new CommandParameter
        {
            Name = "agent",
            Description = "Agent ID (first 8 chars). Clears all if omitted.",
            IsRequired = false
        }
    ];

    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        context.Arguments.TryGetValue("agent", out var agentFilter);

        var agents = pool.ListAgents();

        if (agents.Count == 0)
        {
            return Task.FromResult(new CommandResult
            {
                Success = true,
                Content = "ℹ️ No agents are running."
            });
        }

        var cleared = 0;
        foreach (var agent in agents)
        {
            if (!string.IsNullOrWhiteSpace(agentFilter) &&
                !agent.Id.StartsWith(agentFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pool.ClearHistory(agent.Id);
            cleared++;
        }

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Content = cleared > 0
                ? $"🧹 Cleared conversation history for {cleared} agent(s)."
                : $"❌ No agent found matching `{agentFilter}`."
        });
    }
}
