using JD.AI.Core.Sessions;

namespace JD.AI.Gateway.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sessions").WithTags("Sessions");

        group.MapGet("/", async (SessionStore store, int? limit) =>
        {
            var sessions = await store.ListSessionsAsync(limit: limit ?? 50);
            return Results.Ok(sessions.Select(s => new
            {
                s.Id,
                s.Name,
                s.ProviderName,
                s.ModelId,
                s.CreatedAt,
                s.UpdatedAt,
                s.MessageCount,
                s.TotalTokens,
                s.IsActive
            }));
        })
        .WithName("ListSessions")
        .WithDescription("List all stored sessions.");

        group.MapGet("/{id}", async (string id, SessionStore store) =>
        {
            var session = await store.GetSessionAsync(id);
            return session is null ? Results.NotFound() : Results.Ok(session);
        })
        .WithName("GetSession")
        .WithDescription("Get a session by ID, including turn history.");

        group.MapPost("/{id}/close", async (string id, SessionStore store) =>
        {
            await store.CloseSessionAsync(id);
            return Results.NoContent();
        })
        .WithName("CloseSession")
        .WithDescription("Close an active session.");

        group.MapPost("/{id}/export", async (string id, SessionStore store) =>
        {
            var session = await store.GetSessionAsync(id);
            if (session is null) return Results.NotFound();
            await SessionExporter.ExportAsync(session);
            return Results.Ok(new { Message = "Exported" });
        })
        .WithName("ExportSession")
        .WithDescription("Export a session to the default export directory.");
    }
}
