# OpenClaw UI — Complete Dashboard Specification

> **Verified:** Live UI [2026-04-11] via gateway token — 13 pages confirmed with live content

## Overview

**Application Type:** Single-page application (SPA) for managing an AI agent gateway  
**Entry Point:** `<openclaw-app>` web component  
**Tech Stack:** Web components (likely Lit or similar), TypeScript controllers/views, SignalR for real-time communication, MudBlazor components on some pages (Blazor hybrid)  
**Deployment Model:** Self-hosted gateway with browser-based control dashboard  
**Asset Loading:** Module-bundled JS (`assets/index-<hash>.js`), preloaded modules (i18n, directive, format, string-coerce), single CSS bundle  
**Viewport:** Responsive (`width=device-width, initial-scale=1.0`), supports desktop, tablet, mobile  

## Authentication Architecture

> **IMPORTANT:** The `?session=` URL parameter and gateway WebSocket authentication serve different purposes. Specs for admin panel routes were written primarily from source code inspection and educated inference, not live UI observation. Content marked `[Inferred — needs verification]` has not been confirmed against the running application.

### Two-Tier Authentication

| Tier | Scope | Mechanism | Pages |
|------|-------|-----------|-------|
| **Session key** | Chat only | URL parameter `?session=<key>` | `/chat` |
| **Gateway WebSocket auth** | All admin routes | Token from `openclaw dashboard` CLI | `/control/*`, `/agent/*`, `/settings/*` |

### Chat Session Access

- **Parameter:** `?session=<session-key>`
- **Format:** `agent:<agent-name>:<provider>:<resource-type>:<resource-id>`
- **Example:** `agent:jdai-default:discord:channel:1466622912307007690`
- **Scope:** Authenticates the `/chat` route for agent interaction
- **Persistence:** Stays in URL across navigation but only authenticates chat

### Gateway Dashboard Access

- **CLI command:** `openclaw dashboard` generates a tokenized URL for admin access
- **Token delivery:** URL fragment `#token=<TOKEN>` or query parameter
- **WebSocket URL:** Default `ws://127.0.0.1:18789` (configurable)
- **Optional credentials:** Gateway token (`OPENCLAW_GATEWAY_TOKEN`), password (session-only, not stored)
- **Auth gate UI:** Centered card with OpenClaw logo, "Gateway Dashboard" subtitle, WebSocket URL input, token input, password input, connect button
- **On success:** Gateway dashboard content becomes accessible across all admin routes
- **On failure:** Error message with reconnect option

### Route Authentication Map

| Route | Auth Required | Auth Method | Spec Source |
|-------|---------------|-------------|-------------|
| `/chat` | Session key | `?session=<key>` | Live UI observation |
| `/control/overview` | Gateway WS | Auth gate | Source code + inference |
| `/control/channels` | Gateway WS | Auth gate | Source code + inference |
| `/control/instances` | Gateway WS | Auth gate | Source code + inference |
| `/control/sessions` | Gateway WS | Auth gate | Source code + inference |
| `/control/usage` | Gateway WS | Auth gate | Not yet documented |
| `/control/cron-jobs` | Gateway WS | Auth gate | Not yet documented |
| `/agent/agents` | Gateway WS | Connection required | Source code inspection |
| `/agent/skills` | Gateway WS | Connection required | Source code inspection |
| `/agent/nodes` | Gateway WS | Connection required | Source code inspection |
| `/agent/dreaming` | Gateway WS | Connection required | Source code inspection |
| `/settings/config` | Gateway WS | Connection required | Live UI observation |
| `/settings/communication` | Gateway WS | Connection required | Live UI observation |
| `/settings/appearance` | Gateway WS | Auth gate | Auth gate observed, post-auth inferred |
| `/settings/automation` | Gateway WS | Auth gate | Auth gate observed, post-auth inferred |
| `/settings/infrastructure` | Gateway WS | Auth gate | Auth gate observed, post-auth inferred |
| `/settings/ai-agents` | Gateway WS | Connection required | Source code inspection |
| `/settings/debug` | Gateway WS | Connection required | Source code inspection |
| `/settings/logs` | Gateway WS | Connection required | Source code inspection |

### Session Model

- **Session key format:** `agent:<agent-name>:<provider>:<resource-type>:<resource-id>`
- **Scope:** Persists in URL `?session=` parameter across navigation; authenticates chat route specifically
- **Storage:** URL query parameter (not localStorage)
- **Multi-provider:** Format supports Discord, with extensibility for other providers
- **Example:** `agent:jdai-default:discord:channel:1466622912307007690`

---

## Design System

### Theme System

Three theme families, each with light/dark variants:

