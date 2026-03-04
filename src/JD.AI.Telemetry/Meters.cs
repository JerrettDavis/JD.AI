using System.Diagnostics.Metrics;

namespace JD.AI.Telemetry;

/// <summary>
/// Central <see cref="Meter"/> and instrument definitions for JD.AI metrics.
/// All counters and histograms follow the <c>jdai.*</c> naming convention and
/// use the emerging OpenTelemetry GenAI semantic conventions where applicable.
/// </summary>
public static class Meters
{
    /// <summary>Meter name for agent-level metrics.</summary>
    public const string AgentMeterName = "JD.AI.Agent";

    private static readonly Meter AgentMeter = new(AgentMeterName);

    /// <summary>Total number of agent turns completed.</summary>
    public static readonly Counter<long> TurnCount =
        AgentMeter.CreateCounter<long>(
            "jdai.agent.turns",
            unit: "turns",
            description: "Total number of agent turns completed.");

    /// <summary>Wall-clock duration of each agent turn in milliseconds.</summary>
    public static readonly Histogram<double> TurnDuration =
        AgentMeter.CreateHistogram<double>(
            "jdai.agent.turn_duration",
            unit: "ms",
            description: "Wall-clock duration of each agent turn.");

    /// <summary>Total tokens consumed across all providers (prompt + completion).</summary>
    public static readonly Counter<long> TokensUsed =
        AgentMeter.CreateCounter<long>(
            "jdai.tokens.total",
            unit: "tokens",
            description: "Total tokens consumed across all providers.");

    /// <summary>Number of tool invocations, broken down by tool name.</summary>
    public static readonly Counter<long> ToolCalls =
        AgentMeter.CreateCounter<long>(
            "jdai.tools.invocations",
            unit: "calls",
            description: "Number of tool invocations.");

    /// <summary>Number of provider errors (non-transient or after exhausting retries).</summary>
    public static readonly Counter<long> ProviderErrors =
        AgentMeter.CreateCounter<long>(
            "jdai.providers.errors",
            unit: "errors",
            description: "Number of provider errors after retry exhaustion.");

    /// <summary>Provider API call latency in milliseconds.</summary>
    public static readonly Histogram<double> ProviderLatency =
        AgentMeter.CreateHistogram<double>(
            "jdai.providers.latency",
            unit: "ms",
            description: "Provider API call latency.");

    /// <summary>All meter names — pass to <c>.AddMeter()</c> when configuring OTel.</summary>
    public static string[] AllMeterNames => [AgentMeterName];
}
