using JD.AI.Core.Channels;
using JD.AI.Core.Events;
using Microsoft.Extensions.Logging;

namespace JD.AI.Gateway.Services;

/// <summary>
/// Routes inbound channel messages to agents in the agent pool.
/// Supports routing strategies: round-robin, dedicated (1:1 channel:agent), or tag-based.
/// </summary>
public sealed class AgentRouter
{
    private readonly AgentPoolService _pool;
    private readonly IChannelRegistry _channels;
    private readonly IEventBus _events;
    private readonly ILogger<AgentRouter> _logger;

    // Channel -> Agent mapping
    private readonly Dictionary<string, string> _channelAgentMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    public AgentRouter(
        AgentPoolService pool,
        IChannelRegistry channels,
        IEventBus events,
        ILogger<AgentRouter> logger)
    {
        _pool = pool;
        _channels = channels;
        _events = events;
        _logger = logger;
    }

    /// <summary>Map a channel to a specific agent ID.</summary>
    public void MapChannel(string channelId, string agentId)
    {
        lock (_lock)
            _channelAgentMap[channelId] = agentId;
    }

    /// <summary>Route an inbound message to the mapped agent and return the response.</summary>
    public async Task<string?> RouteAsync(ChannelMessage message, CancellationToken ct = default)
    {
        string? agentId;
        lock (_lock)
            _channelAgentMap.TryGetValue(message.ChannelId, out agentId);

        if (agentId is null)
        {
            _logger.LogWarning("No agent mapped for channel {ChannelId}, dropping message", message.ChannelId);
            await _events.PublishAsync(new GatewayEvent(
                "message.unrouted",
                message.ChannelId,
                DateTimeOffset.UtcNow,
                $"No agent for channel {message.ChannelId}"), ct);
            return null;
        }

        _logger.LogInformation("Routing message from {Channel} to agent {Agent}", message.ChannelId, agentId);

        var response = await _pool.SendMessageAsync(agentId, message.Content, ct);

        // Send response back through the channel
        var channel = _channels.GetChannel(message.ChannelId);
        if (channel is not null && response is not null)
        {
            await channel.SendMessageAsync(message.ChannelId, response, ct);
        }

        return response;
    }

    /// <summary>Get all current channel-to-agent mappings.</summary>
    public IReadOnlyDictionary<string, string> GetMappings()
    {
        lock (_lock)
            return new Dictionary<string, string>(_channelAgentMap);
    }
}
