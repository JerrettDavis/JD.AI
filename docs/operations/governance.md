---
title: Enterprise Governance
description: "Govern JD.AI usage across teams with usage tracking, budget limits, policy enforcement, shared workflows, compliance controls, and multi-tenant isolation."
---

# Enterprise Governance

This guide covers governing JD.AI usage across teams and projects. It addresses usage tracking, cost controls, policy enforcement, shared workflow management, compliance requirements, and multi-tenant workspace isolation.

## Usage Tracking

### Token consumption

JD.AI tracks token usage per session via OpenTelemetry metrics. The `jdai.tokens.total` counter records prompt and completion tokens consumed, tagged by provider:

```bash
# View token usage in the current session
/usage

# View usage across all sessions
/sessions
```

Each session record includes:

- Total prompt tokens
- Total completion tokens
- Provider and model used
- Number of conversational turns

### Cost estimation

JD.AI estimates costs based on provider pricing. View estimated costs per session:

```bash
# Show cost estimate for the current session
/usage --cost
```

> [!NOTE]
> Cost estimates are approximate. Actual billing depends on your provider agreement and pricing tier.

### Per-provider usage

Monitor provider-level usage through the gateway REST API:

```bash
curl http://localhost:18789/api/providers
```

The health endpoint also reports per-provider availability and active model counts. For detailed metrics, export to Prometheus via the OTLP exporter (see [Observability](observability.md)).

## Usage Limits

### Budget limits

Set a maximum spend per session with the `--max-budget-usd` flag:

```bash
# Limit session to $5 USD
jdai --max-budget-usd 5.00

# The session will pause and prompt when the limit is reached
```

### Configuration-based limits

Set default limits in `appsettings.json`:

```json
{
  "Gateway": {
    "Limits": {
      "MaxBudgetUsd": 10.00,
      "MaxTokensPerSession": 100000,
      "MaxTurnsPerSession": 50,
      "MaxConcurrentAgents": 5
    }
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `MaxBudgetUsd` | `null` | Maximum estimated cost per session (USD) |
| `MaxTokensPerSession` | `null` | Maximum total tokens per session |
| `MaxTurnsPerSession` | `null` | Maximum conversational turns per session |
| `MaxConcurrentAgents` | `10` | Maximum simultaneous agent instances |

### Per-user and per-project limits

Apply different limits based on API key identity or project context:

```json
{
  "Gateway": {
    "Auth": {
      "ApiKeys": [
        {
          "Key": "team-a-key",
          "Name": "Team A",
          "Role": "Operator",
          "Limits": {
            "MaxBudgetUsd": 50.00,
            "MaxTokensPerSession": 200000
          }
        }
      ]
    }
  }
}
```

## Policy Enforcement

### Tool allowlists and denylists

Control which tools agents can invoke:

```json
{
  "Gateway": {
    "Policy": {
      "ToolAllowlist": ["read_file", "search", "list_files"],
      "ToolDenylist": ["execute_command", "write_file"]
    }
  }
}
```

- **Allowlist** — if set, only listed tools are permitted
- **Denylist** — listed tools are blocked; all others are allowed
- If both are set, the allowlist takes precedence

### Approved providers

Restrict which AI providers are available:

```json
{
  "Gateway": {
    "Policy": {
      "ApprovedProviders": ["claude-code", "github-copilot", "ollama"]
    }
  }
}
```

Providers not in the approved list are disabled even if credentials are available.

### Model restrictions

Restrict which models agents can use:

```json
{
  "Gateway": {
    "Policy": {
      "ApprovedModels": [
        "claude-sonnet-4-*",
        "gpt-4o",
        "llama3.2:*"
      ]
    }
  }
}
```

Model patterns support wildcards for flexible matching across versions.

## Shared Workflows

### Central workflow store

Organizations can maintain a central store of approved workflows that teams can use:

```text
~/.jdai/workflows/
├── code-review.yaml
├── security-scan.yaml
└── documentation.yaml
```

### Workflow approval process

1. **Author** — a team member creates a workflow definition
2. **Review** — the workflow is reviewed by a team lead or security team
3. **Publish** — approved workflows are added to the central store
4. **Distribute** — workflows are synced to team members via configuration management

### Using shared workflows

```bash
# List available workflows
jdai /workflows

