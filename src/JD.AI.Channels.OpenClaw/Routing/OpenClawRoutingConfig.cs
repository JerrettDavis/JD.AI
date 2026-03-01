namespace JD.AI.Channels.OpenClaw.Routing;

/// <summary>
/// Determines how messages from an OpenClaw channel are handled by JD.AI.
/// </summary>
public enum OpenClawRoutingMode
{
    /// <summary>JD.AI observes events for logging/analytics but never responds.</summary>
    Passthrough,

    /// <summary>JD.AI hijacks the session, suppresses OpenClaw's agent, and responds instead.</summary>
    Intercept,

    /// <summary>Dedicated session with no OpenClaw agent — JD.AI is the sole backend.</summary>
    Proxy,

    /// <summary>Both systems run; JD.AI only responds to messages matching a trigger pattern.</summary>
    Sidecar,
}

/// <summary>Top-level routing configuration for the OpenClaw bridge.</summary>
public sealed class OpenClawRoutingConfig
{
    /// <summary>Default routing mode for channels without explicit configuration.</summary>
    public OpenClawRoutingMode DefaultMode { get; set; } = OpenClawRoutingMode.Passthrough;

    /// <summary>Whether to connect to OpenClaw automatically on startup.</summary>
    public bool AutoConnect { get; set; } = true;

    /// <summary>Per-channel routing overrides keyed by OpenClaw channel name (e.g., "discord", "signal").</summary>
    public Dictionary<string, OpenClawChannelRouteConfig> Channels { get; set; } = new();

    /// <summary>Named agent profiles that channels can reference.</summary>
    public Dictionary<string, OpenClawAgentProfileConfig> AgentProfiles { get; set; } = new();
}

/// <summary>Routing configuration for a specific OpenClaw channel.</summary>
public sealed class OpenClawChannelRouteConfig
{
    /// <summary>Routing mode for this channel.</summary>
    public OpenClawRoutingMode Mode { get; set; } = OpenClawRoutingMode.Passthrough;

    /// <summary>Name of the agent profile to use (references <see cref="OpenClawRoutingConfig.AgentProfiles"/>).</summary>
    public string AgentProfile { get; set; } = "default";

    /// <summary>Optional system prompt override for the agent.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>Command prefix that triggers JD.AI in Sidecar mode (e.g., "/jdai").</summary>
    public string? CommandPrefix { get; set; }

    /// <summary>Regex pattern that triggers JD.AI in Sidecar mode.</summary>
    public string? TriggerPattern { get; set; }
}

/// <summary>Defines an agent configuration profile for processing routed messages.</summary>
public sealed class OpenClawAgentProfileConfig
{
    /// <summary>AI provider name (e.g., "claude-code", "copilot", "ollama").</summary>
    public string Provider { get; set; } = "claude-code";

    /// <summary>Model identifier (e.g., "claude-sonnet-4-5").</summary>
    public string Model { get; set; } = "claude-sonnet-4-5";

    /// <summary>Maximum conversation turns before resetting.</summary>
    public int MaxTurns { get; set; } = 50;

    /// <summary>System prompt for the agent.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>Tool categories enabled for this agent (e.g., "file", "web", "shell").</summary>
    public List<string> Tools { get; set; } = ["file", "web", "shell"];
}
