---
title: "Gateway API Reference"
description: "Complete reference for the JD.AI Gateway control plane — REST endpoints, SignalR hubs, authentication, and rate limiting."
---

# Gateway API Reference

The JD.AI Gateway is an ASP.NET Core control plane that manages agents, sessions, channels, and routing. It exposes a REST API for management operations and SignalR hubs for real-time streaming. The gateway runs as a standalone process on port `18789` by default.

## Quick start

```bash
# Start the gateway
dotnet run --project src/JD.AI.Gateway

# Or, if installed as a tool
jdai-gateway --urls "http://localhost:18789"

# Verify
curl http://localhost:18789/health
```

## Configuration

The gateway reads from `appsettings.json` under the `Gateway` section:

```json
{
  "Gateway": {
    "Server": {
      "Port": 18789,
      "Host": "localhost",
      "Verbose": false
    },
    "Auth": {
      "Enabled": false,
      "ApiKeys": [
        { "Key": "your-api-key", "Name": "Admin", "Role": "Admin" }
      ]
    },
    "RateLimit": {
      "Enabled": true,
      "MaxRequestsPerMinute": 60
    },
    "Channels": [],
    "Providers": []
  }
}
```

### Configuration reference

| Section | Property | Type | Default | Description |
|---------|----------|------|---------|-------------|
| `Server` | `Port` | `int` | `18789` | HTTP listen port |
| `Server` | `Host` | `string` | `localhost` | Bind address |
| `Server` | `Verbose` | `bool` | `false` | Verbose request logging |
| `Auth` | `Enabled` | `bool` | `false` | Require API key authentication |
| `Auth` | `ApiKeys` | `array` | `[]` | List of `{ Key, Name, Role }` entries |
| `RateLimit` | `Enabled` | `bool` | `true` | Per-identity rate limiting |
| `RateLimit` | `MaxRequestsPerMinute` | `int` | `60` | Sliding-window cap |

## Authentication

When `Auth.Enabled` is `true`, all `/api/*` endpoints require an API key. Health and SignalR endpoints are excluded.

```http
GET /api/agents HTTP/1.1
Host: localhost:18789
X-API-Key: your-api-key
```

For SignalR WebSocket connections, pass the key as a query parameter:

```
wss://localhost:18789/hubs/agent?api_key=your-api-key
```

### Roles

| Role | Level | Capabilities |
|------|:-----:|-------------|
| `User` | 1 | Read-only access (list agents, sessions, providers) |
| `Operator` | 2 | Send messages, connect/disconnect channels |
| `Admin` | 3 | Spawn/stop agents, modify configuration |

## REST endpoints

All REST endpoints are grouped under `/api/` and return JSON.

### Sessions

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/sessions?limit=50` | List all sessions (most recent first) |
| `GET` | `/api/sessions/{id}` | Get session details |
| `POST` | `/api/sessions/{id}/close` | Close an active session |
| `POST` | `/api/sessions/{id}/export` | Export a session |

### Agents

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/agents` | List all active agents |
| `POST` | `/api/agents` | Spawn a new agent |
| `POST` | `/api/agents/{id}/message` | Send a message and get response |
| `DELETE` | `/api/agents/{id}` | Stop and remove an agent |

#### Spawn agent

```http
POST /api/agents HTTP/1.1
Content-Type: application/json

{
  "provider": "claude-code",
  "model": "claude-sonnet-4-20250514",
  "systemPrompt": "You are a helpful code reviewer."
}
```

#### Send message

```http
POST /api/agents/a3f8b2e1c4d7/message HTTP/1.1
Content-Type: application/json

{ "message": "Review this function for null reference issues." }
```

### Providers

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/providers` | Detect and list all available providers |
| `GET` | `/api/providers/{name}/models` | Get models for a specific provider |

### Channels

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/channels` | List all registered channels |
| `POST` | `/api/channels/{type}/connect` | Connect a channel adapter |
| `POST` | `/api/channels/{type}/disconnect` | Disconnect a channel adapter |
| `POST` | `/api/channels/{type}/send` | Send a message through a channel |

