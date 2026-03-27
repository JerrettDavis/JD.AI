using System.Runtime.CompilerServices;
using JD.AI.Gateway.Client.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Gateway.Client;

/// <summary>
/// SignalR client for JD.AI Gateway real-time communication.
/// Provides agent chat streaming and event subscriptions.
/// </summary>
public sealed class GatewaySignalRClient : IAsyncDisposable
{
    private readonly HubConnection _agentHub;
    private readonly HubConnection _eventHub;

    public event Action<ActivityEvent>? OnActivityEvent;
    public event Action<string, bool>? OnChannelStatusChanged;
    public event Action<string, string>? OnAgentMessage;
    public event EventHandler? OnConnected;
    public event EventHandler? OnDisconnected;

    public bool IsConnected => _eventHub.State == HubConnectionState.Connected;
    public string? ConnectionError { get; private set; }

    public GatewaySignalRClient(string gatewayUrl)
    {
        var baseUrl = gatewayUrl.TrimEnd('/');

        _eventHub = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/events")
            .WithAutomaticReconnect([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)])
            .AddJsonProtocol()
            .Build();

        _agentHub = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/agent")
            .WithAutomaticReconnect([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)])
            .AddJsonProtocol()
            .Build();

        _eventHub.On<ActivityEvent>("ActivityEvent", evt => OnActivityEvent?.Invoke(evt));
        _eventHub.On<string, bool>("ChannelStatusChanged", (ch, connected) =>
            OnChannelStatusChanged?.Invoke(ch, connected));

        _agentHub.On<string, string>("AgentMessage", (agentId, msg) =>
            OnAgentMessage?.Invoke(agentId, msg));

        _eventHub.Reconnected += _ => { OnConnected?.Invoke(this, EventArgs.Empty); return Task.CompletedTask; };
        _eventHub.Closed += _ => { OnDisconnected?.Invoke(this, EventArgs.Empty); return Task.CompletedTask; };
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            await _eventHub.StartAsync(ct);
            ConnectionError = null;
        }
        catch (Exception ex)
        {
            ConnectionError = $"Events hub: {ex.Message}";
        }

        try
        {
            await _agentHub.StartAsync(ct);
        }
        catch (Exception ex)
        {
            ConnectionError = (ConnectionError is null ? "" : ConnectionError + "; ") + $"Agent hub: {ex.Message}";
        }

        if (ConnectionError is null)
            OnConnected?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Stream chat responses from an agent. Yields chunks as they arrive.
    /// </summary>
    public async IAsyncEnumerable<AgentStreamChunk> StreamChatAsync(
        string agentId, string message, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var stream = _agentHub.StreamAsync<AgentStreamChunk>("StreamChat", agentId, message, ct);
        await foreach (var chunk in stream.WithCancellation(ct))
        {
            yield return chunk;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await _eventHub.DisposeAsync(); } catch { /* ignore */ }
        try { await _agentHub.DisposeAsync(); } catch { /* ignore */ }
    }
}
