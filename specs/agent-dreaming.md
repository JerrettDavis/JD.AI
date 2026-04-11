# Agent > Dreaming Page Specification

> **Verified:** Gateway authentication required. Session params do not bypass WebSocket auth for admin panel.

**Route:** `/agent/dreaming` (alias: `/dreams`)  
**Controller:** `controllers/dreaming.ts` (OpenClaw UI)  
**View:** `views/dreaming.ts`  
**Backend Plugin:** `memory-core` (default, configurable)

## Overview

The Dreaming page is a unique interface for managing the system's memory consolidation engine. It provides:

1. **Dream Scene** - Visual status display and control panel for active dreaming
2. **Dream Diary** - Markdown-formatted record of all completed dream cycles
3. **Configuration** - Settings for dream phases (light, deep, REM), schedules, and storage

## Concept: The Three Dream Phases

The dreaming system processes memories through three sleep-like phases, inspired by neuroscience:

| Phase | Purpose | Configuration | Metrics |
|-------|---------|---------------|---------|
| **Light** | Quick scans for recent important events | `lookbackDays`, `limit` | `lightPhaseHitCount` |
| **Deep** | Scoring and ranking memories by relevance | `minScore`, `minRecallCount`, `minUniqueQueries` | Memory graph processing |
| **REM** | Pattern extraction and consolidation | `minPatternStrength`, `lookbackDays` | `remPhaseHitCount` |

All three phases run on a cron schedule and can be enabled/disabled independently.

## Page Structure

### Header Controls

**Refresh Button**
- Reloads both dreaming status and dream diary content
- Shows "Refreshing..." during load
- Disabled while loading

**Phase Toggle**
- Large toggle switch for enabling/disabling dreaming system entirely
- Labels: "on" / "off"
- Updates system config when toggled
- Disabled while saving

### Sub-Tab Navigation

Two main views:

1. **Scene Tab** (default, `tab === "scene"`)
   - Visual dream visualization
   - Live status display
   - Phase-specific metrics
   
2. **Diary Tab** (toggled via tab buttons, `tab === "diary"`)
   - Markdown dream diary viewer
   - Paginated entries
   - Full memory consolidation history

---

## Scene View

### Visual Dream Visualization

**Animated Background:**
- Starfield with 12 randomly placed stars
  - Stars fade in/out on a 6-second cycle
  - Alternating accent color vs neutral color
  - Each star has individual animation delay
- Moon image (centered, upper area)
- Full-screen background visualization when dreaming is active

**Status Indicator:**

When dreaming is **active** (enabled):
- Animated thought bubble near sleeping lobster
- Shows currently executing dream task:
  - Rotates through 16 dream phrases every 6 seconds
  - Examples: "Consolidating memories", "Tidying knowledge graph", "Replaying conversations"
  - Phrases pulled from localization keys: `dreaming.phrases.*`
- Sleeping lobster illustration (SVG, red gradient)
  - Positioned center-bottom
  - Animated eye blinking or twitching during active dreaming

When dreaming is **idle** (disabled):
- Starfield still visible
- Sleeping lobster at rest (no animation)
- No thought bubble
- Muted colors, reduced visual intensity

### Status Panel (Right Sidebar or Below Scene)

**Enabled State:**
- "Dreaming is **on**" or status badge

**Memory Counts:**
- Short-term memory count: `shortTermCount`
- Total signals: `totalSignalCount`
- Recall signals: `recallSignalCount`
- Daily signals: `dailySignalCount`
- Phase signals: `phaseSignalCount`

**Promotion Stats:**
- "Promoted today": `promotedToday` memories
- "Total promoted": `promotedTotal` memories

**Phase Summary:**
- Light phase:
  - Enabled/disabled badge
  - Next run: `phases.light.nextRunAtMs` (relative time)
  - Hit count: `lightPhaseHitCount` memories found
  - Cron schedule: `phases.light.cron`
- Deep phase:
  - Enabled/disabled badge
  - Next run: `phases.deep.nextRunAtMs`
  - Active scoring
- REM phase:
  - Enabled/disabled badge
  - Next run: `phases.rem.nextRunAtMs`
  - Hit count: `remPhaseHitCount` patterns found

**Storage Info:**
- Mode: `storageMode` (inline / separate / both)
  - inline: DREAMS.md mixed in main config
  - separate: Standalone DREAMS.md file
  - both: Dual copies
- Storage path: `storePath` (if custom)
- Storage error: `storeError` (if any)

**Configuration:**
- Timezone: `timezone` (if set, for cron timing)
- Verbose logging: `verboseLogging` toggle
- Separate reports: `separateReports` toggle
- Phase signal path: `phaseSignalPath` (internal use)

---

## Diary View

### Dream Diary Display

**Purpose:**
- Read-only viewer for all dream cycle records
- Markdown-formatted consolidation logs from the memory-core plugin
- Shows what the system "remembered" in each dream

**Diary Structure:**

