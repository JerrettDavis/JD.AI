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
                    Channels = config.OpenClaw.Channels
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
                OpenClaw = new { config.OpenClaw.Enabled }
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
    }
}