See [Channel Adapters](channels.md) for setup guides.

### Plugins

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/plugins` | List installed plugins and runtime state |
| `GET` | `/api/plugins/{id}` | Get plugin details by id |
| `POST` | `/api/plugins/install` | Install plugin from path, package file, or URL |
| `POST` | `/api/plugins/{id}/enable` | Enable plugin |
| `POST` | `/api/plugins/{id}/disable` | Disable plugin |
| `POST` | `/api/plugins/{id}/update` | Update one plugin from recorded source |
| `POST` | `/api/plugins/update` | Update all installed plugins |
| `DELETE` | `/api/plugins/{id}` | Uninstall plugin |

Install payload:

```json
{
  "source": "https://example.com/plugins/My.Plugin.1.2.0.nupkg",
  "enable": true
}
```

### Audit

Query the in-memory queryable audit event buffer. Audit events are stored in a thread-safe circular buffer with a capacity of approximately 10,000 events. The buffer is cleared on application restart.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/audit/events` | List audit events with optional filters and pagination |
| `GET` | `/api/audit/events/{id}` | Get a single audit event by its event ID |
| `GET` | `/api/audit/stats` | Get summary statistics grouped by severity and action |

#### List events

```
GET /api/audit/events
```

Query parameters:

| Parameter | Type | Description |
|-----------|------|-------------|
| `action` | string | Filter by event action (e.g., `tool.invoke`, `session.create`) |
| `severity` | string | Minimum severity level (`debug`, `info`, `warning`, `error`, `critical`) |
| `sessionId` | string | Filter by session ID |
| `userId` | string | Filter by user ID |
| `resource` | string | Filter by resource substring (tool name, provider, etc.) |
| `from` | ISO 8601 | Start timestamp (inclusive) |
| `until` | ISO 8601 | End timestamp (inclusive) |
| `limit` | int | Maximum results to return (default: 50) |
| `offset` | int | Pagination offset (default: 0) |

```bash
# List the 25 most recent warning-level events
curl http://localhost:18789/api/audit/events?severity=warning&limit=25

# Filter by session
curl http://localhost:18789/api/audit/events?sessionId=sess_abc123

# Filter by tool invocations only
curl http://localhost:18789/api/audit/events?action=tool.invoke&limit=50
```

Response:

```json
{
  "totalCount": 142,
  "count": 25,
  "events": [
    {
      "eventId": "4a9f1b3c8e2d47a6b5c0d9e1f2a3b4c5",
      "timestamp": "2026-03-03T14:22:07.341+00:00",
      "userId": null,
      "sessionId": "sess_abc123",
      "traceId": null,
      "action": "tool.invoke",
      "resource": "read_file",
      "detail": "status=ok; args=path=src/Auth/TokenService.cs",
      "severity": "Debug",
      "policyResult": "Allow"
    }
  ]
}
```

#### Get event

```
GET /api/audit/events/{id}
```

Returns the full event record for the given `eventId`. Returns `404` if the event is not in the buffer.

```bash
curl http://localhost:18789/api/audit/events/4a9f1b3c8e2d47a6b5c0d9e1f2a3b4c5
```

#### Event statistics

```
GET /api/audit/stats
```

Returns summary counts grouped by severity and the top 10 actions by frequency.

```bash
curl http://localhost:18789/api/audit/stats
```

Response:

```json
{
  "totalEvents": 142,
  "bySeverity": {
    "Debug": 98,
    "Info": 12,
    "Warning": 30,
    "Error": 2
  },
  "topActions": {
    "tool.invoke": 105,
    "session.create": 14,
    "session.close": 14,
    "policy.deny": 9
  }
}
```

> [!NOTE]
> The audit buffer holds approximately 10,000 events in memory. On overflow, the oldest events are evicted. Events are not persisted across gateway restarts. For long-term retention, configure an external audit sink — see [Audit Logging](../user-guide/audit-logging.md).

