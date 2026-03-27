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
    public const string RouteKeyMetadataKey = "gateway.routeKey";
    public const string ChannelTypeMetadataKey = "gateway.channelType";

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
        => await RouteAsync(message, sourceChannel: null, ct);

    /// <summary>
    /// Route an inbound message to the mapped agent and send the response via the provided source channel.
    /// </summary>
    public async Task<string?> RouteAsync(
        ChannelMessage message,
        IChannel? sourceChannel,
        CancellationToken ct = default)
    {
        var (agentId, routeKey) = ResolveAgentMapping(message);

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

        _logger.LogInformation("Routing message from {Channel} (route:{RouteKey}) to agent {Agent}",
            message.ChannelId, routeKey ?? "none", agentId);

        var channelType = sourceChannel?.ChannelType ?? ResolveChannelForResponse(message)?.ChannelType;
        var response = channelType is not null
            ? await _pool.SendMessageAsync(agentId, message, channelType, ct)
            : await _pool.SendMessageAsync(agentId, message, ct);

        // Send response back through the channel
        var channel = sourceChannel ?? ResolveChannelForResponse(message);
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

    /// <summary>Get the agent ID currently mapped to a channel, or null.</summary>
    public string? GetAgentForChannel(string channelId)
    {
        lock (_lock)
            return _channelAgentMap.TryGetValue(channelId, out var agentId) ? agentId : null;
    }

    private (string? AgentId, string? RouteKey) ResolveAgentMapping(ChannelMessage message)
    {
        var keys = new List<string>(capacity: 3) { message.ChannelId };
        if (TryGetMetadataValue(message, RouteKeyMetadataKey, out var routeKey))
            keys.Add(routeKey);
        if (TryGetMetadataValue(message, ChannelTypeMetadataKey, out var channelType))
            keys.Add(channelType);

        lock (_lock)
        {
            foreach (var key in keys)
            {
                if (_channelAgentMap.TryGetValue(key, out var agentId))
                    return (agentId, key);
            }
        }

        return (null, null);
    }

    private IChannel? ResolveChannelForResponse(ChannelMessage message)
    {
        if (TryGetMetadataValue(message, ChannelTypeMetadataKey, out var channelType))
            return _channels.GetChannel(channelType);

        return _channels.GetChannel(message.ChannelId);
    }

    private static bool TryGetMetadataValue(ChannelMessage message, string key, out string value)
    {
        if (message.Metadata.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            value = raw;
            return true;
        }

        value = string.Empty;
        return false;
    }

}
