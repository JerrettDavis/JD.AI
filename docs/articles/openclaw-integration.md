---
description: "Bridge JD.AI and OpenClaw gateways for cross-platform multi-AI orchestration — routing agents across Discord, Signal, Slack, and more."
---

# OpenClaw Integration

OpenClaw is an open-source multi-AI gateway that orchestrates conversations across different AI providers and messaging platforms. The JD.AI ↔ OpenClaw integration connects the two gateways via the `OpenClawBridgeChannel`, enabling agents on either platform to communicate, share context, and route messages across boundaries.

## Why integrate with OpenClaw

| Scenario | Without integration | With integration |
|----------|-------------------|-----------------|
| **Multi-gateway routing** | Each gateway manages its own channels independently | Messages flow between gateways — a Discord user on OpenClaw can reach a JD.AI agent |
| **Provider diversity** | Limited to providers available on one gateway | Combine JD.AI's Semantic Kernel providers with OpenClaw's provider ecosystem |
| **Platform coverage** | Each gateway supports its own set of channels | A single conversation can span platforms connected to either gateway |
| **Failover** | If one gateway goes down, its channels are unreachable | Route through the other gateway as a fallback |

## Architecture

The integration uses the `OpenClawBridgeChannel` — a standard `IChannel` adapter that appears in the JD.AI gateway's channel registry alongside Discord, Slack, and other adapters. From the gateway's perspective, OpenClaw is just another messaging platform.

```
┌─────────────────────────────────────────────────┐
│                 JD.AI Gateway                    │
│                                                  │
│  ┌──────────┐ ┌──────────┐ ┌─────────────────┐  │
│  │ Discord  │ │ Telegram │ │ OpenClaw Bridge │──┼──┐
│  │ Channel  │ │ Channel  │ │    Channel      │  │  │
│  └────┬─────┘ └────┬─────┘ └────────┬────────┘  │  │
│       │             │                │           │  │
│  ┌────▼─────────────▼────────────────▼────────┐  │  │
│  │              Channel Registry              │  │  │
│  └────────────────────┬───────────────────────┘  │  │
│                       │                          │  │
│  ┌────────────────────▼───────────────────────┐  │  │  HTTP
│  │              Agent Pool Service            │  │  │  (poll + post)
│  └────────────────────────────────────────────┘  │  │
└──────────────────────────────────────────────────┘  │
                                                      │
┌─────────────────────────────────────────────────────▼──┐
│                   OpenClaw Gateway                      │
│                                                         │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐               │
│  │  Signal  │ │  Slack   │ │  Matrix  │  ...           │
│  │ Channel  │ │ Channel  │ │ Channel  │               │
│  └──────────┘ └──────────┘ └──────────┘               │
└─────────────────────────────────────────────────────────┘
```

### Communication protocol

The bridge uses OpenClaw's HTTP API with two operations:

1. **Outbound (JD.AI → OpenClaw):** `POST /api/messages` with a JSON payload containing the channel, content, sender identity, and metadata.
2. **Inbound (OpenClaw → JD.AI):** `GET /api/messages?since={timestamp}&channel={source}` polled at a configurable interval (default: 1 second).

Messages include metadata that identifies their origin, preventing routing loops:

```json
{
  "channel": "jdai-outbound",
  "content": "Here is the code review...",
  "sender": "jdai",
  "metadata": {
    "source": "jdai-gateway",
    "original_channel": "discord-123456"
  }
}
```

## Setup

### Prerequisites

- A running JD.AI Gateway instance
- A running OpenClaw instance with HTTP API enabled
- Network connectivity between the two gateways

### Step 1: Configure OpenClaw channels

On the OpenClaw side, create two channels for the bridge:

- **`jdai-inbound`** — OpenClaw posts messages here for JD.AI to pick up
- **`jdai-outbound`** — JD.AI posts messages here for OpenClaw to consume

The exact steps depend on your OpenClaw version. Consult the OpenClaw documentation for channel creation.

### Step 2: Register the bridge in JD.AI

**Option A: DI registration in code**

```csharp
builder.Services.AddOpenClawBridge(config =>
{
    config.BaseUrl = "http://openclaw-host:3000";
    config.InstanceName = "production";
    config.ApiKey = "your-openclaw-api-key";
    config.TargetChannel = "jdai-outbound";
    config.SourceChannel = "jdai-inbound";
    config.PollIntervalMs = 1000;
});
```