| Theme | Description | Resolved Names |
|-------|-------------|----------------|
| **Claw** | Default theme | `light`, `dark` |
| **Knot** | Dark-focused alternative | `openknot`, `openknot-light` |
| **Dash** | Light-focused alternative | `dash`, `dash-light` |

**Mode options:** Light, Dark, System (auto-detect from OS preference)

**Implementation:**
- Applied via `data-theme` and `data-theme-mode` attributes on `<html>` root
- JavaScript reads from localStorage on page load, before first render (prevents flash)
- Storage keys: `openclaw.control.settings.v1.theme`, `openclaw.control.settings.v1.themeMode`
- Legacy theme names supported for backwards compatibility: `openknot`, `fieldmanual`, `clawdash`

### Color Tokens

- **Status colors:** Green (connected/healthy), Yellow (degraded/warning), Red (disconnected/error)
- **Severity colors:** Error (red), Warning (orange), Info (blue), Debug/default (gray)
- **Action colors:** Primary (blue) for approve/save, Danger (red) for delete/revoke
- **Message bubbles:** Indigo/blue tint (user), Gray/neutral tint (agent), Red tint (error)

### Typography

- **Monospace:** Used for cron expressions, JSON payloads, agent IDs, model IDs, code blocks
- **Standard:** Used for labels, descriptions, navigation text
- **Heading hierarchy:** H1 equivalents for page titles, H4 for section headers, H6 for detail panels
- **Timestamps:** HH:mm:ss format (24h or 12h based on locale), relative format for durations ("2h 34m", "5d 12h")

### Layout Primitives

- **Persistent left sidebar:** Collapsible navigation with section groupings
- **Two-panel layouts:** Left nav/directory + right content (Config, Communication)
- **Card-based layouts:** Provider cards, agent cards, metric cards
- **Tabbed interfaces:** Agent detail tabs, Settings sub-tabs, Skills status tabs
- **Data grids:** Dense, hover-enabled tables for logs, channels, instances, sessions
- **Modal dialogs:** For detail views, configuration forms, confirmations
- **Snackbar/toast:** Bottom-positioned notifications for save success/failure

---

## Global Shell

### Sidebar Structure

```
[OpenClaw Logo/Icon]          ← Top of sidebar, branded
│
├── Chat                      ← Direct link, default/home
│
├── Control                   ← Expandable section header
│   ├── Overview
│   ├── Channels
│   ├── Instances
│   ├── Sessions
│   ├── Usage
│   └── Cron Jobs
│
├── Agent                     ← Expandable section header
│   ├── Agents
│   ├── Skills
│   ├── Nodes
│   └── Dreaming
│
└── Settings                  ← Expandable section header
    ├── Config
    ├── Communication
    ├── Appearance
    ├── Automation
    ├── Infrastructure
    ├── AI & Agents
    ├── Debug
    └── Logs
```

**Collapse behavior:**
- Toggle button to collapse sidebar to icon-only mode
- Expanded state shows full section/page labels
- Collapse state may persist in localStorage

**Active state behavior:**
- Current page highlighted (bold, color, background)
- Parent section expanded when child is active
- Breadcrumb navigation updates above content area

### Header Elements

- **Breadcrumb:** `Section > Page` format, may be clickable
- **Session indicator:** Current session context displayed
- **Theme selector:** Available in Settings > Appearance (may also appear globally)

### Footer Elements

- Not explicitly documented; likely minimal or absent

---

## Page Specifications

### /chat — Web Chat

**Purpose:** Real-time conversational interface for testing agent responses without external platforms.

**Key Components:**
- Header: "Web Chat" title, agent selector dropdown (populated from `GET /api/agents`), clear chat button (trash icon)
- Message area: Scrollable, user bubbles (right, indigo), agent bubbles (left, gray), streaming cursor indicator
- Input: Text area with Enter-to-send, Shift+Enter for newline, send button

**Critical Interactions:**
- Agent selection enables input field
- Message sending: user bubble appears immediately, input disables, agent response streams via SignalR `StreamChatAsync`
- Streaming: chunks appended in real-time with blinking cursor, cursor removed on completion
- Clear chat removes all messages, shows empty state

**Data Dependencies:**
- `GET /api/agents` — agent list on page load
- SignalR hub `/hubs/agent` — `StreamChatAsync(agentId, message)` returns `IAsyncEnumerable<ChatChunk>`
- ChatChunk: `{ type: "content" | "error", content: string }`

**Notable Details:**
- Chat history is NOT persisted (client-side in-memory only, lost on refresh)
- Single conversation only (no threading, no multi-session)
- No stop/cancel button for streaming (backend supports CancellationToken but not exposed)
- Whitespace preserved via `white-space: pre-wrap`

---

### /control/overview — System Overview `[Inferred — needs verification]`

