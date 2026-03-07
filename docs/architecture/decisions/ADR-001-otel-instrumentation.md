# ADR-001: OpenTelemetry Instrumentation for AgentLoop

## Status

Accepted

## Context

JD.AI provides an enterprise agent runtime platform where observability is a first-class requirement. The codebase already ships the infrastructure for OpenTelemetry (OTel) distributed tracing and metrics:

- `JD.AI.Telemetry/ActivitySources.cs` — defines `ActivitySource` instances for the `JD.AI.Agent`, `JD.AI.Tools`, `JD.AI.Providers`, and `JD.AI.Sessions` instrumentation scopes.
- `JD.AI.Telemetry/Meters.cs` — defines counters and histograms following the emerging [OpenTelemetry GenAI semantic conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/).
- `JD.AI.Telemetry/GenAiAttributes.cs` — defines attribute name constants and extension methods for setting GenAI tags on `Activity` spans.

However, none of this infrastructure was connected to actual execution paths. The `AgentLoop` class (the core LLM interaction loop) used an internal `TraceContext` / timeline mechanism for structured debugging, but emitted no OTel `Activity` spans or metric measurements.

This created a gap: operators deploying JD.AI with an OpenTelemetry collector (OTLP, Zipkin, Jaeger) would see no agent-level spans and no turn metrics, making production observability impossible without custom tooling.

Additionally, Semantic Kernel (SK) ships built-in OTel instrumentation under the `Microsoft.SemanticKernel` activity source name. This source was not registered in `TelemetryServiceExtensions.cs`, so SK's internal spans (connector calls, function invocations) were silently discarded even when telemetry was enabled.

## Decision

Wire the existing OTel infrastructure into the `AgentLoop` execution paths, and register the SK activity source in the telemetry configuration.

### Changes

1. **`JD.AI.Core` → `JD.AI.Telemetry` project reference**  
   Added `<ProjectReference Include="../JD.AI.Telemetry/JD.AI.Telemetry.csproj" />` to `JD.AI.Core.csproj` so `AgentLoop` can reference `ActivitySources`, `Meters`, and `GenAiAttributes` without a circular dependency.

2. **Activity spans in `AgentLoop.RunTurnAsync` and `RunTurnStreamingAsync`**  
   At the start of each method, a `using var turnActivity` is created from `ActivitySources.Agent.StartActivity("agent.turn", ActivityKind.Internal)`. GenAI request attributes (system, model, operation, max_tokens) are set immediately. The `using var` pattern ensures the span is ended automatically when the method exits, regardless of the code path.

   On successful completion, response attributes (output token estimate) and `ActivityStatusCode.Ok` are set. The following metrics are recorded:
   - `Meters.TurnCount` — incremented by 1 with `gen_ai.system` and `gen_ai.request.model` tags.
   - `Meters.TurnDuration` — records wall-clock milliseconds with `gen_ai.system` tag.
   - `Meters.TokensUsed` — records the estimated output token count with `gen_ai.system` tag.

   On terminal errors (general exception catch), `ActivityStatusCode.Error` is set and `Meters.ProviderErrors` is incremented with `gen_ai.system` and `error.type` tags.

3. **`TelemetryServiceExtensions.cs` — SK source registration**  
   Added `.AddSource("Microsoft.SemanticKernel")` alongside the existing `.AddSource(ActivitySources.AllSourceNames)` so SK connector spans are captured by the configured exporter.

### Design choices

- **`using var` for activities** — The auto-dispose pattern is idiomatic for OTel spans in .NET. It ensures spans are always ended even when exceptions propagate, without requiring `finally` blocks or scattered `activity.Stop()` calls.
- **Null-conditional access** — `turnActivity?.SetTag(...)` is used throughout, as `StartActivity` returns `null` when no listener is sampling the source. This is the correct pattern for zero-overhead when telemetry is disabled.
- **Token estimates** — Input token counts are not yet surfaced by the SK `IChatCompletionService` API in a provider-neutral way. Only output token estimates (via `TokenEstimator`) are recorded in this iteration. Input tokens will be added in a future ADR when the SK metadata API stabilizes.
- **Existing `TraceContext` preserved** — The internal timeline/tracing mechanism (`TraceContext`, `turnEntry`) is retained unchanged. OTel spans are added alongside it, not as a replacement. Both systems serve different audiences: the internal timeline feeds the in-process debug view; OTel spans feed external collectors.
- **Error paths** — `Meters.ProviderErrors` is only incremented in the terminal general exception catch, not in the tool-calling retry path (which recovers successfully) or the workflow planning path. This keeps the "errors" metric meaningful.

## Consequences

### Positive

- All production deployments with an OTel collector will now receive `agent.turn` spans with GenAI attributes, enabling distributed trace correlation from user request to LLM response.
- `jdai.agent.turns`, `jdai.agent.turn_duration`, `jdai.tokens.total`, and `jdai.providers.errors` metrics are now emitted, enabling dashboards and alerting on turn rate, latency, token consumption, and error rate.
- SK connector spans (`Microsoft.SemanticKernel`) are captured and correlated with agent turn spans.
- Telemetry is zero-cost when disabled (no OTel listener registered) due to the null-conditional pattern.

### Negative / Trade-offs

- Input token counts are not available yet; the `gen_ai.usage.input_tokens` tag will be empty until SK exposes this through its API.
- The token estimate used for `gen_ai.usage.output_tokens` is a heuristic (character-based approximation), not the provider's reported token count. Accuracy varies by model and language.
- Fallback and retry paths do not emit their own spans; they share the parent `agent.turn` span. Detailed sub-span instrumentation for retries is deferred to a future iteration.
