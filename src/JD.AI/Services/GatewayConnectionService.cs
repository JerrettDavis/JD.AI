using JD.AI.Gateway.Client;
using Spectre.Console;
using GatewayModels = JD.AI.Gateway.Client.Models;

namespace JD.AI.Services;

/// <summary>
/// Manages TUI connection to a running Gateway instance.
/// When connected, agent execution is delegated to Gateway instead of running in-process.
/// </summary>
public sealed class GatewayConnectionService : IAsyncDisposable
{
    private readonly GatewayHttpClient _http;
    private readonly GatewaySignalRClient _signalR;
    private readonly string _gatewayUrl;
    private string? _activeAgentId;

    public bool IsConnected => _signalR.IsConnected;
    public string? ActiveAgentId => _activeAgentId;
    public string GatewayUrl => _gatewayUrl;

    public GatewayConnectionService(string gatewayUrl)
    {
        _gatewayUrl = gatewayUrl.TrimEnd('/');

        var httpClient = new HttpClient { BaseAddress = new Uri(_gatewayUrl + "/") };
        _http = new GatewayHttpClient(httpClient);
        _signalR = new GatewaySignalRClient(_gatewayUrl);
    }

    /// <summary>
    /// Connect to Gateway and verify it's running.
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        // Check if Gateway is reachable
        var healthy = await _http.IsHealthyAsync(ct);
        if (!healthy)
            return false;

        // Connect SignalR for streaming
        await _signalR.ConnectAsync(ct);

        return _signalR.IsConnected;
    }

    /// <summary>
    /// Get or spawn an agent, returning its ID.
    /// </summary>
    public async Task<string?> EnsureAgentAsync(string? provider = null, string? model = null, CancellationToken ct = default)
    {
        // Check for existing agents
        var agents = await _http.GetAgentsAsync(ct);
        if (agents.Length > 0)
        {
            _activeAgentId = agents[0].Id;
            return _activeAgentId;
        }

        // Spawn a new agent with defaults
        var definition = new GatewayModels.AgentDefinition
        {
            Id = $"tui-{Guid.NewGuid():N}"[..16],
            Provider = provider ?? "anthropic",
            Model = model ?? "claude-sonnet-4-20250514",
        };

        var spawned = await _http.SpawnAgentAsync(definition, ct);
        _activeAgentId = spawned?.Id;
        return _activeAgentId;
    }

    /// <summary>
    /// Send a message and stream the response.
    /// </summary>
    public async IAsyncEnumerable<string> SendMessageStreamingAsync(
        string message, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_activeAgentId is null)
            yield break;

        await foreach (var chunk in _signalR.StreamChatAsync(_activeAgentId, message, ct))
        {
            if (chunk.Content is not null)
                yield return chunk.Content;
        }
    }

    /// <summary>
    /// Send a message and get the full response (non-streaming).
    /// </summary>
    public async Task<string?> SendMessageAsync(string message, CancellationToken ct = default)
    {
        if (_activeAgentId is null)
            return null;

        return await _http.SendMessageAsync(_activeAgentId, message, ct);
    }

    /// <summary>
    /// Get Gateway status summary.
    /// </summary>
    public async Task<GatewayModels.GatewayStatus?> GetStatusAsync(CancellationToken ct = default)
        => await _http.GetStatusAsync(ct);

    /// <summary>
    /// Get available providers.
    /// </summary>
    public async Task<GatewayModels.ProviderInfo[]> GetProvidersAsync(CancellationToken ct = default)
        => await _http.GetProvidersAsync(ct);

    /// <summary>
    /// Get sessions list.
    /// </summary>
    public async Task<GatewayModels.SessionInfo[]> GetSessionsAsync(int limit = 50, CancellationToken ct = default)
        => await _http.GetSessionsAsync(limit, ct);

    /// <summary>
    /// Print connection status to console.
    /// </summary>
    public void PrintStatus()
    {
        if (IsConnected)
        {
            AnsiConsole.MarkupLine($"[green]Connected to Gateway[/] at [blue]{_gatewayUrl}[/]");
            if (_activeAgentId is not null)
                AnsiConsole.MarkupLine($"[dim]Active agent: {_activeAgentId}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Not connected[/] to Gateway at [blue]{_gatewayUrl}[/]");
            if (_signalR.ConnectionError is not null)
                AnsiConsole.MarkupLine($"[red]{_signalR.ConnectionError}[/]");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _signalR.DisposeAsync();
    }
}
