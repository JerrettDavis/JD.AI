# Settings > Debug

**Route:** `/settings/debug`  
**Nav Path:** Sidebar > Settings > Debug (future tab)  
**Description:** Diagnostic and troubleshooting tools. Includes WebSocket connection inspection, state debugging, error logs, and test/diagnostic actions.

## Layout

Tabbed or sectioned debug interface with:
- **Connection Inspector** — Active WebSocket connections, connection status, latency
- **State Inspector** — Current gateway state, running agents, active sessions
- **Error Log Viewer** — Recent errors, stack traces, timestamps
- **Diagnostic Actions** — Buttons to test endpoints, clear caches, export debug bundles

(Note: This spec documents the intended Debug interface. Current /settings/debug route requires WebSocket connection to gateway. Full UI exploration pending gateway connection.)

## Components

### Connection Inspector Section

**WebSocket Connections Panel**
- **Active Connection Status** — Badge showing "Connected" (green) or "Disconnected" (red)
- **Connection Details**
  - URL (read-only text)
  - Connection latency / ping time (updated real-time)
  - Bytes sent / received (counters)
  - Last message received (timestamp)
- **Connection Actions**
  - Reconnect button
  - Clear connection cache button

**Message Queue**
- **Queue Depth** — Count of pending messages
- **Last 10 Messages** — Log view showing recent WebSocket messages
  - Columns: Timestamp, Direction (→/←), Message Type, Payload size

### State Inspector Section

**Gateway State Summary**
- **Gateway Status** — Overall status badge (Healthy, Degraded, Error)
- **Uptime** — Duration since last restart
- **Memory Usage** — Current heap memory (MB)
- **Request Count** — Total API requests handled

**Running Agents**
- **Agent List** — Table showing active agents
  - Columns: Agent ID, Provider, Model, Status, Last Activity
  - Rows: One per running agent
  - Row detail: Click to expand and see full agent config

**Active Sessions**
- **Session Count** — Number of active user sessions
- **Session List** — Table showing current sessions
  - Columns: Session ID, User, Created, Last Activity, Actions
  - Actions: View session details, terminate session

### Error Log Viewer Section

**Error Filter Bar**
- **Severity Filter** — Dropdown: All, Debug, Info, Warning, Error, Critical
- **Time Range** — Dropdown or date picker: Last hour, Last 24h, Last 7 days, Custom
- **Search** — Text input for full-text search in error messages
- **Apply Filters** — Button to refresh log view

**Error Log Table**
- **Columns:** Timestamp, Severity, Source, Error Code, Message, Details
- **Rows:** One per error event
- **Row Actions:**
  - View stack trace (expand detail)
  - Copy error to clipboard
  - Mark as resolved (checkbox)

**Error Detail Panel** (collapsible)
- **Stack Trace** — Monospace, pre-formatted text
- **Context** — JSON object showing request/response context
- **Breadcrumb** — Navigation path leading to error

### Diagnostic Actions Section

**Test & Debug Buttons**
- **Test Gateway Connection** — POST /api/health, display response time
- **Test Provider Connectivity** — Dropdown to select provider, button to test each enabled provider
- **Test Agent Spawn** — Spawn temporary test agent, verify it connects and responds
- **Clear In-Memory Cache** — Purge session cache, provider model cache
- **Export Debug Bundle** — Download ZIP containing logs, config snapshot, metrics

**System Health Metrics**
- **CPU Usage** — Percentage bar
- **Memory Usage** — Percentage bar with current/max MB
- **Disk Usage** — Percentage bar
- **Network I/O** — Bytes/sec (in/out)

## Interactions

### Connection Inspector

1. **Reconnect** — Click button → Closes current WebSocket, initiates new connection
   - Success: Green "Connected" badge appears
   - Failure: Red "Disconnected" badge, error message displayed

2. **View Message Detail** — Click message in queue → Expands to show full JSON payload

3. **Clear Cache** — Click button → Confirms, clears message queue history

### State Inspector

1. **View Agent Details** — Click agent row → Expands to show:
   - Full agent configuration (ID, provider, model, system prompt, parameters)
   - Current conversation state if in active session
   - Performance metrics (average response time, tokens used)

2. **Terminate Session** — Click terminate action on session → Confirms, closes session

3. **Refresh State** — Click refresh button → Calls `GetGatewayStatusAsync()` to reload state

