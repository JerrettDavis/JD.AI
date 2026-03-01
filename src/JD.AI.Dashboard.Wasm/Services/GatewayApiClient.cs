using System.Net.Http.Json;
using JD.AI.Dashboard.Wasm.Models;

namespace JD.AI.Dashboard.Wasm.Services;

public sealed class GatewayApiClient(HttpClient http)
{
    // Agents
    public async Task<AgentInfo[]> GetAgentsAsync()
        => await http.GetFromJsonAsync<AgentInfo[]>("api/agents") ?? [];

    public async Task<AgentInfo?> SpawnAgentAsync(AgentDefinition definition)
    {
        var response = await http.PostAsJsonAsync("api/agents", definition);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentInfo>();
    }

    public Task DeleteAgentAsync(string id) =>
        http.DeleteAsync(new Uri($"api/agents/{id}", UriKind.Relative));

    // Channels
    public async Task<ChannelInfo[]> GetChannelsAsync()
        => await http.GetFromJsonAsync<ChannelInfo[]>("api/channels") ?? [];

    public Task ConnectChannelAsync(string type) =>
        http.PostAsync(new Uri($"api/channels/{type}/connect", UriKind.Relative), null);

    public Task DisconnectChannelAsync(string type) =>
        http.PostAsync(new Uri($"api/channels/{type}/disconnect", UriKind.Relative), null);

    // Sessions
    public async Task<SessionInfo[]> GetSessionsAsync(int limit = 50)
        => await http.GetFromJsonAsync<SessionInfo[]>($"api/sessions?limit={limit}") ?? [];

    public Task<SessionInfo?> GetSessionAsync(string id) =>
        http.GetFromJsonAsync<SessionInfo>($"api/sessions/{Uri.EscapeDataString(id)}");

    public Task CloseSessionAsync(string id) =>
        http.PostAsync(new Uri($"api/sessions/{Uri.EscapeDataString(id)}/close", UriKind.Relative), null);

    public Task ExportSessionAsync(string id) =>
        http.PostAsync(new Uri($"api/sessions/{Uri.EscapeDataString(id)}/export", UriKind.Relative), null);

    // Providers
    public async Task<ProviderInfo[]> GetProvidersAsync()
        => await http.GetFromJsonAsync<ProviderInfo[]>("api/providers") ?? [];

    public async Task<ProviderModelInfo[]> GetProviderModelsAsync(string name)
        => await http.GetFromJsonAsync<ProviderModelInfo[]>($"api/providers/{name}/models") ?? [];

    // Routing — API returns Dictionary<string, string>, we convert to RoutingMapping[]
    public async Task<RoutingMapping[]> GetRoutingMappingsAsync()
    {
        var dict = await http.GetFromJsonAsync<Dictionary<string, string>>("api/routing/mappings");
        if (dict is null) return [];
        return dict.Select(kv => new RoutingMapping { ChannelType = kv.Key, AgentId = kv.Value }).ToArray();
    }

    public async Task MapRoutingAsync(string channelId, string agentId)
    {
        var response = await http.PostAsJsonAsync("api/routing/map", new { channelId, agentId });
        response.EnsureSuccessStatusCode();
    }

    // Gateway
    public Task<GatewayStatus?> GetStatusAsync() =>
        http.GetFromJsonAsync<GatewayStatus>("api/gateway/status");

    public Task<object?> GetConfigAsync() =>
        http.GetFromJsonAsync<object>("api/gateway/config");

    // OpenClaw
    public Task<object?> GetOpenClawStatusAsync() =>
        http.GetFromJsonAsync<object>("api/gateway/openclaw/status");

    public Task<object[]?> GetOpenClawAgentsAsync() =>
        http.GetFromJsonAsync<object[]>("api/gateway/openclaw/agents");

    public Task SyncOpenClawAsync() =>
        http.PostAsync(new Uri("api/gateway/openclaw/agents/sync", UriKind.Relative), null);
}
