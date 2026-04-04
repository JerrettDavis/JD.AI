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
    private readonly IGatewayHttpClient _http;
    private readonly IGatewaySignalRClient _signalR;
    private readonly HttpClient? _ownedHttpClient;
    private readonly string _gatewayUrl;
    private string? _activeAgentId;

    public bool IsConnected => _signalR.IsConnected;
    public string? ActiveAgentId => _activeAgentId;
    public string GatewayUrl => _gatewayUrl;

    public GatewayConnectionService(string gatewayUrl)
    {
        _gatewayUrl = gatewayUrl.TrimEnd('/');
        _ownedHttpClient = new HttpClient { BaseAddress = new Uri(_gatewayUrl + "/") };
        _http = new GatewayHttpClientAdapter(new GatewayHttpClient(_ownedHttpClient));
        _signalR = new GatewaySignalRClientAdapter(new GatewaySignalRClient(_gatewayUrl));
    }

    internal GatewayConnectionService(
        string gatewayUrl,
        IGatewayHttpClient http,
        IGatewaySignalRClient signalR)
    {
        _gatewayUrl = gatewayUrl.TrimEnd('/');
        _http = http;
        _signalR = signalR;
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
        var agents = await _http.GetAgentsAsync(ct);
        var existing = agents.FirstOrDefault(agent =>
            (provider is null || string.Equals(agent.Provider, provider, StringComparison.OrdinalIgnoreCase))
            && (model is null || string.Equals(agent.Model, model, StringComparison.OrdinalIgnoreCase)));

        if (existing is not null)
        {
            _activeAgentId = existing.Id;
            return _activeAgentId;
        }

        var providers = await _http.GetProvidersAsync(ct);
        var providerInfo = SelectProvider(providers, provider, model);
        if (providerInfo is null)
            return null;

        var selectedProvider = providerInfo.Name;
        var selectedModel = SelectModel(providerInfo, model);
        if (selectedModel is null)
            return null;

        var definition = new GatewayModels.AgentDefinition
        {
            Id = $"tui-{Guid.NewGuid():N}"[..16],
            Provider = selectedProvider!,
            Model = selectedModel!,
        };

        var spawnedId = await _http.SpawnAgentAsync(definition, ct);
        _activeAgentId = spawnedId;
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
    /// Returns the most recent persisted session ID, or null when none exist.
    /// </summary>
    public async Task<string?> GetLatestSessionAsync(CancellationToken ct = default)
    {
        var sessions = await _http.GetSessionsAsync(1, ct).ConfigureAwait(false);
        return sessions.FirstOrDefault()?.Id;
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
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 1000);

        return await _http.GetSessionsAsync(limit, ct);
    }

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
        _ownedHttpClient?.Dispose();
    }

    private static GatewayModels.ProviderInfo? SelectProvider(
        GatewayModels.ProviderInfo[] providers,
        string? provider,
        string? model)
    {
        if (provider is not null)
        {
            return providers.FirstOrDefault(p =>
                p.IsAvailable
                && p.Models.Length > 0
                && string.Equals(p.Name, provider, StringComparison.OrdinalIgnoreCase));
        }

        if (model is not null)
        {
            return providers.FirstOrDefault(p =>
                p.IsAvailable
                && p.Models.Any(m => string.Equals(m.Id, model, StringComparison.OrdinalIgnoreCase)));
        }

        return providers.FirstOrDefault(p => p.IsAvailable && p.Models.Length > 0);
    }

    private static string? SelectModel(GatewayModels.ProviderInfo provider, string? model)
    {
        if (model is null)
            return provider.Models.FirstOrDefault()?.Id;

        return provider.Models.FirstOrDefault(m => string.Equals(m.Id, model, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    internal interface IGatewayHttpClient
    {
        Task<bool> IsHealthyAsync(CancellationToken ct = default);
        Task<GatewayModels.AgentInfo[]> GetAgentsAsync(CancellationToken ct = default);
        Task<string?> SpawnAgentAsync(GatewayModels.AgentDefinition definition, CancellationToken ct = default);
        Task<string?> SendMessageAsync(string agentId, string message, CancellationToken ct = default);
        Task<GatewayModels.SessionInfo[]> GetSessionsAsync(int limit = 50, CancellationToken ct = default);
        Task<GatewayModels.GatewayStatus?> GetStatusAsync(CancellationToken ct = default);
        Task<GatewayModels.ProviderInfo[]> GetProvidersAsync(CancellationToken ct = default);
    }

    internal interface IGatewaySignalRClient : IAsyncDisposable
    {
        bool IsConnected { get; }
        string? ConnectionError { get; }
        Task ConnectAsync(CancellationToken ct = default);
        IAsyncEnumerable<GatewayModels.AgentStreamChunk> StreamChatAsync(
            string agentId,
            string message,
            CancellationToken ct = default);
    }

    private sealed class GatewayHttpClientAdapter(GatewayHttpClient inner) : IGatewayHttpClient
    {
        public Task<bool> IsHealthyAsync(CancellationToken ct = default) => inner.IsHealthyAsync(ct);
        public Task<GatewayModels.AgentInfo[]> GetAgentsAsync(CancellationToken ct = default) => inner.GetAgentsAsync(ct);
        public Task<string?> SpawnAgentAsync(GatewayModels.AgentDefinition definition, CancellationToken ct = default) => inner.SpawnAgentAsync(definition, ct);
        public Task<string?> SendMessageAsync(string agentId, string message, CancellationToken ct = default) => inner.SendMessageAsync(agentId, message, ct);
        public Task<GatewayModels.SessionInfo[]> GetSessionsAsync(int limit = 50, CancellationToken ct = default) => inner.GetSessionsAsync(limit, ct);
        public Task<GatewayModels.GatewayStatus?> GetStatusAsync(CancellationToken ct = default) => inner.GetStatusAsync(ct);
        public Task<GatewayModels.ProviderInfo[]> GetProvidersAsync(CancellationToken ct = default) => inner.GetProvidersAsync(ct);
    }

    private sealed class GatewaySignalRClientAdapter(GatewaySignalRClient inner) : IGatewaySignalRClient
    {
        public bool IsConnected => inner.IsConnected;
        public string? ConnectionError => inner.ConnectionError;
        public Task ConnectAsync(CancellationToken ct = default) => inner.ConnectAsync(ct);
        public IAsyncEnumerable<GatewayModels.AgentStreamChunk> StreamChatAsync(string agentId, string message, CancellationToken ct = default)
            => inner.StreamChatAsync(agentId, message, ct);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
