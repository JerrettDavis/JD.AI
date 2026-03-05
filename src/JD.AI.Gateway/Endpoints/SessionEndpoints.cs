using JD.AI.Core.Sessions;

namespace JD.AI.Gateway.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/sessions").WithTags("Sessions");

        group.MapGet("/", async (SessionStore store, int? limit, string? cursor) =>
        {
            var clampedLimit = PaginationHelper.ClampLimit(limit);
            var offset = PaginationHelper.DecodeCursor(cursor);
            var sessions = await store.ListSessionsAsync(limit: offset + clampedLimit + 1);
            var items = sessions.Select(s => new
            {
                s.Id, s.Name, s.ProviderName, s.ModelId,
                s.CreatedAt, s.UpdatedAt, s.MessageCount, s.TotalTokens, s.IsActive
            }).ToList();

            // If no cursor was provided, return plain array for backward compat
            if (string.IsNullOrEmpty(cursor))
                return Results.Ok(items.Take(clampedLimit));

            return Results.Ok(PaginationHelper.Paginate(items, limit, cursor));
        })
        .WithName("ListSessions")
        .WithDescription("List stored sessions. Pass ?cursor= for paginated response.");

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
