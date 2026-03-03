using System.Text;
using JD.AI.Core.Commands;
using JD.AI.Gateway.Config;
using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Commands;

/// <summary>Lists available models and current agent model selections.</summary>
public sealed class ModelsCommand(
    AgentPoolService pool,
    GatewayConfig config) : IChannelCommand
{
    public string Name => "models";
    public string Description => "Lists available models and current agent selections.";
    public IReadOnlyList<CommandParameter> Parameters => [];

    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("**Available Models**");
        sb.AppendLine();

        // Show configured providers
        var providers = config.Providers?.Where(p => p.Enabled) ?? [];
        foreach (var provider in providers)
        {
            sb.AppendLine($"📦 **{provider.Name}** — enabled");
        }

        // Show configured agent definitions
        sb.AppendLine();
        sb.AppendLine("**Configured Agent Models:**");
        foreach (var agentDef in config.Agents ?? [])
        {
            sb.AppendLine($"• `{agentDef.Id}` — {agentDef.Provider}/{agentDef.Model}");
        }

        // Show running agents
        var agents = pool.ListAgents();
        if (agents.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Running Agents:**");
            foreach (var agent in agents)
            {
                sb.AppendLine($"• `{agent.Id[..8]}` — {agent.Provider}/{agent.Model} (active)");
            }
        }

        return Task.FromResult(new CommandResult { Success = true, Content = sb.ToString() });
    }
}
