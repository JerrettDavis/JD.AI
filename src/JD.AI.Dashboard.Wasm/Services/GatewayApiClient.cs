using System.Net.Http.Json;
using System.Text.Json;
using JD.AI.Dashboard.Wasm.Models;

namespace JD.AI.Dashboard.Wasm.Services;

public sealed class GatewayApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions AuditPayloadJsonOptions = new()
    {
        WriteIndented = true,
    };

    // Agents
    public async Task<AgentInfo[]> GetAgentsAsync()
        => await http.GetFromJsonAsync<AgentInfo[]>("api/v1/agents") ?? [];

    public async Task<string> SpawnAgentAsync(AgentDefinition definition)
    {
        var response = await http.PostAsJsonAsync(
            "api/v1/agents",
            new
            {
                definition.Provider,
                definition.Model,
                definition.SystemPrompt,
            });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreatedAgentResponse>();
        return string.IsNullOrWhiteSpace(created?.Id)
            ? throw new InvalidOperationException("Failed to deserialize created agent id")
            : created.Id;
    }

    public async Task DeleteAgentAsync(string id)
    {
        var response = await http.DeleteAsync(new Uri($"api/v1/agents/{Uri.EscapeDataString(id)}", UriKind.Relative));
        response.EnsureSuccessStatusCode();
    }

    // Channels
    public async Task<ChannelInfo[]> GetChannelsAsync()
        => await http.GetFromJsonAsync<ChannelInfo[]>("api/channels") ?? [];

    public Task ConnectChannelAsync(string type) =>
        SendWithoutContentAsync(HttpMethod.Post, $"api/channels/{Uri.EscapeDataString(type)}/connect");

    public Task DisconnectChannelAsync(string type) =>
        SendWithoutContentAsync(HttpMethod.Post, $"api/channels/{Uri.EscapeDataString(type)}/disconnect");

    // Sessions
    public async Task<SessionInfo[]> GetSessionsAsync(int limit = 50)
        => await http.GetFromJsonAsync<SessionInfo[]>($"api/sessions?limit={limit}") ?? [];

    public Task<SessionInfo?> GetSessionAsync(string id) =>
        http.GetFromJsonAsync<SessionInfo>($"api/sessions/{Uri.EscapeDataString(id)}");

    public Task CloseSessionAsync(string id) =>
        SendWithoutContentAsync(HttpMethod.Post, $"api/sessions/{Uri.EscapeDataString(id)}/close");

    public Task ExportSessionAsync(string id) =>
        SendWithoutContentAsync(HttpMethod.Post, $"api/sessions/{Uri.EscapeDataString(id)}/export");

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
        SendWithoutContentAsync(HttpMethod.Post, "api/gateway/openclaw/agents/sync");

    public async Task<AgentDetailInfo?> GetAgentDetailAsync(string id)
        => await http.GetFromJsonAsync<AgentDetailInfo>($"api/v1/agents/{Uri.EscapeDataString(id)}");

    public async Task SetDefaultAgentAsync(string id)
    {
        var response = await http.PostAsync(
            new Uri($"api/v1/agents/{Uri.EscapeDataString(id)}/default", UriKind.Relative), null);
        response.EnsureSuccessStatusCode();
    }

    // Skills
    public async Task<SkillInfo[]> GetSkillsAsync()
        => await http.GetFromJsonAsync<SkillInfo[]>("api/v1/skills") ?? [];

    public async Task ToggleSkillAsync(string id, bool enabled)
    {
        var action = enabled ? "enable" : "disable";
        var response = await http.PostAsync(
            new Uri($"api/v1/skills/{Uri.EscapeDataString(id)}/{action}", UriKind.Relative), null);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateSkillConfigAsync(string id, Dictionary<string, string> config)
    {
        var response = await http.PutAsJsonAsync(
            new Uri($"api/v1/skills/{Uri.EscapeDataString(id)}/config", UriKind.Relative), config);
        response.EnsureSuccessStatusCode();
    }

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
        SendWithoutContentAsync(HttpMethod.Delete, $"api/plugins/{Uri.EscapeDataString(id)}");

    // Audit / Logs
    public async Task<AuditEvent[]> GetAuditEventsAsync(
        int limit = 100,
        string? action = null,
        string? severity = null,
        string? resource = null)
    {
        var query = new List<string> { $"limit={limit}" };

        if (!string.IsNullOrWhiteSpace(action))
            query.Add($"action={Uri.EscapeDataString(action)}");

        if (!string.IsNullOrWhiteSpace(severity))
            query.Add($"severity={Uri.EscapeDataString(severity)}");

        if (!string.IsNullOrWhiteSpace(resource))
            query.Add($"resource={Uri.EscapeDataString(resource)}");

        var response = await http.GetFromJsonAsync<AuditEventsResponse>($"api/v1/audit/events?{string.Join("&", query)}");
        return response?.Events.Select(MapAuditEvent).ToArray() ?? [];
    }

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
        => SendWithoutContentAsync(HttpMethod.Delete, $"api/v1/gateway/apikeys/{Uri.EscapeDataString(maskedKey)}");

    public async Task<RotateApiKeyResponse> RotateApiKeyAsync(string maskedKey, int? expiryDays = null)
    {
        var request = new { ExpiryDays = expiryDays };
        var response = await http.PostAsJsonAsync($"api/v1/gateway/apikeys/{Uri.EscapeDataString(maskedKey)}/rotate", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RotateApiKeyResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    // Memory / Knowledge
    public async Task<MemorySearchResult[]> SearchMemoryAsync(MemorySearchRequest request)
    {
        var response = await http.PostAsJsonAsync("api/v1/memory/search", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MemorySearchResult[]>() ?? [];
    }

    public async Task<MemoryIndexResponse> IndexMemoryAsync(MemoryIndexRequest request)
    {
        var response = await http.PostAsJsonAsync("api/v1/memory/index", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MemoryIndexResponse>()
            ?? new MemoryIndexResponse();
    }

    public async Task<MemoryStats> GetMemoryStatsAsync()
        => await http.GetFromJsonAsync<MemoryStats>("api/v1/memory/stats") ?? new MemoryStats();

    private static AuditEvent MapAuditEvent(GatewayAuditEvent response) =>
        new()
        {
            Id = response.EventId,
            Timestamp = response.Timestamp,
            Level = NormalizeSeverity(response.Severity),
            Source = response.Resource
                ?? response.SessionId
                ?? response.UserId
                ?? response.ToolName
                ?? string.Empty,
            EventType = response.Action,
            Message = response.Detail
                ?? response.ToolResult
                ?? response.Resource
                ?? response.Action,
            Payload = JsonSerializer.Serialize(response, AuditPayloadJsonOptions),
        };

    private static string NormalizeSeverity(JsonElement severity) =>
        severity.ValueKind switch
        {
            JsonValueKind.String => severity.GetString()?.Trim().ToLowerInvariant() switch
            {
                "warn" => "warning",
                { Length: > 0 } text => text,
                _ => "info",
            },
            JsonValueKind.Number when severity.TryGetInt32(out var value) => value switch
            {
                0 => "debug",
                1 => "info",
                2 => "warning",
                3 => "error",
                4 => "critical",
                _ => "info",
            },
            _ => "info",
        };

    // Config
    public Task<ConfigSchema?> GetConfigSchemaAsync() =>
        http.GetFromJsonAsync<ConfigSchema>("api/config/schema");

    public Task<System.Text.Json.JsonDocument?> GetCurrentConfigAsync() =>
        http.GetFromJsonAsync<System.Text.Json.JsonDocument>("api/config/current");

    public async Task SaveConfigAsync(object config)
    {
        var response = await http.PostAsJsonAsync("api/config/save", config);
        response.EnsureSuccessStatusCode();
    }

    public async Task ApplyConfigAsync(object config)
    {
        var response = await http.PostAsJsonAsync("api/config/apply", config);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetConfigAsync()
    {
        var response = await http.PostAsync(new Uri("api/config/reset", UriKind.Relative), null);
        response.EnsureSuccessStatusCode();
    }

    private async Task SendWithoutContentAsync(HttpMethod method, string relativeUri)
    {
        using var request = new HttpRequestMessage(method, new Uri(relativeUri, UriKind.Relative));
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private sealed class AuditEventsResponse
    {
        public int TotalCount { get; init; }
        public int Count { get; init; }
        public GatewayAuditEvent[] Events { get; init; } = [];
    }

    private sealed class GatewayAuditEvent
    {
        public string EventId { get; init; } = string.Empty;
        public DateTimeOffset Timestamp { get; init; }
        public string? UserId { get; init; }
        public string? SessionId { get; init; }
        public string? TraceId { get; init; }
        public string Action { get; init; } = string.Empty;
        public string? Resource { get; init; }
        public string? Detail { get; init; }
        public JsonElement Severity { get; init; }
        public JsonElement? PolicyResult { get; init; }
        public string? ToolName { get; init; }
        public string? ToolArguments { get; init; }
        public string? ToolResult { get; init; }
        public long? DurationMs { get; init; }
        public string? PreviousHash { get; init; }
        public string? TenantId { get; init; }
    }

    private sealed record CreatedAgentResponse(string Id);
}