The `AddOpenClawBridge` extension method:
1. Registers the `OpenClawConfig` as a singleton
2. Configures an `HttpClient` with the base URL and API key header
3. Registers `OpenClawBridgeChannel` as an `IChannel` in the DI container

**Option B: Gateway configuration**

```json
{
  "Gateway": {
    "Channels": [
      {
        "Type": "openclaw",
        "Name": "OpenClaw Production",
        "Settings": {
          "BaseUrl": "http://openclaw-host:3000",
          "InstanceName": "production",
          "ApiKey": "your-openclaw-api-key",
          "TargetChannel": "jdai-outbound",
          "SourceChannel": "jdai-inbound",
          "PollIntervalMs": "1000"
        }
      }
    ]
  }
}
```

### Step 3: Connect and verify

After the gateway starts, connect the bridge via the API:

```bash
# Connect the OpenClaw bridge
curl -X POST http://localhost:18789/api/channels/openclaw/connect \
     -H "X-API-Key: your-jdai-key"

# Verify it's connected
curl http://localhost:18789/api/channels \
     -H "X-API-Key: your-jdai-key"
```

Expected output:

```json
[
  { "channelType": "openclaw", "displayName": "OpenClaw (production)", "isConnected": true }
]
```

### Step 4: Route messages to an agent

```csharp
var registry = app.Services.GetRequiredService<IChannelRegistry>();
var pool = app.Services.GetRequiredService<AgentPoolService>();
var openClaw = registry.GetChannel("openclaw")!;

openClaw.MessageReceived += async (msg) =>
{
    var response = await pool.SendMessageAsync(targetAgentId, msg.Content);
    if (response is not null)
    {
        await openClaw.SendMessageAsync(msg.ChannelId, response);
    }
};
```

## Configuration reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BaseUrl` | `string` | `http://localhost:3000` | OpenClaw HTTP API base URL |
| `InstanceName` | `string` | `local` | Friendly name for this instance |
| `ApiKey` | `string?` | `null` | API key for OpenClaw authentication (sent as `X-API-Key` header) |
| `TargetChannel` | `string` | `default` | Channel name for outbound messages (JD.AI → OpenClaw) |
| `SourceChannel` | `string` | `default` | Channel name for inbound messages (OpenClaw → JD.AI) |
| `PollIntervalMs` | `int` | `1000` | Milliseconds between inbound message polls |

## Message flow

### Inbound (OpenClaw → JD.AI)

```
1. External user sends message via Signal (connected to OpenClaw)
2. OpenClaw routes message to the "jdai-inbound" channel
3. JD.AI's OpenClawBridgeChannel polls GET /api/messages?since=...&channel=jdai-inbound
4. Bridge converts OpenClawInboundMessage → ChannelMessage
5. MessageReceived event fires
6. Gateway routes ChannelMessage to the target agent
7. Agent processes and responds
```

### Outbound (JD.AI → OpenClaw)

```
1. JD.AI agent generates a response
2. Gateway calls OpenClawBridgeChannel.SendMessageAsync
3. Bridge POSTs OpenClawOutboundMessage to /api/messages
4. OpenClaw receives the message on "jdai-outbound" channel
5. OpenClaw routes the message to the appropriate external channel
6. External user receives the response via Signal
```

### Metadata preservation

Every outbound message includes metadata identifying its origin:

```csharp
Metadata = new Dictionary<string, string>
{
    ["source"] = "jdai-gateway",
    ["original_channel"] = conversationId
}
```

This metadata enables:
- **Loop prevention:** OpenClaw can skip messages that originated from JD.AI
- **Tracing:** Trace a conversation across both gateways
- **Routing:** OpenClaw can route the response back to the original platform

## Orchestration patterns

### Pattern 1: Cross-platform agent routing

Route users from any platform to a JD.AI agent, regardless of which gateway their channel is connected to.

```
Discord (JD.AI) ──→ JD.AI Agent ←── Signal (OpenClaw)
Telegram (JD.AI) ──→              ←── Slack (OpenClaw)
```

**Configuration:** Connect Discord and Telegram directly to JD.AI. Connect Signal and Slack to OpenClaw. Bridge the gateways with the OpenClaw channel adapter. All messages route to the same JD.AI agent pool.

