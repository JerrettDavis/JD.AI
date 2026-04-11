# Control > Overview

**Route:** `/control/overview`  
**Nav Path:** Control > Overview  
**Description:** System-wide status dashboard providing high-level system health, gateway connection status, and operational snapshots.

**Verified:** Live UI [2026-04-11] via session-persistent auth

## Status

**✅ Authenticated Content Available** — Page is protected by authentication and displays full dashboard when authenticated via token.

## Authenticated Content (Live Observed)

The page displays a comprehensive dashboard with gateway connection info, system status snapshots, recent sessions, skills inventory, and operational logs.

### Main Sections

#### 1. Gateway Access Configuration

- **WebSocket URL input** — Gateway connection endpoint (displayed, not editable in this view)
- **Gateway Token input** — Authentication token field (password-masked)
- **Password field** — Optional secondary auth mechanism, labeled "Password (not stored)"
- **Default Session Key dropdown** — Language selection (currently: English, with support for 简体中文, 繁體中文, Português, Deutsch, Español, 日本語, 한국어, Français, Türkçe, Українська, Bahasa Indonesia, Polski)
- **Connect button** — Re-establish connection with current settings
- **Refresh button** — Manually refresh all dashboard data

#### 2. Snapshot Section

System operational metrics (read-only display):

- **STATUS** — Current state indicator (value: "OK")
- **UPTIME** — Time since last start (value: "19h")
- **TICK INTERVAL** — Heartbeat/polling interval (value: "30s")
- **LAST CHANNELS REFRESH** — Last successful channel config reload (value: "just now")
- **COST** — Current operational cost summary (value: "$0.00", "0 tokens · 28 msgs")

#### 3. Operational Counters

Quick-view cards showing current system state:

- **SESSIONS** — Active session count (value: "10", "Recent session keys tracked by the gateway")
- **SKILLS** — Active skills vs total (value: "46/71", "46 active")
- **CRON** — Scheduled jobs status (value: "12 jobs", "2 failed")

#### 4. Recent Sessions List

Chronological listing of active/recent sessions with details:

- Session key identifier
- Associated model name
- Time elapsed since last activity (e.g., "58m ago", "17h ago", "18h ago")

Example entries observed:
- `heartbeat` (qwen3.5b:9b, 58m ago)
- `discord:679904321848344624#jarvis` (gpt-5.3-codex, 17h ago)
- `discord:g-1466622912307007690-heartbeat` (qwen3.5b:9b, 18h ago)
- `webchat:679904321848344624#jarvis` (MiniMax-M2.7, 18h ago)
- `webchat:679904321848344624#jarvis` (gpt-5.4, 18h ago)

#### 5. Skills Dependencies Alert

Attention banner showing skills with missing dependencies:
- "Skills with missing dependencies: camsnap, discord, model-usage +8 more"

#### 6. Cron Jobs Failed Alert

Alert indicating failed scheduled jobs:
- "2 cron jobs failed: todoist-check, ralph loop nudge"

#### 7. Event Log (Recent Activity)

Chronological event timeline with JSON data:
- **Example entry:** Health check event at 2:50:29 PM with status info, channel configurations, and health data

#### 8. Gateway Logs

Raw log entries in JSON format showing system activity:
- Log entries with module names, channel IDs, filtering reasons, runtime info
- Full structured metadata including timestamps, log levels, file paths, and methods
- Example: Discord auto-reply logs showing message filtering decisions

## Layout

The page likely follows a **dashboard grid layout**:

- **Top section:** Gateway Access configuration card (full width)
- **Middle section:** Snapshots grid displaying key metrics (2-4 columns, responsive)
- **Bottom section:** System Events timeline or Quick Actions (full width or side panel)
- **Side panel (optional):** Collapsible detailed metrics or status history

## Components Detail

### Status Indicators

- **Color-coded badges:** Green (Connected/Healthy), Yellow (Degraded/Warning), Red (Disconnected/Error)
- **Animated connection state:** Pulsing or animated icon when attempting to connect
- **Tooltip support:** Hover for detailed status descriptions

### Gauges and Charts

