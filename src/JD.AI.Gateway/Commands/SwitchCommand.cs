using JD.AI.Core.Commands;
using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Commands;

/// <summary>Switches the model for a running agent.</summary>
public sealed class SwitchCommand(AgentPoolService pool) : IChannelCommand
{
    public string Name => "switch";
    public string Description => "Switch the model for an agent (spawns a new agent with the specified model).";
    public IReadOnlyList<CommandParameter> Parameters =>
    [
        new CommandParameter
        {
            Name = "model",
            Description = "Model name to switch to (e.g., gpt-4, llama3.2:latest)",
            IsRequired = true
        },
        new CommandParameter
        {
            Name = "provider",
            Description = "Provider name (e.g., Ollama, Claude). Defaults to current agent's provider.",
            IsRequired = false
        }
    ];

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        if (!context.Arguments.TryGetValue("model", out var model) ||
            string.IsNullOrWhiteSpace(model))
        {
            return new CommandResult
            {
                Success = false,
                Content = "❌ Please specify a model name. Usage: `jdai-switch <model> [provider]`"
            };
        }

        // Determine provider
        var agents = pool.ListAgents();
        context.Arguments.TryGetValue("provider", out var provider);

        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = agents.Count > 0 ? agents[0].Provider : "Ollama";
        }

        try
        {
            var agentId = await pool.SpawnAgentAsync(provider, model, systemPrompt: null, ct);
            return new CommandResult
            {
                Success = true,
                Content = $"✅ Spawned new agent `{agentId[..8]}` with **{provider}/{model}**.\n" +
                          "Note: Update routing rules to direct messages to this agent."
            };
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            return new CommandResult
            {
                Success = false,
                Content = $"❌ Failed to spawn agent: {ex.Message}"
            };
        }
#pragma warning restore CA1031
    }
}
