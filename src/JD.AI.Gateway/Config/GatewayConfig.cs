#pragma warning disable CA2227 // Collection properties should be read only — needed for IOptions binding

namespace JD.AI.Gateway.Config;

public sealed class GatewayConfig
{
    public ServerConfig Server { get; set; } = new();
    public AuthConfig Auth { get; set; } = new();
    public RateLimitConfig RateLimit { get; set; } = new();
    public IList<ChannelConfig> Channels { get; set; } = [];
    public IList<ProviderConfig> Providers { get; set; } = [];
    public IList<AgentDefinition> Agents { get; set; } = [];
    public RoutingConfig Routing { get; set; } = new();
    public OpenClawGatewayConfig OpenClaw { get; set; } = new();
    public TelemetryGatewayConfig Telemetry { get; set; } = new();
    public EventBusConfig EventBus { get; set; } = new();
}

/// <summary>Event bus infrastructure settings nested under <c>Gateway:EventBus</c>.</summary>
public sealed class EventBusConfig
{
    /// <summary>Provider type: <c>"InProcess"</c> (default) or <c>"Redis"</c>.</summary>
    public string Provider { get; set; } = "InProcess";

    /// <summary>Redis connection string. Required when <see cref="Provider"/> is <c>"Redis"</c>.</summary>
    public string? RedisConnectionString { get; set; }
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
    public IList<ApiKeyEntry> ApiKeys { get; set; } = [];
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
    public IDictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
}

public sealed class ProviderConfig
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public IDictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
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
    public IList<string> Tools { get; set; } = [];
    public ModelParameters Parameters { get; set; } = new();

    /// <summary>
    /// Ordered list of fallback providers to try when the primary provider is unavailable.
    /// Each entry is "provider/model" (e.g. "copilot/claude-sonnet-4-6") or just "provider"
    /// (auto-selects first available model).
    /// </summary>
    public IList<string> FallbackProviders { get; set; } = [];
}

/// <summary>Tunable model inference parameters (Ollama, OpenAI, etc.).</summary>
public sealed class ModelParameters
{
    /// <summary>Sampling temperature (0.0–2.0). Lower = deterministic, higher = creative.</summary>
    public double? Temperature { get; set; }

    /// <summary>Nucleus sampling. Only tokens with cumulative probability ≤ TopP are considered.</summary>
    public double? TopP { get; set; }

    /// <summary>Top-K sampling. Limits token selection to the K most probable tokens.</summary>
    public int? TopK { get; set; }

    /// <summary>Maximum tokens in the generated response.</summary>
    public int? MaxTokens { get; set; }

    /// <summary>Context window size in tokens (Ollama num_ctx). 0 = provider default.</summary>
    public int? ContextWindowSize { get; set; }

    /// <summary>Penalizes tokens that have already appeared. Range: -2.0 to 2.0.</summary>
    public double? FrequencyPenalty { get; set; }

    /// <summary>Penalizes tokens based on whether they appear at all. Range: -2.0 to 2.0.</summary>
    public double? PresencePenalty { get; set; }

    /// <summary>Ollama-specific repeat penalty (1.0 = no penalty).</summary>
    public double? RepeatPenalty { get; set; }

    /// <summary>Random seed for reproducible output. Null = random each time.</summary>
    public int? Seed { get; set; }

    /// <summary>Sequences that cause the model to stop generating further tokens.</summary>
    public IList<string> StopSequences { get; set; } = [];
}

/// <summary>Routing rules that map channels to agents.</summary>
public sealed class RoutingConfig
{
    public IList<RoutingRule> Rules { get; set; } = [];
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
    public IDictionary<string, OpenClawChannelConfig> Channels { get; set; } = new Dictionary<string, OpenClawChannelConfig>();

    /// <summary>JD.AI agents to register with OpenClaw so they appear in its dashboard.</summary>
    public IList<OpenClawAgentRegistration> RegisterAgents { get; set; } = [];
}

/// <summary>Defines a JD.AI agent to register with OpenClaw as a native agent.</summary>
public sealed class OpenClawAgentRegistration
{
    /// <summary>Agent ID in OpenClaw (e.g., "jdai-default").</summary>
    public string Id { get; set; } = "";

    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Emoji identifier.</summary>
    public string Emoji { get; set; } = "🤖";

    /// <summary>Agent theme/persona.</summary>
    public string Theme { get; set; } = "JD.AI agent";

    /// <summary>Model identifier for display (actual execution is via JD.AI).</summary>
    public string? Model { get; set; }

    /// <summary>JD.AI gateway agent ID to route execution to.</summary>
    public string? GatewayAgentId { get; set; }

    /// <summary>Channel bindings in OpenClaw.</summary>
    public IList<OpenClawBindingConfig> Bindings { get; set; } = [];
}

/// <summary>Channel binding for an OpenClaw agent registration.</summary>
public sealed class OpenClawBindingConfig
{
    public string Channel { get; set; } = "";
    public string? AccountId { get; set; }
    public string? PeerKind { get; set; }
    public string? PeerId { get; set; }
    public string? GuildId { get; set; }
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

/// <summary>Telemetry settings nested under <c>Gateway:Telemetry</c> in appsettings.json.</summary>
public sealed class TelemetryGatewayConfig
{
    /// <summary>Whether OTel tracing and metrics are enabled. Defaults to <c>true</c>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Logical service name reported in traces and metrics. Defaults to <c>"jdai"</c>.</summary>
    public string ServiceName { get; set; } = "jdai";

    /// <summary>Exporter type: <c>"console"</c> (default), <c>"otlp"</c>, or <c>"zipkin"</c>.</summary>
    public string Exporter { get; set; } = "console";

    /// <summary>Exporter endpoint URI (e.g. <c>http://localhost:4317</c> for OTLP).</summary>
    public string? Endpoint { get; set; }
}
