namespace JD.AI.Dashboard.Wasm.Models;

public record ChannelInfo
{
    public string Type { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Enabled { get; init; }
    public bool Connected { get; init; }
    public string? AssignedAgentId { get; init; }
    public string? Model { get; init; }
    public string? RoutingMode { get; init; }
    public string? StatusMessage { get; init; }
    public DateTimeOffset? LastActivity { get; init; }
}
