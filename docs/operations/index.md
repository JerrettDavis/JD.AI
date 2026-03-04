---
title: Operations Overview
description: "Guide to deploying, monitoring, securing, and governing JD.AI in production environments."
---

# Operations Overview

This section covers deploying, monitoring, securing, and governing JD.AI in production environments. Whether you are running a single-instance developer setup or a multi-team enterprise deployment, these guides provide the operational knowledge you need.

## What's covered

### [Service Deployment](deployment.md)

Install and run JD.AI Gateway as a system service. Covers Windows Service installation, Linux systemd configuration, Docker container deployment, reverse proxy setup, auto-update management, and daemon CLI commands.

### [Observability](observability.md)

Monitor JD.AI with OpenTelemetry distributed tracing and metrics. Covers ActivitySource and Meter instrumentation, health check endpoints, Kubernetes probe configuration, Prometheus and Grafana integration, the `/doctor` diagnostic command, and log configuration.

### [Admin Dashboard](dashboard.md)

Manage the gateway through the Blazor WebAssembly dashboard. Covers accessing the UI, real-time session monitoring, agent lifecycle management, channel status, provider configuration, and dashboard authentication.

### [Security & Credentials](security.md)

Secure your JD.AI deployment end-to-end. Covers encrypted credential storage (DPAPI/AES), API key management, session data security, MCP server policies, local model file controls, gateway authentication, network security, and audit logging.

### [Enterprise Governance](governance.md)

Govern JD.AI usage across teams and projects. Covers token consumption tracking, budget limits, policy enforcement (tool allowlists, approved providers, model restrictions), shared workflow management, compliance and audit trails, and multi-tenant workspace isolation.

### [Gateway Administration](gateway-admin.md)

Administer the JD.AI Gateway at scale. Covers configuration management with hot reload, horizontal scaling and load balancing, channel and agent pool management, SignalR hub diagnostics, and backup and disaster recovery procedures.

## Architecture overview

```text
┌──────────────┐   ┌──────────────┐   ┌──────────────┐
│   Channels   │   │   Dashboard  │   │  REST / gRPC  │
│ (Discord,    │   │  (Blazor     │   │   Clients     │
│  Slack, etc.)│   │   WASM)      │   │               │
└──────┬───────┘   └──────┬───────┘   └──────┬───────┘
       │                  │                   │
       └──────────┬───────┴───────────────────┘
                  ▼
       ┌─────────────────────┐
       │    JD.AI Gateway    │  ← Service Deployment
       │  (ASP.NET Core)     │  ← Observability (OTel)
       │                     │  ← Security (Auth, TLS)
       ├─────────────────────┤
       │  Agent Pool Service │  ← Governance (Limits)
       │  Provider Registry  │
       │  Session Store      │
       └─────────┬──────────┘
                 │
       ┌─────────▼──────────┐
       │   AI Providers      │
       │ (Claude, Copilot,   │
       │  Ollama, Local...)  │
       └────────────────────┘
```

## Prerequisites

- **.NET 10.0 SDK or Runtime** — required for all deployment modes
- **`jdai-daemon` tool** — for service installation (`dotnet tool install -g JD.AI.Daemon`)
- **At least one AI provider** — see [Providers](../reference/providers.md) for setup

## Quick health check

Verify a running gateway is healthy:

```bash
# Full health report
curl http://localhost:18789/health

# Readiness probe (returns 503 when unhealthy)
curl -f http://localhost:18789/health/ready

# Run in-channel diagnostics
/doctor
```

## Key directories

| Path | Purpose |
|------|---------|
| `~/.jdai/config.json` | Global default provider and model |
| `~/.jdai/sessions.db` | SQLite session database |
| `~/.jdai/credentials/` | Encrypted credential store |
| `~/.jdai/models/` | Local GGUF models and registry |
| `~/.jdai/exports/` | Exported session JSON files |
| `/etc/systemd/system/jdai-daemon.service` | Linux systemd unit (after install) |

## Next steps

- New to JD.AI? Start with [Service Deployment](deployment.md) to get the gateway running.
- Already deployed? Set up [Observability](observability.md) to monitor your instance.
- Running in production? Review [Security](security.md) and [Governance](governance.md).
- Managing teams? See [Enterprise Governance](governance.md) for usage tracking and policy enforcement.
