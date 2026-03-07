using JD.AI.Core.Governance.Audit;

namespace JD.AI.Gateway.Endpoints;

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/audit").WithTags("Audit");

        group.MapGet("/events", async (
            IQueryableAuditSink store,
            string? action,
            string? severity,
            string? sessionId,
            string? userId,
            string? resource,
            DateTimeOffset? from,
            DateTimeOffset? until,
            int? limit,
            int? offset) =>
        {
            AuditSeverity? minSeverity = null;
            if (severity is not null && Enum.TryParse<AuditSeverity>(severity, ignoreCase: true, out var parsed))
                minSeverity = parsed;

            var query = new AuditQuery
            {
                Action = action,
                MinSeverity = minSeverity,
                SessionId = sessionId,
                UserId = userId,
                Resource = resource,
                From = from,
                Until = until,
                Limit = limit ?? 50,
                Offset = offset ?? 0,
            };

            var result = await store.QueryAsync(query);
            return Results.Ok(new
            {
                result.TotalCount,
                Count = result.Events.Count,
                result.Events,
            });
        })
        .WithName("QueryAuditEvents")
        .WithDescription("Query audit events with optional filters.");

        group.MapGet("/events/{eventId}", async (string eventId, IQueryableAuditSink store) =>
        {
            // Search for a specific event by ID
            var result = await store.QueryAsync(new AuditQuery { Limit = 1000 });
            var evt = result.Events.FirstOrDefault(e =>
                string.Equals(e.EventId, eventId, StringComparison.Ordinal));
            return evt is null ? Results.NotFound() : Results.Ok(evt);
        })
        .WithName("GetAuditEvent")
        .WithDescription("Get a single audit event by its event ID.");

        group.MapGet("/stats", async (IQueryableAuditSink store) =>
        {
            var all = await store.QueryAsync(new AuditQuery { Limit = 1000 });
            var events = all.Events;

            var bySeverity = events
                .GroupBy(e => e.Severity)
                .ToDictionary(g => g.Key.ToString(), g => g.Count());

            var byAction = events
                .GroupBy(e => e.Action)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key, g => g.Count());

            return Results.Ok(new
            {
                TotalEvents = store.Count,
                BySeverity = bySeverity,
                TopActions = byAction,
            });
        })
        .WithName("GetAuditStats")
        .WithDescription("Get summary statistics about audit events.");
    }
}