The diary is stored as a markdown file (default: `DREAMS.md`) with markers:

```markdown
<!-- openclaw:dreaming:diary:start -->

*April 5, 2026, 3:00 AM*
---
[Dream phase notes and memory consolidations]

*April 4, 2026, 9:30 PM*
---
[Previous cycle notes]

<!-- openclaw:dreaming:diary:end -->
```

Entries are parsed by:
1. Looking for `<!-- openclaw:dreaming:diary:start -->` and `<!-- openclaw:dreaming:diary:end -->` markers
2. If markers exist, extract content between them; otherwise use entire file
3. Split on `\n---\n` (horizontal rule separators)
4. Extract date from lines wrapped in `*asterisks*`
5. Collect remaining non-heading, non-comment lines as entry body

### Diary Viewer Controls

**File Info:**
- Shows `dreamDiaryPath` (e.g., "DREAMS.md")
- Status: "Found" / "Not found" based on `dreamDiaryContent !== null`

**Pagination:**
- Display current page and total entry count
- Previous/Next page buttons
- Page input field to jump to specific entry

**Entry Display:**
- Current entry date (if available)
- Entry body text, formatted as markdown
- Show line count or character count for entry size

**Refresh:**
- "Refresh diary" button to reload from disk
- Shows "Loading..." while fetching
- Displays error if diary cannot be read (`dreamDiaryError`)

**No Diary State:**
- "Diary not found at [path]" if `dreamDiaryContent === null`
- Suggestion to run a dream cycle to create it

---

## State & Type Definitions

### DreamingState (Controller)

```typescript
export type DreamingState = {
  client: GatewayBrowserClient | null;           // API connection
  connected: boolean;                             // Connection status
  configSnapshot: ConfigSnapshot | null;          // Current config
  applySessionKey: string;                        // Config session key
  
  // Dreaming status
  dreamingStatusLoading: boolean;                 // Fetching status
  dreamingStatusError: string | null;             // Status fetch error
  dreamingStatus: DreamingStatus | null;          // Current status
  dreamingModeSaving: boolean;                    // Toggling enabled/disabled
  
  // Dream diary
  dreamDiaryLoading: boolean;                     // Fetching diary
  dreamDiaryError: string | null;                 // Diary fetch error
  dreamDiaryPath: string | null;                  // Path to diary file
  dreamDiaryContent: string | null;               // Full diary markdown (null = not found)
  
  lastError: string | null;                       // General error display
};
```

### DreamingStatus (Full Status Response)

```typescript
export type DreamingStatus = {
  enabled: boolean;                    // Dreaming is active
  timezone?: string;                   // IANA timezone for cron
  verboseLogging: boolean;             // Debug logging enabled
  storageMode: "inline" | "separate" | "both";
  separateReports: boolean;            // Split dream report formatting
  
  // Memory counters
  shortTermCount: number;              // Recent short-term memories
  recallSignalCount: number;           // Memories tagged for recall
  dailySignalCount: number;            // Today's signal count
  totalSignalCount: number;            // Lifetime signals
  phaseSignalCount: number;            // Current phase signals
  lightPhaseHitCount: number;          // Light phase finds
  remPhaseHitCount: number;            // REM phase finds
  
  // Promotion stats
  promotedTotal: number;               // Total promoted memories
  promotedToday: number;               // Today's promotions
  
  // Storage paths
  storePath?: string;                  // Dream store location
  phaseSignalPath?: string;            // Phase signal cache path
  storeError?: string;                 // Storage error msg
  phaseSignalError?: string;           // Phase signal error
  
  // Phase configurations
  phases: {
    light: LightDreamingStatus;        // Light phase config
    deep: DeepDreamingStatus;          // Deep phase config
    rem: RemDreamingStatus;            // REM phase config
  };
};
```

### Phase Status Types

```typescript
type DreamingPhaseStatusBase = {
  enabled: boolean;                    // Phase is active
  cron: string;                        // Cron schedule expression
  managedCronPresent: boolean;         // System manages cron
  nextRunAtMs?: number;                // Next execution timestamp
};

type LightDreamingStatus = DreamingPhaseStatusBase & {
  lookbackDays: number;                // Scan recent N days
  limit: number;                       // Max memories to process
};

type DeepDreamingStatus = DreamingPhaseStatusBase & {
  limit: number;
  minScore: number;                    // 0-1 relevance threshold
  minRecallCount: number;              // Min query recall history
  minUniqueQueries: number;            // Min query diversity
  recencyHalfLifeDays: number;         // Decay factor for recency
  maxAgeDays?: number;                 // Max memory age
};

type RemDreamingStatus = DreamingPhaseStatusBase & {
  lookbackDays: number;
  limit: number;
  minPatternStrength: number;          // 0-1 pattern confidence
};
```

### DreamingProps (View)

