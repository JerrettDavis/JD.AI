# Agent > Skills

> **Verified:** Gateway authentication required. Session params do not bypass WebSocket auth for admin panel.

**Route:** `/skills` or `/agent/skills`  
**Nav Path:** Agent > Skills  
**Description:** Browse and manage installed skills, view their status and configuration, and discover new skills from ClawHub.

## Layout

The Skills page uses a single-column layout with sections:

- **Header** (top): Title, description, and Refresh button
- **Status Tabs**: Filter skills by status (All, Ready, Needs Setup, Disabled)
- **Installed Skills Section**:
  - Search/filter input
  - Skills grouped by category (collapsible details sections)
  - Individual skill cards in grid layout
- **ClawHub Section** (below):
  - Title and description
  - Search input for ClawHub registry
  - Search results list (if any)
  - Detail modal (when skill selected from results)

## Components

### Page Header

**Title Section**
- Main title: "Skills"
- Subtitle: "Installed skills and their status."
- Adjacent **Refresh** button (primary action)
  - Disabled when loading or not connected
  - Shows "Loading…" while refreshing
  - Triggers skill status refresh

### Status Filter Tabs

**Tabs (horizontal button group)**
- **All**: Shows all skills, count of total
- **Ready**: Shows skills that are enabled AND eligible, count
- **Needs Setup**: Shows skills that are enabled but NOT eligible, count
- **Disabled**: Shows skills that are disabled, count
- Active tab has "active" styling
- Each tab shows skill count badge

**Behavior:**
- Clicking tab filters displayed skills
- Filter state persists across page navigation
- Count updates when data refreshes

### Search / Filter Input

**Search Box**
- Type: Text input field
- Placeholder: "Filter installed skills"
- Autocomplete: "off"
- Real-time filtering as user types
- Searches across: skill name, description, source

**Status Display**
- Shows count: "{count} shown"
- Updates as search results change
- Text styled as muted/secondary

### Installed Skills Groups

**Collapsible Group**
- Type: `<details>` element (can expand/collapse)
- Default state: Expanded (open)
- Header styling: "agent-skills-header" class
- Shows:
  - Group category label (e.g., "Core Skills", "Integrations")
  - Skill count in group (muted text)

**Skills Grid (within group)**
- Grid layout: 2-3 columns (responsive)
- Each cell: Individual skill card (see below)

### Individual Skill Card

**Skill Card Structure**
- Card container with skill status styling:
  - Class "ok" if enabled and eligible (ready)
  - Class "warn" if enabled but not eligible (needs setup)
  - Class "muted" if disabled
  
**Skill Card Content**
- **Skill Name** (primary text, larger)
- **Description** (secondary text, smaller, clamped to 2-3 lines)
- **Status Indicators** (chips/badges showing):
  - Eligibility status (Ready / Needs Setup / Disabled)
  - Missing prerequisites (if applicable)
  - Reasons for eligibility (if needs setup)
  
**Skill Card Actions**
- **Toggle Switch/Checkbox** (enable/disable skill)
  - Left-aligned in card header
  - On/off state reflects skill.disabled flag
  - Fires `onToggle(skillKey, enabled)` on change
  - Disabled during saving
  
- **Configuration Button** (secondary, if skill has API key field)
  - Shows edit icon or "Configure"
  - Opens detail modal with configuration form
  
- **Details/Expand Button** (clickable entire card or specific button)
  - Opens skill detail modal
  - Shows full description, dependencies, configuration UI

### Skill Detail Modal

**Trigger:** Click skill card or Configure button

**Modal Structure**
- Modal title: Skill name
- Modal body sections:
  - **Description**: Full skill description (not clamped)
  - **Status**: Eligibility status with reasons (why needs setup)
  - **Dependencies**: List of missing prerequisites
  - **Configuration Form** (if applicable):
    - Input fields for skill-specific settings (e.g., API keys)
    - Text input for secrets/credentials
    - Validation status
    - Save/Cancel buttons
  - **Links** (if skill has docs/repo):
    - Styled as safe external links
    - Opens in new tab with rel="noopener noreferrer"

**Modal Actions**
- **Save** button: Saves configuration changes
  - Shows loading state while saving
  - Shows success/error message
- **Cancel** button: Closes modal without saving
- **Close** (X button): Same as Cancel

**Error Handling:**
- Shows error callout if skill configuration fails
- Form remains open for retry

### ClawHub Section

**Section Header**
- Title: "ClawHub"
- Subtitle: "Search and install skills from the registry"

**ClawHub Search**
- Text input field
- Placeholder: "Search ClawHub skills…"
- Autocomplete: "off"
- Real-time search as user types
- Shows "Searching…" muted text while in flight

**ClawHub Search Results**

**No Results State:**
- Muted text: "No skills found on ClawHub."

**Results List:**
- List layout (similar to agent/files list)
- Each result item shows:
  - **Display Name** (main title, clickable)
  - **Summary** (clamped to 120 chars, or slug if no summary)
  - **Version** (if available, muted small text)
  - **Install Button** (secondary)
    - Shows "Install" normally
    - Shows "Installing…" while in flight for that skill
    - Disabled when any install is in flight
    - Fires `onClawHubInstall(slug)` on click
    - Click is stop-propagation to prevent triggering detail modal

**Behavior:**
- Clicking result item (not Install button) opens ClawHub Detail Modal
- Install button submit fires installation request
- Success shows snackbar message below search results