**Purpose:** System-wide status dashboard with gateway connection, health metrics, and operational snapshots.

**Key Components:**
- Auth gate: WebSocket URL, gateway token, password, connect button
- Gateway access section: connection config, default session key, language selector
- Snapshots section: status indicator, uptime counter, tick interval, last refresh timestamps, health indicator, queue status, active counts (sessions, agents, channels), CPU/memory gauges
- Optional: event timeline, quick action shortcuts

**Critical Interactions:**
- Authentication flow (WebSocket URL + token + password → connect)
- Dashboard auto-refreshes on configurable interval (5-10 seconds)
- Status indicators update in real-time via WebSocket
- Manual refresh button for on-demand metric reload

**Data Dependencies:**
- WebSocket connection with authentication handshake
- Snapshot data fetched via WebSocket messages
- Real-time status updates via WebSocket subscription

**Notable Details:**
- Auth gate blocks all content until connected
- Preferences stored in localStorage: `openclaw.control.gateway.endpoint`, `openclaw.control.settings.v1.language`
- Dashboard grid layout (responsive, 2-4 columns)

---

### /control/channels — Channel Management `[Inferred — needs verification]`

**Purpose:** Manage and monitor Discord channels connected to the system.

**Key Components:**
- Page header with "Add Channel" button
- Search/filter bar
- Channels table: name, server/guild, status, member count, last activity, created date, actions
- Pagination controls

**Critical Interactions:**
- Click row → detail view/panel
- Add channel → modal with server/channel selection
- Edit/delete with confirmation dialogs
- Real-time filtering on search input

**Data Dependencies:**
- `GET /api/channels` — channel list
- `POST /api/channels` — create
- `PUT /api/channels/:id` — update
- `DELETE /api/channels/:id` — disconnect
- WebSocket events: `channel.created`, `channel.updated`, `channel.deleted`, `channel.status_changed`

---

### /control/instances — Instance Management `[Inferred — needs verification]`

**Purpose:** Monitor and manage system instances with real-time status.

**Key Components:**
- Deploy/Add Instance button
- Status overview cards (total, running, failed, resource usage)
- Search/filter bar with status, type, region filters
- Instances table: name, type, status, CPU, memory, uptime, last updated, IP, actions
- Detail panel with full config, metrics, logs

**Critical Interactions:**
- Deploy wizard (type, region, resources, env vars)
- Restart, stop, delete with confirmations
- View logs, SSH/connect per instance
- Real-time metric updates via WebSocket

**Data Dependencies:**
- `GET /api/instances` — instance list with metrics
- `POST /api/instances` — create
- `POST /api/instances/:id/restart` — restart
- `POST /api/instances/:id/stop` — stop
- `DELETE /api/instances/:id` — delete
- WebSocket events: `instance.created`, `instance.status_changed`, `instance.metrics_updated`

---

### /control/sessions — Session Tracking `[Inferred — needs verification]`

**Purpose:** Track and manage active user sessions with device/platform info.

**Key Components:**
- Summary cards (active count, total today, longest duration)
- Search/filter bar (user, IP, device, status, date range)
- Sessions table: username, platform, IP, status, started, last activity, duration, location, actions
- Detail panel with full session info

**Critical Interactions:**
- Terminate session (with confirmation, logs out user)
- Revoke session (prevent reuse)
- Terminate All Sessions (admin bulk action)
- Duration and Last Activity update in real-time

**Data Dependencies:**
- `GET /api/sessions` — session list
- `POST /api/sessions/:id/terminate` — end session
- `POST /api/sessions/terminate-all` — bulk clear
- WebSocket events: `session.created`, `session.activity`, `session.terminated`

---

### /control/usage — Usage Analytics

**Purpose:** Resource consumption metrics and analytics.

**Status:** Spec not yet documented. Route exists in navigation; content pending gateway exploration.

---

### /control/cron-jobs — Scheduled Jobs

**Purpose:** Create and manage scheduled tasks.

**Status:** Spec not yet documented. Route exists in navigation; content pending gateway exploration.

---

### /agent/agents — Agent Management `[Source code inspection]`

**Purpose:** Manage spawned AI agents with detailed inspection and configuration.

**Key Components:**
- Toolbar: agent selector dropdown, Copy ID, Set Default, Refresh buttons
- Empty state: "Select an agent" card
- Tabbed detail panel:
  - **Overview:** Agent ID, provider, model, identity, system prompt
  - **Files:** Config file editor (read/edit/save)
  - **Tools:** Tool catalog, effective tools, profile selector, allow/deny overrides
  - **Skills:** Installed skills with toggle/search/batch actions
  - **Channels:** Active channel accounts
  - **Cron:** Agent-specific scheduled jobs with "Run now"

