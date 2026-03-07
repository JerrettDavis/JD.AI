namespace JD.AI.Dashboard.Wasm.Models;

/// <summary>
/// The routing API returns Dictionary&lt;string, string&gt; (channelType → agentId).
/// This wrapper is used by the UI for structured display and editing.
/// </summary>
public record RoutingMapping
{
    public string ChannelType { get; set; } = "";
    public string AgentId { get; set; } = "";
}