### Health

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/health` | Full JSON health report |
| `GET` | `/health/ready` | Readiness probe (200/503) |
| `GET` | `/health/live` | Liveness probe (always 200) |
| `GET` | `/ready` | Readiness shortcut |

Health endpoints are not gated by authentication or rate limiting.

## SignalR hubs

### Agent Hub (`/hubs/agent`)

Real-time streaming chat with agents.

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `StreamChat` | `agentId`, `message` | `IAsyncEnumerable<AgentStreamChunk>` | Stream response tokens |

```json
{ "type": "start",   "agentId": "a3f8b2e1c4d7", "content": null }
{ "type": "content", "agentId": "a3f8b2e1c4d7", "content": "Here is my analysis..." }
{ "type": "end",     "agentId": "a3f8b2e1c4d7", "content": null }
```

**C# client:**

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:18789/hubs/agent")
    .Build();

await connection.StartAsync();

await foreach (var chunk in connection.StreamAsync<AgentStreamChunk>(
    "StreamChat", agentId, "Explain this code"))
{
    if (chunk.Type == "content")
        Console.Write(chunk.Content);
}
```

### Event Hub (`/hubs/events`)

Gateway-wide event streaming.

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `StreamEvents` | `eventTypeFilter?` | `IAsyncEnumerable<GatewayEvent>` | Stream filtered events |

**Event types:**

| Event | Description |
|-------|-------------|
| `agent.spawned` | New agent created |
| `agent.turn_complete` | Agent completed a turn |
| `agent.stopped` | Agent stopped and removed |
| `channel.connected` | Channel adapter connected |
| `channel.disconnected` | Channel adapter disconnected |
| `channel.message_received` | Message arrived from external channel |

## Architecture

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  REST Client  │     │  SignalR Hub  │     │   Channel    │
└──────┬───────┘     └──────┬───────┘     └──────┬───────┘
       │                     │                     │
       └─────────┬───────────┘                     │
                 ▼                                  │
       ┌──────────────────┐              ┌─────────▼────────┐
       │   Gateway API    │◄────────────►│  Channel Registry │
       │  (Minimal APIs)  │              └─────────┬────────┘
       └────────┬─────────┘                        │
                │                        ┌─────────▼────────┐
       ┌────────▼─────────┐              │   Event Bus      │
       │  Agent Pool Svc  │              │ (InProcessEventBus)│
       │  (IHostedService)│              └──────────────────┘
       └────────┬─────────┘
                │
       ┌────────▼──────────┐
       │  Provider Registry │
       │  (SK Kernels)      │
       └───────────────────┘
```

### Key services

| Service | Lifetime | Description |
|---------|----------|-------------|
| `AgentPoolService` | Singleton + `IHostedService` | Manages live agent instances |
| `ChannelRegistry` | Singleton | Thread-safe channel adapter registry |
| `InProcessEventBus` | Singleton | Event pub/sub |
| `SessionStore` | Singleton | SQLite session persistence |
| `ProviderRegistry` | Singleton | Provider detection and kernel building |
| `ApiKeyAuthProvider` | Singleton | API key validation |
| `SlidingWindowRateLimiter` | Singleton | Per-identity rate limiting |

### Middleware pipeline

1. **CORS** — allows all origins (configurable)
2. **API key auth** (when enabled) — authenticates `/api/*` requests
3. **Rate limiting** (when enabled) — per-identity request caps

## Error responses

```json
{ "error": "Unauthorized" }
```

| Status | Meaning |
|:------:|---------|
| `401` | Missing or invalid API key |
| `403` | Insufficient role |
| `404` | Resource not found |
| `429` | Rate limit exceeded |

## See also

- [Channel Adapters](channels.md) — setup guides for all channels
- [Plugin SDK](plugins.md) — extend the gateway with plugins
- [OpenClaw Integration](openclaw-integration.md) — cross-gateway orchestration
- [Architecture Overview](index.md) — gateway in the system architecture
- [Audit Logging](../user-guide/audit-logging.md) — audit event schema, sinks, and external retention
- [Security & Credentials](../operations/security.md) — audit model and security checklist
