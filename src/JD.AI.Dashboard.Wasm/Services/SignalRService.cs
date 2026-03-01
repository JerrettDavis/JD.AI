using Microsoft.AspNetCore.SignalR.Client;
using JD.AI.Dashboard.Wasm.Models;

namespace JD.AI.Dashboard.Wasm.Services;

public sealed class SignalRService : IAsyncDisposable
{
    private HubConnection? _eventHub;
    private HubConnection? _agentHub;
    private readonly string _baseUrl;

    public event Action<ActivityEvent>? OnActivityEvent;
    public event Action<string, bool>? OnChannelStatusChanged;
    public event Action<string, string>? OnAgentMessage;
    public event Action? OnStateChanged;

    public bool IsConnected => _eventHub?.State == HubConnectionState.Connected;

    public SignalRService(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task ConnectAsync()
    {
        _eventHub = new HubConnectionBuilder()
            .WithUrl($"{_baseUrl}/hubs/events")
            .WithAutomaticReconnect()
            .Build();

        _agentHub = new HubConnectionBuilder()
            .WithUrl($"{_baseUrl}/hubs/agent")
            .WithAutomaticReconnect()
            .Build();

        _eventHub.On<ActivityEvent>("ActivityEvent", evt =>
        {
            OnActivityEvent?.Invoke(evt);
            OnStateChanged?.Invoke();
        });

        _eventHub.On<string, bool>("ChannelStatusChanged", (channel, connected) =>
        {
            OnChannelStatusChanged?.Invoke(channel, connected);
            OnStateChanged?.Invoke();
        });

        _agentHub.On<string, string>("AgentMessage", (agentId, message) =>
        {
            OnAgentMessage?.Invoke(agentId, message);
        });

        _eventHub.Reconnecting += _ => { OnStateChanged?.Invoke(); return Task.CompletedTask; };
        _eventHub.Reconnected += _ => { OnStateChanged?.Invoke(); return Task.CompletedTask; };

        try { await _eventHub.StartAsync(); } catch { /* Gateway may not be running */ }
        try { await _agentHub.StartAsync(); } catch { /* Gateway may not be running */ }
    }

    public async ValueTask DisposeAsync()
    {
        if (_eventHub is not null) await _eventHub.DisposeAsync();
        if (_agentHub is not null) await _agentHub.DisposeAsync();
    }
}
