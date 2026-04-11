# Control > Overview

**Route:** `/control/overview`  
**Nav Path:** Control > Overview  
**Description:** System-wide status dashboard providing high-level system health, gateway connection status, and operational snapshots.

## Status

**⚠️ Authentication Required** — This page requires gateway authentication via WebSocket connection before the UI content is accessible. A login gate similar to other Settings pages presents the authentication interface.

## Authentication Gate (Current State)

The Overview page is protected by a login gate with the following fields:

### Layout
- Centered card-based form on a full-screen background
- Header with OpenClaw logo and "Gateway Dashboard" subtitle

### Components

- **WebSocket URL input** — Text field with placeholder `ws://127.0.0.1:18789`, stores the gateway connection endpoint
- **Gateway Token input** — Password field with placeholder `OPENCLAW_GATEWAY_TOKEN (optional)`, allows optional token authentication
- **Password input** — Password field labeled "Password (not stored)", secondary auth mechanism
- **Token visibility toggle button** — Icon button to show/hide Gateway Token field
- **Password visibility toggle button** — Icon button to show/hide Password field
- **Connect button** — Submit button to establish WebSocket connection and proceed to authenticated content

## Expected Content (Post-Authentication)

Once authenticated, the Overview page should present the following major sections:

### Gateway Access Section

Configuration and connection details for the OpenClaw gateway:

- **WebSocket URL display** — Current connected gateway endpoint (read-only or editable)
- **Gateway Token display** — Masked display of active token (for reference, not editable)
- **Password field** — Optional password field for session authentication
- **Default Session Key input** — Text field specifying the default session context (e.g., "agent:jdai-default:discord:channel:...")
- **Language selector** — Dropdown menu for UI localization language selection
- **Connect button** — Re-establish or update gateway connection
- **Refresh button** — Manually refresh connection status and reload dashboard data

### Snapshots Section

Real-time operational metrics and system health indicators:

- **Status indicator** — Current gateway connection status (Connected/Disconnected/Connecting/Failed)
- **Uptime counter** — Duration since last restart or connection establishment (e.g., "5d 12h 34m")
- **Tick Interval display** — Polling/heartbeat interval for status updates (e.g., "500ms")
- **Last Channels Refresh timestamp** — Last successful channel configuration reload
- **Last Sessions Refresh timestamp** — Last successful session data synchronization
- **Gateway Health indicator** — Overall system health status (Healthy/Degraded/Critical)
- **Message Queue Status** — Current message queue depth and processing rate
- **Active Sessions count** — Number of currently active sessions across all agents
- **Active Agents count** — Number of running agent instances
- **Connected Channels count** — Number of integrated communication channels
- **Memory Usage gauge** — Current memory consumption (absolute and percentage)
- **CPU Usage gauge** — Current CPU utilization (percentage)

### System Events / Recent Activity (Optional)

If included, this section displays recent operational events:

- **Event timeline** — Chronological list of recent system events (connection changes, errors, configuration updates)
- **Event details** — Expandable entries showing timestamp, severity (Info/Warning/Error), and message
- **Clear log button** — Option to clear event history

### Quick Actions (Optional)

If included, shortcuts for common administrative tasks:

- **Restart Gateway button** — Trigger gateway restart
- **View Logs button** — Navigate to Settings > Logs
- **Configuration button** — Navigate to Settings > Config
- **Channel Management button** — Navigate to Control > Channels

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
