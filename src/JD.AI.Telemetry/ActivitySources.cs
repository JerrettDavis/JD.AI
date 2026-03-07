using System.Diagnostics;

namespace JD.AI.Telemetry;

/// <summary>
/// Central <see cref="ActivitySource"/> definitions for JD.AI distributed tracing.
/// All instrumentation in JD.AI uses these named sources so that a single
/// <c>AddSource(ActivitySources.AllSourceNames)</c> call registers them all.
/// </summary>
public static class ActivitySources
{
    /// <summary>Source name for agent-level spans (turns, spawning, compaction).</summary>
    public const string AgentSourceName = "JD.AI.Agent";

    /// <summary>Source name for tool-invocation spans.</summary>
    public const string ToolsSourceName = "JD.AI.Tools";

    /// <summary>Source name for provider API call spans.</summary>
    public const string ProvidersSourceName = "JD.AI.Providers";

    /// <summary>Source name for session-persistence spans.</summary>
    public const string SessionsSourceName = "JD.AI.Sessions";

    /// <summary>Traces agent turns, spawning, and lifecycle operations.</summary>
    public static readonly ActivitySource Agent = new(AgentSourceName);

    /// <summary>Traces individual tool invocations.</summary>
    public static readonly ActivitySource Tools = new(ToolsSourceName);

    /// <summary>Traces provider API calls and retries.</summary>
    public static readonly ActivitySource Providers = new(ProvidersSourceName);

    /// <summary>Traces session persistence operations.</summary>
    public static readonly ActivitySource Sessions = new(SessionsSourceName);

    /// <summary>All source names — pass to <c>.AddSource()</c> when configuring OTel.</summary>
    public static readonly string[] AllSourceNames =
        [AgentSourceName, ToolsSourceName, ProvidersSourceName, SessionsSourceName];
}
