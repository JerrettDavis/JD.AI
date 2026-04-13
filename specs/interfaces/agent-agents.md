# Agent > Agents

> **Verified:** Gateway authentication required. Session params do not bypass WebSocket auth for admin panel.

**Route:** `/agents` or `/agent/agents`  
**Nav Path:** Agent > Agents  
**Description:** Manage spawned AI agents, monitor their status, and inspect their workspace configurations.

## Layout

The Agents page uses a split layout:

- **Toolbar** (top): Agent selector dropdown, action buttons (Copy ID, Set Default, Refresh), and error display
- **Main Content** (below): Either a "select an agent" prompt or detailed agent inspection panel with tabbed interface

## Components

### Toolbar Section

**Agent Selector (select dropdown)**
- Type: HTML `<select>` element
- Label: None (dropdown is self-explanatory)
- Options: List of all active agents, each showing:
  - Agent ID (primary identifier)
  - Badge text: "(default)" for the default agent, or other badge if applicable
- Behavior: 
  - Disabled when agents list is loading or no agents exist
  - Selecting an agent updates the selected agent and displays its details
- Data: Populated from `AgentsListResult.agents[]`

**Toolbar Action Buttons**
- **Copy ID** (secondary button)
  - Visible when an agent is selected
  - Copies the selected agent's ID to clipboard on click
  - Always enabled
  
- **Set Default** (secondary button)
  - Visible when an agent is selected
  - Shows "Set Default" if agent is not default
  - Shows "Default" (disabled) if agent is already default
  - Fires `onSetDefault(agentId)` on click
  
- **Refresh** (primary button)
  - Always visible
  - Shows "Loading…" text while agents list is loading
  - Fires `onRefresh()` on click

**Error Display**
- Type: Callout/alert component (danger styling)
- Visible when `props.error` is truthy
- Displays error message text
- Positioned below toolbar buttons with margin

### Main Content Area

**Empty State (when no agent selected)**
- Card component with centered content
- Card title: "Select an agent"
- Card subtitle: "Pick an agent to inspect its workspace and tools."

**Agent Details Panel (when agent selected)**
- Displays selected agent information with tabbed interface
- Tabs available:
  - **Overview** (default): Agent metadata, model configuration, identity info
  - **Files**: Configuration files for the agent
  - **Tools**: Available tools and access control
  - **Skills**: Installed agent skills with status
  - **Channels**: Active channel accounts
  - **Cron**: Scheduled jobs for this agent
  
- Each tab shows a badge count of items (if applicable)
- Tab switching updates URL hash and `activePanel` state

**Agent Information Display (Overview Tab)**
- Shows:
  - Agent ID
  - Provider name
  - Model information with fallback list
  - Cached agent identity (name, description)
  - System prompt configuration
  - Config reload/save controls

**Files Tab**
- File list showing agent config files
- File selection in sidebar updates main content to show file editor
- Displays file contents (read-only or editable depending on state)
- Save/Reset buttons for file changes

**Tools Tab**
- Displays tools catalog
- Shows effective tools available to the agent
- Tool profile selector (predefined access control profiles)
- Allow/Deny list editors for tool overrides

**Skills Tab**
- Installed skills list grouped by category
- Status indicators for each skill (ready, needs-setup, disabled)
- Search/filter input
- Status tabs: All, Ready, Needs Setup, Disabled
- Toggle enable/disable for each skill
- Batch actions: Clear all, Disable all

**Channels Tab**
- Active channel integrations for this agent
- Channel account summary
- Status refresh control

**Cron Tab**
- Scheduled jobs list
- Jobs filtered to selected agent
- Job status and last run information
- "Run now" action button per job

### Spawn Agent Dialog (modal)

**Trigger:** Click "Spawn Agent" button (planned feature)

**Dialog Fields:**
- **Agent ID** (text input, required)
  - Placeholder: Suggest unique ID format
  - Validation: Cannot be empty, must be unique
  
- **Provider** (text input)
  - Suggests available providers (e.g., "ollama", "openai")
  
- **Model** (text input)
  - Suggests available models from catalog
  
- **System Prompt** (multiline textarea)
  - Initial system prompt for the agent
  
- **Max Turns** (numeric input)
  - Conversation turn limit
  
**Dialog Actions:**
- **Cancel** button: Closes dialog without saving
- **Spawn** button: Validates form, submits spawn request
  - Disabled if required fields are empty
  - Shows loading state while spawning

**Behavior:**
- Form validation prevents submission with empty Agent ID
- Success shows snackbar with "spawned" message
- Agents list refreshes automatically after successful spawn
- Dialog closes on success

