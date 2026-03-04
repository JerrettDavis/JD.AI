---
title: Security & Credentials
description: "Comprehensive security guide for JD.AI — credential management, API key security, session protection, MCP policies, gateway authentication, network security, and audit logging."
---

# Security & Credentials

JD.AI is designed with a security-first approach. All credentials are encrypted at rest, sessions are stored locally, and external connections require explicit opt-in. This guide covers every security surface of a production JD.AI deployment.

## Credential Management

### Encrypted credential store

JD.AI stores API keys and secrets in an encrypted credential store at `~/.jdai/credentials/`. The encryption method is platform-specific:

| Platform | Encryption | Details |
|----------|-----------|---------|
| Windows | DPAPI | Per-user encryption tied to the Windows account |
| Linux | AES-256-GCM | Key derived from machine-specific entropy |
| macOS | AES-256-GCM | Key derived from machine-specific entropy |

Credentials are never stored in plain text. The `/provider add` wizard automatically encrypts keys before writing them to the store.

### Credential resolution chain

When JD.AI needs an API key, it resolves through this priority chain:

1. **CLI flags** — `--api-key` passed at launch (not persisted)
2. **Environment variables** — e.g. `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`
3. **Encrypted credential store** — `~/.jdai/credentials/`
4. **OAuth session** — for Claude Code, GitHub Copilot, Codex (automatic token exchange)

### Adding credentials

Use the interactive wizard to securely store provider credentials:

```bash
# Interactive — prompts for key with masked input
jdai /provider add openai

# The wizard validates the key before storing
# Keys are encrypted immediately and never echoed
```

### Removing credentials

```bash
# Remove stored credentials for a provider
jdai /provider remove openai

# Verify removal
jdai /provider list
```

## API Key Security

### Storage

- API keys are encrypted at rest using platform-native encryption (DPAPI or AES-256-GCM)
- Keys are decrypted in memory only when needed for API calls
- The credential store files have restrictive file permissions (`600` on Linux/macOS)

### Rotation

To rotate an API key:

```bash
# Remove the old key
jdai /provider remove openai

# Add the new key
jdai /provider add openai
```

### Logging protections

- API keys are **never** written to log files
- Provider detection logs show authentication method but not credentials
- Health check responses include provider status but not key material
- The `/doctor` command redacts all sensitive values

## Session Security

### Local storage

- All sessions are stored in a local SQLite database at `~/.jdai/sessions.db`
- No session data is sent to any cloud service or telemetry endpoint
- Session data includes conversation history, token counts, and tool invocations

### Export controls

```bash
# Export a session to a JSON file (local only)
jdai /export my-session

# Exports go to ~/.jdai/exports/ by default
```

- Session exports are written to the local filesystem only
- No automatic cloud sync or backup to external services
- Export files contain full conversation history — handle with care

### Database security

- The SQLite database is stored in the user's home directory with standard file permissions
- No encryption-at-rest for the session database (rely on OS-level disk encryption for sensitive environments)
- Use full-disk encryption (BitLocker, LUKS) for additional protection

## MCP Security

### Local-only by default

JD.AI's Model Context Protocol (MCP) server connections are local-only by default:

- MCP servers run on `localhost` and are not exposed to the network
- No remote MCP connections are established without explicit configuration
- Tool invocations through MCP are subject to the same permission checks as built-in tools

### Remote MCP opt-in

To connect to a remote MCP server, explicit configuration is required:

```json
{
  "MCP": {
    "Servers": [
      {
        "Name": "remote-tools",
        "Endpoint": "https://mcp.example.com",
        "Transport": "sse",
        "ApiKey": "stored-in-credential-store"
      }
    ]
  }
}
```

Remote MCP connections should use TLS and authenticate with API keys stored in the encrypted credential store.

## Local Model Security

### File loading controls

- GGUF model files are loaded only from user-specified paths (`~/.jdai/models/` by default)
- The model directory is configurable via `JDAI_MODELS_DIR` environment variable
- No models are automatically downloaded without explicit user action (`/local download`)

### Download verification

- Model downloads go through the `/local download` command which requires user confirmation
- HuggingFace downloads use the authenticated API when `HF_TOKEN` is set
- Downloaded files are stored in the configured models directory only

## Gateway Security

### API key authentication

Enable API key authentication for the gateway REST API:

```json
{
  "Gateway": {
    "Auth": {
      "Enabled": true,
      "ApiKeys": [
        { "Key": "admin-key-here", "Name": "Admin", "Role": "Admin" },
        { "Key": "operator-key-here", "Name": "Operator", "Role": "Operator" },
        { "Key": "readonly-key-here", "Name": "ReadOnly", "Role": "User" }
      ]
    }
  }
}
```

### Bearer token authentication

Pass the API key via the `X-API-Key` header:

```bash
curl -H "X-API-Key: admin-key-here" http://localhost:18789/api/agents
```

For SignalR WebSocket connections, use the query parameter:

```text
wss://localhost:18789/hubs/agent?api_key=admin-key-here
```

### Role-based access

