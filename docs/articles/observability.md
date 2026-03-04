---
description: "OpenTelemetry tracing, metrics, health checks, and the /doctor command for JD.AI Gateway observability."
---

# Observability

JD.AI Gateway ships with built-in observability via OpenTelemetry distributed tracing and metrics, ASP.NET Core health checks, and a human-readable `/doctor` diagnostic command — all zero-config by default.

## Quick start

By default the gateway writes traces and metrics to stdout (console exporter). No additional infrastructure is required to get started.

```bash
# Start the gateway — telemetry is on by default
dotnet run --project src/JD.AI.Gateway

# Check health
curl http://localhost:18789/health
# {"status":"Healthy","description":"Gateway operational","data":{"activeAgents":0,"uptime":"00:00:12"}}

# Run the diagnostic command (in a connected channel)
/doctor
```

## OpenTelemetry

JD.AI uses the standard .NET `System.Diagnostics.ActivitySource` (traces) and `System.Diagnostics.Metrics` (metrics) APIs, wired into OpenTelemetry through the `JD.AI.Telemetry` library.

### Distributed tracing

Four named activity sources are registered automatically:

| Source | Spans emitted |
|--------|--------------|
| `JD.AI.Agent` | `jdai.agent.turn` — one span per conversational turn; attributes: `gen_ai.system`, `gen_ai.request.model`, `jdai.turn.index`, `jdai.agent.turn_count` |
| `JD.AI.Tools` | Tool invocations |
| `JD.AI.Providers` | `jdai.provider.chat_completion` — one span per provider API call; attributes include retry attempt number |
| `JD.AI.Sessions` | Session persistence operations |

Span status semantics:

- `Ok` — operation completed successfully
- `Unset` — operation was cancelled (client disconnect, graceful shutdown); **not** counted as an error
- `Error` — unexpected exception; always accompanied by a non-zero `jdai.providers.errors` increment

### Metrics

All instruments are in the `JD.AI.Agent` meter under the `jdai.*` namespace.

| Instrument | Kind | Unit | Description |
|---|---|---|---|
| `jdai.agent.turns` | Counter | turns | Total agent turns completed |
| `jdai.agent.turn_duration` | Histogram | ms | Wall-clock time per turn |
| `jdai.tokens.total` | Counter | tokens | Prompt + completion tokens consumed |
| `jdai.tools.invocations` | Counter | calls | Tool invocations, tagged by tool name |
| `jdai.providers.errors` | Counter | errors | Errors after retry exhaustion (cancellations excluded) |
| `jdai.providers.latency` | Histogram | ms | Per-provider API call latency |

All counters carry a `gen_ai.system` tag set to the provider name (e.g. `claude-code`, `github-copilot`, `ollama`), following the [OpenTelemetry GenAI semantic conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/).

### Configuration

#### `appsettings.json`

Telemetry is configured under `Gateway:Telemetry`:

```json
{
  "Gateway": {
    "Telemetry": {
      "Enabled": true,
      "ServiceName": "jdai",
      "Exporter": "console",
      "Endpoint": null
    }
  }
}
```

| Property | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `true` | Set `false` to disable all OTel instrumentation |
| `ServiceName` | `string` | `"jdai"` | Logical service name in traces and metrics. Overridden by `OTEL_SERVICE_NAME` if set. |
| `Exporter` | `string` | `"console"` | See exporter table below |
| `Endpoint` | `string?` | `null` | Exporter endpoint URI; uses exporter default if absent or invalid |

#### Exporters

| `Exporter` value | Traces | Metrics | Notes |
|---|---|---|---|
| `"console"` | ✔ stdout | ✔ stdout | Default; useful for development |
| `"otlp"` | ✔ OTLP/gRPC | ✔ OTLP/gRPC | Connects to Jaeger, Grafana, Honeycomb, etc. |
| `"zipkin"` | ✔ Zipkin HTTP | ✔ console | Zipkin does not support metrics; metrics fall back to console |

#### Environment variables

Standard OpenTelemetry environment variables take precedence over `appsettings.json`:

| Variable | Effect |
|---|---|
| `OTEL_SERVICE_NAME` | Overrides `Gateway:Telemetry:ServiceName` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Activates OTLP mode and sets the endpoint (overrides `Exporter` and `Endpoint`) |

### Sending to Jaeger (OTLP)

```bash
# Start Jaeger all-in-one
docker run -d --name jaeger \
  -p 4317:4317 \
  -p 16686:16686 \
  jaegertracing/all-in-one:latest

# Point JD.AI at it
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
dotnet run --project src/JD.AI.Gateway
```