## Interactions

- **Select agent**: Click dropdown option → updates selected agent, displays its details
- **Copy ID**: Click button → copies agent ID to clipboard
- **Set Default**: Click button → sets agent as default (if not already)
- **Refresh agents**: Click Refresh button → reloads agents list and related data
- **Switch tabs**: Click tab header → updates active panel, loads panel-specific data
- **Spawn agent** (planned): Click Spawn button → open dialog → fill form → submit
- **Delete agent**: Click delete button in agents list → confirmation → stops agent → refresh
- **Edit agent files**: Click file → edit content → Save/Reset buttons
- **Toggle skill**: Click skill toggle → enable/disable skill → config updates
- **Filter skills**: Type in search box → filters displayed skills

## State / Data

**Loading States:**
- Full page loading: Shows skeleton rows (5 placeholder rows) while agents list loads
- Panel-specific loading: Each tab shows loading state while its data loads
- Dialog submission: Spawn button shows loading while request in flight

**Empty States:**
- No agents available: Shows message "No active agents" in selection dropdown
- No agent selected: Shows prompt card to select an agent
- No data in tab: Shows "No files found", "No skills found", etc.

**Error States:**
- Top-level error: Displays in error callout below toolbar
- Panel-level errors: Each tab displays its own error message
- Submission errors: Dialog shows error message, remains open for retry

**Agent Data:**
- Agent list: Refreshed on page load and when Refresh clicked
- Selected agent details: Loaded when agent selection changes
- Agent skills: Loaded when Skills tab accessed
- Agent files: Loaded when Files tab accessed
- Agent tools: Loaded when Tools tab accessed
- Channels snapshot: Loaded when Channels tab accessed
- Cron jobs: Loaded when Cron tab accessed

## API / WebSocket Calls

**Initial Load:**
- Fetch agents list: `GET /api/agents` → `AgentsListResult`

**Agent Selection:**
- Fetch agent details: `GET /api/agents/{id}` → agent metadata
- Fetch agent identity: `GET /api/agents/{id}/identity` → `AgentIdentityResult`
- Fetch config: `GET /api/agents/{id}/config` → `ConfigSnapshot`

**Files Tab:**
- Fetch files list: `GET /api/agents/{id}/files` → `AgentsFilesListResult`
- Fetch file content: `GET /api/agents/{id}/files/{name}` → file text
- Save file: `POST /api/agents/{id}/files/{name}` with content
- Reload config: `POST /api/agents/{id}/config/reload`

**Tools Tab:**
- Fetch tools catalog: `GET /api/tools/catalog` → `ToolsCatalogResult`
- Fetch effective tools: `GET /api/agents/{id}/tools/effective` → `ToolsEffectiveResult`
- Update tools profile: `POST /api/agents/{id}/tools/profile` with profile name
- Update tools overrides: `POST /api/agents/{id}/tools/overrides` with allow/deny lists
- Save config: `POST /api/agents/{id}/config/save`

**Skills Tab:**
- Fetch skills status: `GET /api/agents/{id}/skills` → `SkillStatusReport`
- Toggle skill: `POST /api/agents/{id}/skills/{skillKey}/toggle` with enabled flag
- Clear skills: `POST /api/agents/{id}/skills/clear`
- Disable all: `POST /api/agents/{id}/skills/disable-all`

**Channels Tab:**
- Fetch channels snapshot: `GET /api/channels/status` → `ChannelsStatusSnapshot`
- Refresh channels: `POST /api/channels/refresh`

**Cron Tab:**
- Fetch cron jobs: `GET /api/agents/{id}/cron` → `CronJob[]`
- Run job now: `POST /api/agents/{id}/cron/{jobId}/run`

**Spawn Agent:**
- Create agent: `POST /api/agents` with spawn parameters

**Delete/Stop Agent:**
- Stop agent: `DELETE /api/agents/{id}` → confirm before delete

## Notes

- Agent selection persists in URL fragment or session state
- Active panel (tab) persists in URL fragment
- File editor draft changes are stored in component state; not saved until Save button clicked
- Skills and tools are specific to each agent; switching agents reloads panel data
- Config state (dirty/saving flags) prevents accidental loss of unsaved changes
- Skill status report includes eligibility reasons (e.g., "needs API key")
- Tool overrides are stored per-agent in config; profiles are global or per-provider
- Channel accounts and cron jobs are fetched system-wide but filtered by agent on display
- Error recovery: Refresh button reloads all data and clears errors