- **Uptime counter:** Uses monospace font for clarity, formatted as "Xd Xh Xm Xs"
- **Resource gauges:** Radial or linear progress indicators with percentage labels
- **Queue depth:** Numeric display with mini chart or bar showing trend (increasing/stable/decreasing)

### Forms

- **Input fields:** Standard text inputs with clear labels and placeholder text
- **Dropdowns:** Language selection with common locales (en-US, es-ES, fr-FR, de-DE, ja-JP, etc.)
- **Buttons:** Primary action buttons (Connect, Refresh) with loading states during async operations

## Interactions

### Authentication Flow
- Enter WebSocket URL, optional token, optional password → Click Connect → Validate credentials and establish WS connection
- Click token/password visibility toggles → Reveal/mask sensitive input values
- On successful auth → Hide login gate, show authenticated dashboard content

### Dashboard Interactions
- Click Refresh button → Poll gateway for latest snapshots, update all displayed metrics
- Click WebSocket URL field (if editable) → Modify connection endpoint and reconnect
- Click event in timeline (if present) → Expand to show full event details
- Hover on metrics → Show tooltips with explanations
- Click gauge or metric → (Optional) Navigate to detailed analytics page

### Automatic Updates
- WebSocket maintains real-time connection with periodic heartbeats
- Snapshots auto-refresh on configurable interval (e.g., every 5-10 seconds)
- Status indicators update immediately on connection state change

## State / Data

### Gateway Connection State
- **Not connected** → Show authentication gate
- **Connecting** → Show connecting indicator with spinner
- **Connected** → Display authenticated dashboard
- **Failed** → Show error message and reconnect button

### Persisted Preferences
- **WebSocket URL:** Stored in localStorage under `openclaw.control.gateway.endpoint` or `openclaw.control.settings.v1.*`
- **Default Session Key:** Stored in localStorage or browser session storage
- **Language preference:** Stored in localStorage under `openclaw.control.settings.v1.language`
- **Theme preference:** Controlled separately (see Settings > Appearance)

### Real-time Data
- **Snapshots:** Fetched from gateway via WebSocket on page load and at regular intervals
- **Status indicators:** Updated on WebSocket message receipt (no polling)
- **Events:** Received via WebSocket push notifications

## API / WebSocket Calls

### Authentication
- **WebSocket Initial Connection:** Handshake message with gateway token and password credentials
- **Response:** Acknowledgment with session ID or error (Auth Failed)

### Data Fetching
- **Get Snapshots:** Message to retrieve current system snapshots (status, uptime, metrics)
- **Subscribe to Status Updates:** Ongoing subscription to real-time status changes via WebSocket
- **Get Recent Events:** Fetch recent system event log (if event timeline included)

### Configuration Updates
- **Update Default Session Key:** POST or WebSocket message to persist new default session
- **Update Language:** POST to update locale preference
- **Update Gateway Endpoint:** Disconnect current connection, establish new WebSocket to new endpoint

### Refresh Actions
- **Refresh Snapshots:** Explicit request to re-fetch all dashboard metrics
- **Reconnect Gateway:** Close current WS connection and establish new connection

## Notes

- **Authentication Gate:** Until the user authenticates via WebSocket, no dashboard content is visible. This matches the behavior of other protected pages (Settings > Appearance, etc.).
- **Real-time vs. Polled Data:** Status indicators should update in real-time via WebSocket. Heavier metrics (resource usage, queue depth) may be polled periodically to reduce overhead.
- **Responsiveness:** The dashboard should adapt to mobile/tablet screens; metrics grid may collapse to single column on smaller viewports.
- **Error Handling:** Connection failures, timeouts, or gateway unreachability should display user-friendly error messages with reconnect options.
- **Empty States:** If no agents/sessions/channels are active, display helpful prompts to guide users to setup pages (Control > Channels, Agent > Agents).
- **Locale Support:** Language selector should support at least English, Spanish, French, German, Japanese; backend must respond with translations for all UI strings.
- **Accessibility:** All interactive elements should be keyboard-navigable; status indicators should include aria-labels for screen readers.