```typescript
export type DreamingProps = {
  active: boolean;                     // Dreaming enabled
  shortTermCount: number;
  totalSignalCount: number;
  phaseSignalCount: number;
  promotedCount: number;
  dreamingOf: string | null;           // Current task phrase
  nextCycle: string | null;            // Relative time to next run
  timezone: string | null;
  statusLoading: boolean;
  statusError: string | null;
  modeSaving: boolean;
  dreamDiaryLoading: boolean;
  dreamDiaryError: string | null;
  dreamDiaryPath: string | null;
  dreamDiaryContent: string | null;
  
  onRefresh: () => void;               // Reload status
  onRefreshDiary: () => void;          // Reload diary
  onToggleEnabled: (enabled: boolean) => void;
  onRequestUpdate?: () => void;        // Request re-render
};
```

---

## Data Flow

### Initialization

1. Page mounts
2. Calls `loadDreamingStatus(state)` → fetches `doctor.memory.status`
3. Calls `loadDreamDiary(state)` → fetches `doctor.memory.dreamDiary`
4. Polling starts via `startNodesPolling()` (shared with Nodes page)

### Status Response

**API Call:** `client.request("doctor.memory.status", {})`

**Response Structure:**
```json
{
  "dreaming": {
    "enabled": true,
    "timezone": "America/Los_Angeles",
    "phases": {
      "light": { "enabled": true, "cron": "0 2 * * *", "nextRunAtMs": 1712793600000, ... },
      "deep": { "enabled": true, "cron": "0 4 * * *", ... },
      "rem": { "enabled": true, "cron": "0 6 * * *", ... }
    },
    "shortTermCount": 42,
    "totalSignalCount": 1500,
    "lightPhaseHitCount": 28,
    ...
  }
}
```

### Dream Diary Response

**API Call:** `client.request("doctor.memory.dreamDiary", {})`

**Response Structure:**
```json
{
  "found": true,
  "path": "DREAMS.md",
  "content": "<!-- openclaw:dreaming:diary:start -->\n*April 5, 2026, 3:00 AM*\n---\n..."
}
```

### Toggle Enabled

**API Call:** `client.request("config.patch", { baseHash, raw, sessionKey, note })`

**Patch Payload:**
```json
{
  "plugins": {
    "entries": {
      "memory-core": {
        "config": {
          "dreaming": {
            "enabled": true
          }
        }
      }
    }
  }
}
```

---

## Event Handlers

| Handler | Triggered | Responsibility |
|---------|-----------|-----------------|
| `onRefresh()` | Manual refresh button | Reload status & diary |
| `onRefreshDiary()` | Diary refresh button | Reload diary only |
| `onToggleEnabled(enabled)` | Phase toggle switch | Enable/disable dreaming via config patch |
| `onRequestUpdate()` | Tab switch button | Trigger view re-render (internal) |

---

## Visual Design Elements

### Scene Colors & Animation

- **Background:** Dark (space theme)
- **Starfield:** White/accent stars on dark background
  - Fade-in animation: 6-second loop
  - Staggered delays for visual depth
- **Moon:** Gray/white disk, static
- **Lobster:** Red gradient (#ff4d4d to #991b1b)
  - SVG illustration with antennae
  - Eyes drawn with stroke (dark)
  - Animated during active dreaming
- **Thought Bubble:** Light background with text
  - Positioned above lobster
  - Dark text
  - Rounded corners

### State-Based Styling

**Active Dreaming:**
- Bright starfield, animated lobster, visible bubble
- Color intensity: normal
- Animations enabled

**Idle Dreaming:**
- Muted colors, static lobster, no bubble
- Reduced opacity
- Animations paused

### Typography

- **Tab labels:** Bold, clickable with active indicator
- **Status labels:** Monospace for cron expressions
- **Timestamps:** Relative format ("in 4 hours", "2 hours ago")
- **Diary dates:** Human-readable, centered on entries

---

## Error Handling

- **Status Load Error:** `dreamingStatusError` displays error callout
- **Diary Load Error:** `dreamDiaryError` displays in diary section
- **Diary Not Found:** "Diary not found at [path]" message
- **Config Patch Error:** `dreamingStatusError` updated, error callout shown

---

## Related Pages

- **Settings > Config** - Configure dreaming plugin and phase schedules
- **Control > Logs** - View dream cycle execution logs
- **Agent > Agents** - Agents benefit from consolidated dream memories

---

## Localization Keys

All user-facing text references i18n keys:

| Context | Key Path |
|---------|----------|
| Scene tab | `dreaming.tabs.scene` |
| Diary tab | `dreaming.tabs.diary` |
| Refresh button | `dreaming.header.refresh` |
| Refreshing state | `dreaming.header.refreshing` |
| Toggle on | `dreaming.header.on` |
| Toggle off | `dreaming.header.off` |
| Dream phrases (16 total) | `dreaming.phrases.*` |

---

**Last Updated:** April 2026  
**Status:** Documented from OpenClaw UI source v2026.4.6  
**Plugin:** memory-core  
**API Contract:** `doctor.memory.status` and `doctor.memory.dreamDiary` endpoints
