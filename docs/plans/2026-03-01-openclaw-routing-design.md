# OpenClaw Channel Routing Design

## Problem

JD.AI has a working WebSocket bridge to OpenClaw, but messages from OpenClaw channels
(Discord, Signal) are only processed by OpenClaw's built-in agents. We need the ability
to route those messages through JD.AI's Semantic Kernel-based agent loop instead,
with configurable per-channel behavior.

## Approach

Introduce a routing mode abstraction with four modes, configurable per OpenClaw channel
via `appsettings.json`. A new `OpenClawRoutingService` hosted service manages the
lifecycle: connecting the bridge, subscribing to events, and dispatching messages
to the correct handler based on channel and mode.

## Routing Modes

| Mode | Behavior |
|------|----------|
| **Passthrough** | JD.AI observes events (logging/analytics) but never responds. OpenClaw handles everything. |
| **Intercept** | JD.AI hijacks the session — aborts OpenClaw's agent processing, routes through JD.AI's kernel, sends response back via OpenClaw. |
| **Proxy** | JD.AI creates a dedicated OpenClaw session with no built-in agent. All messages route through JD.AI; OpenClaw is pure transport. |
| **Sidecar** | Both systems run. JD.AI only processes messages matching a trigger (command prefix, @mention, or regex). OpenClaw handles the rest. |

## Configuration

```json
{
  "OpenClaw": {
    "WebSocketUrl": "ws://127.0.0.1:18789",
    "AutoConnect": true,
    "DefaultMode": "Passthrough",
    "Channels": {
      "discord": {
        "Mode": "Intercept",
        "AgentProfile": "default",
        "SystemPrompt": "You are a helpful assistant on Discord."
      },
      "signal": {
        "Mode": "Sidecar",
        "CommandPrefix": "/jdai",
        "AgentProfile": "default"
      }
    },
    "AgentProfiles": {
      "default": {
        "Provider": "claude-code",
        "Model": "claude-sonnet-4-5",
        "MaxTurns": 50,
        "Tools": ["file", "web", "shell"]
      }
    }
  }
}
```

## Event Flow

1. OpenClaw WebSocket delivers a `chat` event
2. `OnEvent` now inspects **all** streams (not just `assistant`)
3. For `stream == "user"`, the routing service checks the source channel
4. Mode-specific handler processes the message:
   - **Intercept**: `chat.abort` → JD.AI agent → `chat.send`
   - **Proxy**: JD.AI agent → `chat.send` (no abort needed, no OpenClaw agent)
   - **Sidecar**: Check trigger match → if yes, JD.AI agent → `chat.send`
   - **Passthrough**: Log event, no action

## Components

### New Types
- `OpenClawRoutingMode` enum (Passthrough, Intercept, Proxy, Sidecar)
- `OpenClawRoutingConfig` — top-level config binding
- `OpenClawChannelRouteConfig` — per-channel config
- `OpenClawAgentProfileConfig` — agent profile definition
- `OpenClawRoutingService` — BackgroundService orchestrator
- `IOpenClawModeHandler` — interface for mode-specific logic
- `InterceptModeHandler`, `ProxyModeHandler`, `SidecarModeHandler`, `PassthroughModeHandler`

### Modified Types
- `OpenClawBridgeChannel.OnEvent` — dispatch user + assistant events, include source channel metadata
- `OpenClawConfig` — add routing config properties
- `ServiceCollectionExtensions` — register routing service and mode handlers
