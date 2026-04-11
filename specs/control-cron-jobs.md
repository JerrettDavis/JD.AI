# Control: Cron Jobs
n> **Verified:** Real UI via authenticated playwright [2026-04-11]


**Route:** `/cron` (alt: `/control/cron-jobs`)  
**Nav Path:** Sidebar > Control > Cron Jobs  
**Description:** Management interface for scheduled recurring jobs with filtering, execution controls, and job editing.

## Layout

The page is organized into a single-column layout with a sidebar:
1. **Sidebar** (left, collapsible) — Navigation and global controls
2. **Main content** (right) — Status overview, filters, and job list

The main content is vertically stacked:
- **Header** with page title, subtitle, and status summary
- **Filter panel** with search and multi-select dropdowns
- **Job list** — Table/card layout showing all scheduled jobs with inline actions
- **Job details** — Expand/collapse per-job to see full PROMPT, DELIVERY, and Agent info

## Components

**Page Title & Subtitle**
- Text: "Cron Jobs"
- Subtext: "Wakeups and recurring runs."

**Status Overview Cards** (3 cards)
1. **Enabled** — Yes/No toggle showing whether cron is active
2. **Jobs** — Total count (e.g., "12 shown of 12")
3. **Next Wake** — Timestamp or "n/a" when no jobs due soon

**Refresh & Reset Controls**
- **Refresh button** — Reloads job list from gateway
- **Reset button** — Clears all filters to default state

**Filter Panel**
- **Search input** — "Search jobs" placeholder; filters by job name
- **Enabled Status filter** — Dropdown: "All", "Enabled", "Disabled"
- **Schedule Type filter** — Dropdown: "All", "At", "Every", "Cron"
- **Last Run Status filter** — Dropdown: "All", "OK", "Error", "Skipped"

**Sort Controls**
- **Sort dropdown** — Options: "Next run", "Recently updated", "Name"
- **Sort direction buttons** — "Ascending" / "Descending"
- **Reset sort** — Clears sort to default

**Job List**
- Each job is a collapsible card showing:
  - **Name** — Job identifier/title (e.g., "Jarvis-Claude progress heartbeat")
  - **Schedule** — Human readable format (e.g., "Every 15m", "At 09:00", "0 12 * * *")
  - **Prompt section** (collapsed by default, expandable) — Full text of what the job does
  - **Delivery section** (expandable) — How results are sent (e.g., "announce (discord -> 1466622912307007690)")
  - **Agent** — Name of the agent executing the job (e.g., "jdai-default")
  - **Status badge** — "ok", "error", or other status
  - **Next** — When the job runs next (time string or "n/a")
  - **Last** — When it last ran (relative time, e.g., "21d ago")
  - **Enabled toggle** — Checkbox or switch to enable/disable

**Job Action Buttons** (per job)
- **Edit** — Open job editor dialog
- **Clone** — Duplicate this job with a new name
- **Enable** — Toggle enabled state (may be redundant with toggle)
- **Run** — Execute job immediately, non-blocking
- **Run if due** — Execute only if schedule criteria are met
- **History** — View past execution logs for this job
- **Remove** — Delete the job (likely with confirmation)

**Job Edit Dialog** (modal, inferred)
- **Name** field — Text input
- **Schedule** field — Cron expression or human-readable format selector
  - Dropdown: "Every" (with duration), "At" (with time), "Cron" (with expression)
- **Prompt** field — Text area for the action description
- **Delivery** field — Dropdown or select for delivery method
  - Options: "announce (discord -> channel_id)", other integrations
- **Agent** field — Dropdown of available agents
- **Enabled** toggle — Boolean
- **Save** / **Cancel** buttons

## Interactions

- **Search input** — Filters job list in real-time by name
- **Filter dropdowns** — Changes visible jobs immediately (client-side or server-side)
- **Sort buttons** — Reorders job list
- **Edit button** → Opens modal dialog to modify job details
- **Clone button** → Opens modal with existing values pre-filled, new name required
- **Enable/Disable toggle** → Immediately syncs to gateway (no save needed)
- **Run button** → Executes job and shows status (success or error)
- **Run if due button** → Runs only if schedule conditions are met; useful for testing
- **History button** → Opens panel or sidebar showing past runs with timestamps and outcomes
- **Remove button** → Opens confirmation dialog before deletion
- **Refresh button** → Reloads job list from gateway

## State / Data

**Data loaded on page load:**
1. List of all cron jobs with current enabled/disabled state
2. Each job's schedule, last run time, next run time, and status

**Loading state:**
- Job list may show skeleton placeholders while loading
- "Refresh" button disabled during load

**Empty state:**
- If no jobs: "No scheduled jobs" message
- If all filtered out: "No jobs match filters" message

**Error state:**
- If a job fails to execute: Status shows "error" with timestamp
- If gateway call fails: Error banner at top with retry

**Running state:**
- When job is executing (after "Run" button): Status may show "running" or spinner
- After completion: Status updates to "ok" or "error" with last run time

## API / WebSocket Calls

Gateway WebSocket messages (inferred from UI):
- `gateway:cron:list` — Fetch all jobs
- `gateway:cron:create { name, schedule, prompt, delivery, agent, enabled }` — Create new job
- `gateway:cron:update { id, name?, schedule?, prompt?, delivery?, agent?, enabled? }` — Update job
- `gateway:cron:delete { id }` — Delete job
- `gateway:cron:run { id, force? }` — Execute job (force = run regardless of schedule)
- `gateway:cron:history { id, limit? }` — Fetch execution history for a job
- `gateway:cron:toggle { id, enabled }` — Enable/disable job

## Notes

- Jobs can run on any agent; gateway routes execution to the specified agent
- Schedule format supports both cron expressions and human-readable durations (Every X, At Y)
- Last run time is stored even after the job completes; helps identify stale jobs
- Job Prompt is the instruction sent to the agent; Delivery specifies where results go
- Cron enable/disable is separate from agent enable/disable; a job can be disabled even if agent is active
- Cloning a job is useful for creating similar recurring tasks without manual setup
- History view is critical for debugging failed jobs and auditing agent activity