**Critical Interactions:**
- Agent selection loads full detail across all tabs
- File editing with save/reset (draft state in component)
- Tool profile and override management
- Skill toggle (immediate), config save (explicit)
- Spawn agent dialog (ID, provider, model, system prompt, max turns)

**Data Dependencies:**
- `GET /api/agents` — agent list
- `GET /api/agents/{id}` — agent details
- `GET /api/agents/{id}/identity` — cached identity
- `GET /api/agents/{id}/files` — file list
- `GET /api/agents/{id}/tools/effective` — effective tools
- `GET /api/agents/{id}/skills` — skill status per agent
- `PUT /api/agents/{id}/tools/overrides` — update overrides
- `POST /api/agents` — spawn new agent
- `DELETE /api/agents/{id}` — stop agent

---

### /agent/skills — Skill Library `[Source code inspection]`

**Purpose:** Browse installed skills, manage their status, and discover new skills from ClawHub.

**Key Components:**
- Status tabs: All, Ready, Needs Setup, Disabled (with counts)
- Search input for real-time filtering
- Skills grouped by category in collapsible sections, grid layout
- Individual skill cards: name, description, status badge, toggle switch, configure button
- Skill detail modal: full description, dependencies, config form, save/cancel
- ClawHub section: search input, results list, install button, detail modal

**Critical Interactions:**
- Toggle skill enable/disable (immediate, no save needed)
- Configure skill (opens modal with form for API keys etc.)
- Search ClawHub → view detail → install skill
- Skill eligibility: "Ready" (eligible + enabled), "Needs Setup" (enabled but missing deps), "Disabled"

**Data Dependencies:**
- `GET /api/skills/status` — skill status report
- `POST /api/skills/{skillKey}/toggle` — enable/disable
- `POST /api/skills/{skillKey}/config` — save configuration
- `GET /api/clawhub/search?q={query}` — search registry
- `POST /api/clawhub/skills/{slug}/install` — install from registry

**Notable Details:**
- Skills are system-wide (not per-agent; unlike Agent > Agents tab which shows agent-specific skills)
- Eligibility determined by missing deps (env vars, API keys, packages)
- ClawHub is a public registry; installation adds skills to system

---

### /agent/nodes — Node Management `[Source code inspection]`

**Purpose:** Manage distributed execution nodes, device pairing, and execution policies.

**Key Components:**
- **Execution Approvals section:** Target selector (Gateway/Node), agent selector, approval rules editor (form + raw JSON)
- **Bindings section:** Default node binding, per-agent binding table, save/discard
- **Devices section:** Pending requests (approve/reject), paired devices with token management (rotate/revoke)
- **Nodes section:** Live connected node list with metadata, refresh button

**Critical Interactions:**
- Approve/reject device pairing requests
- Rotate/revoke device tokens
- Bind agents to specific execution nodes
- Configure execution approval rules per agent/target
- Node list polls automatically

**Data Dependencies:**
- `client.request("node.list", {})` — node list via WebSocket
- Device pairing API for approve/reject/rotate/revoke
- Config API for bindings and approval rules
- Polling interval for live updates

**Notable Details:**
- TypeScript controller/view architecture (not Blazor)
- Device tokens have role-based scopes with lifecycle tracking
- Approval rules support form mode and raw JSON editing

---

### /agent/dreaming — Memory Consolidation `[Source code inspection]`

**Purpose:** Visual interface for the three-phase memory consolidation engine.

**Key Components:**
- Header: Refresh button, master enable/disable toggle
- **Scene tab** (default):
  - Animated starfield (12 stars, 6s fade cycle)
  - Moon illustration (centered)
  - Sleeping lobster SVG (red gradient, animated during active dreaming)
  - Thought bubble with rotating phrases (16 phrases, 6s rotation)
  - Status panel: memory counts, promotion stats, phase summaries, storage info
- **Diary tab:**
  - Dream diary viewer (markdown from DREAMS.md)
  - Pagination (page through entries)
  - Entries parsed by `<!-- openclaw:dreaming:diary:start/end -->` markers
  - File info and refresh

**Critical Interactions:**
- Toggle dreaming on/off (patches config: `plugins.entries.memory-core.config.dreaming.enabled`)
- Switch between Scene and Diary tabs
- Refresh status and diary independently
- Phase metrics auto-update

**Data Dependencies:**
- `client.request("doctor.memory.status", {})` — dreaming status
- `client.request("doctor.memory.dreamDiary", {})` — diary content
- `client.request("config.patch", {...})` — toggle enabled

**Notable Details:**
- Three phases: Light (recent scans), Deep (scoring/ranking), REM (pattern extraction)
- Each phase has independent cron schedule and enable/disable
- Memory counters: shortTermCount, totalSignalCount, recallSignalCount, promotedToday/Total
- Backend plugin: `memory-core` (configurable)
- Localization keys for all 16 dream phrases

