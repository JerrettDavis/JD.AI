using Microsoft.AspNetCore.SignalR;

namespace JD.AI.Gateway.Hubs;

/// <summary>
/// Real-time hub for agent interactions — streaming responses, tool calls, status updates.
/// </summary>
public sealed class AgentHub : Hub
{
    /// <summary>
    /// Client sends a message to an agent; response is streamed back via <c>AgentResponse</c>.
    /// </summary>
    public async IAsyncEnumerable<AgentStreamChunk> StreamChat(
        string agentId,
        string message,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // HACK: wire to AgentPoolService streaming
        yield return new AgentStreamChunk("start", agentId, null);
        yield return new AgentStreamChunk("content", agentId, $"Echo: {message}");
        yield return new AgentStreamChunk("end", agentId, null);
        await Task.CompletedTask;
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "agents");
        await base.OnConnectedAsync();
    }
}

public record AgentStreamChunk(string Type, string AgentId, string? Content);
