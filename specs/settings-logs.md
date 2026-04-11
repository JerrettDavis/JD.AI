# Settings > Logs
n> **Verified:** Real UI via authenticated playwright [2026-04-11]

>**Verified:** Gateway authentication required. Session params do not bypass WebSocket auth for admin panel.

**Route:** `/logs`  
**Nav Path:** Sidebar > Settings > Logs  
**Description:** Real-time audit log viewer with filtering, search, and event detail inspection.

## Layout

Single-page scrollable layout with:
- **Header** — Title "Logs", auto-refresh toggle, manual refresh button
- **Filter Panel** — Horizontal row of filter controls
- **Log Grid** — Data table showing audit events in chronological order
- **Detail Panel** (conditional) — Expanded event detail when a log entry is selected

## Components

### Page Header

**Title & Controls Row**
- **Page Title** — "Logs" (Heading 4)
- **Auto-refresh Toggle** — Boolean switch labeled "Auto-refresh", default off
- **Refresh Button** — Icon button (circular refresh icon), triggers manual reload

### Filter Panel

Horizontal grid with dense spacing and light background.

**Severity Filter**
- **Label:** "Severity"
- **Type:** Dropdown select
- **Options:** Debug, Info, Warning, Error, Critical, (blank for all)
- **Default:** Blank (show all)

**Event Type Filter**
- **Label:** "Event Type"
- **Type:** Dropdown select
- **Options:**
  - tool.invoke
  - session.create
  - session.close
  - policy.deny
  - (blank for all)
- **Default:** Blank (show all)

**Source Filter**
- **Label:** "Source"
- **Type:** Text input
- **Placeholder:** "Filter by source (e.g. 'agent-default', 'channel-discord')"
- **Clearable:** Yes

**Search Filter**
- **Label:** "Search"
- **Type:** Text input with search icon adornment
- **Placeholder:** "Search by message, event type, source"
- **Clearable:** Yes

**Apply Button**
- **Text:** "Apply"
- **Action:** Triggers `LoadAsync()` with current filter values
- **Position:** Right-aligned, below search field

### Log Grid

Data grid table with dense, hover-enabled styling.

**Columns:**
1. **Timestamp** — DateTime formatted as "g" (localized short/long format)
   - Sortable, ascending by default
2. **Level** — Severity badge
   - Chip/badge with color coding: Error (red), Warning (orange), Info (blue), Debug (default)
   - Text content: lowercase severity name (e.g., "error", "info")
3. **Source** — String
   - Source identifier (e.g., "agent-default", "channel-discord")
   - Sortable
4. **Event** — String
   - Event type identifier (e.g., "tool.invoke", "session.create")
   - Sortable
5. **Message** — String
   - Human-readable event message
   - Truncated if very long
   - Sortable
6. **Detail Indicator** (conditional) — Icon button
   - Visibility icon, only shows if event has payload data
   - Click to toggle detail panel

**Row Behavior:**
- Click row → Highlights row, may open detail panel if detail icon exists
- Multi-column sort: Click column header to sort by that column (toggles ascending/descending)
- Hover state: Subtle background highlight

**Empty State:**
- When no logs match filters:
  - Centered icon (Note/Event icon, opacity 0.3)
  - Text: "No log events found."

**Loading State:**
- Skeleton rows (5 rows × 5 columns) while data loads

### Detail Panel

Appears below grid when a log entry with payload is selected.

**Header Row**
- **Title:** "Event Detail" (Heading 6)
- **Close Button** — Icon button (X icon), clears selection and hides panel

**Payload Display**
- **Format:** Monospace, pre-formatted text
- **Content:** Full JSON payload
- **White-space:** Preserved (pre-wrap)
- **Font Size:** 0.85rem
- **Overflow:** Scrollable if very large

## Interactions

### Filter & Load

1. **Adjust Filters** — User changes any filter (Severity, Event Type, Source, Search)
   - Filters update state immediately (no debounce)
   - Grid does NOT auto-refresh

2. **Apply Filters** — Click "Apply" button → Calls `LoadAsync()`
   - `_loading` flag set to true
   - Grid shows skeleton loaders
   - Calls `Api.GetAuditEventsAsync(limit: 1000, action: eventTypeFilter, severity: severityFilter)`
   - Results filtered client-side by Source and Search text
   - `_filteredEvents` updated, grid re-renders
   - `_loading` flag cleared

3. **Clear Filter** — Click X icon on clearable filter → Filter value cleared immediately, but grid not updated until Apply clicked

4. **Manual Refresh** — Click refresh icon button → Calls `LoadAsync()` with current filter state (same as Apply)

### Auto-Refresh

1. **Enable Auto-Refresh** — Toggle switch on
   - Starts a timer that calls `LoadAsync()` every 5 seconds
   - Timer continues until user disables auto-refresh or navigates away

2. **Disable Auto-Refresh** — Toggle switch off
   - Timer stops, no further auto-refreshes

