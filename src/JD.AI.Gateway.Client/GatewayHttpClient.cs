using System.Net.Http.Json;
using JD.AI.Gateway.Client.Models;

namespace JD.AI.Gateway.Client;

/// <summary>
/// HTTP client for JD.AI Gateway REST API.
/// Covers agents, sessions, providers, channels, routing, memory, plugins, and gateway status.
/// </summary>
public sealed class GatewayHttpClient(HttpClient http)
{
    private sealed record SendMessageResponse(string Response);
    private sealed record CreatedAgentResponse(string Id);

    // --- Agents ---

    public async Task<AgentInfo[]> GetAgentsAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<AgentInfo[]>("api/v1/agents", ct) ?? [];

    public async Task<string?> SpawnAgentAsync(AgentDefinition definition, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/v1/agents", definition, ct);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<CreatedAgentResponse>(cancellationToken: ct);
        return created?.Id;
    }

    public async Task<string?> SendMessageAsync(string agentId, string message, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"api/v1/agents/{Segment(agentId, nameof(agentId))}/message", new { message }, ct);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            var payload = await response.Content.ReadFromJsonAsync<SendMessageResponse>(cancellationToken: ct);
            return payload?.Response;
        }

        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task DeleteAgentAsync(string agentId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync(new Uri($"api/v1/agents/{Segment(agentId, nameof(agentId))}", UriKind.Relative), ct);
        response.EnsureSuccessStatusCode();
    }

    // --- Sessions ---

    public async Task<SessionInfo[]> GetSessionsAsync(int limit = 50, CancellationToken ct = default)
    {
        ValidateSessionLimit(limit);
        return await http.GetFromJsonAsync<SessionInfo[]>($"api/v1/sessions?limit={limit}", ct) ?? [];
    }

    public async Task<SessionInfo?> GetSessionAsync(string id, CancellationToken ct = default)
        => await http.GetFromJsonAsync<SessionInfo>($"api/v1/sessions/{Segment(id, nameof(id))}", ct);

    public async Task CloseSessionAsync(string id, CancellationToken ct = default)
    {
        var response = await http.PostAsync(new Uri($"api/v1/sessions/{Segment(id, nameof(id))}/close", UriKind.Relative), null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task ExportSessionAsync(string id, CancellationToken ct = default)
    {
        var response = await http.PostAsync(new Uri($"api/v1/sessions/{Segment(id, nameof(id))}/export", UriKind.Relative), null, ct);
        response.EnsureSuccessStatusCode();
    }

    // --- Providers ---

    public async Task<ProviderInfo[]> GetProvidersAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<ProviderInfo[]>("api/v1/providers", ct) ?? [];

    public async Task<ProviderModelInfo[]> GetProviderModelsAsync(string name, CancellationToken ct = default)
        => await http.GetFromJsonAsync<ProviderModelInfo[]>($"api/v1/providers/{Segment(name, nameof(name))}/models", ct) ?? [];

    // --- Channels ---

    public async Task<ChannelInfo[]> GetChannelsAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<ChannelInfo[]>("api/v1/channels", ct) ?? [];

    public async Task ConnectChannelAsync(string type, CancellationToken ct = default)
    {
        var response = await http.PostAsync(new Uri($"api/v1/channels/{Segment(type, nameof(type))}/connect", UriKind.Relative), null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DisconnectChannelAsync(string type, CancellationToken ct = default)
    {
        var response = await http.PostAsync(new Uri($"api/v1/channels/{Segment(type, nameof(type))}/disconnect", UriKind.Relative), null, ct);
        response.EnsureSuccessStatusCode();
    }

    // --- Routing ---

    public async Task<RoutingMapping[]> GetRoutingMappingsAsync(CancellationToken ct = default)
    {
        var dict = await http.GetFromJsonAsync<Dictionary<string, string>>("api/v1/routing/mappings", ct);
        return dict?.Select(kv => new RoutingMapping(kv.Key, kv.Value)).ToArray() ?? [];
    }

    public async Task MapRoutingAsync(string channelId, string agentId, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/v1/routing/map", new { channelId, agentId }, ct);
        response.EnsureSuccessStatusCode();
    }

    // --- Gateway Status ---

    public async Task<GatewayStatus?> GetStatusAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<GatewayStatus>("api/v1/gateway/status", ct);

    // --- Memory ---

    public async Task IndexDocumentAsync(string content, string? source = null, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/v1/memory/index", new { content, source }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string[]> SearchMemoryAsync(string query, int limit = 5, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/v1/memory/search", new { query, limit }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<string[]>(cancellationToken: ct) ?? [];
    }

    // --- Health ---

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await http.GetAsync(new Uri("api/v1/gateway/status", UriKind.Relative), ct);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    private static string Segment(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return Uri.EscapeDataString(value);
    }

    private static void ValidateSessionLimit(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 1000);
    }
}