Open the Jaeger UI at `http://localhost:16686` and search for service `jdai`.

### Sending to Zipkin

```json
{
  "Gateway": {
    "Telemetry": {
      "Exporter": "zipkin",
      "Endpoint": "http://localhost:9411/api/v2/spans"
    }
  }
}
```

> [!NOTE]
> Zipkin does not support the OpenTelemetry metrics protocol. When `Exporter` is `"zipkin"`, metrics continue to be written to the console exporter.

## Health checks

The gateway runs four health checks automatically:

| Check | Tag | Failure status | Condition |
|---|---|---|---|
| `gateway` | — | Degraded | Gateway service not operational |
| `providers` | `providers` | Degraded | No AI providers are reachable |
| `session_store` | `storage` | Unhealthy | SQLite database inaccessible or `sessions` table missing |
| `disk_space` | `storage` | Degraded | Less than 100 MB free in the data directory (configurable) |
| `memory` | `memory` | Degraded | Managed heap exceeds 1 GB (configurable) |

The `disk_space` and `memory` thresholds are the defaults. Both checks accept a constructor parameter to customize the threshold — see [Registering custom health checks](#registering-custom-health-checks) for an example.

### Health endpoints

| Endpoint | Description | Status codes |
|---|---|---|
| `GET /health` | All checks — full JSON report | `200` always |
| `GET /health/ready` | Readiness probe — `200` when Healthy or Degraded, `503` when Unhealthy | `200` / `503` |
| `GET /health/live` | Liveness probe — always `200` while the process is running | `200` |

#### Full health response example

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0423180",
  "entries": {
    "gateway": {
      "status": "Healthy",
      "description": "Gateway operational",
      "data": { "activeAgents": 2, "uptime": "00:14:22" }
    },
    "providers": {
      "status": "Healthy",
      "description": "2/3 providers reachable",
      "data": {
        "available": ["claude-code", "github-copilot"],
        "unavailable": ["ollama"],
        "availableCount": 2,
        "totalCount": 3
      }
    },
    "session_store": {
      "status": "Healthy",
      "description": "SQLite OK (14 sessions)",
      "data": { "sessionCount": 14, "dbPath": "/home/user/.jdai/sessions.db" }
    },
    "disk_space": {
      "status": "Healthy",
      "description": "98.4 GB free",
      "data": { "freeSpaceBytes": 105693839360, "freeSpaceGb": 98.4, "directory": "/home/user/.jdai" }
    },
    "memory": {
      "status": "Healthy",
      "description": "142 MB managed heap",
      "data": { "allocatedBytes": 148938752, "allocatedMb": 142.0 }
    }
  }
}
```

#### Kubernetes probe configuration

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 18789
  initialDelaySeconds: 5
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /health/ready
    port: 18789
  initialDelaySeconds: 10
  periodSeconds: 15
```

## `/doctor` command

The `/doctor` gateway command runs all registered health checks and renders a human-readable diagnostic report in the connected channel.

```text
=== JD.AI Doctor ===
Version:  1.0.0
Runtime:  .NET 10.0.0
Health:   ✔ Healthy

Checks:
  ✔ Gateway      — Gateway operational
  ✔ Providers    — 2/3 providers reachable
  ⚠ Disk Space   — Low disk space: 0.4 GB free (minimum: 100 MB)
  ✔ Memory       — 142 MB managed heap
  ✔ Session Store — SQLite OK (14 sessions)
```

Status icons:

| Icon | Meaning |
|------|---------|
| `✔` | Healthy |
| `⚠` | Degraded — gateway is operational but running with reduced capability |
| `✘` | Unhealthy — a critical dependency is unavailable |

## Registering custom health checks

Call `AddJdAiHealthChecks()` and chain additional checks with the standard ASP.NET Core builder:

```csharp
builder.Services.AddJdAiHealthChecks()
    .AddCheck<MyCustomCheck>("my-check", tags: ["custom"]);
```

To add the built-in checks without the gateway-specific `GatewayHealthCheck`, call the underlying extension directly:

```csharp
services.AddJdAiHealthChecks(); // registers providers, session_store, disk_space, memory
```

## See also

- [Gateway API Reference](gateway-api.md) — health endpoint details and REST API
- [Configuration](configuration.md) — full environment variable reference
- [Service Deployment](service-deployment.md) — running the gateway as a system service
