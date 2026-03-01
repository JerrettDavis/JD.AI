namespace JD.AI.Dashboard.Wasm.Models;

public record GatewayStatus
{
    public bool IsRunning { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public int ActiveAgents { get; init; }
    public int ActiveChannels { get; init; }
    public int ActiveSessions { get; init; }
    public OpenClawStatus? OpenClaw { get; init; }
}

public record OpenClawStatus
{
    public bool Connected { get; init; }
    public string? Endpoint { get; init; }
    public int RegisteredAgents { get; init; }
    public int ActiveBindings { get; init; }
    public string? LastError { get; init; }
}

public record ActivityEvent
{
    public string EventType { get; init; } = "";
    public string SourceId { get; init; } = "";
    public DateTimeOffset Timestamp { get; init; }
    public string? Message { get; init; }
}
