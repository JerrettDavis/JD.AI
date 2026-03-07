# JD.AI Gateway Dashboard Design

## Overview

A Blazor WebAssembly dashboard for the JD.AI Gateway that provides real-time visibility into channels, agents, sessions, providers, and OpenClaw integration. The primary use case is managing channel-to-agent routing overrides — controlling where JD.AI intercepts OpenClaw's default agent routing.

## Technology Stack

- **Framework**: Blazor WebAssembly (standalone)
- **UI Library**: MudBlazor (Material Design)
- **Backend**: JD.AI.Gateway REST API + SignalR hubs
- **Target**: .NET 10.0
- **Project**: `src/JD.AI.Dashboard.Wasm/`

## Architecture

The dashboard is a separate Blazor WASM project that communicates with the Gateway through its existing API surface:

- **REST API** (`/api/*`): CRUD operations for agents, channels, sessions, routing, providers, gateway config
- **SignalR** (`/hubs/events`, `/hubs/agent`): Real-time status updates and activity feed
- **Shared DTOs**: Referenced from `JD.AI.Contracts` or shared project

## Pages

### Overview (Index)

Summary cards: total agents, active channels, active sessions, OpenClaw bridge status. Recent activity feed from SignalR events.

### Channels

The centerpiece page. MudDataGrid of all channels (Discord, Signal, Slack, Telegram, Web) showing:

- Connection status chips (Connected/Disconnected/Error)
- Current agent assignment and model
- Expandable per-channel configuration
- **OpenClaw Override Editor**: Dialog-based editor for each channel binding
  - Agent selector dropdown
  - Model selector (Ollama + cloud models)
  - Routing mode: Passthrough / Sidecar / Intercept
  - Enable/disable override toggle
  - Save triggers routing API + OpenClaw sync
- Quick actions: connect/disconnect, sync with OpenClaw, test channel

### Agents

MudDataGrid of agent instances: ID, provider, model, status, session count, uptime. Actions: spawn, test message, stop. Expandable to show system prompt and active sessions.

### Sessions

Browsable session history with filters (agent, channel, date range). Turn-by-turn conversation viewer with message bubbles, token counts, model per turn. Export and close actions.

### Providers

Lists configured providers with available models. Ollama shows local models with size and context window. Health indicators per provider.

### Routing

Channel-to-agent mapping table with dropdown-based reassignment. Visual representation of the routing pipeline.

### Settings

Read-only gateway config (secrets redacted), live operational status, OpenClaw bridge diagnostics.

## Real-Time Updates

SignalR subscriptions provide:

- Channel status changes (connect/disconnect)
- Agent activity (message processing, errors)
- Session updates (new sessions, closes)
- OpenClaw bridge health changes

## Project Structure

```
src/JD.AI.Dashboard.Wasm/
├── JD.AI.Dashboard.Wasm.csproj
├── wwwroot/
│   ├── index.html
│   └── css/custom.css
├── Layout/
│   ├── MainLayout.razor
│   └── NavMenu.razor
├── Pages/
│   ├── Index.razor
│   ├── Channels.razor
│   ├── Agents.razor
│   ├── Sessions.razor
│   ├── Providers.razor
│   ├── Routing.razor
│   └── Settings.razor
├── Components/
│   ├── OpenClawOverrides.razor
│   ├── ChannelStatusCard.razor
│   ├── AgentCard.razor
│   ├── ModelSelector.razor
│   └── ActivityFeed.razor
├── Services/
│   ├── GatewayApiClient.cs
│   ├── SignalRService.cs
│   └── OpenClawService.cs
└── Program.cs
```