### Error Log Viewer

1. **Apply Filters** — Select severity/time range/search text → Click Apply → Table re-renders with filtered results

2. **View Stack Trace** — Click row detail icon → Expands error detail panel showing stack trace and context

3. **Copy to Clipboard** — Click copy icon on error → Error JSON copied to system clipboard, toast confirmation

4. **Mark Resolved** — Check checkbox on error row → Marks as acknowledged (visual: grayed out)

### Diagnostic Actions

1. **Test Gateway Connection** — Click button → Shows loading spinner, then displays:
   - Response time (milliseconds)
   - HTTP status code
   - Result badge (green if successful, red if failed)

2. **Test Provider** — Select provider from dropdown → Click "Test {ProviderName}" → Calls provider health endpoint
   - Success: Green result showing available models
   - Failure: Red result with error message

3. **Test Agent Spawn** — Click button → 
   - Creates temporary agent with default config
   - Waits for connection
   - Sends test prompt "Hello, respond with 'Agent ready'"
   - Verifies response received
   - Cleans up temporary agent
   - Shows result badge (green/red) with response time

4. **Clear Cache** — Click button → Confirmation dialog → Confirms and displays:
   - Session cache cleared (count)
   - Model cache cleared (count)
   - Success toast

5. **Export Debug Bundle** — Click button →
   - Starts download of `debug-bundle-{timestamp}.zip`
   - Contains:
     - `logs.jsonl` — Recent audit logs
     - `config.json` — Current gateway configuration (secrets redacted)
     - `status.json` — Current gateway status snapshot
     - `metrics.json` — Performance metrics for last 1 hour
     - `errors.jsonl` — Error logs from last 7 days

## State / Data

### Data Loaded On Page Init

- `GatewayStatus` — Current status object (health, uptime, memory, request count)
- `Agents` — List of running agents with current state
- `Sessions` — List of active sessions
- `RecentErrors` — Last 100 error events
- `ConnectionMetrics` — WebSocket latency, message counts

### Real-Time Updates

- **WebSocket Latency** — Updated every ping (typically 30 sec)
- **Error Log** — New errors appended in real-time via SignalR subscription
- **Agent Status** — Updated when agents start/stop
- **Memory Usage** — Refreshed every 5-10 seconds

### Loading States

- Initial page load: Skeleton loaders for each section
- Connection loss: Red "Disconnected" badge, "Retry" button becomes prominent
- Test in progress: Loading spinner on test button, disabled until complete

### Error States

- Gateway unreachable: Large error panel "Unable to connect to gateway"
- Partial data failure: Section shows error, other sections load normally
- Export failure: Toast with error message, retry button

## API / WebSocket Calls

- `GET /api/gateway/health` — Gateway health check (connection, status)
- `GET /api/gateway/status` — Gateway status (uptime, memory, agents, sessions)
- `GET /api/audit-events?limit=100&severity={severity}&since={timestamp}` — Error logs
- `POST /api/gateway/health/test` — Test gateway connectivity
- `POST /api/providers/{name}/test` — Test individual provider
- `POST /api/agents/test-spawn` — Spawn and test temporary agent
- `POST /api/cache/clear` — Clear in-memory caches
- `GET /api/debug/export-bundle` — Download debug bundle ZIP
- **WebSocket Events:**
  - `agent.spawned` — New agent started
  - `agent.terminated` — Agent stopped
  - `error.logged` — Error event (real-time notification)
  - `session.started` — New user session
  - `session.closed` — User session ended

## Notes

- **Security Consideration:** Debug page should be restricted to admin users or local-only access
- **Stack Trace Details:** Sensitive information (API keys, passwords) should be automatically redacted from displayed errors
- **Export Bundle Privacy:** Debug bundle should redact all secrets and PII from exported files
- **Real-Time Limits:** Keep in-memory message queue and error log sizes bounded (e.g., max 1000 entries) to prevent memory leaks
- **Test Timeouts:** Agent spawn test should timeout after 10 seconds if no response
- **Provider Test Parallelization:** If testing multiple providers, show progress for each rather than blocking on first
- **Metrics Retention:** Metrics should be aggregated by 5-minute buckets; keep 24 hours of history
- **Error Grouping:** Identical errors (same stack trace) should be collapsed with count indicator
