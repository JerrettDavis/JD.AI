namespace JD.AI.Dashboard.Wasm.Models;

public record RoutingMapping
{
    public string ChannelType { get; init; } = "";
    public string AgentId { get; init; } = "";
    public string? ConversationPattern { get; init; }
}
