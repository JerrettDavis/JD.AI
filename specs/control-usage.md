# Control: Usage

**Route:** `/control/usage`  
**Nav Path:** Sidebar > Control > Usage  
**Description:** Dashboard showing API usage metrics, token costs, and system performance analytics with filtering and export capabilities.

## Layout

The page is organized into a two-column layout:
1. **Sidebar** (left, collapsible) — Navigation and global controls
2. **Main content** (right) — Metrics, charts, and data tables

The main content is vertically stacked:
- **Header** with page title, subtitle, and pin/export controls
- **Filter panel** with time ranges, metric toggles, and timezone selector
- **Usage Overview section** — 9 key metric cards with contextual labels
- **Data visualizations** — Multiple chart areas (Activity by Time, Daily Usage, Tokens By Type)
- **Top X sections** — Breakdowns by Models, Providers, Tools, Agents, Channels
- **Error analytics** — Peak error days/hours
- **Sessions table** — Detailed per-session data with sorting and filtering

## Components

**Page Title & Subtitle**
- Text: "Usage"
- Subtext: "See where tokens go, when sessions spike, and what drives cost."

**Pin / Export Controls**
- **Pin button** — Toggles page pinning (persistent in sidebar)
- **Export dropdown** — Options: "Sessions CSV", "Daily CSV", "JSON"

**Time Range Selector**
- Quick buttons: "Today", "7d", "30d"
- Custom date range: Two date inputs (from/to)
- Applies to all charts and metrics below

**Metric Type Toggles**
- **Tokens** (default) — Shows token counts
- **Cost** — Shows dollar amounts for all metrics

**Timezone Selector**
- Dropdown options: "Local", "UTC"
- Affects all time-based displays

**Filter Panel**
- **Filter (client-side)** — Text input for session key queries (e.g., "key:agent:main:cron*", "model:gpt")
- Three dropdown filters:
  - Agent: "All" or specific agent name
  - Provider: "All" or specific provider
  - Model: "All" or specific model
- **Refresh button** — Reloads usage data from gateway
- Display count: "N sessions in range"

**Usage Overview Metric Cards** (9 cards)
1. **Messages** — Count with breakdown (X user · Y assistant)
2. **Throughput** — Tokens/min and $/min
3. **Tool Calls** — Count and "N tools used"
4. **Avg Tokens / Msg** — Average across sessions
5. **Cache Hit Rate** — Percentage with cached/prompt token counts
6. **Error Rate** — Percentage with error count and avg session duration
7. **Avg Cost / Msg** — Per-message cost
8. **Sessions** — Count of sessions in range
9. **Errors** — Total error count

**Data Visualization Sections**
- **Activity by Time** — Chart showing timeline of usage
- **Daily Usage** — Bar/area chart by day
- **Tokens By Type** — Breakdown card showing:
  - Output tokens (count)
  - Input tokens (count)
  - Cache Write tokens (count)
  - Cache Read tokens (count)
  - Total tokens (sum)

**Top X Breakdown Cards** (6 cards)
- **Top Models** — Model name, cost, token count, message count
- **Top Providers** — Provider name, cost, token count, message count
- **Top Tools** — Tool usage with call counts
- **Top Agents** — Agent name and associated cost
- **Top Channels** — Channel/platform and stats
- **Peak Error Days** — Day, error %, error count, message count
- **Peak Error Hours** — Hour of day, error %, message count

**Sessions Table**
- **Columns:**
  - Checkbox (select multiple rows)
  - Session ID (clickable link)
  - Agent name
  - Message count
  - Error count
  - Duration (human readable, e.g., "12h 59m")

- **Sort options:** Cost, Errors, Messages, Recent, Tokens
- **Sort direction:** Ascending (↓) / Descending
- **View modes:** "All" or "Recently viewed"
- **Bulk actions:** "Select All", "Clear"
- **Row actions:** Copy session ID to clipboard

## Interactions

- **Time range buttons** (Today/7d/30d) — Updates all charts and metrics immediately
- **Custom date range** — Drag endpoints or type dates; updates on blur/Enter
- **Metric toggle** (Tokens/Cost) — Converts all numeric displays between token counts and dollar amounts
- **Filter changes** — Client-side filtered session list in table below
- **Refresh button** — Reloads raw usage data from gateway
- **Chart interaction** (on charts) — Clicking bars refines days (as tooltip suggests)
- **Session ID copy** — Click "Copy" button on a row to copy to clipboard
- **Sort buttons** — Click dropdown or header to re-sort sessions table
- **Pin button** — Toggles visibility in sidebar favorites

## State / Data

**Data loaded on page load:**
1. Usage metrics for selected date range and filters
2. Breakdown charts (Activity by Time, Daily Usage, etc.)
3. Top X data (models, providers, tools, agents, channels)
4. Full session list for sorting/filtering
5. Error analytics

**Loading state:**
- Charts may show skeleton/placeholder during data fetch
- "Refresh" button disabled while loading

**Empty state:**
- If no sessions in range: "No data" message in charts
- If no errors: "—" or 0 in error-related metrics

**Error state:**
- If gateway call fails: Error banner at top with retry option

## API / WebSocket Calls

Gateway WebSocket messages (inferred from UI data):
- `gateway:usage:get { dateRange, agent?, provider?, model? }` — Fetch usage data
- `gateway:usage:breakdown { type: 'models'|'providers'|'tools'|'agents'|'channels', dateRange }` — Fetch breakdown data
- `gateway:sessions:list { dateRange, filters, sort, limit }` — Fetch session list
- `gateway:errors:analytics { dateRange }` — Fetch error stats

## Notes

- All metrics auto-scale based on time range (e.g., 30d shows lower granularity than 1d)
- Cache metrics may show "—" if no caching enabled
- Error rate is a key KPI for monitoring agent stability
- Sessions table supports client-side filtering only (no server-side pagination visible)
- Cost calculation likely uses model pricing from gateway config
