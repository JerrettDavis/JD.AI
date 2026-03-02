using JD.AI.Gateway.Services;
using Microsoft.AspNetCore.SignalR;

namespace JD.AI.Gateway.Hubs;

/// <summary>
/// Real-time hub for agent interactions — streaming responses, tool calls, status updates.
/// </summary>
public sealed class AgentHub(AgentPoolService pool) : Hub
{
    /// <summary>
    /// Client sends a message to an agent; response is streamed back as chunks.
    /// </summary>
    public async IAsyncEnumerable<AgentStreamChunk> StreamChat(
        string agentId,
        string message,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return new AgentStreamChunk("start", agentId, null);

        string? response;
        string? error = null;
        try
        {
            response = await pool.SendMessageAsync(agentId, message, ct);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            response = null;
        }

        if (error is not null)
        {
            yield return new AgentStreamChunk("error", agentId, error);
            yield break;
        }

        if (response is null)
        {
            yield return new AgentStreamChunk("error", agentId, "Agent not found");
            yield break;
        }

        // Stream response in chunks for a progressive feel
        const int chunkSize = 80;
        for (var i = 0; i < response.Length; i += chunkSize)
        {
            var len = Math.Min(chunkSize, response.Length - i);
            yield return new AgentStreamChunk("content", agentId, response.Substring(i, len));

            if (len == chunkSize)
                await Task.Delay(15, ct);
        }

        yield return new AgentStreamChunk("end", agentId, null);
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "agents");
        await base.OnConnectedAsync();
    }
}

public record AgentStreamChunk(string Type, string AgentId, string? Content);
