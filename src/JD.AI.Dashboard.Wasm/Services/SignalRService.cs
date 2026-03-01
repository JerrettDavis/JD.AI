using Microsoft.AspNetCore.SignalR.Client;
using JD.AI.Dashboard.Wasm.Models;

namespace JD.AI.Dashboard.Wasm.Services;

public sealed class ActivityEventArgs(ActivityEvent activity) : EventArgs
{
    public ActivityEvent Activity { get; } = activity;
}

public sealed class ChannelStatusEventArgs(string channel, bool connected) : EventArgs
{
    public string Channel { get; } = channel;
    public bool Connected { get; } = connected;
}

public sealed class AgentMessageEventArgs(string agentId, string message) : EventArgs
{
    public string AgentId { get; } = agentId;
    public string Message { get; } = message;
}

public sealed class SignalRService : IAsyncDisposable
{
    private HubConnection? _eventHub;
    private HubConnection? _agentHub;
    private readonly string _baseUrl;

    public event EventHandler<ActivityEventArgs>? OnActivityEvent;
    public event EventHandler<ChannelStatusEventArgs>? OnChannelStatusChanged;
    public event EventHandler<AgentMessageEventArgs>? OnAgentMessage;
    public event EventHandler? OnStateChanged;

    public bool IsConnected => _eventHub?.State == HubConnectionState.Connected;
    public string ConnectionError { get; private set; } = string.Empty;

    public SignalRService(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task ConnectAsync()
    {
        try
        {
            _eventHub = new HubConnectionBuilder()
                .WithUrl($"{_baseUrl}/hubs/events")
                .WithAutomaticReconnect([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)])
                .Build();

            _agentHub = new HubConnectionBuilder()
                .WithUrl($"{_baseUrl}/hubs/agent")
                .WithAutomaticReconnect([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)])
                .Build();

            _eventHub.On<ActivityEvent>("ActivityEvent", evt =>
            {
                OnActivityEvent?.Invoke(this, new ActivityEventArgs(evt));
                OnStateChanged?.Invoke(this, EventArgs.Empty);
            });

            _eventHub.On<string, bool>("ChannelStatusChanged", (channel, connected) =>
            {
                OnChannelStatusChanged?.Invoke(this, new ChannelStatusEventArgs(channel, connected));
                OnStateChanged?.Invoke(this, EventArgs.Empty);
            });

            _agentHub.On<string, string>("AgentMessage", (agentId, message) =>
            {
                OnAgentMessage?.Invoke(this, new AgentMessageEventArgs(agentId, message));
            });

            _eventHub.Reconnecting += _ => { OnStateChanged?.Invoke(this, EventArgs.Empty); return Task.CompletedTask; };
            _eventHub.Reconnected += _ => { ConnectionError = string.Empty; OnStateChanged?.Invoke(this, EventArgs.Empty); return Task.CompletedTask; };
            _eventHub.Closed += _ => { OnStateChanged?.Invoke(this, EventArgs.Empty); return Task.CompletedTask; };

            ConnectionError = string.Empty;
            await _eventHub.StartAsync();
        }
        catch (Exception ex)
        {
            ConnectionError = ex.Message;
        }

        try
        {
            if (_agentHub is not null)
                await _agentHub.StartAsync();
        }
        catch
        {
            // Agent hub is optional
        }

        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        if (_eventHub is not null)
        {
            try { await _eventHub.DisposeAsync(); } catch { /* ignore */ }
        }

        if (_agentHub is not null)
        {
            try { await _agentHub.DisposeAsync(); } catch { /* ignore */ }
        }
    }
}