---

### /settings/config — Core Configuration

**Purpose:** Centralized configuration management with form and raw JSON editing modes.

**Key Components:**
- Top action bar: mode toggle (Form/Raw), changes badge, status indicator, save/apply/reset/reload buttons
- Left sidebar: hierarchical category navigation with search
  - **Core:** Environment, Authentication, Updates, Meta, Logging, Diagnostics, CLI, Secrets
  - **AI & Agents:** Agents, Models, Skills, Tools, Memory, Session
  - **Communication:** Channels, Messages, Broadcast, Talk, Audio
  - **Automation:** Commands, Hooks, Bindings, Cron, Approvals, Plugins
  - **Infrastructure:** Gateway, Web, Browser, NodeHost, CanvasHost, Discovery, Media, ACP, MCP
  - **Appearance:** Theme, UI, Setup Wizard
- Main content: form fields (text, number, boolean, select, color picker, array editors) or raw JSON editor

**Critical Interactions:**
- Navigate sections via sidebar → loads fields
- Search filters sections and fields
- Toggle Form/Raw mode (validates on switch)
- Edit field → changes badge updates
- Save (persist to disk), Apply (live reload without persist), Reset, Reload
- Sensitive fields masked with `***`, eye toggle to reveal

**Data Dependencies:**
- `GET /api/config/schema` — field definitions
- `GET /api/config/current` — current values
- `GET /api/config/ui-hints` — labels, help text
- `POST /api/config/save` — persist
- `POST /api/config/apply` — live reload
- WebSocket events: `config.changed`, `schema.updated`

**Notable Details:**
- Schema-driven: all fields, types, validation rules from JSON schema
- No auto-save; explicit save required
- Deprecated fields shown with warning badge
- Deep nested objects render as collapsible sub-sections

---

### /settings/communication — Communication Setup

**Purpose:** Configure all messaging channel integrations and global messaging settings.

**Key Components:**
- Left panel: provider directory (13+ providers with status badges)
- Right panel: provider-specific config form with account tabs
- Providers: Discord, Slack, Telegram, WhatsApp, Signal, iMessage, Mattermost, Microsoft Teams, Feishu, Matrix, IRC, Zalo, Google Chat
- Global settings: Broadcast config, Messages config (prefix, queue, debounce, ack reactions, status reactions), Commands config, Audio/Talk config

**Critical Interactions:**
- Select provider → loads config form
- Add/remove accounts per provider
- Toggle enable/disable per provider
- Test connection (pings provider)
- Configure message acknowledgment reactions and status emoji
- Set up authorization (owner allowlist, per-channel allowlist)

**Data Dependencies:**
- `GET /api/channels/providers` — provider list
- `GET /api/channels/{provider}/config` — provider config
- `POST /api/channels/{provider}/test` — test connectivity
- `GET /api/channels/messages/config` — global messages settings
- WebSocket events: `channel.status-changed`, `channel.config-updated`

**Notable Details:**
- Multi-account support per provider (tabbed interface)
- Sensitive fields encrypted in transit, cannot retrieve previously saved tokens
- Message prefix supports template variables: `{model}`, `{modelFull}`, `{provider}`, `{thinkingLevel}`, `{identity.name}`
- Status reactions: thinking, tool, coding, web, done, error, stall (soft/hard), compacting

---

### /settings/appearance — Theme & UI `[Inferred — needs verification]`

**Purpose:** Theme selection, color mode, and UI customization.

**Key Components:**
- Auth gate (same pattern as Overview)
- Theme selector: Claw, Knot, Dash
- Mode toggle: Light, Dark, System
- Expected: color pickers, font settings, layout density, preview `[Inferred — post-auth content not observed]`

**Data Dependencies:**
- localStorage keys: `openclaw.control.settings.v1.theme`, `openclaw.control.settings.v1.themeMode`
- WebSocket connection for auth

**Notable Details:**
- Full content behind auth gate (currently documented at auth gate level only)
- Theme applied via `data-theme` attribute on `<html>` before first render

---

### /settings/automation — Automation Rules `[Inferred — needs verification]`

**Purpose:** Configure event-driven automation rules with triggers, conditions, and actions.

**Key Components:**
- Auth gate (observed)
- Automation rules list (table or cards) with status indicators `[Inferred — post-auth content not observed]`
- Create new automation form: name, description, trigger config, action config, conditions, enable/disable `[Inferred]`
- Trigger types: event-based (message received, status change, webhook), schedule-based (cron) `[Inferred]`
- Action types: send notification, log event, call webhook, update state `[Inferred]`

**Data Dependencies:**
- WebSocket connection for CRUD operations on rules
- Rules stored server-side in gateway

