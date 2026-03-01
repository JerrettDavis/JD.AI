namespace JD.AI.Gateway.Config;

public sealed class GatewayConfig
{
    public ServerConfig Server { get; set; } = new();
    public AuthConfig Auth { get; set; } = new();
    public RateLimitConfig RateLimit { get; set; } = new();
    public List<ChannelConfig> Channels { get; set; } = [];
    public List<ProviderConfig> Providers { get; set; } = [];
    public List<AgentDefinition> Agents { get; set; } = [];
    public RoutingConfig Routing { get; set; } = new();
    public OpenClawGatewayConfig OpenClaw { get; set; } = new();
}

public sealed class ServerConfig
{
    public int Port { get; set; } = 18789;
    public string Host { get; set; } = "localhost";
    public bool Verbose { get; set; }
}

public sealed class AuthConfig
{
    public bool Enabled { get; set; }
    public List<ApiKeyEntry> ApiKeys { get; set; } = [];
}

public sealed class ApiKeyEntry
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "User";
}

public sealed class RateLimitConfig
{
    public bool Enabled { get; set; } = true;
    public int MaxRequestsPerMinute { get; set; } = 60;
}

public sealed class ChannelConfig
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool AutoConnect { get; set; }
    public Dictionary<string, string> Settings { get; set; } = [];
}

public sealed class ProviderConfig
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public Dictionary<string, string> Settings { get; set; } = [];
}

/// <summary>An agent to auto-spawn on gateway startup.</summary>
public sealed class AgentDefinition
{
    public string Id { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string? SystemPrompt { get; set; }
    public bool AutoSpawn { get; set; } = true;
    public int MaxTurns { get; set; }
    public List<string> Tools { get; set; } = [];
}

/// <summary>Routing rules that map channels to agents.</summary>
public sealed class RoutingConfig
{
    public List<RoutingRule> Rules { get; set; } = [];
    public string DefaultAgentId { get; set; } = "";
}

/// <summary>A single channel-to-agent routing rule.</summary>
public sealed class RoutingRule
{
    public string ChannelType { get; set; } = "";
    public string AgentId { get; set; } = "";
    public string? ConversationPattern { get; set; }
}

/// <summary>OpenClaw-specific gateway configuration.</summary>
public sealed class OpenClawGatewayConfig
{
    public bool Enabled { get; set; }
    public string WebSocketUrl { get; set; } = "ws://127.0.0.1:18789/ws/gateway";
    public bool AutoConnect { get; set; } = true;
    public string DefaultMode { get; set; } = "Passthrough";
    public Dictionary<string, OpenClawChannelConfig> Channels { get; set; } = [];
}

/// <summary>Per-OpenClaw-channel routing config (maps to OpenClawChannelRouteConfig).</summary>
public sealed class OpenClawChannelConfig
{
    public string Mode { get; set; } = "Passthrough";
    public string? AgentId { get; set; }
    public string? CommandPrefix { get; set; }
    public string? TriggerPattern { get; set; }
    public string? SystemPrompt { get; set; }
}
