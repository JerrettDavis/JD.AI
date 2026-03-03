---
description: "Complete reference for the JD.AI Gateway control plane — REST endpoints, SignalR hubs, authentication, and rate limiting."
---

# Gateway API Reference

The JD.AI Gateway is an ASP.NET Core control plane that manages agents, sessions, channels, and routing. It exposes a REST API for management operations and SignalR hubs for real-time streaming. The gateway runs as a standalone process (or hosted service) on port `18789` by default.

## Quick start

```bash
# Start the gateway
dotnet run --project src/JD.AI.Gateway
```

Or, if installed as a tool:

```bash
jdai-gateway --urls "http://localhost:18789"
```

Verify it is running:

```bash
curl http://localhost:18789/health
# {"status":"Healthy","description":"Gateway operational","data":{"activeAgents":0,"uptime":"00:00:12"}}
```

## Configuration

The gateway reads from `appsettings.json` under the `Gateway` section. All settings have sensible defaults — you can run the gateway with zero configuration for local development.

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
    "Channels": [
      { "Type": "discord", "Name": "My Discord", "Settings": { "BotToken": "..." } }
    ],
    "Providers": [
      { "Name": "claude-code", "Enabled": true }
    ]
  }
}
```

### Configuration reference

| Section | Property | Type | Default | Description |
|---------|----------|------|---------|-------------|
| `Server` | `Port` | `int` | `18789` | HTTP listen port |
| `Server` | `Host` | `string` | `localhost` | Bind address |
| `Server` | `Verbose` | `bool` | `false` | Enable verbose request logging |
| `Auth` | `Enabled` | `bool` | `false` | Require API key authentication |
| `Auth` | `ApiKeys` | `array` | `[]` | List of `{ Key, Name, Role }` entries |
| `RateLimit` | `Enabled` | `bool` | `true` | Enable per-identity rate limiting |
| `RateLimit` | `MaxRequestsPerMinute` | `int` | `60` | Sliding-window cap per identity or IP |
| `Channels` | — | `array` | `[]` | Channel adapter configurations |
| `Providers` | — | `array` | `[]` | Provider overrides |

## Authentication

When `Auth.Enabled` is `true`, all `/api/*` endpoints require an API key. Health and SignalR endpoints are excluded from auth enforcement.

Pass the key via the `X-API-Key` header:

```http
GET /api/agents HTTP/1.1
Host: localhost:18789
X-API-Key: your-api-key
```

For SignalR WebSocket connections (which cannot set custom headers), pass the key as a query parameter:

```
wss://localhost:18789/hubs/agent?api_key=your-api-key
```

### Roles

Each API key has an associated role. Roles are hierarchical — higher roles include all permissions of lower roles.

| Role | Level | Capabilities |
|------|:-----:|-------------|
| `User` | 1 | Read-only access (list agents, sessions, providers) |
| `Operator` | 2 | Send messages, connect/disconnect channels |
| `Admin` | 3 | Spawn/stop agents, modify configuration |

Apply role requirements to endpoints with the `RequireRole` filter:

```csharp
group.MapPost("/", handler).RequireRole(GatewayRole.Admin);
```

### Rate limiting

When enabled, the gateway applies a sliding-window rate limiter keyed on the authenticated identity ID (or the client IP address for unauthenticated requests). Requests that exceed the limit receive a `429 Too Many Requests` response.

## REST endpoints

All REST endpoints are grouped under `/api/` and return JSON. Standard HTTP status codes apply: `200` for success, `201` for resource creation, `204` for no content, `404` when a resource is not found, and `401`/`403` for auth errors.

### Sessions

Manage stored conversation sessions.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/sessions?limit=50` | List all sessions (most recent first) |
| `GET` | `/api/sessions/{id}` | Get session details including turn history |
| `POST` | `/api/sessions/{id}/close` | Close an active session |
| `POST` | `/api/sessions/{id}/export` | Export a session to the default export directory |

#### List sessions response

```json
[
  {
    "id": "sess_a1b2c3",
    "name": "Refactor auth module",
    "providerName": "claude-code",
    "modelId": "claude-sonnet-4-20250514",
    "createdAt": "2025-01-15T10:30:00Z",
    "updatedAt": "2025-01-15T11:45:00Z",
    "messageCount": 24,
    "totalTokens": 18500,
    "isActive": true
  }
]
```

### Agents

Manage live agent instances. Each agent has its own Semantic Kernel, chat history, and lifecycle.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/agents` | List all active agent instances |
| `POST` | `/api/agents` | Spawn a new agent |
| `POST` | `/api/agents/{id}/message` | Send a message and get the response |
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

Response (`201 Created`):

```json
{
  "id": "a3f8b2e1c4d7"
}
```

#### Send message

```http
POST /api/agents/a3f8b2e1c4d7/message HTTP/1.1
Content-Type: application/json

{
  "message": "Review this function for potential null reference issues."
}
```

Response:

```json
{
  "response": "I found two potential null reference issues..."
}
```

#### List agents response

```json
[
  {
    "id": "a3f8b2e1c4d7",
    "provider": "claude-code",
    "model": "claude-sonnet-4-20250514",
    "turnCount": 5,
    "createdAt": "2025-01-15T10:30:00Z"
  }
]
```

### Providers

Detect and query available AI providers.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/providers` | Detect and list all available providers with their models |
| `GET` | `/api/providers/{name}/models` | Get models for a specific provider |

#### List providers response

```json
[
  {
    "name": "claude-code",
    "isAvailable": true,
    "statusMessage": "Authenticated via CLI session",
    "models": [
      { "id": "claude-sonnet-4-20250514", "displayName": "Claude Sonnet 4", "providerName": "claude-code" },
      { "id": "claude-opus-4-20250514", "displayName": "Claude Opus 4", "providerName": "claude-code" }
    ]
  }
]
```

### Channels

Manage messaging channel adapters. See [Channel Adapters](channels.md) for detailed setup instructions for each channel type.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/channels` | List all registered channels and their connection status |
| `POST` | `/api/channels/{type}/connect` | Connect a channel adapter |
| `POST` | `/api/channels/{type}/disconnect` | Disconnect a channel adapter |
| `POST` | `/api/channels/{type}/send` | Send a message through a channel |

#### List channels response

```json
[
  { "channelType": "discord", "displayName": "Discord", "isConnected": true },
  { "channelType": "slack", "displayName": "Slack", "isConnected": false },
  { "channelType": "web", "displayName": "WebChat", "isConnected": true }
]
```

#### Send a message through a channel

```http
POST /api/channels/discord/send HTTP/1.1
Content-Type: application/json

{
  "conversationId": "1234567890",
  "content": "Hello from the gateway!"
}
```

### Health

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/health` | ASP.NET health check — reports active agent count and uptime |
| `GET` | `/ready` | Readiness probe — returns `200` when the gateway can accept requests |

These endpoints are not gated by authentication or rate limiting.

## SignalR hubs

The gateway exposes two SignalR hubs for real-time communication. Both use the standard ASP.NET Core SignalR protocol and support WebSocket and Server-Sent Events transports.

### Agent Hub (`/hubs/agent`)

Real-time streaming chat with agents. Clients join the `agents` group on connection.

**Server methods:**

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `StreamChat` | `agentId`, `message` | `IAsyncEnumerable<AgentStreamChunk>` | Stream agent response tokens in real-time |

**`AgentStreamChunk` format:**

```json
{ "type": "start",   "agentId": "a3f8b2e1c4d7", "content": null }
{ "type": "content", "agentId": "a3f8b2e1c4d7", "content": "Here is my analysis..." }
{ "type": "content", "agentId": "a3f8b2e1c4d7", "content": " of the code." }
{ "type": "end",     "agentId": "a3f8b2e1c4d7", "content": null }
```

**JavaScript client example:**

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:18789/hubs/agent")
    .build();

await connection.start();

// Stream chat response
const stream = connection.stream("StreamChat", agentId, "Explain this code");
stream.subscribe({
    next: (chunk) => {
        if (chunk.type === "content") {
            process.stdout.write(chunk.content);
        }
    },
    complete: () => console.log("\nDone"),
    error: (err) => console.error(err)
});
```

**C# client example:**

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

Gateway-wide event streaming. Clients join the `events` group on connection.

**Server methods:**

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `StreamEvents` | `eventTypeFilter?` | `IAsyncEnumerable<GatewayEvent>` | Stream gateway events, optionally filtered by type |

**`GatewayEvent` format:**

```json
{
  "type": "agent.spawned",
  "sourceId": "a3f8b2e1c4d7",
  "timestamp": "2025-01-15T10:30:00Z",
  "data": { "provider": "claude-code", "model": "claude-sonnet-4-20250514" }
}
```

**Event types:**

| Event | Description |
|-------|-------------|
| `agent.spawned` | A new agent instance was created |
| `agent.turn_complete` | An agent completed a conversational turn |
| `agent.stopped` | An agent was stopped and removed |
| `channel.connected` | A channel adapter connected |
| `channel.disconnected` | A channel adapter disconnected |
| `channel.message_received` | A message arrived from an external channel |

**Filtered streaming example:**

```csharp
// Only receive agent-related events
await foreach (var evt in connection.StreamAsync<GatewayEvent>(
    "StreamEvents", "agent.*"))
{
    Console.WriteLine($"[{evt.Type}] {evt.SourceId} at {evt.Timestamp}");
}
```

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
| `AgentPoolService` | Singleton + `IHostedService` | Manages live agent instances with per-agent `Kernel` and `ChatHistory` |
| `ChannelRegistry` | Singleton | Thread-safe in-memory registry of all `IChannel` adapters |
| `InProcessEventBus` | Singleton | Publishes and streams `GatewayEvent` instances |
| `SessionStore` | Singleton | SQLite-backed session persistence at `~/.jdai/sessions.db` |
| `ProviderRegistry` | Singleton | Detects Claude Code, GitHub Copilot, OpenAI Codex, Ollama, and Local providers |
| `ApiKeyAuthProvider` | Singleton | Validates API keys and resolves `GatewayIdentity` |
| `SlidingWindowRateLimiter` | Singleton | Per-identity sliding-window rate limiter |

### Middleware pipeline

The gateway uses a standard ASP.NET Core middleware pipeline:

1. **CORS** — allows all origins (configurable for production)
2. **API key auth** (when `Auth.Enabled`) — authenticates `/api/*` requests via `X-API-Key` header or `api_key` query parameter; skips `/health`, `/ready`, and `/hubs/` paths
3. **Rate limiting** (when `RateLimit.Enabled`) — enforces per-identity request caps; skips health endpoints

## Error responses

All error responses follow a consistent format:

```json
{ "error": "Unauthorized" }
```

| Status | Meaning |
|:------:|---------|
| `401` | Missing or invalid API key |
| `403` | Authenticated but insufficient role |
| `404` | Resource not found |
| `429` | Rate limit exceeded |

## See also

- [Channel Adapters](channels.md) — setup guides for Discord, Signal, Slack, Telegram, WebChat, and OpenClaw
- [Plugin SDK](plugins.md) — extend the gateway with custom plugins
- [OpenClaw Integration](openclaw-integration.md) — cross-gateway orchestration
- [Configuration](configuration.md) — general JD.AI configuration
