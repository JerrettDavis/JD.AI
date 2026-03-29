using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace JD.AI.Core.Tracing;

/// <summary>
/// Internal instrumentation statics for AgentLoop. Uses the same ActivitySource and Meter
/// names as <c>JD.AI.Telemetry.ActivitySources</c> and <c>JD.AI.Telemetry.Meters</c> so
/// that all spans and metrics flow through the single OTel pipeline configured by the host.
/// Avoids a circular project reference (JD.AI.Telemetry already references JD.AI.Core).
/// </summary>
internal static class AgentInstrumentation
{
    // Must match JD.AI.Telemetry.ActivitySources.AgentSourceName
    internal static readonly ActivitySource AgentSource = new("JD.AI.Agent");

    private static readonly Meter AgentMeter = new("JD.AI.Agent");

    // Must match JD.AI.Telemetry.Meters instrument names
    internal static readonly Counter<long> TurnCount =
        AgentMeter.CreateCounter<long>("jdai.agent.turns", unit: "turns");

    internal static readonly Histogram<double> TurnDuration =
        AgentMeter.CreateHistogram<double>("jdai.agent.turn_duration", unit: "ms");

    internal static readonly Counter<long> TokensUsed =
        AgentMeter.CreateCounter<long>("jdai.tokens.total", unit: "tokens");

    internal static readonly Counter<long> ProviderErrors =
        AgentMeter.CreateCounter<long>("jdai.providers.errors", unit: "errors");

    internal static readonly Counter<long> ToolCalls =
        AgentMeter.CreateCounter<long>("jdai.tool.calls", unit: "calls");

    internal static readonly Counter<long> CircuitBreakerTrips =
        AgentMeter.CreateCounter<long>("jdai.safety.circuit_breaker_trips", unit: "trips");

    internal static readonly Counter<long> LoopDetections =
        AgentMeter.CreateCounter<long>("jdai.safety.loop_detections", unit: "detections");

    // GenAI semantic convention attribute names (mirrors JD.AI.Telemetry.GenAiAttributes)
    internal const string AttrSystem = "gen_ai.system";
    internal const string AttrRequestModel = "gen_ai.request.model";
    internal const string AttrRequestMaxTokens = "gen_ai.request.max_tokens";
    internal const string AttrOperationName = "gen_ai.operation.name";
    internal const string AttrUsageOutputTokens = "gen_ai.usage.output_tokens";
}
