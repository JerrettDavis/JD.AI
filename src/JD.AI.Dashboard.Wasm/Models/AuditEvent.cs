namespace JD.AI.Dashboard.Wasm.Models;

public record AuditEvent
{
    public string Id { get; init; } = "";

    public DateTimeOffset Timestamp { get; init; }

    public string Level { get; init; } = "info";

    public string Source { get; init; } = "";

    public string EventType { get; init; } = "";

    public string Message { get; init; } = "";

    public string? Payload { get; init; }
}
