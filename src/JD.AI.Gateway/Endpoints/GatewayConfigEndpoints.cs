using JD.AI.Channels.OpenClaw;
using JD.AI.Channels.OpenClaw.Routing;
using JD.AI.Gateway.Config;
using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Endpoints;

public static class GatewayConfigEndpoints
{
    public static void MapGatewayConfigEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gateway").WithTags("Gateway");

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
            var id = await pool.SpawnAgentAsync(def.Provider, def.Model, def.SystemPrompt, ct);
            return Results.Created($"/api/agents/{id}", new
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
            await registrar.UnregisterAgentsAsync(ct);

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
        group.MapGet("/openclaw/status", () =>
        {
            var bridge = app.Services.GetService<OpenClawBridgeChannel>();
            if (bridge is null)
                return Results.Ok(new { Enabled = false, Message = "OpenClaw integration not enabled" });

            var routingService = app.Services.GetServices<IHostedService>()
                .OfType<OpenClawRoutingService>()
                .FirstOrDefault();

            var recentEvents = routingService?.GetRecentEvents() ?? [];

            return Results.Ok(new
            {
                Enabled = true,
                Connected = bridge.IsConnected,
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
        group.MapPut("/config/agents", (List<AgentDefinition> update, GatewayConfig config, IConfiguration root) =>
        {
            config.Agents = update;
            WriteConfigSection(root, "Gateway:Agents", update);
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
        group.MapPut("/config/routing", (RoutingConfig update, GatewayConfig config, IConfiguration root) =>
        {
            config.Routing = update;
            WriteConfigSection(root, "Gateway:Routing", update);
            return Results.Ok(config.Routing);
        })
        .WithName("UpdateRoutingConfig")
        .WithDescription("Update the gateway routing configuration.");

        // PUT /api/gateway/config/openclaw — update OpenClaw config
        group.MapPut("/config/openclaw", (OpenClawGatewayConfig update, GatewayConfig config, IConfiguration root) =>
        {
            config.OpenClaw = update;
            WriteConfigSection(root, "Gateway:OpenClaw", update);
            return Results.Ok(config.OpenClaw);
        })
        .WithName("UpdateOpenClawConfig")
        .WithDescription("Update the OpenClaw bridge configuration.");
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
                    var serialized = System.Text.Json.JsonSerializer.Serialize(value,
                        new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase, WriteIndented = true });
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
}