**Notable Details:**
- Full content behind auth gate (documented at expected-content level)
- Condition logic may support complex expressions (AND, OR, NOT)

---

### /settings/infrastructure — Infrastructure Config `[Inferred — needs verification]`

**Purpose:** Server infrastructure, connections, resources, and deployment settings.

**Key Components:**
- Auth gate (observed)
- Server configuration: address, port, protocol, SSL/TLS `[Inferred — post-auth content not observed]`
- Connection settings: timeouts, retries, pool size `[Inferred]`
- Resource limits: memory, CPU, connections, request queue `[Inferred]`
- Environment configuration: variables, runtime mode, debug toggle
- Deployment: worker instances, load balancer, auto-scaling, health checks

**Data Dependencies:**
- WebSocket connection for config fetch/update
- Server status API for current resource usage

**Notable Details:**
- Full content behind auth gate (documented at expected-content level)
- Infrastructure changes may require service restart
- Some settings may be read-only if managed by orchestration layer

---

### /settings/ai-agents — Providers & Agents `[Source code inspection]`

**Purpose:** Configure AI providers and agent definitions with model parameters.

**Key Components:**
- **Providers tab:** Horizontal card layout per provider
  - Status avatar (green/default), name, description, enable toggle, test button
  - Models table (appears after successful test): Model ID, Display Name
- **Agents tab:** Stacked expandable cards per agent
  - Basic fields: Agent ID, Provider (dropdown), Model (dropdown/text), Max Turns, Auto-Spawn toggle
  - System Prompt textarea
  - Collapsible Model Parameters panel: Temperature (0-2), Top-P (0-1), Top-K (1-200), Max Tokens (1-131072), Context Window (0-1048576), Frequency Penalty (-2 to 2), Presence Penalty (-2 to 2), Repeat Penalty (0-3), Seed, Stop Sequences
  - Add Agent Definition, Save Agents buttons

**Critical Interactions:**
- Enable/disable provider (toggle)
- Test provider → fetches available models, displays in table
- Add agent → new card appended with AutoSpawn=true
- Select provider → model dropdown populates dynamically
- Save providers / Save agents (separate actions)

**Data Dependencies:**
- `GET /api/gateway/config` — provider and agent configs
- `POST /api/gateway/providers/test?name={name}` — test and fetch models
- `PUT /api/gateway/providers` — save provider config
- `PUT /api/gateway/agents` — save agent definitions

**Notable Details:**
- Model parameters default to provider defaults when cleared
- Stop sequences: comma-separated text input, stored as string array
- Context Window field specific to Ollama provider
- Max Turns = 0 means unlimited
- No client-side agent ID uniqueness validation

---

### /settings/debug — Diagnostics `[Source code inspection]`

**Purpose:** Debugging and troubleshooting tools for gateway administration.

**Key Components:**
- **Connection Inspector:** Active WebSocket status, latency, bytes sent/received, last 10 messages
- **State Inspector:** Gateway status (health, uptime, memory), running agents table, active sessions table
- **Error Log Viewer:** Severity/time range/search filters, error table with stack traces, mark-as-resolved
- **Diagnostic Actions:** Test gateway, test provider, test agent spawn, clear cache, export debug bundle (ZIP)
- **System Health:** CPU, memory, disk, network I/O gauges

**Critical Interactions:**
- Reconnect WebSocket
- View agent detail (expand row)
- Terminate session from debug view
- Filter and search error logs, view stack traces, copy to clipboard
- Test connectivity for gateway and individual providers
- Test agent spawn (creates temp agent, sends test prompt, verifies response)
- Export debug bundle (logs, config, metrics, errors as ZIP)

**Data Dependencies:**
- `GET /api/gateway/health` — health check
- `GET /api/gateway/status` — status snapshot
- `GET /api/audit-events` — error logs
- `POST /api/agents/test-spawn` — test agent
- `POST /api/cache/clear` — clear caches
- `GET /api/debug/export-bundle` — download ZIP
- WebSocket events: `agent.spawned`, `agent.terminated`, `error.logged`, `session.started`, `session.closed`

**Notable Details:**
- Admin/local-only access recommended
- Sensitive data auto-redacted from stack traces and debug bundles
- In-memory limits: max 1000 entries for message queue and error log
- Agent spawn test timeout: 10 seconds

---

### /settings/logs — Audit Log Viewer `[Source code inspection]`

**Purpose:** Real-time audit event viewer with filtering and inspection.

**Key Components:**
- Header: "Logs" title, auto-refresh toggle, manual refresh button
- Filter panel: Severity dropdown (Debug/Info/Warning/Error/Critical), Event Type dropdown (tool.invoke/session.create/session.close/policy.deny), Source text input, Search text input, Apply button
- Log grid: Timestamp, Level (color-coded chip), Source, Event, Message, Detail indicator
- Detail panel: Event JSON payload in monospace pre-formatted text

