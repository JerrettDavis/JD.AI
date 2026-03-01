using JD.AI.Channels.OpenClaw;
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
                    c.Type, c.Name, c.Enabled, c.AutoConnect,
                    Settings = c.Settings.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value.StartsWith("env:", StringComparison.OrdinalIgnoreCase)
                            ? kv.Value : "***")
                }),
                Agents = config.Agents.Select(a => new
                {
                    a.Id, a.Provider, a.Model, a.AutoSpawn, a.MaxTurns
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
                        a.Id, a.Name, a.Emoji, a.Theme, a.Model,
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
    }
}
