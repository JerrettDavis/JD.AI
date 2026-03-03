using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Endpoints;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/agents").WithTags("Agents");

        group.MapGet("/", (AgentPoolService pool) =>
        {
            var agents = pool.ListAgents();
            return Results.Ok(agents);
        })
        .WithName("ListAgents")
        .WithDescription("List all active agent instances.");

        group.MapPost("/", async (SpawnAgentRequest request, AgentPoolService pool, CancellationToken ct) =>
        {
            var agentId = await pool.SpawnAgentAsync(
                request.Provider,
                request.Model,
                request.SystemPrompt,
                ct);
            return Results.Created($"/api/agents/{agentId}", new { Id = agentId });
        })
        .WithName("SpawnAgent")
        .WithDescription("Spawn a new agent instance.");

        group.MapPost("/{id}/message", async (string id, SendMessageRequest request, AgentPoolService pool, CancellationToken ct) =>
        {
            var response = await pool.SendMessageAsync(id, request.Message, ct);
            return response is null
                ? Results.NotFound()
                : Results.Ok(new { Response = response });
        })
        .WithName("SendMessage")
        .WithDescription("Send a message to an agent and get the response.");

        group.MapDelete("/{id}", (string id, AgentPoolService pool) =>
        {
            pool.StopAgent(id);
            return Results.NoContent();
        })
        .WithName("StopAgent")
        .WithDescription("Stop and remove an agent instance.");
    }
}

public record SpawnAgentRequest(
    string Provider,
    string Model,
    string? SystemPrompt = null);

public record SendMessageRequest(string Message);
