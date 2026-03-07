using JD.AI.Core.Channels;

namespace JD.AI.Gateway.Endpoints;

public static class ChannelEndpoints
{
    public static void MapChannelEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/channels").WithTags("Channels");

        group.MapGet("/", (IChannelRegistry registry) =>
        {
            return Results.Ok(registry.Channels.Select(c => new
            {
                c.ChannelType,
                c.DisplayName,
                c.IsConnected
            }));
        })
        .WithName("ListChannels")
        .WithDescription("List all registered messaging channels and their status.");

        group.MapPost("/{type}/connect", async (string type, IChannelRegistry registry, CancellationToken ct) =>
        {
            var channel = registry.GetChannel(type);
            if (channel is null) return Results.NotFound();
            await channel.ConnectAsync(ct);
            return Results.Ok(new { channel.ChannelType, channel.IsConnected });
        })
        .WithName("ConnectChannel")
        .WithDescription("Connect a channel adapter.");

        group.MapPost("/{type}/disconnect", async (string type, IChannelRegistry registry, CancellationToken ct) =>
        {
            var channel = registry.GetChannel(type);
            if (channel is null) return Results.NotFound();
            await channel.DisconnectAsync(ct);
            return Results.Ok(new { channel.ChannelType, channel.IsConnected });
        })
        .WithName("DisconnectChannel")
        .WithDescription("Disconnect a channel adapter.");

        group.MapPost("/{type}/send", async (
            string type,
            ChannelSendRequest request,
            IChannelRegistry registry,
            CancellationToken ct) =>
        {
            var channel = registry.GetChannel(type);
            if (channel is null) return Results.NotFound();
            await channel.SendMessageAsync(request.ConversationId, request.Content, ct);
            return Results.Accepted();
        })
        .WithName("SendChannelMessage")
        .WithDescription("Send a message through a specific channel.");
    }
}

public record ChannelSendRequest(string ConversationId, string Content);
