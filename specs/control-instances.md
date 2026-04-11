# Control > Instances

> **Verified:** Real UI via authenticated playwright [2026-04-11]

**Route:** `/control/instances`  
**Nav Path:** Control > Instances  
**Description:** Monitor and manage system instances (agent containers, services, or processing nodes), including their status, configuration, and resource usage.

## Layout
The Instances page displays a list/table view of running or available system instances with real-time status monitoring. Likely includes:
- Header with page title and optional "Deploy Instance" or "Scale" button
- Quick stats panel showing total instances, running count, and resource utilization
- Search/filter bar for instance lookup by name, type, or status
- Main table showing instance details with live status indicators
- Optional detail view or right sidebar for instance configuration

## Components

- **Page Header** — Title "Instances" with KPI summary (Total, Running, Failed)
- **Deploy/Add Instance Button** — Primary action to create or deploy new instance
- **Status Overview Cards** — Quick stats showing:
  - Total Instances count
  - Running/Active count
  - Failed/Error count
  - Resource usage (CPU, Memory %)
- **Search Bar** — Text input to filter instances by name, ID, or hostname
- **Filter/Sort Controls** — Dropdowns for:
  - Status filter (Running, Stopped, Failed, Pending)
  - Instance type (Agent, Service, Worker, etc.)
  - Region or environment (if applicable)
  - Sort by (Name, Status, CPU usage, Memory usage, Last Updated)
- **Instances Table** — Columnar view showing:
  - **Name/ID** — Instance identifier with icon indicating type
  - **Type** — Instance category (Agent, Service, Worker, etc.)
  - **Status** — Live status badge (Running, Stopped, Error) with color indicator
  - **CPU Usage** — Percentage or visual bar chart
  - **Memory Usage** — Percentage or visual bar chart
  - **Uptime** — Duration since instance started
  - **Last Updated** — Timestamp of most recent activity
  - **IP Address or Endpoint** — Network location (if public-facing)
  - **Actions** — Button group: View Logs, SSH/Connect, Restart, Stop, Configure, Delete
- **Pagination or Infinite Scroll** — If many instances exist
- **Empty State** — "No instances running" message with deploy button
- **Real-Time Indicators** — Status badges update live without page refresh

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
