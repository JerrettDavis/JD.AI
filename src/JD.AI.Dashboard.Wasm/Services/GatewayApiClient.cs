using System.Net.Http.Json;
using JD.AI.Dashboard.Wasm.Models;

namespace JD.AI.Dashboard.Wasm.Services;

public sealed class GatewayApiClient(HttpClient http)
{
    // Agents
    public Task<AgentInfo[]?> GetAgentsAsync() =>
        http.GetFromJsonAsync<AgentInfo[]>("api/agents");

    public async Task<AgentInfo?> SpawnAgentAsync(AgentDefinition definition)
    {
        var response = await http.PostAsJsonAsync("api/agents", definition);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentInfo>();
    }

    public Task DeleteAgentAsync(string id) =>
        http.DeleteAsync($"api/agents/{id}");

    // Channels
    public Task<ChannelInfo[]?> GetChannelsAsync() =>
        http.GetFromJsonAsync<ChannelInfo[]>("api/channels");

    public Task ConnectChannelAsync(string type) =>
        http.PostAsync($"api/channels/{type}/connect", null);

    public Task DisconnectChannelAsync(string type) =>
        http.PostAsync($"api/channels/{type}/disconnect", null);

    // Sessions
    public Task<SessionInfo[]?> GetSessionsAsync(int limit = 50) =>
        http.GetFromJsonAsync<SessionInfo[]>($"api/sessions?limit={limit}");

    public Task<SessionInfo?> GetSessionAsync(string id) =>
        http.GetFromJsonAsync<SessionInfo>($"api/sessions/{Uri.EscapeDataString(id)}");

    public Task CloseSessionAsync(string id) =>
        http.PostAsync($"api/sessions/{Uri.EscapeDataString(id)}/close", null);

    public Task ExportSessionAsync(string id) =>
        http.PostAsync($"api/sessions/{Uri.EscapeDataString(id)}/export", null);

    // Providers
    public Task<ProviderInfo[]?> GetProvidersAsync() =>
        http.GetFromJsonAsync<ProviderInfo[]>("api/providers");

    public Task<ProviderModelInfo[]?> GetProviderModelsAsync(string name) =>
        http.GetFromJsonAsync<ProviderModelInfo[]>($"api/providers/{name}/models");

    // Routing
    public Task<RoutingMapping[]?> GetRoutingMappingsAsync() =>
        http.GetFromJsonAsync<RoutingMapping[]>("api/routing/mappings");

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
        http.PostAsync("api/gateway/openclaw/agents/sync", null);
}
