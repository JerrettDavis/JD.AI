# OpenClaw Navigation & Sidebar

**Route:** `/chat` (main entry point)  
**Nav Path:** Global Navigation / Sidebar  
**Description:** Global navigation sidebar providing access to all major sections and subsections of the OpenClaw control interface.

## Layout

The OpenClaw UI is organized around a **persistent left sidebar** containing the primary navigation structure. The main content area expands to fill available space. The interface supports three theme variations: **Claw** (default), **Knot**, and **Dash**, each with light/dark mode options.

### Sidebar Structure
- **Logo/Home Area** — Top of sidebar (branded OpenClaw icon)
- **Navigation Sections** — Hierarchical menu with expandable/collapsible sections
- **Session Indicator** — Current session context (agent:jdai-default:discord:channel:...)
- **Settings/Preferences Anchor** — Settings access (typically bottom of sidebar or top-right global controls)

## Components

### Navigation Sections

The sidebar is organized into **4 primary sections**, each containing subsections:

#### 1. Chat
- **Purpose:** User-facing chat interface for interacting with agents
- **Route:** `/chat`
- **Active State:** Highlighted when on chat page
- **Behavior:** Typically acts as default/home section

#### 2. Control
- **Purpose:** System administration and instance management
- **Route:** `/control` (prefix)
- **Subsections:**
  - Overview — `/control/overview` — System-wide status, dashboards
  - Channels — `/control/channels` — Message channel configuration
  - Instances — `/control/instances` — Agent instance lifecycle management
  - Sessions — `/control/sessions` — Session tracking and management
  - Usage — `/control/usage` — Resource consumption metrics
  - Cron Jobs — `/control/cron-jobs` — Scheduled task management

#### 3. Agent
- **Purpose:** Agent development and orchestration tools
- **Route:** `/agent` (prefix)
- **Subsections:**
  - Agents — `/agent/agents` — List, create, manage agent definitions
  - Skills — `/agent/skills` — Skill library and management
  - Nodes — `/agent/nodes` — Node graph editor for agent composition
  - Dreaming — `/agent/dreaming` — Agent learning/training interface

#### 4. Settings
- **Purpose:** System configuration and preferences
- **Route:** `/settings` (prefix)
- **Subsections:**
  - Config — `/settings/config` — Core configuration parameters
  - Communication — `/settings/communication` — Channel/integration setup
  - Appearance — `/settings/appearance` — UI theme, mode, locale
  - Automation — `/settings/automation` — Workflow and trigger configuration
  - Infrastructure — `/settings/infrastructure` — Deployment, scaling, monitoring
  - AI & Agents — `/settings/ai-agents` — LLM model selection, agent defaults
  - Debug — `/settings/debug` — Debugging tools and options
  - Logs — `/settings/logs` — System logs, filtering, export

### Global Header Elements

#### Session Context Display
- Current session identifier displayed in UI (e.g., "agent:jdai-default:discord:channel:1466622912307007690")
- Typically shown in breadcrumb or session indicator panel
- Allows context switching for multi-session workflows

#### Theme Selector (if visible)
- Theme options: Claw, Knot, Dash
- Mode options: Light, Dark, System (automatic based on OS preference)
- Storage: Persisted in localStorage under `openclaw.control.settings.v1.*`
- Applied via `data-theme` attribute on `<html>` root element

#### Global Actions (if present)
- May include user avatar / logout
- Notifications or status indicators
- Help/documentation links

### Breadcrumb Navigation

**Format:** Navigation path displayed as hierarchical indicators above main content  
**Example:** `Sidebar > Section > Page`  
**Behavior:**
- Updates automatically when navigating to new page
- May be clickable to return to parent section
- Helps orient user in multi-level navigation structure

### Collapse/Expand Behavior

- Sidebar may support **collapse/expand** toggle to maximize content area
- Collapsed state typically shows icons only
- Expanded state shows full section/page labels
- State may be persisted in localStorage

## Interactions

### Navigation Clicks
- Click on primary section (Chat, Control, Agent, Settings) → expands/highlights that section
- Click on subsection item → navigates to that page (route change)
- Active section/page is visually distinguished (highlight, bold, color)

### Session Switching
- User can switch between sessions (if multiple exist)
- Session change updates the URL query parameter (`?session=...`)
- Active session highlighted in UI

### Theme Switching
- User selects theme from preferences (if exposed in Settings > Appearance)
- JavaScript reads setting from localStorage on page load
- Applies `data-theme` and `data-theme-mode` attributes
- Page re-renders with new theme CSS

### Sidebar Collapse
- User clicks collapse button (if present)
- Sidebar width reduces to icon-only display
- Expanded state restores on toggle or reload (if persisted)

## State / Data

### Loading State
- Sidebar is available immediately (no loading required)
- Navigation items are static (no data fetching)

### Active Page State
- Current page tracked via URL pathname
- Active section/subsection highlighted in sidebar
- Breadcrumb updates to reflect current location

### User Preferences
- Theme: Loaded from localStorage on app startup
- Sidebar collapse state: May be persisted in localStorage
- Settings applied before first render to avoid flashing incorrect theme

## API / WebSocket Calls

**Navigation sidebar itself:** No direct API calls  
**Session context:** Session identifier passed via URL query parameter, may be used by other pages to fetch session data

## URL Patterns

The application uses **URL path-based routing**. All routes follow this pattern:

```
/chat
/control/overview
/control/channels
/control/instances
/control/sessions
/control/usage
/control/cron-jobs
/agent/agents
/agent/skills
/agent/nodes
/agent/dreaming
/settings/config
/settings/communication
/settings/appearance
/settings/automation
/settings/infrastructure
/settings/ai-agents
/settings/debug
/settings/logs
```

**Query Parameters:**
- `?session=<session-id>` — specifies active session context (required, persists across navigation)

## Notes

### Theme Implementation
- Three theme variations: `claw` (default), `knot`, `dash`
- Two modes: `light`, `dark`, `system` (auto-detect)
- Legacy theme names mapped to new variants for backward compatibility
- Resolved theme names: `light`, `dark`, `openknot`, `openknot-light`, `dash`, `dash-light`

### SPA Characteristics
- Single-page application (SPA) using client-side routing
- Initial HTML is minimal; all UI rendered by JavaScript
- Entry point: `<openclaw-app>` web component
- Asset loading includes preloaded modules for i18n, directives, formatting utilities

### Future Considerations
- Search/command palette (if planned) would integrate with sidebar
- Mobile responsive sidebar behavior (hamburger menu, etc.) not documented here
- Keyboard shortcuts for navigation (e.g., Cmd+K, Alt+G) not currently discoverable from static structure

### Testing Notes
- Theme persistence can be verified by checking localStorage keys matching `openclaw.control.settings.v1.*`
- Session switching can be tested by modifying URL query parameter
- All routes should be navigable from sidebar without requiring manual URL edits
