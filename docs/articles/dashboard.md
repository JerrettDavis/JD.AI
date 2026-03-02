# Admin Dashboard

The JD.AI Admin Dashboard is a Blazor WebAssembly application that provides a real-time management interface for the JD.AI Gateway. It connects to the Gateway via REST API and SignalR for live updates.

## Getting Started

1. Start the Gateway:

   ```bash
   cd src/JD.AI.Gateway
   dotnet run
   ```

2. Open the dashboard at `http://localhost:5189/`

The top-right corner shows a **LIVE** indicator when the dashboard is connected to the Gateway via SignalR.

## Overview

The Overview page provides a high-level summary of the Gateway state, including active agents, channels, sessions, and OpenClaw bridge status.

![Gateway Overview](../images/dashboard/dashboard-overview.png)

Key metrics displayed:

- **Agents** — number of currently running agent instances
- **Channels** — active communication channels (Discord, Signal, WebChat, etc.)
- **Sessions** — active chat sessions
- **OpenClaw** — bridge connection status

The **OpenClaw Bridge** section shows whether the bridge is enabled and which agents are registered.

**Recent Activity** displays real-time events as they occur.

## Web Chat

The Chat page provides a built-in web interface for communicating with agents directly from the dashboard.

![Web Chat](../images/dashboard/dashboard-chat.png)

Features:

- **Agent selector** — choose which agent to chat with from the dropdown
- **Streaming responses** — tokens stream in real-time via SignalR
- **Message history** — full conversation displayed with user/assistant bubbles
- **Clear conversation** — reset the chat at any time

Messages are sent to the Gateway's `AgentHub` and streamed back as chunked responses.

## Channels

The Channels page shows all configured communication channels and their connection status.

![Channels](../images/dashboard/dashboard-channels.png)

Each channel card displays:

- Channel name and type (web, discord, signal, etc.)
- Connection status (Online/Offline)
- **Disconnect** — stop the channel
- **Override** — configure channel-specific settings

The **Sync OpenClaw** button synchronizes channel configuration with the OpenClaw bridge.

## Agents

The Agents page lists all currently running agent instances with the ability to spawn new ones or stop existing agents.

![Agents](../images/dashboard/dashboard-agents.png)

The table shows:

| Column | Description |
|--------|-------------|
| ID | Unique agent instance identifier |
| Provider | AI provider (ollama, claude-code, github-copilot) |
| Model | Model being used (e.g., qwen3.5:27b) |
| Turns | Number of conversation turns completed |
| Created | When the agent was spawned |

Click **+ Spawn Agent** to create a new agent instance. Use the trash icon to stop an agent.

## Model Providers

The Providers page shows all configured AI providers, their connection status, and available models.

![Providers](../images/dashboard/settings-providers.png)

Each provider card shows:

- Provider name and status (Online/Offline)
- Number of available models
- Model catalog with ID and display name

Supported providers include Claude Code, GitHub Copilot, and Ollama (with 60+ local models).

## Settings

The Settings page provides a comprehensive WYSIWYG editor for all Gateway configuration. Changes are applied in real-time and persisted to the configuration file.

### Server Tab

![Server Settings](../images/dashboard/dashboard-settings.png)

Configure core server parameters:

- **Network** — Host and port binding
- **Behavior** — Verbose logging toggle
- **Authentication** — Enable/disable API key authentication
- **Rate Limiting** — Toggle rate limiting and set max requests per minute

### Providers Tab

![Provider Settings](../images/dashboard/settings-providers.png)

Manage AI provider configurations:

- Add/remove providers
- Configure endpoints and API keys
- Set default models per provider

### Agents Tab

![Agent Settings](../images/dashboard/settings-agents.png)

Define agent configurations with:

- Agent name, provider, and model selection
- System prompt customization
- **Model Parameters** (expandable panel):
  - Temperature (0.0–2.0)
  - Top P / Top K
  - Max Tokens
  - Context Window Size
  - Frequency / Presence / Repeat Penalty
  - Random Seed
  - Stop Sequences

### Channels Tab

Configure channel adapters (Discord, Signal, Slack, Telegram, WebChat).

### Routing Tab

Define message routing rules:

- Default agent assignment
- Channel-to-agent mappings
- Routing strategy selection

### OpenClaw Tab

Configure the OpenClaw bridge:

- Bridge URL and connection settings
- Agent registration
- Channel override mappings

## Real-Time Updates

The dashboard uses SignalR for real-time communication:

- **Event Hub** — receives agent events, channel status changes, and activity logs
- **Agent Hub** — streams chat responses for the web chat interface

Connection status is shown in the top-right corner with a green "LIVE" indicator.

## Architecture

```
┌─────────────────┐     REST API      ┌──────────────────┐
│   Dashboard      │ ◄──────────────► │   Gateway API    │
│   (Blazor WASM)  │     SignalR       │   (ASP.NET Core) │
│                  │ ◄──────────────► │                  │
│  - Pages/        │                   │  - Endpoints/    │
│  - Components/   │                   │  - Hubs/         │
│  - Services/     │                   │  - Services/     │
└─────────────────┘                   └──────────────────┘
```

The dashboard is served as static files from the Gateway and communicates entirely through HTTP and WebSocket connections.