**ClawHub Detail Modal**

**Trigger:** Click ClawHub search result

**Modal Structure**
- Modal title: Skill display name (from ClawHub detail)
- Modal body sections:
  - **Description**: Full description from ClawHub
  - **Author/Source**: Creator/publisher info
  - **Version**: Latest available version
  - **Documentation**: Link to skill docs (safe external link)
  - **Requirements**: Dependencies, prerequisites
  - **Installation Status**: Shows success/error messages

**Modal Actions**
- **Install** button: Installs skill from ClawHub
  - Shows loading state
  - Fires `onClawHubInstall(slug)`
  - Shows success message on completion
  - Closes modal (or shows message with close button)
  
- **Close** button: Closes modal without installing

### Messages and Alerts

**Loading State**
- Full page: Skeleton loaders shown for skills list
- Search: "Searching…" text appears while ClawHub search in flight
- Install: "Installing…" shown on Install button

**Error Messages**
- ClawHub search error: Red callout above results
  - Shows error message text
- Installation error: Red callout below search input or in detail modal
- General connection error: Shown as gray text "Not connected to gateway."

**Success Messages**
- Skill installed: Success snackbar with "installed" text
- Configuration saved: Success message in detail modal
- Shows message text and auto-dismisses after 3-4 seconds

**Empty States**
- No skills installed: "No skills found." message in main area
- No skills match filter: "No skills found." message
- ClawHub no results: "No skills found on ClawHub."
- Not connected: "Not connected to gateway." instead of empty list

## Interactions

- **Filter by status**: Click tab → filters skills, updates count
- **Search skills**: Type in search input → filters skills real-time, updates count
- **Toggle skill**: Click toggle on skill card → enables/disables skill
- **View skill detail**: Click skill card → opens detail modal
- **Configure skill**: Click Configure button → opens detail modal with form
- **Save configuration**: Fill form → click Save → updates skill config
- **Search ClawHub**: Type in ClawHub search → fetches results list
- **View ClawHub skill**: Click result → opens ClawHub detail modal
- **Install skill**: Click Install button → installs skill → success message
- **Close modal**: Click X or outside modal → dismisses modal
- **Refresh**: Click Refresh button → reloads skills status and data

## State / Data

**Loading States:**
- Initial page load: Skills list shows loading skeleton
- ClawHub search: Shows "Searching…" while fetching
- Skill config save: Save button shows loading, input disabled
- Install from ClawHub: Install button shows "Installing…"

**Filter/Search State:**
- Active status tab: Persisted in component state
- Search text: Persisted in component state as user types
- Filtered results: Computed from skills array based on status and search

**Skill Status:**
- **eligible**: boolean (skill has all requirements met)
- **disabled**: boolean (skill is disabled)
- **reasons**: string[] (if not eligible, why not met)
- **missing**: string[] (if not eligible, what's missing)

**ClawHub State:**
- ClawHub search query: Stores user input
- Search results: List of `ClawHubSearchResult`
- Selected detail: `ClawHubSkillDetail` for currently viewed skill
- Install status: Tracks which skill is currently installing

**Edit State:**
- Configuration edits: `Record<string, string>` for form field values
- Messages: `SkillMessageMap` for success/error feedback per skill

**Data Structure:**
- Skills list: `SkillStatusEntry[]` from `SkillStatusReport.skills`
- Each skill includes: name, skillKey, description, source, disabled, eligible, reasons
- ClawHub results: `ClawHubSearchResult[]` with displayName, slug, summary, version
- ClawHub detail: `ClawHubSkillDetail` with full description, author, repo link, etc.

## API / WebSocket Calls

**Initial Load:**
- Fetch skills status: `GET /api/skills/status` → `SkillStatusReport`

**Skill Management:**
- Refresh skills: `POST /api/skills/refresh` → re-fetch status
- Toggle skill enabled: `POST /api/skills/{skillKey}/toggle` with enabled flag
- Update skill config: `POST /api/skills/{skillKey}/config` with form data
- Get skill detail: `GET /api/skills/{skillKey}` → detailed config and status

**ClawHub Integration:**
- Search ClawHub: `GET /api/clawhub/search?q={query}` → `ClawHubSearchResult[]`
- Get ClawHub detail: `GET /api/clawhub/skills/{slug}` → `ClawHubSkillDetail`
- Install from ClawHub: `POST /api/clawhub/skills/{slug}/install` → installation job ID
- Poll install status: `GET /api/clawhub/install/{jobId}` → status and progress

## Notes

- Skills are system-wide, not agent-specific (unlike Agent > Agents tab)
- Skill eligibility is determined by missing dependencies (environment variables, API keys, installed packages)
- Reasons for ineligibility are user-facing explanations (e.g., "Missing OPENAI_API_KEY")
- ClawHub is a registry of publicly available skills; installation adds them to the system
- Skill toggle enable/disable is immediate (no save required), but configuration changes require explicit Save
- Search is case-insensitive and matches partial words
- ClawHub install shows progress and handles rate limiting gracefully
- Skill detail modal can show authentication errors if credentials are invalid
- External links to skill docs/repos use safe URL resolution to prevent XSS
- Skills can have dependencies on other skills; installing a skill may trigger installs of dependencies
- Status refresh may take a few seconds if skills require network requests to validate eligibility
