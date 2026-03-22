using JD.AI.Channels.OpenClaw;
using JD.AI.Channels.OpenClaw.Routing;
using JD.AI.Core.Config;
using JD.AI.Gateway.Config;
using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Endpoints;

public static class GatewayConfigEndpoints
{
    private static readonly System.Text.Json.JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void MapGatewayConfigEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/gateway").WithTags("Gateway");

        // GET /api/gateway/config — current gateway configuration (redacted secrets)
        group.MapGet("/config", (GatewayConfig config) =>
        {
            return Results.Ok(new
            {
                config.Server,
                Auth = new { config.Auth.Enabled, KeyCount = config.Auth.ApiKeys.Count },
                config.RateLimit,
                Channels = config.Channels.Select(c => new
                {
                    c.Type,
                    c.Name,
                    c.Enabled,
                    c.AutoConnect,
                    Settings = c.Settings.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value.StartsWith("env:", StringComparison.OrdinalIgnoreCase)
                            ? kv.Value : "***")
                }),
                Agents = config.Agents.Select(a => new
                {
                    a.Id,
                    a.Provider,
                    a.Model,
                    a.AutoSpawn,
                    a.MaxTurns
                }),
                config.Routing,
                OpenClaw = new
                {
                    config.OpenClaw.Enabled,
                    config.OpenClaw.WebSocketUrl,
                    config.OpenClaw.AutoConnect,
                    config.OpenClaw.DefaultMode,
                    Channels = config.OpenClaw.Channels,
                    RegisteredAgents = config.OpenClaw.RegisterAgents.Select(a => new
                    {
                        a.Id,
                        a.Name,
                        a.Emoji,
                        a.Theme,
                        a.Model,
                        Bindings = a.Bindings.Count
                    })
                }
            });
        })
        .WithName("GetGatewayConfig")
        .WithDescription("Get current gateway configuration with secrets redacted.");

        // GET /api/gateway/status — live operational status
        group.MapGet("/status", (
            GatewayConfig config,
            AgentPoolService pool,
            AgentRouter router,
            JD.AI.Core.Channels.IChannelRegistry channels) =>
        {
            var registrar = app.Services.GetService<OpenClawAgentRegistrar>();
            var bridge = app.Services.GetService<OpenClawBridgeChannel>();
            var (overrideActive, overrideChannels) = AnalyzeOpenClawOverrides(config.OpenClaw);
            return Results.Ok(new
            {
                Status = "running",
                Uptime = DateTimeOffset.UtcNow,
                Channels = channels.Channels.Select(c => new
                {
                    c.ChannelType,
                    c.DisplayName,
                    c.IsConnected
                }),
                Agents = pool.ListAgents(),
                Routes = router.GetMappings(),
                OpenClaw = new
                {
                    config.OpenClaw.Enabled,
                    Connected = bridge?.IsConnected ?? false,
                    config.OpenClaw.DefaultMode,
                    OverrideActive = overrideActive,
                    OverrideChannels = overrideChannels,
                    RegisteredAgents = registrar?.RegisteredAgentIds ?? (IReadOnlyList<string>)[]
                }
            });
        })
        .WithName("GetGatewayStatus")
        .WithDescription("Get live operational status of the gateway.");

        // POST /api/gateway/channels/{type}/connect — connect a channel at runtime
        group.MapPost("/channels/{type}/connect", async (
            string type,
            JD.AI.Core.Channels.IChannelRegistry channels,
            CancellationToken ct) =>
        {
            var channel = channels.GetChannel(type);
            if (channel is null)
                return Results.NotFound(new { Error = $"Channel '{type}' not registered" });

            if (channel.IsConnected)
                return Results.Ok(new { channel.ChannelType, Status = "already_connected" });

            await channel.ConnectAsync(ct);
            return Results.Ok(new { channel.ChannelType, Status = "connected" });
        })
        .WithName("ConnectGatewayChannel")
        .WithDescription("Connect a registered channel at runtime.");

        // POST /api/gateway/channels/{type}/disconnect — disconnect at runtime
        group.MapPost("/channels/{type}/disconnect", async (
            string type,
            JD.AI.Core.Channels.IChannelRegistry channels,
            CancellationToken ct) =>
        {
            var channel = channels.GetChannel(type);
            if (channel is null)
                return Results.NotFound(new { Error = $"Channel '{type}' not registered" });

            await channel.DisconnectAsync(ct);
            return Results.Ok(new { channel.ChannelType, Status = "disconnected" });
        })
        .WithName("DisconnectGatewayChannel")
        .WithDescription("Disconnect a channel at runtime.");

        // POST /api/gateway/agents/spawn — spawn agent from inline definition
        group.MapPost("/agents/spawn", async (
            AgentDefinition def,
            AgentPoolService pool,
            CancellationToken ct) =>
        {
            var id = await pool.SpawnAgentAsync(
                def.Provider, def.Model, def.SystemPrompt, ct, def.Parameters,
                def.FallbackProviders as IReadOnlyList<string> ?? [.. def.FallbackProviders]);
            return Results.Created($"/api/v1/agents/{id}", new
            {
                Id = id,
                def.Provider,
                def.Model,
                Source = "runtime"
            });
        })
        .WithName("SpawnGatewayAgent")
        .WithDescription("Spawn an agent from an inline definition.");

        // GET /api/gateway/openclaw/agents — list JD.AI agents registered with OpenClaw
        group.MapGet("/openclaw/agents", () =>
        {
            var registrar = app.Services.GetService<OpenClawAgentRegistrar>();
            if (registrar is null)
                return Results.Ok(new { Agents = Array.Empty<string>(), Message = "OpenClaw integration not enabled" });

            return Results.Ok(new
            {
                Agents = registrar.RegisteredAgentIds,
                Count = registrar.RegisteredAgentIds.Count
            });
        })
        .WithName("GetOpenClawAgents")
        .WithDescription("List JD.AI agents registered with the OpenClaw gateway.");

        // POST /api/gateway/openclaw/agents/sync — re-sync agent registrations with OpenClaw
        group.MapPost("/openclaw/agents/sync", async (
            GatewayConfig config,
            CancellationToken ct) =>
        {
            var registrar = app.Services.GetService<OpenClawAgentRegistrar>();
            if (registrar is null)
                return Results.BadRequest(new { Error = "OpenClaw integration not enabled" });

            // Unregister current, then re-register from config
            await registrar.UnregisterAgentsAsync(BuildManagedAgentIds(config.OpenClaw), ct);

            var definitions = config.OpenClaw.RegisterAgents.Select(reg => new JdAiAgentDefinition
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

            await registrar.RegisterAgentsAsync(definitions, ct);

            return Results.Ok(new
            {
                Message = "Agent registrations synced",
                Agents = registrar.RegisteredAgentIds
            });
        })
        .WithName("SyncOpenClawAgents")
        .WithDescription("Re-synchronize JD.AI agent registrations with the OpenClaw gateway.");

        // GET /api/gateway/openclaw/status — diagnostic endpoint for bridge status
        group.MapGet("/openclaw/status", (GatewayConfig config) =>
        {
            var bridge = app.Services.GetService<OpenClawBridgeChannel>();
            if (bridge is null)
                return Results.Ok(new { Enabled = false, Message = "OpenClaw integration not enabled" });

            var routingService = app.Services.GetServices<IHostedService>()
                .OfType<OpenClawRoutingService>()
                .FirstOrDefault();

            var recentEvents = routingService?.GetRecentEvents() ?? [];
            var (overrideActive, overrideChannels) = AnalyzeOpenClawOverrides(config.OpenClaw);

            return Results.Ok(new
            {
                Enabled = true,
                Connected = bridge.IsConnected,
                config.OpenClaw.DefaultMode,
                OverrideActive = overrideActive,
                OverrideChannels = overrideChannels,
                ChannelType = bridge.ChannelType,
                DisplayName = bridge.DisplayName,
                RecentEventCount = recentEvents.Count,
                RecentEvents = recentEvents
                    .TakeLast(20)
                    .Select(e => new { e.Time, e.EventName, e.Summary })
            });
        })
        .WithName("GetOpenClawStatus")
        .WithDescription("Diagnostic endpoint showing OpenClaw bridge connection status and recent events.");

        // POST /api/gateway/openclaw/bridge/disable — runtime cleanup + disconnect
        group.MapPost("/openclaw/bridge/disable", async (
            GatewayConfig config,
            CancellationToken ct) =>
        {
            var bridge = app.Services.GetService<OpenClawBridgeChannel>();
            var registrar = app.Services.GetService<OpenClawAgentRegistrar>();
            if (bridge is null)
            {
                return Results.Ok(new
                {
                    BridgeDisabled = false,
                    SessionCleanupDeleted = 0,
                    Message = "OpenClaw integration not enabled",
                });
            }

            var deleted = await DisableOpenClawBridgeRuntimeAsync(
                bridge,
                registrar,
                config.OpenClaw,
                ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                BridgeDisabled = true,
                SessionCleanupDeleted = deleted,
            });
        })
        .WithName("DisableOpenClawBridgeRuntime")
        .WithDescription("Clean JD.AI-managed OpenClaw sessions and disconnect bridge runtime.");

        // GET /api/gateway/config/raw — full typed config for editor (no redaction except secrets)
        group.MapGet("/config/raw", (GatewayConfig config) => Results.Ok(config))
            .WithName("GetGatewayConfigRaw")
            .WithDescription("Get full typed gateway configuration for the settings editor.");

        // PUT /api/gateway/config/server — update server section
        group.MapPut("/config/server", (ServerConfig update, GatewayConfig config, IConfiguration root) =>
        {
            config.Server = update;
            WriteConfigSection(root, "Gateway:Server", update);
            return Results.Ok(config.Server);
        })
        .WithName("UpdateServerConfig")
        .WithDescription("Update the gateway server configuration.");

        // PUT /api/gateway/config/auth — update auth section
        group.MapPut("/config/auth", (AuthConfig update, GatewayConfig config, IConfiguration root) =>
        {
            config.Auth = update;
            WriteConfigSection(root, "Gateway:Auth", update);
            return Results.Ok(new { config.Auth.Enabled, KeyCount = config.Auth.ApiKeys.Count });
        })
        .WithName("UpdateAuthConfig")
        .WithDescription("Update the gateway auth configuration.");

        // PUT /api/gateway/config/ratelimit — update rate limit section
        group.MapPut("/config/ratelimit", (RateLimitConfig update, GatewayConfig config, IConfiguration root) =>
        {
            config.RateLimit = update;
            WriteConfigSection(root, "Gateway:RateLimit", update);
            return Results.Ok(config.RateLimit);
        })
        .WithName("UpdateRateLimitConfig")
        .WithDescription("Update the gateway rate-limit configuration.");

        // PUT /api/gateway/config/providers — update providers list
        group.MapPut("/config/providers", (List<ProviderConfig> update, GatewayConfig config, IConfiguration root) =>
        {
            config.Providers = update;
            WriteConfigSection(root, "Gateway:Providers", update);
            return Results.Ok(config.Providers);
        })
        .WithName("UpdateProvidersConfig")
        .WithDescription("Update the gateway providers configuration.");

        // PUT /api/gateway/config/agents — update agent definitions
        group.MapPut("/config/agents", async (List<AgentDefinition> update, GatewayConfig config, IConfiguration root, CancellationToken ct) =>
        {
            config.Agents = update;
            WriteConfigSection(root, "Gateway:Agents", update);
            if (NormalizeRoutingForAgentSet(config))
                WriteConfigSection(root, "Gateway:Routing", config.Routing);
            await PersistSharedGatewayDefaultAsync(config, ct).ConfigureAwait(false);
            return Results.Ok(config.Agents);
        })
        .WithName("UpdateAgentsConfig")
        .WithDescription("Update the gateway agents configuration.");

        // PUT /api/gateway/config/channels — update channel definitions
        group.MapPut("/config/channels", (List<ChannelConfig> update, GatewayConfig config, IConfiguration root) =>
        {
            config.Channels = update;
            WriteConfigSection(root, "Gateway:Channels", update);
            return Results.Ok(config.Channels);
        })
        .WithName("UpdateChannelsConfig")
        .WithDescription("Update the gateway channels configuration.");

        // PUT /api/gateway/config/routing — update routing config
        group.MapPut("/config/routing", async (RoutingConfig update, GatewayConfig config, IConfiguration root, CancellationToken ct) =>
        {
            config.Routing = update;
            WriteConfigSection(root, "Gateway:Routing", update);
            await PersistSharedGatewayDefaultAsync(config, ct).ConfigureAwait(false);
            return Results.Ok(config.Routing);
        })
        .WithName("UpdateRoutingConfig")
        .WithDescription("Update the gateway routing configuration.");

        // PUT /api/gateway/config/openclaw — update OpenClaw config
        group.MapPut("/config/openclaw", async (
            OpenClawGatewayConfig update,
            GatewayConfig config,
            IConfiguration root,
            CancellationToken ct) =>
        {
            var previousOpenClaw = config.OpenClaw;
            config.OpenClaw = update;
            WriteConfigSection(root, "Gateway:OpenClaw", update);

            var bridge = app.Services.GetService<OpenClawBridgeChannel>();
            var registrar = app.Services.GetService<OpenClawAgentRegistrar>();

            if (bridge is not null)
            {
                if (!update.Enabled && bridge.IsConnected)
                {
                    await DisableOpenClawBridgeRuntimeAsync(
                        bridge,
                        registrar,
                        previousOpenClaw,
                        ct).ConfigureAwait(false);
                }
                else if (update.Enabled && update.AutoConnect && !bridge.IsConnected)
                {
                    await bridge.ConnectAsync(ct).ConfigureAwait(false);
                }
            }

            return Results.Ok(config.OpenClaw);
        })
        .WithName("UpdateOpenClawConfig")
        .WithDescription("Update the OpenClaw bridge configuration.");
    }

    private static async Task<int> DisableOpenClawBridgeRuntimeAsync(
        OpenClawBridgeChannel bridge,
        OpenClawAgentRegistrar? registrar,
        OpenClawGatewayConfig config,
        CancellationToken ct)
    {
        if (!bridge.IsConnected)
        {
            try
            {
                await bridge.ConnectAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                return 0;
            }
        }

        var deleted = await bridge.DeleteSessionsByPrefixAsync(
            BuildManagedSessionPrefixes(config),
            BuildManagedSessionContains(config),
            deleteTranscript: true,
            ct).ConfigureAwait(false);

        if (registrar is not null)
            await registrar.UnregisterAgentsAsync(BuildManagedAgentIds(config), ct).ConfigureAwait(false);

        if (bridge.IsConnected)
            await bridge.DisconnectAsync(ct).ConfigureAwait(false);

        return deleted;
    }

    private static string[] BuildManagedSessionPrefixes(OpenClawGatewayConfig config)
    {
        var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"agent:{OpenClawAgentRegistrar.AgentIdPrefix}"
        };

        foreach (var registration in config.RegisterAgents)
        {
            if (string.IsNullOrWhiteSpace(registration.Id))
                continue;

            prefixes.Add($"agent:{registration.Id.Trim()}:");
        }

        return prefixes.ToArray();
    }

    private static string[] BuildManagedAgentIds(OpenClawGatewayConfig config) =>
        config.RegisterAgents
            .Select(reg => reg.Id?.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string[] BuildManagedSessionContains(OpenClawGatewayConfig config)
    {
        var fragments = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "g-agent-"
        };

        foreach (var registration in config.RegisterAgents)
        {
            if (!string.IsNullOrWhiteSpace(registration.Id))
                fragments.Add(registration.Id.Trim());

            foreach (var binding in registration.Bindings)
            {
                if (!string.IsNullOrWhiteSpace(binding.Channel))
                    fragments.Add($"{binding.Channel.Trim()}:g-agent-");
            }
        }

        foreach (var channel in config.Channels.Keys)
        {
            if (!string.IsNullOrWhiteSpace(channel))
                fragments.Add($"{channel.Trim()}:g-agent-");
        }

        return fragments.ToArray();
    }

    private static async Task PersistSharedGatewayDefaultAsync(
        GatewayConfig config,
        CancellationToken ct)
    {
        var (agentId, agent) = ResolveSharedDefaultAgent(config);
        if (agent is null ||
            string.IsNullOrWhiteSpace(agent.Provider) ||
            string.IsNullOrWhiteSpace(agent.Model))
        {
            return;
        }

        using var configStore = new AtomicConfigStore();
        await configStore
            .SetGatewayDefaultAgentAsync(agent.Provider, agent.Model, agentId, ct)
            .ConfigureAwait(false);
    }

    private static (string AgentId, AgentDefinition? Agent) ResolveSharedDefaultAgent(GatewayConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var preferredId = string.IsNullOrWhiteSpace(config.Routing.DefaultAgentId)
            ? "default"
            : config.Routing.DefaultAgentId.Trim();

        var selected = config.Agents.FirstOrDefault(agent =>
            string.Equals(agent.Id, preferredId, StringComparison.OrdinalIgnoreCase));
        if (selected is not null)
            return (preferredId, selected);

        selected = config.Agents.FirstOrDefault(agent =>
            string.Equals(agent.Id, "default", StringComparison.OrdinalIgnoreCase));
        if (selected is not null)
            return (selected.Id, selected);

        selected = config.Agents.FirstOrDefault();
        return selected is null ? (preferredId, null) : (selected.Id, selected);
    }

    private static bool NormalizeRoutingForAgentSet(GatewayConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var changed = false;
        var validIds = config.Agents
            .Select(agent => agent.Id?.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (validIds.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(config.Routing.DefaultAgentId))
            {
                config.Routing.DefaultAgentId = string.Empty;
                changed = true;
            }

            if (config.Routing.Rules.Count > 0)
            {
                config.Routing.Rules = [];
                changed = true;
            }

            return changed;
        }

        var currentDefault = config.Routing.DefaultAgentId?.Trim();
        if (string.IsNullOrWhiteSpace(currentDefault) || !validIds.Contains(currentDefault))
        {
            var fallbackDefault = config.Agents
                .FirstOrDefault(agent => string.Equals(agent.Id, "default", StringComparison.OrdinalIgnoreCase))
                ?.Id;
            fallbackDefault ??= config.Agents
                .FirstOrDefault(agent => !string.IsNullOrWhiteSpace(agent.Id))
                ?.Id;
            fallbackDefault ??= string.Empty;

            if (!string.Equals(config.Routing.DefaultAgentId, fallbackDefault, StringComparison.Ordinal))
            {
                config.Routing.DefaultAgentId = fallbackDefault;
                changed = true;
            }
        }

        var initialRuleCount = config.Routing.Rules.Count;
        config.Routing.Rules = config.Routing.Rules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.AgentId) && validIds.Contains(rule.AgentId.Trim()))
            .ToList();
        if (config.Routing.Rules.Count != initialRuleCount)
            changed = true;

        return changed;
    }

    /// <summary>Persists a config section to appsettings.json via JSON merge.</summary>
    private static void WriteConfigSection<T>(IConfiguration root, string sectionPath, T value)
    {
        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(appSettingsPath)) return;

        var json = File.ReadAllText(appSettingsPath);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        using var ms = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(ms, new System.Text.Json.JsonWriterOptions { Indented = true }))
        {
            MergeSection(writer, doc.RootElement, sectionPath.Split(':'), 0, value);
        }

        File.WriteAllBytes(appSettingsPath, ms.ToArray());
    }

    private static void MergeSection<T>(
        System.Text.Json.Utf8JsonWriter writer,
        System.Text.Json.JsonElement current,
        string[] pathSegments,
        int depth,
        T value)
    {
        writer.WriteStartObject();
        foreach (var prop in current.EnumerateObject())
        {
            if (depth < pathSegments.Length &&
                prop.Name.Equals(pathSegments[depth], StringComparison.OrdinalIgnoreCase))
            {
                writer.WritePropertyName(prop.Name);
                if (depth == pathSegments.Length - 1)
                {
                    // Replace this node with the serialized value
                    var serialized = System.Text.Json.JsonSerializer.Serialize(value, CamelCaseOptions);
                    using var replacement = System.Text.Json.JsonDocument.Parse(serialized);
                    replacement.RootElement.WriteTo(writer);
                }
                else
                {
                    MergeSection(writer, prop.Value, pathSegments, depth + 1, value);
                }
            }
            else
            {
                prop.WriteTo(writer);
            }
        }
        writer.WriteEndObject();
    }

    private static (bool OverrideActive, string[] OverrideChannels) AnalyzeOpenClawOverrides(
        OpenClawGatewayConfig config)
    {
        if (!config.Enabled)
            return (false, []);

        var overrideActive = !string.Equals(
            config.DefaultMode,
            "Passthrough",
            StringComparison.OrdinalIgnoreCase);
        var channels = new List<string>();

        foreach (var (channelName, channelConfig) in config.Channels)
        {
            var mode = string.IsNullOrWhiteSpace(channelConfig.Mode)
                ? config.DefaultMode
                : channelConfig.Mode;
            if (string.Equals(mode, "Passthrough", StringComparison.OrdinalIgnoreCase))
                continue;

            overrideActive = true;
            channels.Add(channelName);
        }

        return (overrideActive, [.. channels.Order(StringComparer.OrdinalIgnoreCase)]);
    }
}
