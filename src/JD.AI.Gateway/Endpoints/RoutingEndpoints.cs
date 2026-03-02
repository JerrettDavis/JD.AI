using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Endpoints;

public static class RoutingEndpoints
{
    public static void MapRoutingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/routing").WithTags("Routing");

        // GET /api/routing/mappings — list channel→agent mappings
        group.MapGet("/mappings", (AgentRouter router) =>
            Results.Ok(router.GetMappings()));

        // POST /api/routing/map — map a channel to an agent
        group.MapPost("/map", (MapRequest req, AgentRouter router) =>
        {
            router.MapChannel(req.ChannelId, req.AgentId);
            return Results.Ok(new { Status = "mapped", req.ChannelId, req.AgentId });
        });
    }
}

public record MapRequest(string ChannelId, string AgentId);
