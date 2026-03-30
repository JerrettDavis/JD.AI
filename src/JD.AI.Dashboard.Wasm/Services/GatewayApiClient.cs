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

    public Task<GatewayConfigModel?> GetConfigAsync() =>
        http.GetFromJsonAsync<GatewayConfigModel>("api/gateway/config/raw");

    // Config section updates
    public async Task<ServerConfigModel?> UpdateServerConfigAsync(ServerConfigModel config)
    {
        var response = await http.PutAsJsonAsync("api/gateway/config/server", config);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ServerConfigModel>();
    }

    public async Task UpdateAuthConfigAsync(AuthConfigModel config)
    {
        var response = await http.PutAsJsonAsync("api/gateway/config/auth", config);
        response.EnsureSuccessStatusCode();
    }

    public async Task<RateLimitConfigModel?> UpdateRateLimitConfigAsync(RateLimitConfigModel config)
    {
        var response = await http.PutAsJsonAsync("api/gateway/config/ratelimit", config);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RateLimitConfigModel>();
    }

    public async Task UpdateProvidersConfigAsync(IList<ProviderConfigModel> providers)
    {
        var response = await http.PutAsJsonAsync("api/gateway/config/providers", providers);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAgentsConfigAsync(IList<AgentDefinition> agents)
    {
        var response = await http.PutAsJsonAsync("api/gateway/config/agents", agents);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateChannelsConfigAsync(IList<ChannelConfigModel> channels)
    {
        var response = await http.PutAsJsonAsync("api/gateway/config/channels", channels);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateRoutingConfigAsync(RoutingConfigModel routing)
    {
        var response = await http.PutAsJsonAsync("api/gateway/config/routing", routing);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateOpenClawConfigAsync(OpenClawConfigModel openClaw)
    {
        var response = await http.PutAsJsonAsync("api/gateway/config/openclaw", openClaw);
        response.EnsureSuccessStatusCode();
    }

    // OpenClaw
    public Task<object?> GetOpenClawStatusAsync() =>
        http.GetFromJsonAsync<object>("api/gateway/openclaw/status");

    public Task<object[]?> GetOpenClawAgentsAsync() =>
        http.GetFromJsonAsync<object[]>("api/gateway/openclaw/agents");

    public Task SyncOpenClawAsync() =>
        http.PostAsync(new Uri("api/gateway/openclaw/agents/sync", UriKind.Relative), null);

    // Plugins
    public async Task<PluginInfo[]> GetPluginsAsync()
        => await http.GetFromJsonAsync<PluginInfo[]>("api/plugins") ?? [];

    public async Task InstallPluginAsync(string pluginId)
    {
        var response = await http.PostAsJsonAsync("api/plugins/install", new { pluginId });
        response.EnsureSuccessStatusCode();
    }

    public async Task EnablePluginAsync(string id)
    {
        var response = await http.PostAsync(new Uri($"api/plugins/{Uri.EscapeDataString(id)}/enable", UriKind.Relative), null);
        response.EnsureSuccessStatusCode();
    }

    public async Task DisablePluginAsync(string id)
    {
        var response = await http.PostAsync(new Uri($"api/plugins/{Uri.EscapeDataString(id)}/disable", UriKind.Relative), null);
        response.EnsureSuccessStatusCode();
    }

    public Task UninstallPluginAsync(string id) =>
        http.DeleteAsync(new Uri($"api/plugins/{Uri.EscapeDataString(id)}", UriKind.Relative));

    // Audit / Logs
    public async Task<AuditEvent[]> GetAuditEventsAsync(int limit = 100)
        => await http.GetFromJsonAsync<AuditEvent[]>($"api/audit?limit={limit}") ?? [];

    // API Keys
    public async Task<ApiKeyDisplayModel[]> GetApiKeysAsync()
        => await http.GetFromJsonAsync<ApiKeyDisplayModel[]>($"api/v1/gateway/apikeys") ?? [];

    public async Task<CreateApiKeyResponse> CreateApiKeyAsync(CreateApiKeyRequest request)
    {
        var response = await http.PostAsJsonAsync("api/v1/gateway/apikeys", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    public Task RevokeApiKeyAsync(string maskedKey)
        => http.DeleteAsync(new Uri($"api/v1/gateway/apikeys/{Uri.EscapeDataString(maskedKey)}", UriKind.Relative));

    public async Task<RotateApiKeyResponse> RotateApiKeyAsync(string maskedKey, int? expiryDays = null)
    {
        var request = new { ExpiryDays = expiryDays };
        var response = await http.PostAsJsonAsync($"api/v1/gateway/apikeys/{Uri.EscapeDataString(maskedKey)}/rotate", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RotateApiKeyResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize response");
    }
}
