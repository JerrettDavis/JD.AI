using JD.AI.Core.Commands;
using JD.AI.Core.Providers;
using JD.AI.Gateway.Services;
using Microsoft.Extensions.Logging;

namespace JD.AI.Gateway.Commands;

/// <summary>View or switch the AI provider for the current channel's agent.</summary>
public sealed class ProviderCommand(
    AgentRouter router,
    AgentPoolService pool,
    IProviderRegistry registry,
    ILogger<ProviderCommand> logger) : IChannelCommand
{
    public string Name => "provider";
    public string Description => "View or switch the AI provider for this channel's agent.";
    public IReadOnlyList<CommandParameter> Parameters =>
    [
        new CommandParameter
        {
            Name = "name",
            Description = "Provider name to switch to (e.g., claude, ollama, copilot)",
            IsRequired = false
        }
    ];

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var channelId = string.IsNullOrWhiteSpace(context.ChannelId)
            ? context.ChannelType
            : context.ChannelId;
        var currentAgentId = router.GetAgentForChannel(channelId);

        if (!context.Arguments.TryGetValue("name", out var providerName) ||
            string.IsNullOrWhiteSpace(providerName))
        {
            // Show current provider
            if (currentAgentId is null)
            {
                return new CommandResult
                {
                    Success = true,
                    Content = "📡 No agent mapped to this channel. Use `/jdai-route` first."
                };
            }

            var agentInfo = pool.ListAgents()
                .FirstOrDefault(a => string.Equals(a.Id, currentAgentId, StringComparison.Ordinal));

            return new CommandResult
            {
                Success = true,
                Content = agentInfo is not null
                    ? $"🔌 **{agentInfo.Provider}** — model: `{agentInfo.Model}`"
                    : $"🔌 Agent `{currentAgentId[..Math.Min(8, currentAgentId.Length)]}` (provider unknown)"
            };
        }

        if (currentAgentId is null)
        {
            return new CommandResult
            {
                Success = false,
                Content = "❌ No agent mapped to this channel. Use `/jdai-route <agent>` first."
            };
        }

        // Switch provider: find matching provider and its default model
        var providers = await registry.DetectProvidersAsync(ct);
        var exactMatches = providers.Where(p =>
            string.Equals(p.Name, providerName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var partialMatches = exactMatches.Count == 0
            ? providers.Where(p =>
                p.Name.Contains(providerName, StringComparison.OrdinalIgnoreCase))
                .ToList()
            : [];
        var matches = exactMatches.Count > 0 ? exactMatches : partialMatches;

        if (matches.Count == 0)
        {
            var available = string.Join(", ",
                providers.Where(p => p.IsAvailable).Select(p => $"**{p.Name}**"));
            return new CommandResult
            {
                Success = false,
                Content = $"❌ Provider **{providerName}** not found or offline.\nAvailable: {available}"
            };
        }

        if (matches.Count > 1)
        {
            var candidates = string.Join(", ", matches.Select(p => $"**{p.Name}**"));
            return new CommandResult
            {
                Success = false,
                Content = $"❌ Ambiguous provider **{providerName}**.\nMatches: {candidates}"
            };
        }

        var match = matches[0];

        if (!match.IsAvailable)
        {
            var available = string.Join(", ",
                providers.Where(p => p.IsAvailable).Select(p => $"**{p.Name}**"));
            return new CommandResult
            {
                Success = false,
                Content = $"❌ Provider **{providerName}** not found or offline.\nAvailable: {available}"
            };
        }

        if (match.Models.Count == 0)
        {
            return new CommandResult
            {
                Success = false,
                Content = $"❌ **{match.Name}** has no available models."
            };
        }

        // Spawn a new agent with the first model from the matched provider
        var model = match.Models[0];
        try
        {
            var previousAgentId = currentAgentId;
            var newAgentId = await pool.SpawnAgentAsync(match.Name, model.Id, systemPrompt: null, ct);
            router.MapChannel(channelId, newAgentId);
            if (!string.IsNullOrWhiteSpace(previousAgentId) &&
                !router.GetMappings().Any(mapping =>
                    string.Equals(mapping.Value, previousAgentId, StringComparison.Ordinal)))
            {
                pool.StopAgent(previousAgentId);
            }

            return new CommandResult
            {
                Success = true,
                Content = $"✅ Switched to **{match.Name}** / `{model.DisplayName}` (`{newAgentId[..8]}`)\n" +
                          $"Channel **{channelId}** now routes to this agent."
            };
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to switch provider for channel {ChannelId}", channelId);
            return new CommandResult
            {
                Success = false,
                Content = "❌ Failed to switch provider."
            };
        }
#pragma warning restore CA1031
    }
}
