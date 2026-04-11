# OpenClaw UI — Feature Matrix

## Feature Categories

1. **Real-Time Communication** — WebSocket streaming, SignalR, live updates
2. **Agent Management** — Spawn, configure, inspect, delete agents
3. **Skill Management** — Install, enable/disable, configure, discover skills
4. **Channel Integration** — Multi-provider messaging (Discord, Slack, Telegram, etc.)
5. **Configuration Management** — Form + raw JSON editing, schema-driven settings
6. **Monitoring & Observability** — Status dashboards, metrics, health indicators
7. **Session & Access Control** — Session tracking, authentication, device pairing
8. **Scheduling** — Cron jobs, timed automation rules
9. **Memory & Learning** — Dream phases, memory consolidation, signal tracking
10. **Node & Device Management** — Distributed execution, device pairing, token lifecycle
11. **Debugging & Diagnostics** — Error logs, state inspection, debug bundles
12. **Theming & Customization** — Theme selection, color modes, layout density

## Matrix

| Feature | Chat | Overview | Channels | Instances | Sessions | Usage | Cron | Agents | Skills | Nodes | Dreaming | Config | Comms | Appear | Auto | Infra | AI&Agents | Debug | Logs |
|---------|------|----------|----------|-----------|----------|-------|------|--------|--------|-------|----------|--------|-------|--------|------|-------|-----------|-------|------|
| **Real-time streaming** | X | X | | X | X | | | | | X | X | | | | | | | X | X |
| **SignalR/WebSocket** | X | X | X | X | X | | | | | X | X | X | X | | | | | X | X |
| **CRUD operations** | | | X | X | X | | | X | X | X | | X | X | | X | | X | | |
| **Table/grid view** | | | X | X | X | | | | | | | | | | | | | X | X |
| **Search/filter** | | | X | X | X | | | | X | | | X | X | | | | | X | X |
| **Status badges** | | X | X | X | X | | | X | X | X | X | X | X | | | | X | X | X |
| **Form editing** | | X | | | | | | X | X | X | | X | X | X | X | X | X | | |
| **Raw JSON editing** | | | | | | | | | | X | | X | | | | | | | |
| **Auth gate** | | X | | | | | | | | | | | | X | X | X | | | |
| **Agent selector** | X | | | | | | | X | | | | | | | | | X | | |
| **Provider config** | | | | | | | | | | | | | X | | | | X | | |
| **Multi-account** | | | | | | | | | | | | | X | | | | | | |
| **Bulk actions** | | | | | X | | | | X | | | | | | | | | | |
| **Confirmation dialogs** | | | X | X | X | | | X | | X | | | | | | | | | |
| **Detail panel/modal** | | | X | X | X | | | X | X | X | | | | | | | | X | X |
| **Pagination** | | | X | X | X | | | | | | X | | | | | | | | |
| **Auto-refresh** | | X | | X | X | | | | | X | | | | | | | | X | X |
| **Metrics/gauges** | | X | | X | | | | | | | X | | | | | | | X | |
| **Token management** | | | | | | | | | | X | | | | | | | | | |
| **Device pairing** | | | | | | | | | | X | | | | | | | | | |
| **Animated visuals** | | | | | | | | | | | X | | | | | | | | |
| **Markdown rendering** | | | | | | | | | | | X | | | | | | | | |
| **Cron scheduling** | | | | | | | X | X | | | X | X | | | | | | | |
| **Theme switching** | | | | | | | | | | | | | | X | | | | | |
| **Export/download** | | | | | | | | | | | | | | | | | | X | |
| **Test connectivity** | | X | | | | | | | | | | | X | | | X | X | X | |
| **Sensitive field masking** | | X | | | | | | | | | | X | X | | | | | | |
| **ClawHub registry** | | | | | | | | | X | | | | | | | | | | |
| **Execution approvals** | | | | | | | | | | X | | | | | | | | | |
| **Agent-node bindings** | | | | | | | | | | X | | | | | | | | | |

## Unique Features by Page

### /chat
- Message streaming with cursor indicator (blinking bar during agent response)
- User/agent message bubble layout with timestamps
- Enter-to-send, Shift+Enter for newline
- In-memory only chat (no persistence)

### /control/overview
- Gateway access configuration (WebSocket URL, token, password)
- System snapshots grid (uptime, tick interval, refresh timestamps)
- Health indicators (Healthy/Degraded/Critical)
- Language selector for UI localization
- CPU/memory resource gauges

### /control/channels
- Discord channel management with server/guild association
- Channel status monitoring (active/inactive/error)
- Last activity tracking per channel

### /control/instances
- Instance deployment wizard (type, region, resources, env vars)
- Real-time CPU/memory monitoring per instance
- Instance lifecycle actions (restart, stop, SSH/connect)
- Status overview cards (total, running, failed counts)

### /control/sessions
- Session termination and revocation
- Platform/device detection with geolocation
- "Terminate All Sessions" bulk action
- Session duration tracking with live updates

