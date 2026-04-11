# Control > Instances

> **Verified:** Live UI [2026-04-11] via gateway token

**Route:** `/control/instances`  
**Nav Path:** Control > Instances  
**Description:** Monitor and manage connected system instances (clients, gateways, and presence beacons) with real-time connection status and metadata.

## Live UI Observations

**Page Title:** "Instances"  
**Subtitle:** "Connected clients and nodes."

### Main Section: "Connected Instances"
- **Description:** "Presence beacons from the gateway and clients."
- **Refresh Button:** Manual sync trigger
- **Data Table Columns:**
  - Instance name (e.g., "JDH-PRO-05", "openclaw-control-ui")
  - IP address (e.g., "169.254.83.107")
  - Role (e.g., "gateway", "control-ui")
  - Windows version (e.g., "windows 10.0.26200")
  - Platform type (e.g., "Windows", "x64")
  - Version (e.g., "2026.4.8")
  - Last activity (e.g., "just now")
  - Last input (e.g., "n/a", actual time)
  - Connection reason (e.g., "self", "connect", "disconnect")
  - Client info (e.g., "openclaw-control-ui", "webchat control-ui")
  - Scopes (e.g., "5 scopes")
  - Platform (e.g., "Win32")

### Live Data Example
Currently connected:
- **JDH-PRO-05** (169.254.83.107) — gateway@2026.4.8 — just now
- Multiple **openclaw-control-ui** (webchat/control-ui operator) entries showing connection history with timestamps

### Real-Time Behavior
- Timestamp shows "just now" for recent activity
- Multiple connection/disconnection events logged with reasons
- List updates with each new beacon from gateway

## Components

- **Page Header** — Title "Instances" with subtitle
- **Refresh Button** — Manual update trigger
- **Connected Instances Section** — Live beacon table
  - Shows all active and recently-active instances
  - Real-time connection status
  - IP address tracking
  - Version and platform metadata
  - Connection event logging (connect/disconnect/self reasons)

## Interactions

- **Refresh** — Force sync of instance list from gateway
- **Row Selection** — May reveal details or connection logs
- **Real-Time Updates** — Beacons update without full page refresh

## Interactions

- **Click Instance Row** → Opens detail panel or modal showing:
  - Full configuration (environment variables, instance type, region)
  - Real-time metrics (CPU, memory, network, disk)
  - Recent logs and event history
  - Restart, stop, scale, or delete options
- **Click Deploy/Add Instance** → Opens form/wizard to:
  - Select instance type (Agent, Service, Worker, etc.)
  - Choose deployment region or environment
  - Configure resource allocation (CPU, memory limits)
  - Set environment variables
  - Submit to create
- **Click Restart** → Shows confirmation, then restarts instance
- **Click Stop** → Shows confirmation, then stops instance (may keep configuration)
- **Click Delete** → Shows confirmation of data loss, then removes instance
- **Click View Logs** → Opens log viewer/tail showing recent instance output
- **Click SSH/Connect** → May open a terminal or show connection string
- **Search Input** → Real-time filtering of table rows
- **Status Badge** → Hovering may show detailed error message or reason for failure
- **CPU/Memory Bar** → Hovering may show detailed resource metrics (detailed values, historical trend)

## State / Data

- **Initial Load** — Fetches all instances and displays table
- **Loading State** — Skeleton/spinner while instances are being fetched
- **Real-Time Updates** — Status and metrics update live (likely via WebSocket)
- **Empty State** — When zero instances exist
- **Error State** — If instance API is unavailable
- **Filtered View** — Table updates as filters are applied
- **Confirmation Dialogs** — Before destructive actions (delete, stop)
- **Detail Panel** — May be sticky or dismiss when clicking elsewhere

## API / WebSocket Calls

Expected endpoints:
- `GET /api/instances` — Fetch all instances with current status and metrics
- `GET /api/instances/:id` — Fetch single instance details
- `POST /api/instances` — Create/deploy new instance
- `PATCH /api/instances/:id` — Update instance configuration
- `POST /api/instances/:id/restart` — Restart instance
- `POST /api/instances/:id/stop` — Stop instance
- `DELETE /api/instances/:id` — Delete instance
- `GET /api/instances/:id/logs` — Fetch instance logs (may support streaming)
- `GET /api/instances/:id/metrics` — Fetch historical metrics (CPU, memory trends)

WebSocket messages (for real-time updates):
- `instance.created` — New instance deployed
- `instance.status_changed` — Instance status changed (Running, Stopped, Error)
- `instance.metrics_updated` — CPU/memory metrics updated
- `instance.log_entry` — New log entry added to instance
- `instance.deleted` — Instance removed

## Notes

- Verify real-time update mechanism (WebSocket vs. polling)
- Check if instances can be grouped or sorted by region/environment
- Confirm whether table has selectable rows for bulk actions (bulk restart, delete)
- Verify presence of resource limit indicators (if instance is approaching CPU/memory limits)
- Check if logs are shown in real-time or via separate view
- Determine if instance configuration is editable after creation
- Verify whether "Last Updated" refers to instance activity or metrics refresh
- Check if there's a "Scale" or "Duplicate" action for quickly creating similar instances