3. **Real-Time Updates** — WebSocket event received → If `_autoRefresh` is true
   - Calls `LoadAsync()` immediately (bypasses timer)
   - Grid updates with new events

### Log Inspection

1. **View Detail** — Click visibility icon on log row
   - Payload panel appears below grid
   - If same row clicked again, panel hides

2. **Change Selection** — Click detail icon on different row
   - Previous panel closes
   - New panel opens for selected row

3. **Close Detail** — Click X button in detail panel header
   - Panel hides, selection cleared

4. **Copy Detail** (future enhancement) — Right-click or copy button in detail panel
   - Full payload JSON copied to clipboard
   - Toast confirmation shown

### Sort & Paginate

1. **Sort by Column** — Click column header (Timestamp, Level, Source, Event, Message)
   - Ascending first, then descending, then no sort
   - Visual indicator (▲/▼) shown in header
   - Multi-column sort supported (click multiple headers)

2. **Dense Hover** — Hover over log row
   - Subtle background color change
   - Indicates row is interactive

(Pagination: If log count exceeds grid capacity, automatic scrolling; no explicit pagination buttons)

## State / Data

### Data Loaded On Page Init

- `_allEvents` — Array of all audit events (max 1000 from API)
- `_filteredEvents` — Filtered/searched subset of `_allEvents`
- `_loading` — Boolean, true while fetching
- `_autoRefresh` — Boolean, default false

### Filter State

- `_severityFilter` — Selected severity (null for all)
- `_eventTypeFilter` — Selected event type (null for all)
- `_sourceFilter` — Source search text
- `_searchText` — Full-text search text

### Detail State

- `_selectedEventId` — ID of selected event (null if no selection)

### Real-Time Updates

- **SignalR Subscription** — Listens to `OnActivityEvent` from SignalR service
- **Update Trigger** — New activity event received → If `_autoRefresh` is true, call `LoadAsync()` and `StateHasChanged()`

### Timer

- `_timer` — Repeating timer, interval 5 seconds
- **On Dispose:** Timer is disposed and SignalR event handler unsubscribed

## API / WebSocket Calls

- `GET /api/audit-events?limit={limit}&action={eventType}&severity={severity}` — Fetch audit events
  - **Parameters:**
    - `limit` — Max events to return (1000)
    - `action` — Event type filter (optional, e.g., "tool.invoke")
    - `severity` — Severity filter (optional, e.g., "error")
  - **Returns:** `AuditEvent[]`
    - Fields: `Id` (string), `Timestamp` (DateTime), `Level` (string), `Source` (string), `EventType` (string), `Message` (string), `Payload` (JSON string)

- **WebSocket Event — ActivityEvent:** Received via SignalR `OnActivityEvent`
  - Triggers auto-refresh if enabled
  - Contains timestamp and new event details

## Notes

- **Event Model (AuditEvent):**
  ```csharp
  public class AuditEvent
  {
      public string Id { get; set; }
      public DateTime Timestamp { get; set; }
      public string Level { get; set; }         // "debug", "info", "warning", "error", "critical"
      public string Source { get; set; }        // e.g., "agent-default", "channel-discord"
      public string EventType { get; set; }     // e.g., "tool.invoke", "session.create"
      public string Message { get; set; }       // Human-readable summary
      public string Payload { get; set; }       // JSON string with full details
  }
  ```

- **Filter Behavior:**
  - Severity and Event Type filters are server-side (passed to API)
  - Source and Search filters are client-side (applied after API response)
  - Combining multiple filters uses AND logic (all conditions must match)

- **Sort Behavior:**
  - Default sort: Timestamp descending (newest first)
  - MudDataGrid handles multi-column sort automatically
  - Sort state is client-side; API always returns events in insertion order

- **Auto-Refresh Polling:**
  - Timer interval: 5 seconds
  - No exponential backoff; retries on failure
  - On connection failure, timer continues running; errors logged to snackbar

- **Event Retention:**
  - API returns max 1000 events
  - Events older than 7 days may be archived
  - Detail payload can be very large (JSON); displayed as-is with no truncation

- **Severity Color Mapping:**
  - Error → Color.Error (red)
  - Warning/Warn → Color.Warning (orange)
  - Info → Color.Info (blue)
  - Debug/Default → Color.Default (gray)

- **Empty Placeholder:**
  - Shown when `_filteredEvents.Length == 0`
  - Icon: Material icon "EventNote", size Large, opacity 30%
  - Text: "No log events found." (secondary color)

- **SignalR Integration:**
  - Injected `SignalRService` manages WebSocket connection
  - Event handler registered in `OnInitializedAsync`, unregistered in `Dispose`
  - Handler is lambda that calls `LoadAsync()` if `_autoRefresh` is true

- **Performance Considerations:**
  - Grid is dense (no extra padding) to show more rows per screen
  - Hover effect is subtle to avoid distraction
  - Payload rendering uses monospace to preserve JSON formatting
  - No lazy-loading; all 1000 events loaded into memory