### /agent/agents
- Tabbed agent inspection (Overview, Files, Tools, Skills, Channels, Cron)
- Agent file editor with save/reset
- Tool access control profiles with allow/deny overrides
- Agent-specific skill toggles and batch actions
- Spawn agent dialog

### /agent/skills
- ClawHub skill registry search and one-click install
- Skill eligibility system (Ready/Needs Setup/Disabled with reasons)
- Skill configuration forms for API keys and credentials
- Category-grouped collapsible skill cards

### /agent/nodes
- Execution approval rules (gateway vs. node targets)
- Agent-to-node binding configuration
- Device pairing workflow (pending requests, approve/reject)
- Token lifecycle management (rotate, revoke)
- Node polling with live refresh

### /agent/dreaming
- Animated dream scene (starfield, moon, sleeping lobster SVG)
- Three-phase memory consolidation (Light, Deep, REM)
- Dream diary viewer with paginated markdown entries
- Memory signal counters and promotion stats
- Rotating dream phrases (16 localized phrases)

### /settings/config
- Two-panel layout with hierarchical sidebar navigation
- Form mode and raw JSON mode with toggle
- 6 categories, ~30 configuration sections
- Changes badge with unsaved count
- Config file opener (external editor)
- Schema-driven validation with error highlighting

### /settings/communication
- 13+ messaging provider support (Discord, Slack, Telegram, WhatsApp, Signal, iMessage, etc.)
- Multi-account management per provider (tabbed)
- Provider-specific configuration forms
- Global message settings (prefix, queue, debounce, acknowledgment reactions)
- Status reaction emoji customization (thinking, tool, coding, web, done, error, stall)
- Text-to-speech configuration
- Authorization and command access policies

### /settings/appearance
- Theme selection: Claw (default), Knot, Dash
- Mode: Light, Dark, System (auto-detect)
- Resolved theme names: light, dark, openknot, openknot-light, dash, dash-light
- localStorage persistence with pre-render application

### /settings/automation
- Automation rule CRUD (create, read, update, delete)
- Trigger types: event-based and schedule-based
- Action types: notification, logging, webhook, state update
- Condition/filter logic builder (AND, OR, NOT)
- Rule enable/disable toggle

### /settings/infrastructure
- Server configuration (address, port, protocol, SSL)
- Connection settings (timeouts, retries, pool size)
- Resource limits (memory, CPU, connections)
- Environment variable management
- Deployment scaling policies

### /settings/ai-agents
- Provider card layout with enable toggle and test button
- Model discovery (test provider to fetch available models)
- Agent definition cards with full parameter control
- 10 model parameters (temperature, top-p, top-k, max tokens, context window, etc.)
- System prompt configuration per agent
- Auto-spawn toggle per agent

### /settings/debug
- WebSocket connection inspector (latency, bytes, messages)
- Gateway state inspector (agents, sessions, memory)
- Error log viewer with severity filtering and stack traces
- Diagnostic actions: test gateway, test provider, test agent spawn, clear cache, export debug bundle
- System health metrics (CPU, memory, disk, network)

### /settings/logs
- Audit event log viewer with 4 filter dimensions (severity, event type, source, search)
- Auto-refresh toggle with 5-second polling
- SignalR real-time event subscription
- Event detail panel with full JSON payload
- Multi-column sortable data grid
- Severity color coding (Error=red, Warning=orange, Info=blue, Debug=gray)

## Cross-Cutting Features

| Feature | Pages Where Present |
|---------|-------------------|
| WebSocket/SignalR communication | Chat, Overview, Channels, Instances, Sessions, Nodes, Dreaming, Config, Communication, Debug, Logs |
| Authentication gate | Overview, Appearance, Automation, Infrastructure |
| Status badges (color-coded) | Overview, Channels, Instances, Sessions, Agents, Skills, Nodes, Dreaming, Config, Communication, AI & Agents, Debug, Logs |
| Search/filter controls | Channels, Instances, Sessions, Skills, Config, Communication, Debug, Logs |
| Table/data grid views | Channels, Instances, Sessions, Debug, Logs |
| Form-based configuration | Overview, Agents, Skills, Nodes, Config, Communication, Appearance, Automation, Infrastructure, AI & Agents |
| Sensitive field masking | Overview, Config, Communication |
| Real-time auto-updates | Overview, Instances, Sessions, Nodes, Dreaming, Debug, Logs |
| Confirmation dialogs (destructive actions) | Channels, Instances, Sessions, Agents, Nodes |
| Snackbar/toast notifications | Chat, Agents, Skills, AI & Agents |
| Empty state messaging | Chat, Channels, Instances, Sessions, Agents, Skills, Nodes, Dreaming, Config, Logs |
| Loading skeletons | Agents, Skills, Config, Logs |
| Theme system (data-theme attribute) | All pages (applied globally via `<html>` element) |
| Session context parameter (?session=) | All pages (global URL parameter) |