| Role | Level | Capabilities |
|------|:-----:|-------------|
| `User` | 1 | Read-only access (list agents, sessions, providers) |
| `Operator` | 2 | Send messages, connect/disconnect channels |
| `Admin` | 3 | Spawn/stop agents, modify configuration |

### Rate limiting

When enabled, the gateway applies a sliding-window rate limiter:

```json
{
  "Gateway": {
    "RateLimit": {
      "Enabled": true,
      "MaxRequestsPerMinute": 60
    }
  }
}
```

Requests exceeding the limit receive a `429 Too Many Requests` response. Rate limiting is keyed on authenticated identity or client IP.

### CORS configuration

By default, the gateway allows all origins for local development. Restrict CORS in production:

```json
{
  "Gateway": {
    "Cors": {
      "AllowedOrigins": ["https://dashboard.example.com"],
      "AllowedMethods": ["GET", "POST", "DELETE"],
      "AllowCredentials": true
    }
  }
}
```

## Network Security

### TLS termination

JD.AI Gateway runs on HTTP by default. Use a reverse proxy (Nginx, Caddy, Traefik) for TLS termination in production. See [Deployment](deployment.md) for reverse proxy configuration examples.

### Forwarded headers

When behind a reverse proxy, configure trusted headers:

```json
{
  "Gateway": {
    "Server": {
      "TrustedProxies": ["10.0.0.0/8", "172.16.0.0/12"],
      "ForwardedHeaders": true
    }
  }
}
```

The gateway respects `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Real-IP` headers from trusted proxies.

### Binding to localhost

For single-machine deployments, bind the gateway to localhost only:

```json
{
  "Gateway": {
    "Server": {
      "Host": "localhost",
      "Port": 18789
    }
  }
}
```

## Audit Logging

JD.AI has two separate audit mechanisms that serve different purposes.

### Operational Logs

Traditional application logs written to standard logging sinks (stdout, file, Seq, etc.). Configured via the `Logging` section of `appsettings.json`.

These logs include:

- Provider authentication events (success/failure, no credentials logged)
- Agent spawn and stop events
- Channel connect/disconnect events
- API authentication failures
- Rate limit violations
- Configuration changes via the dashboard

Configure structured logging for production:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "JD.AI.Security": "Information",
      "JD.AI.Gateway.Auth": "Warning"
    }
  }
}
```

Log retention is managed by your log aggregation system (Seq, ELK, CloudWatch, etc.).

### Audit Events

Structured events stored in a queryable in-memory circular buffer. Unlike operational logs, audit events are structured, filterable, and accessible through the CLI and REST API in real time.

**Buffer characteristics:**

- **Capacity**: approximately 10,000 events
- **Overflow**: oldest events are evicted when the buffer is full
- **Persistence**: in-memory only — events are cleared on application restart

**Event types recorded:**

- Every tool invocation (with outcome: `ok`, `denied`, or `user_denied`)
- Session create and close events
- Policy denial events

**Access methods:**

- **`/audit` command** — view recent events interactively with severity filtering (see [Commands Reference](../reference/commands.md))
- **`GET /api/audit/events`** — query events via REST API with full filter and pagination support
- **`GET /api/audit/stats`** — summary counts grouped by severity and action

**Example — query warning-level events via REST:**

```bash
curl http://localhost:18789/api/audit/events?severity=warning&limit=25
```

**Example — view recent events in the CLI:**

```text
/audit --severity warning
```

For long-term retention and external sink configuration (file, Elasticsearch, webhook), see [Audit Logging](../user-guide/audit-logging.md).

For the full REST API reference for audit endpoints, see the [Gateway API Reference](../developer-guide/gateway-api.md).

### Retention

- Audit events in the in-memory buffer are cleared on application restart
- Operational log retention is managed by your log aggregation system
- Session data is retained in SQLite indefinitely until manually deleted
- To persist audit events across restarts, configure an external audit sink

## Security checklist

Use this checklist for production deployments:

- [ ] Enable gateway API key authentication (`Auth.Enabled: true`)
- [ ] Use strong, unique API keys for each role
- [ ] Place the gateway behind a TLS-terminating reverse proxy
- [ ] Restrict CORS to known dashboard origins
- [ ] Bind to `localhost` if not serving external clients
- [ ] Enable full-disk encryption on the host
- [ ] Configure rate limiting to prevent abuse
- [ ] Route logs to a centralized logging system
- [ ] Review provider credentials periodically and rotate as needed
- [ ] Restrict MCP servers to local-only unless explicitly required
- [ ] Configure an external audit sink (file, Elasticsearch, or webhook) for long-term audit event retention
- [ ] Monitor `/api/audit/events?severity=warning` periodically for policy denial activity

## See also

- [Deployment](deployment.md) — reverse proxy and TLS configuration
- [Governance](governance.md) — policy enforcement and usage limits
- [Gateway Administration](gateway-admin.md) — operational management
- [Providers](../reference/providers.md) — credential resolution and provider setup
- [Audit Logging](../user-guide/audit-logging.md) — external sink configuration and event schema
- [Gateway API Reference](../developer-guide/gateway-api.md) — REST audit endpoints
