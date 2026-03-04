---
title: Observability
description: "OpenTelemetry tracing, metrics, health checks, Kubernetes probes, Prometheus/Grafana integration, and the /doctor command for JD.AI Gateway."
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

# Run the diagnostic command (in a connected channel)
/doctor
```

## OpenTelemetry Integration

JD.AI uses the standard .NET `System.Diagnostics.ActivitySource` (traces) and `System.Diagnostics.Metrics` (metrics) APIs, wired into OpenTelemetry through the `JD.AI.Telemetry` library.

### ActivitySource names

Four named activity sources are registered automatically:

| Source | Spans emitted |
|--------|--------------|
| `JD.AI.Agent` | `jdai.agent.turn` — one span per conversational turn; attributes: `gen_ai.system`, `gen_ai.request.model`, `jdai.turn.index`, `jdai.agent.turn_count` |
| `JD.AI.Tools` | Tool invocations |
| `JD.AI.Providers` | `jdai.provider.chat_completion` — one span per provider API call; attributes include retry attempt number |
| `JD.AI.Sessions` | Session persistence operations |

### Span status semantics

- **Ok** — operation completed successfully
- **Unset** — operation was cancelled (client disconnect, graceful shutdown); **not** counted as an error
- **Error** — unexpected exception; always accompanied by a non-zero `jdai.providers.errors` increment

### Meter names and metrics

All instruments are in the `JD.AI.Agent` meter under the `jdai.*` namespace:

| Instrument | Kind | Unit | Description |
|---|---|---|---|
| `jdai.agent.turns` | Counter | turns | Total agent turns completed |
| `jdai.agent.turn_duration` | Histogram | ms | Wall-clock time per turn |
| `jdai.tokens.total` | Counter | tokens | Prompt + completion tokens consumed |
| `jdai.tools.invocations` | Counter | calls | Tool invocations, tagged by tool name |
| `jdai.providers.errors` | Counter | errors | Errors after retry exhaustion (cancellations excluded) |
| `jdai.providers.latency` | Histogram | ms | Per-provider API call latency |

All counters carry a `gen_ai.system` tag set to the provider name (e.g. `claude-code`, `github-copilot`, `ollama`), following the [OpenTelemetry GenAI semantic conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/).

## Telemetry Configuration

### appsettings.json

Telemetry is configured under `Gateway:Telemetry`:

```json
{
  "Gateway": {
    "Telemetry": {
      "Enabled": true,
      "ServiceName": "jdai",
      "Exporter": "console",
      "OtlpProtocol": "grpc",
      "Endpoint": null
    }
  }
}
```

| Property | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `true` | Set `false` to disable all OTel instrumentation |
| `ServiceName` | `string` | `"jdai"` | Logical service name in traces and metrics |
| `Exporter` | `string` | `"console"` | Exporter type (see table below) |
| `OtlpProtocol` | `string` | `"grpc"` | OTLP transport: `"grpc"` (port 4317) or `"http"` (port 4318) |
| `Endpoint` | `string?` | `null` | Exporter endpoint URI; uses exporter default if absent |

### Exporters

| `Exporter` value | Traces | Metrics | Notes |
|---|---|---|---|
| `"console"` | ✔ stdout | ✔ stdout | Default; useful for development |
| `"otlp"` | ✔ OTLP | ✔ OTLP | Connects to Jaeger, Grafana, Honeycomb, etc. |
| `"zipkin"` | ✔ Zipkin HTTP | ✔ console | Zipkin does not support metrics |

### Environment variables

Standard OpenTelemetry environment variables take precedence over `appsettings.json`:

| Variable | Effect |
|---|---|
| `OTEL_SERVICE_NAME` | Overrides `Gateway:Telemetry:ServiceName` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Activates OTLP mode and sets the endpoint |

## Sending to Jaeger (OTLP)

```bash
# Start Jaeger all-in-one
docker run -d --name jaeger \
  -p 4317:4317 \
  -p 4318:4318 \
  -p 16686:16686 \
  jaegertracing/all-in-one:latest

# Start gateway with OTLP export
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
dotnet run --project src/JD.AI.Gateway
```

Open the Jaeger UI at `http://localhost:16686` and search for service `jdai`.

## Prometheus / Grafana Integration

Export metrics to Prometheus using the OTLP exporter with an OpenTelemetry Collector:

```yaml
# otel-collector-config.yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317

exporters:
  prometheus:
    endpoint: 0.0.0.0:8889

service:
  pipelines:
    metrics:
      receivers: [otlp]
      exporters: [prometheus]
```

Configure Prometheus to scrape the collector:

```yaml
# prometheus.yml
scrape_configs:
  - job_name: jdai
    static_configs:
      - targets: ["otel-collector:8889"]
```

In Grafana, add Prometheus as a data source and query JD.AI metrics:

- `jdai_agent_turns_total` — total agent turns
- `jdai_tokens_total` — token consumption over time
- `jdai_providers_latency_bucket` — provider latency distribution
- `jdai_providers_errors_total` — error rate by provider

## Health Check Endpoints

The gateway runs health checks automatically:

| Check | Tag | Failure status | Condition |
|---|---|---|---|
| `gateway` | — | Degraded | Gateway service not operational |
| `providers` | `providers` | Degraded | No AI providers are reachable |
| `session_store` | `storage` | Unhealthy | SQLite database inaccessible |
| `disk_space` | `storage` | Degraded | Less than 100 MB free in data directory |
| `memory` | `memory` | Degraded | Managed heap exceeds 1 GB |

### Endpoints

| Endpoint | Description | Status codes |
|---|---|---|
| `GET /health` | All checks — full JSON report | `200` always |
| `GET /health/ready` | Readiness probe — `200` for Healthy/Degraded, `503` for Unhealthy | `200` / `503` |
| `GET /health/live` | Liveness probe — always `200` while process is running | `200` |

### Full health response example

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
      "data": { "available": ["claude-code", "github-copilot"], "unavailable": ["ollama"] }
    },
    "session_store": {
      "status": "Healthy",
      "description": "SQLite OK (14 sessions)",
      "data": { "sessionCount": 14, "dbPath": "/home/user/.jdai/sessions.db" }
    }
  }
}
```

## Kubernetes Probes

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

## /doctor Command

The `/doctor` gateway command runs all registered health checks and renders a human-readable diagnostic report in the connected channel:

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

| Icon | Meaning |
|------|---------|
| `✔` | Healthy |
| `⚠` | Degraded — gateway is operational but running with reduced capability |
| `✘` | Unhealthy — a critical dependency is unavailable |

## Log Configuration

Configure logging in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "JD.AI": "Information",
      "JD.AI.Providers": "Debug"
    }
  },
  "Gateway": {
    "Server": {
      "Verbose": false
    }
  }
}
```

Set `Gateway:Server:Verbose` to `true` for detailed request logging. Use structured logging sinks (Serilog, Seq, etc.) for production environments.

## See also

- [Service Deployment](deployment.md) — running the gateway as a system service
- [Gateway Administration](gateway-admin.md) — monitoring and scaling
- [Gateway API Reference](../developer-guide/gateway-api.md) — REST API and health endpoint details
