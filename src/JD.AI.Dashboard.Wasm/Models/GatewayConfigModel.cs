namespace JD.AI.Dashboard.Wasm.Models;

/// <summary>Full gateway config matching Gateway:Config section shape.</summary>
public sealed class GatewayConfigModel
{
    public ServerConfigModel Server { get; set; } = new();
    public AuthConfigModel Auth { get; set; } = new();
    public RateLimitConfigModel RateLimit { get; set; } = new();
    public List<ProviderConfigModel> Providers { get; set; } = [];
    public List<AgentDefinition> Agents { get; set; } = [];
    public List<ChannelConfigModel> Channels { get; set; } = [];
    public RoutingConfigModel Routing { get; set; } = new();
    public OpenClawConfigModel OpenClaw { get; set; } = new();
}

public sealed class ServerConfigModel
{
    public int Port { get; set; } = 15790;
    public string Host { get; set; } = "localhost";
    public bool Verbose { get; set; }
}

public sealed class AuthConfigModel
{
    public bool Enabled { get; set; }
    public List<ApiKeyEntryModel> ApiKeys { get; set; } = [];
}

public sealed class ApiKeyEntryModel
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "User";
}

public sealed class RateLimitConfigModel
{
    public bool Enabled { get; set; } = true;
    public int MaxRequestsPerMinute { get; set; } = 60;
}

public sealed class ProviderConfigModel
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public Dictionary<string, string> Settings { get; set; } = [];
}

public sealed class ChannelConfigModel
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool AutoConnect { get; set; }
    public Dictionary<string, string> Settings { get; set; } = [];
}

public sealed class RoutingConfigModel
{
    public string DefaultAgentId { get; set; } = "";
    public List<RoutingRuleModel> Rules { get; set; } = [];
}

public sealed class RoutingRuleModel
{
    public string ChannelType { get; set; } = "";
    public string AgentId { get; set; } = "";
    public string? ConversationPattern { get; set; }
}

public sealed class OpenClawConfigModel
{
    public bool Enabled { get; set; }
    public string WebSocketUrl { get; set; } = "ws://127.0.0.1:18789/ws/gateway";
    public bool AutoConnect { get; set; } = true;
    public string DefaultMode { get; set; } = "Passthrough";
    public Dictionary<string, OpenClawChannelConfigModel> Channels { get; set; } = [];
    public List<OpenClawAgentRegistrationModel> RegisterAgents { get; set; } = [];
}

public sealed class OpenClawChannelConfigModel
{
    public string Mode { get; set; } = "Passthrough";
    public string? AgentId { get; set; }
    public string? CommandPrefix { get; set; }
    public string? TriggerPattern { get; set; }
    public string? SystemPrompt { get; set; }
}

public sealed class OpenClawAgentRegistrationModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Emoji { get; set; } = "🤖";
    public string Theme { get; set; } = "JD.AI agent";
    public string? Model { get; set; }
    public string? GatewayAgentId { get; set; }
    public List<OpenClawBindingModel> Bindings { get; set; } = [];
}

public sealed class OpenClawBindingModel
{
    public string Channel { get; set; } = "";
    public string? AccountId { get; set; }
    public string? PeerKind { get; set; }
    public string? PeerId { get; set; }
    public string? GuildId { get; set; }
}