**Critical Interactions:**
- Adjust filters → click Apply to reload (filters don't auto-apply)
- Auto-refresh toggle: 5-second polling timer + SignalR real-time events
- Click detail icon → expand payload panel below grid
- Multi-column sorting (click headers)
- Manual refresh button

**Data Dependencies:**
- `GET /api/audit-events?limit=1000&action={type}&severity={sev}` — fetch events
- SignalR `OnActivityEvent` — real-time event subscription
- Client-side filtering for Source and Search text

**Notable Details:**
- AuditEvent model: `{ Id, Timestamp, Level, Source, EventType, Message, Payload }`
- Severity + Event Type filters are server-side; Source + Search are client-side
- Default sort: Timestamp descending (newest first)
- Max 1000 events loaded; events older than 7 days may be archived
- No explicit pagination; all events in memory with scrolling
- MudDataGrid component (Blazor) with dense, hover-enabled styling

---

## Data & API Architecture

### Known API Patterns

**REST API base:** `/api/`

| Pattern | Examples |
|---------|---------|
| Resource listing | `GET /api/agents`, `GET /api/channels`, `GET /api/instances`, `GET /api/sessions` |
| Resource detail | `GET /api/agents/{id}`, `GET /api/channels/{id}`, `GET /api/sessions/{id}` |
| Resource creation | `POST /api/agents`, `POST /api/channels`, `POST /api/instances` |
| Resource update | `PUT /api/channels/{id}`, `PUT /api/gateway/providers`, `PUT /api/gateway/agents` |
| Resource deletion | `DELETE /api/agents/{id}`, `DELETE /api/channels/{id}`, `DELETE /api/instances/{id}` |
| Action endpoints | `POST /api/instances/{id}/restart`, `POST /api/sessions/:id/terminate` |
| Configuration | `GET /api/config/schema`, `GET /api/config/current`, `POST /api/config/save`, `POST /api/config/apply` |
| Gateway management | `GET /api/gateway/config`, `GET /api/gateway/health`, `GET /api/gateway/status` |
| Testing | `POST /api/gateway/providers/test?name={name}`, `POST /api/agents/test-spawn` |
| Audit/logging | `GET /api/audit-events?limit=N&action=X&severity=Y` |
| ClawHub registry | `GET /api/clawhub/search?q={query}`, `POST /api/clawhub/skills/{slug}/install` |
| Skill management | `GET /api/skills/status`, `POST /api/skills/{key}/toggle` |

### WebSocket Communication Model

**Primary protocol:** SignalR (ASP.NET Core)  
**Hub endpoint:** `/hubs/agent`  
**Gateway protocol:** Custom WebSocket at configurable URL (default `ws://127.0.0.1:18789`)

**SignalR patterns:**
- `StreamChatAsync(agentId, message)` → `IAsyncEnumerable<ChatChunk>` (chat streaming)
- `OnActivityEvent` subscription (real-time audit events)
- Status change notifications (channels, instances, sessions)

**Gateway WebSocket patterns:**
- `client.request("node.list", {})` — fetch node list
- `client.request("doctor.memory.status", {})` — dreaming status
- `client.request("doctor.memory.dreamDiary", {})` — diary content
- `client.request("config.patch", {...})` — update configuration
- Authentication handshake on connection

### State Management Approach

- **Component-level state:** Each page manages its own state (loading, error, data)
- **URL state:** Session key persists in `?session=` parameter, active tab in URL hash
- **localStorage:** Theme preferences, gateway endpoint, language, sidebar collapse state
- **No global store:** Pages fetch data independently on mount; no shared state bus
- **Real-time updates:** WebSocket subscriptions per page; auto-refresh timers where appropriate
- **Dirty tracking:** Config and file editors track unsaved changes with badges and prevention

---

## Recreation Checklist

Build order for recreating the OpenClaw UI from scratch:

### Phase 1: Foundation
1. Set up SPA framework with client-side routing (19 routes)
2. Implement theme system (3 themes x 2 modes, localStorage persistence, pre-render application)
3. Build global shell: persistent sidebar with collapsible sections, breadcrumb, session context
4. Implement WebSocket gateway authentication gate (reusable across auth-gated pages)
5. Set up SignalR client for real-time subscriptions

### Phase 2: Core Pages
6. Build `/chat` — agent selector, message bubbles, SignalR streaming, input handling
7. Build `/settings/config` — two-panel layout, sidebar nav, form mode, raw JSON editor, save/apply/reset
8. Build `/settings/ai-agents` — provider cards with test, agent cards with model parameters
9. Build `/settings/communication` — provider directory, multi-account forms, global messaging settings

### Phase 3: Control Pages
10. Build `/control/overview` — auth gate, gateway access form, snapshots dashboard with gauges
11. Build `/control/channels` — CRUD table with search/filter
12. Build `/control/instances` — CRUD table with real-time metrics, deploy wizard
13. Build `/control/sessions` — session table with terminate/revoke, summary cards
14. Build `/control/usage` — analytics dashboard (spec pending)
15. Build `/control/cron-jobs` — scheduled job management (spec pending)

### Phase 4: Agent Pages
16. Build `/agent/agents` — agent selector, 6-tab detail panel, spawn dialog
17. Build `/agent/skills` — status tabs, skill cards, ClawHub search + install
18. Build `/agent/nodes` — execution approvals, bindings, device pairing, node list
19. Build `/agent/dreaming` — animated scene view, diary viewer, phase controls

### Phase 5: Settings Pages
20. Build `/settings/appearance` — theme picker with preview (behind auth gate)
21. Build `/settings/automation` — rule CRUD with trigger/action/condition builders (behind auth gate)
22. Build `/settings/infrastructure` — server/resource/deployment config (behind auth gate)
23. Build `/settings/debug` — connection/state inspectors, error viewer, diagnostic actions
24. Build `/settings/logs` — audit log grid with filters, auto-refresh, detail panel

### Phase 6: Polish
25. Implement i18n (localization keys, language selector)
26. Add responsive layouts for tablet and mobile
27. Implement keyboard accessibility across all interactive elements
28. Add empty states, loading skeletons, and error handling for all pages
29. Implement snackbar/toast notification system
30. Final theme QA across all 6 theme variants

---

## Gaps & Verification Needed

> **Provenance note:** Only `/chat`, `/settings/config`, `/settings/communication`, and the auth gates on `/settings/appearance`, `/settings/automation`, `/settings/infrastructure`, and `/control/overview` were observed via live UI. All other page specs were derived from source code inspection (`controllers/*.ts`, `views/*.ts`, Blazor `.razor` files). Content marked `[Inferred — needs verification]` or `[Source code inspection]` should be validated against the running application once gateway authentication is available via `openclaw dashboard`.

| Area | Gap | Status |
|------|-----|--------|
| **Auth architecture** | Session key only authenticates `/chat`; admin routes need `openclaw dashboard` token | Corrected in spec; needs token format verification |
| `/control/usage` | No spec exists | Pending — requires gateway auth to explore |
| `/control/cron-jobs` | No spec exists | Pending — requires gateway auth to explore |
| `/control/overview` | Post-auth dashboard content inferred from code | Needs live UI verification |
| `/control/channels` | Entire spec inferred from code patterns | Needs live UI verification |
| `/control/instances` | Entire spec inferred from code patterns | Needs live UI verification |
| `/control/sessions` | Entire spec inferred from code patterns | Needs live UI verification |
| `/agent/agents` | Spec from source code inspection | Needs visual confirmation |
| `/agent/skills` | Spec from source code inspection | Needs visual confirmation |
| `/agent/nodes` | Spec from source code inspection | Needs visual confirmation |
| `/agent/dreaming` | Spec from source code inspection | Needs visual confirmation |
| `/settings/appearance` | Post-auth content not observed | Only auth gate documented; expected content inferred |
| `/settings/automation` | Post-auth content not observed | Only auth gate documented; expected content inferred |
| `/settings/infrastructure` | Post-auth content not observed | Only auth gate documented; expected content inferred |
| `/settings/ai-agents` | Spec from Blazor source code | Needs visual confirmation |
| `/settings/debug` | Spec from source code inspection | Needs visual confirmation |
| `/settings/logs` | Spec from Blazor source code | Needs visual confirmation |
| Auth gate | Shared or per-page? | Appears identical across all gated pages; likely a shared component |
| Gateway token format | Exact token format from `openclaw dashboard` | Needs verification: `#token=<TOKEN>` vs query param |
| Mobile responsive | Sidebar behavior on mobile | Not documented (hamburger menu, etc.) |
| Keyboard shortcuts | Command palette, navigation shortcuts | Not discoverable from static structure |
| Permission model | Admin vs. user role differences | Referenced but not fully specified |
| Error boundaries | Global error handling strategy | Per-page error handling documented; no global boundary spec |
| Offline behavior | What happens when gateway disconnects mid-session | Some pages mention reconnect; no unified offline strategy |
| Routing for `/settings/ai-agents` | Route may be `/settings` with tab selection rather than `/settings/ai-agents` | Spec shows tabbed interface under `/settings` |
| Routing for `/settings/logs` | Spec shows route as `/logs`, sidebar shows under Settings | May have dual routes or redirect |