# Run a shared workflow
jdai /workflow run code-review
```

## Compliance

### Audit trail

JD.AI maintains an audit trail through:

- **Session records** — full conversation history with timestamps, token counts, and tool invocations
- **Gateway events** — agent lifecycle, channel activity, and authentication events (via SignalR Event Hub)
- **OpenTelemetry traces** — distributed traces for every agent turn and provider call
- **Queryable audit buffer** — structured audit events stored in an in-memory circular buffer (~10,000 events) accessible via the `/audit` command and REST API

Export audit data for compliance review:

```bash
# Export a specific session
jdai /export session-id

# Sessions are exported to ~/.jdai/exports/ as JSON
```

## Retrieving Audit Data

The audit system provides two complementary access methods for inspecting what actions occurred during agent sessions.

### Interactive (CLI)

Use the `/audit` command in the JD.AI interactive prompt or channel adapters to inspect recent events:

```text
# Show all recent audit events
/audit

# Show only warning-level events and above
/audit --severity warning

# Show only error-level events
/audit --severity error

# Show the last 50 events
/audit --limit 50
```

### Programmatic (REST API)

Query the audit buffer via the Gateway REST API for automation and compliance tooling:

```bash
# List all recent events
curl http://localhost:18789/api/audit/events

# Filter by severity
curl http://localhost:18789/api/audit/events?severity=warning&limit=25

# Filter by session ID
curl "http://localhost:18789/api/audit/events?sessionId=sess_abc123"

# Filter by action type (e.g., policy denials only)
curl "http://localhost:18789/api/audit/events?action=policy.deny"

# Get summary statistics
curl http://localhost:18789/api/audit/stats
```

The `/api/audit/stats` endpoint returns counts grouped by severity and action type — useful for dashboards and compliance reports:

```json
{
  "totalEvents": 142,
  "bySeverity": { "Debug": 98, "Info": 12, "Warning": 30, "Error": 2 },
  "topActions": { "tool.invoke": 105, "session.create": 14, "policy.deny": 9 }
}
```

> [!NOTE]
> The in-memory audit buffer holds approximately 10,000 events and is cleared on restart. For long-term audit retention required by compliance frameworks, configure an external audit sink. See [Audit Logging](../user-guide/audit-logging.md) for file, Elasticsearch, and webhook sink configuration.

See [Gateway API Reference](../developer-guide/gateway-api.md) for the full REST API reference including all filter parameters and response schemas.

### Data residency

- All session data is stored locally in `~/.jdai/sessions.db`
- No data is sent to JD.AI servers or third-party analytics
- Cloud provider API calls go directly to the provider's endpoint
- Use local models (Ollama, LLamaSharp) for fully air-gapped operation

### No-telemetry mode

Disable all OpenTelemetry instrumentation:

```json
{
  "Gateway": {
    "Telemetry": {
      "Enabled": false
    }
  }
}
```

Or via environment variable:

```bash
export OTEL_SDK_DISABLED=true
```

This disables all traces, metrics, and telemetry export. Health checks continue to function.

## Multi-Tenant Isolation

### Workspace isolation

Isolate teams by running separate gateway instances with distinct data directories:

```bash
# Team A
jdai-daemon run --data-dir /data/team-a/.jdai --port 18790

# Team B
jdai-daemon run --data-dir /data/team-b/.jdai --port 18791
```

Each instance maintains its own:

- Session database
- Credential store
- Configuration
- Local models

### Per-team configuration

Use environment-specific configuration files:

```bash
# Team A configuration
jdai-daemon run --config /etc/jdai/team-a/appsettings.json

# Team B configuration
jdai-daemon run --config /etc/jdai/team-b/appsettings.json
```

### API key isolation

Different API keys can scope access to specific resources:

```json
{
  "Gateway": {
    "Auth": {
      "ApiKeys": [
        { "Key": "team-a-admin", "Name": "Team A Admin", "Role": "Admin" },
        { "Key": "team-a-user", "Name": "Team A User", "Role": "User" },
        { "Key": "team-b-admin", "Name": "Team B Admin", "Role": "Admin" }
      ]
    }
  }
}
```

## See also

- [Security](security.md) — credential management and authentication
- [Observability](observability.md) — metrics and monitoring for usage tracking
- [Gateway Administration](gateway-admin.md) — scaling and operational management
- [Dashboard](dashboard.md) — real-time monitoring UI
- [Audit Logging](../user-guide/audit-logging.md) — audit event schema, sinks, and configuration
- [Gateway API Reference](../developer-guide/gateway-api.md) — REST audit endpoints
