using JD.AI.Channels.OpenClaw;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using JD.AI.Core.Events;
using JD.AI.Gateway.Config;

namespace JD.AI.Gateway.Services;

/// <summary>
/// Hosted service that orchestrates gateway startup: registers channels from config,
/// auto-connects channels, auto-spawns agents, wires message routing,
/// and registers JD.AI agents with OpenClaw.
/// </summary>
public sealed class GatewayOrchestrator : IHostedService
{
    private readonly GatewayConfig _config;
    private readonly ChannelFactory _channelFactory;
    private readonly IChannelRegistry _channels;
    private readonly AgentPoolService _agentPool;
    private readonly AgentRouter _router;
    private readonly IEventBus _events;
    private readonly ILogger<GatewayOrchestrator> _logger;
    private readonly OpenClawAgentRegistrar? _agentRegistrar;
    private readonly OpenClawBridgeChannel? _openClawBridge;
    private readonly ICommandRegistry? _commandRegistry;
    private readonly List<RegisteredChannel> _registeredChannels = [];

    // Track spawned agent IDs from config (definition.Id → pool agentId)
    private readonly Dictionary<string, string> _spawnedAgents = new(StringComparer.OrdinalIgnoreCase);

    public GatewayOrchestrator(
        GatewayConfig config,
        ChannelFactory channelFactory,
        IChannelRegistry channels,
        AgentPoolService agentPool,
        AgentRouter router,
        IEventBus events,
        ILogger<GatewayOrchestrator> logger,
        OpenClawAgentRegistrar? agentRegistrar = null,
        OpenClawBridgeChannel? openClawBridge = null,
        ICommandRegistry? commandRegistry = null)
    {
        _config = config;
        _channelFactory = channelFactory;
        _channels = channels;
        _agentPool = agentPool;
        _router = router;
        _events = events;
        _logger = logger;
        _agentRegistrar = agentRegistrar;
        _openClawBridge = openClawBridge;
        _commandRegistry = commandRegistry;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Gateway orchestrator starting...");

        // Phase 1: Register channels from config
        await RegisterChannelsAsync(cancellationToken);

        // Phase 2: Auto-spawn agents from config
        await SpawnAgentsAsync(cancellationToken);

        // Phase 3: Wire routing rules
        WireRoutingRules();

        // Phase 4: Auto-connect channels marked for auto-connect
        await AutoConnectChannelsAsync(cancellationToken);

        // Phase 5: Register commands with command-aware channels
        await RegisterChannelCommandsAsync(cancellationToken);

        // Phase 6: Wire MessageReceived events to the router
        WireMessageRouting();

        // Phase 7: Register JD.AI agents with OpenClaw
        await RegisterOpenClawAgentsAsync(cancellationToken);

        await _events.PublishAsync(
            new GatewayEvent("gateway.started", "orchestrator", DateTimeOffset.UtcNow,
                new
                {
                    Channels = _channels.Channels.Count,
                    Agents = _agentPool.ListAgents().Count,
                    Routes = _router.GetMappings().Count,
                    OpenClawAgents = _agentRegistrar?.RegisteredAgentIds.Count ?? 0
                }), cancellationToken);

        _logger.LogInformation(
            "Gateway orchestrator ready — {Channels} channels, {Agents} agents, {Routes} routes",
            _channels.Channels.Count, _agentPool.ListAgents().Count, _router.GetMappings().Count);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Gateway orchestrator shutting down...");

            // Unregister JD.AI agents from OpenClaw
            if (_agentRegistrar is not null)
            {
                try
                {
                    var managedIds = _config.OpenClaw.RegisterAgents
                        .Select(reg => reg.Id?.Trim())
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Cast<string>()
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    await _agentRegistrar.UnregisterAgentsAsync(managedIds, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error unregistering agents from OpenClaw");
                }
            }

            // Disconnect all channels
            foreach (var channel in _channels.Channels)
            {
                try
                {
                    if (channel.IsConnected)
                        await channel.DisconnectAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disconnecting channel '{Type}'", channel.ChannelType);
                }
            }

            // Stop all agents
            foreach (var agent in _agentPool.ListAgents())
            {
                _agentPool.StopAgent(agent.Id);
            }

            await _events.PublishAsync(
                new GatewayEvent("gateway.stopped", "orchestrator", DateTimeOffset.UtcNow, null), cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Swallow during graceful shutdown (e.g., test teardown)
        }
    }

    private async Task RegisterChannelsAsync(CancellationToken ct)
    {
        _registeredChannels.Clear();
        var enabledChannels = _config.Channels.Where(c => c.Enabled).ToList();
        var typeCounts = enabledChannels
            .GroupBy(c => c.Type, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var typeOrdinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var channelConfig in enabledChannels)
        {
            try
            {
                var channel = _channelFactory.Create(channelConfig);
                if (channel is null) continue;

                _channels.Register(channel);
                var routeKey = BuildRouteKey(channel, channelConfig, typeCounts, typeOrdinals);
                _registeredChannels.Add(new RegisteredChannel(channel, channelConfig, routeKey));
                _logger.LogInformation("Registered channel '{Type}' ({Name})",
                    channelConfig.Type, channelConfig.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register channel '{Type}'", channelConfig.Type);
            }
        }

        // Include channels registered externally (e.g., tests/integration hosts) so
        // default routing and command wiring still cover them.
        var existingKeys = new HashSet<string>(
            _registeredChannels.Select(c => c.RouteKey),
            StringComparer.OrdinalIgnoreCase);
        foreach (var channel in _channels.Channels)
        {
            if (_registeredChannels.Any(c => ReferenceEquals(c.Channel, channel)))
                continue;

            var fallbackConfig = enabledChannels.FirstOrDefault(c =>
                                     string.Equals(c.Type, channel.ChannelType, StringComparison.OrdinalIgnoreCase))
                                 ?? new ChannelConfig
                                 {
                                     Type = channel.ChannelType,
                                     Name = channel.DisplayName,
                                     Enabled = true,
                                     AutoConnect = false
                                 };

            var routeKey = channel.ChannelType;
            var suffix = 2;
            while (existingKeys.Contains(routeKey))
                routeKey = $"{channel.ChannelType}:{suffix++}";

            _registeredChannels.Add(new RegisteredChannel(channel, fallbackConfig, routeKey));
            existingKeys.Add(routeKey);
        }
    }

    private async Task SpawnAgentsAsync(CancellationToken ct)
    {
        foreach (var def in _config.Agents.Where(a => a.AutoSpawn))
        {
            try
            {
                var poolId = await _agentPool.SpawnAgentAsync(
                    def.Provider, def.Model, def.SystemPrompt, ct, def.Parameters,
                    def.FallbackProviders as IReadOnlyList<string> ?? [.. def.FallbackProviders]);

                _spawnedAgents[def.Id] = poolId;
                _logger.LogInformation(
                    "Auto-spawned agent '{Id}' → pool:{PoolId} ({Provider}/{Model})",
                    def.Id, poolId, def.Provider, def.Model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-spawn agent '{Id}' ({Provider}/{Model})",
                    def.Id, def.Provider, def.Model);
            }
        }
    }

    private void WireRoutingRules()
    {
        var mappedRouteKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in _config.Routing.Rules)
        {
            var agentId = ResolveAgentId(rule.AgentId);
            if (agentId is null)
            {
                _logger.LogWarning("Routing rule for channel '{Channel}' references unknown agent '{Agent}'",
                    rule.ChannelType, rule.AgentId);
                continue;
            }

            var matches = ResolveRuleTargets(rule);
            if (matches.Count == 0)
            {
                _logger.LogWarning(
                    "Routing rule target not found for ChannelType='{ChannelType}', ChannelName='{ChannelName}'",
                    rule.ChannelType, rule.ChannelName ?? "(none)");
                continue;
            }

            foreach (var target in matches)
            {
                _router.MapChannel(target.RouteKey, agentId);
                mappedRouteKeys.Add(target.RouteKey);
                _logger.LogInformation("Mapped route '{RouteKey}' ({Type}/{Name}) → agent '{Agent}'",
                    target.RouteKey, target.Channel.ChannelType, target.Config.Name, agentId);
            }
        }

        // Wire default agent for any channel not explicitly mapped
        if (!string.IsNullOrEmpty(_config.Routing.DefaultAgentId))
        {
            var defaultId = ResolveAgentId(_config.Routing.DefaultAgentId);
            if (defaultId is not null)
            {
                foreach (var registered in _registeredChannels)
                {
                    if (!mappedRouteKeys.Contains(registered.RouteKey))
                    {
                        _router.MapChannel(registered.RouteKey, defaultId);
                        mappedRouteKeys.Add(registered.RouteKey);
                        _logger.LogDebug("Default-mapped channel '{Channel}' → agent '{Agent}'",
                            registered.RouteKey, defaultId);
                    }
                }
            }
        }

    }

    private async Task AutoConnectChannelsAsync(CancellationToken ct)
    {
        foreach (var registered in _registeredChannels)
        {
            if (!registered.Config.AutoConnect) continue;

            try
            {
                await registered.Channel.ConnectAsync(ct);
                _logger.LogInformation("Auto-connected channel '{Type}' ({RouteKey})",
                    registered.Channel.ChannelType, registered.RouteKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-connect channel '{Type}' ({RouteKey})",
                    registered.Channel.ChannelType, registered.RouteKey);
            }
        }
    }

    private async Task RegisterChannelCommandsAsync(CancellationToken ct)
    {
        if (_commandRegistry is null || _commandRegistry.Commands.Count == 0) return;

        foreach (var registered in _registeredChannels)
        {
            var channel = registered.Channel;
            if (channel is ICommandAwareChannel commandChannel)
            {
                try
                {
                    await commandChannel.RegisterCommandsAsync(_commandRegistry, ct);
                    _logger.LogInformation(
                        "Registered {Count} commands with {Channel}",
                        _commandRegistry.Commands.Count, channel.ChannelType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to register commands with channel '{Type}'",
                        channel.ChannelType);
                }
            }
        }
    }

    private void WireMessageRouting()
    {
        foreach (var registered in _registeredChannels)
        {
            var channel = registered.Channel;
            channel.MessageReceived += async originalMessage =>
            {
                try
                {
                    var metadata = new Dictionary<string, string>(originalMessage.Metadata, StringComparer.OrdinalIgnoreCase)
                    {
                        [AgentRouter.RouteKeyMetadataKey] = registered.RouteKey,
                        [AgentRouter.ChannelTypeMetadataKey] = channel.ChannelType,
                    };
                    var msg = originalMessage with { Metadata = metadata };

                    var dispatch = await GatewayCommandDispatcher.TryDispatchAsync(
                        _commandRegistry,
                        channel.ChannelType,
                        msg.Content,
                        invokerId: msg.SenderId,
                        channelId: msg.ChannelId,
                        invokerDisplayName: msg.SenderDisplayName,
                        ct: CancellationToken.None);

                    if (dispatch.Handled)
                    {
                        await channel.SendMessageAsync(msg.ChannelId, dispatch.Response, CancellationToken.None);
                        _logger.LogInformation(
                            "Handled fast-path command on {ChannelType}: {Command}",
                            channel.ChannelType,
                            dispatch.SourceLabel ?? dispatch.CommandName ?? "command");
                        return;
                    }

                    _logger.LogDebug("Routing message from {Channel}/{Sender}",
                        msg.ChannelId, msg.SenderDisplayName ?? msg.SenderId);
                    await _router.RouteAsync(msg, channel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error routing message from {Channel}", originalMessage.ChannelId);
                }
            };
        }

        _logger.LogDebug("Wired MessageReceived → AgentRouter for {Count} channels",
            _registeredChannels.Count);
    }

    private List<RegisteredChannel> ResolveRuleTargets(RoutingRule rule)
    {
        if (!string.IsNullOrWhiteSpace(rule.ChannelName))
        {
            return _registeredChannels
                .Where(c => string.Equals(c.Config.Name, rule.ChannelName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return _registeredChannels
            .Where(c => string.Equals(c.Channel.ChannelType, rule.ChannelType, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string BuildRouteKey(
        IChannel channel,
        ChannelConfig config,
        Dictionary<string, int> typeCounts,
        Dictionary<string, int> typeOrdinals)
    {
        var type = channel.ChannelType;
        var duplicateTypeCount = typeCounts.TryGetValue(type, out var count) ? count : 0;
        if (duplicateTypeCount <= 1)
            return type;

        if (!typeOrdinals.TryGetValue(type, out var nextOrdinal))
            nextOrdinal = 0;
        nextOrdinal++;
        typeOrdinals[type] = nextOrdinal;

        var name = config.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(name))
            return $"{type}:{name}";

        return $"{type}:{nextOrdinal}";
    }

    private sealed record RegisteredChannel(IChannel Channel, ChannelConfig Config, string RouteKey);

    /// <summary>
    /// Resolves a config agent ID (e.g., "default") to the actual pool agent ID.
    /// If the ID is already a pool ID (hex string), returns it directly.
    /// </summary>
    private string? ResolveAgentId(string configId)
    {
        if (_spawnedAgents.TryGetValue(configId, out var poolId))
            return poolId;

        // Maybe it's already a pool agent ID
        if (_agentPool.ListAgents().Any(a => string.Equals(a.Id, configId, StringComparison.Ordinal)))
            return configId;

        return null;
    }

    private async Task RegisterOpenClawAgentsAsync(CancellationToken ct)
    {
        if (_agentRegistrar is null || !_config.OpenClaw.Enabled ||
            _config.OpenClaw.RegisterAgents.Count == 0)
            return;

        // Ensure the OpenClaw bridge is connected before registering
        if (_openClawBridge is not null && !_openClawBridge.IsConnected)
        {
            try
            {
                await _openClawBridge.ConnectAsync(ct);
                _logger.LogInformation("Connected to OpenClaw for agent registration");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to OpenClaw — cannot register agents");
                return;
            }
        }

        // Convert config registrations to JdAiAgentDefinition
        var definitions = _config.OpenClaw.RegisterAgents.Select(reg => new JdAiAgentDefinition
        {
            Id = reg.Id,
            Name = string.IsNullOrEmpty(reg.Name) ? $"JD.AI: {reg.Id}" : reg.Name,
            Emoji = reg.Emoji,
            Theme = reg.Theme,
            Model = reg.Model,
            Bindings = reg.Bindings.Select(b => new AgentBinding
            {
                Channel = b.Channel,
                AccountId = b.AccountId,
                GuildId = b.GuildId,
                Peer = !string.IsNullOrEmpty(b.PeerId)
                    ? new AgentBindingPeer { Kind = b.PeerKind ?? "direct", Id = b.PeerId }
                    : null,
            }).ToList(),
        }).ToList();

        await _agentRegistrar.RegisterAgentsAsync(definitions, ct);

        _logger.LogInformation(
            "Registered {Count} JD.AI agents with OpenClaw: {Ids}",
            _agentRegistrar.RegisteredAgentIds.Count,
            string.Join(", ", _agentRegistrar.RegisteredAgentIds));
    }
}
