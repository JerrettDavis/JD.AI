# Agent > Agents

> **Verified:** Real UI via authenticated playwright [2026-04-11]

**Route:** `/agent/agents`  
**Nav Path:** Agent > Agents  
**Description:** Manage and inspect spawned AI agents with detailed configuration, credentials, and status monitoring.

## Layout Structure

```
┌─ Header (full width)
│  ├─ OpenClaw logo
│  ├─ Breadcrumb: › Agents
│  ├─ Command palette search (⌘K)
│  └─ Sidebar (left-aligned)
│
├─ Main Content Area
│  ├─ Page title: "Agents"
│  ├─ Toolbar
│  │  ├─ Agent selector dropdown
│  │  ├─ Copy ID button
│  │  ├─ Set Default button
│  │  ├─ Refresh button
│  │  └─ Additional actions (if any)
│  │
│  └─ Agent List / Detail View
│     ├─ List of active agents
│     └─ Selected agent details panel
│
└─ Footer / Version info
```

## Key UI Components

### Header Section
- **Logo:** OpenClaw branding
- **Breadcrumb:** `› Agents`
- **Search:** Command palette trigger (⌘K)

### Sidebar Navigation
The sidebar remains visible and shows the full navigation structure:
- **CHAT** → Chat
- **CONTROL** → Overview, Channels, Instances, Sessions, Usage, Cron Jobs
- **AGENT** → Agents, Skills, Nodes, Dreaming
- **SETTINGS** → Config, Communications, Appearance, Automation, Infrastructure, AI & Agents, Debug, Logs
- **Docs** link
- **VERSION** indicator (e.g., v2026.4.8 with update available badge)

### Main Content Area

#### Title & Description
- **Heading:** "Agents"
- **Subheading:** "Manage spawned AI agents with detailed inspection and configuration."

#### Toolbar
- **Agent Selector Dropdown:** Select which agent to inspect/manage
- **Copy ID:** Copy the selected agent's ID to clipboard
- **Set Default:** Mark selected agent as default
- **Refresh:** Re-fetch agent list and status
- **Additional Controls:** May include create agent, delete, or settings

#### Agents List / Detail Panel
- **Columns/Fields visible:**
  - Agent name/ID
  - Status (active, inactive, error)
  - Type (AI model type, if applicable)
  - Credentials/configuration status
  - Last activity timestamp
  - Action buttons (edit, delete, etc.)

- **Interactions:**
  - Click to select an agent and view details
  - Inline actions (delete, edit, duplicate)
  - Bulk actions if multiple agents selected

### Version Footer
- **Current version:** v2026.4.8
- **Update indicator:** "Update available: v2026.4.9" (clickable)
- **Update now button:** Triggers upgrade process

## Behavioral Patterns

### Agent Selection & Inspection
1. User selects an agent from the dropdown or list
2. Details panel updates to show agent configuration
3. Copy ID, Set Default, and other actions become context-aware (apply to selected agent)

### Refresh & Updates
- Refresh button re-syncs agent state from backend
- List updates with real-time status changes
- Timestamp shows last update time

### Error Handling
- Failed agent loads show error badge
- Configuration errors highlighted in details panel
- Network errors shown as toast notifications

## State Indicators

- **Connected agents:** Green status indicator
- **Disconnected agents:** Gray status indicator
- **Error state:** Red status indicator
- **Loading state:** Spinner or skeleton loader

## Related Pages

- [Settings > AI & Agents](settings-ai-agents.md) — Configure AI model settings and agent defaults
- [Agent > Skills](agent-skills.md) — Manage agent capabilities and skill mappings
- [Agent > Nodes](agent-nodes.md) — Inspect agent nodes and workload distribution
- [Chat](chat.md) — Test agents in live chat interface

## Notes

- Agent management is credential-aware; changing credentials requires re-authentication
- Multiple agents can be spawned and managed independently
- Agent defaults affect which agent is pre-selected in chat and other interfaces
- Real-time sync via WebSocket connection to gateway