### Pattern 2: Provider failover

Use OpenClaw's providers when JD.AI's primary provider is unavailable.

```csharp
openClaw.MessageReceived += async (msg) =>
{
    // Check if local providers are available
    var providers = await providerRegistry.DetectProvidersAsync(ct);
    if (providers.Any(p => p.IsAvailable))
    {
        // Use local agent
        var response = await pool.SendMessageAsync(localAgentId, msg.Content);
        await openClaw.SendMessageAsync(msg.ChannelId, response ?? "No response");
    }
    else
    {
        // Forward to OpenClaw's agents
        await openClaw.SendMessageAsync(msg.ChannelId, 
            $"/route-to-agent {msg.Content}");
    }
};
```

### Pattern 3: Multi-gateway fan-out

Distribute analysis tasks across both gateways for parallel processing.

```
User message arrives
    ├── JD.AI Agent (code review)    → uses Claude via Semantic Kernel
    └── OpenClaw Agent (security)    → uses GPT via OpenClaw
            ↓
    Synthesizer agent merges results
```

### Pattern 4: Platform-specific routing

Route messages to different agents based on the originating platform:

```csharp
openClaw.MessageReceived += async (msg) =>
{
    // OpenClaw metadata indicates the original platform
    var response = msg.Metadata.GetValueOrDefault("source_platform") switch
    {
        "signal" => await pool.SendMessageAsync(securityAgentId, msg.Content),
        "slack" => await pool.SendMessageAsync(devOpsAgentId, msg.Content),
        _ => await pool.SendMessageAsync(generalAgentId, msg.Content)
    };

    if (response is not null)
        await openClaw.SendMessageAsync(msg.ChannelId, response);
};
```

## Error handling and resilience

The bridge includes built-in error handling:

- **Connection verification:** `ConnectAsync` checks that the OpenClaw instance is reachable before starting the poll loop.
- **Poll failure backoff:** If a poll request fails, the bridge logs a warning and waits 5 seconds before retrying, preventing tight failure loops.
- **Graceful disconnection:** `DisconnectAsync` cancels the poll loop and waits for it to complete before returning.
- **HttpClient management:** The DI registration uses `AddHttpClient<T>`, which provides automatic connection pooling and DNS refresh.

### Monitoring

Use the gateway's event stream to monitor the bridge:

```csharp
await foreach (var evt in eventHub.StreamEvents("channel.*"))
{
    Console.WriteLine($"[{evt.Timestamp:HH:mm:ss}] {evt.Type}: {evt.SourceId}");
}
```

### Health checks

The gateway's `/health` endpoint reports overall status. To verify the OpenClaw bridge specifically:

```bash
# Check channel status
curl http://localhost:18789/api/channels | jq '.[] | select(.channelType == "openclaw")'
```

## Troubleshooting

### Bridge connects but no messages arrive

1. Verify the `SourceChannel` matches the channel name configured in OpenClaw
2. Check that OpenClaw is actually posting messages to the inbound channel
3. Lower `PollIntervalMs` temporarily (e.g., `500`) to rule out timing issues
4. Check the gateway logs for `"Error polling OpenClaw messages"` warnings

### Messages sent but not received by OpenClaw

1. Verify the `TargetChannel` matches the channel name OpenClaw is reading from
2. Check that the `BaseUrl` is correct and includes the protocol (e.g., `http://`)
3. If OpenClaw requires authentication, verify the `ApiKey` is set correctly
4. Test the OpenClaw API directly: `curl http://openclaw-host:3000/api/health`

### Connection fails on startup

1. Ensure the OpenClaw instance is running and reachable from the JD.AI gateway
2. Check firewall rules between the two hosts
3. Verify the OpenClaw health endpoint responds: `GET /api/health`

### Duplicate messages

If messages appear duplicated, check that:
- Only one JD.AI gateway instance is polling the same `SourceChannel`
- The `since` timestamp filter is working correctly (check clock synchronization between hosts)
- Loop prevention metadata is being respected on the OpenClaw side

## See also

- [Channel Adapters](channels.md) — all channel adapter setup guides
- [Gateway API Reference](gateway-api.md) — REST endpoints and SignalR hubs
- [Team Orchestration](orchestration.md) — multi-agent coordination strategies
