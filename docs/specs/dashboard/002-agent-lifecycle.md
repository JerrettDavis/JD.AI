# ADR-002: Agent Lifecycle

**Status:** Accepted
**Date:** 2026-03-05
**Relates to:** Agents page (`Pages/Agents.razor`), `Components/SpawnAgentDialog.razor`, `Components/SettingsAgentsTab.razor`

## Context

Operators need to manage AI agent instances: spawning new agents with specific provider/model configurations, monitoring running agents, and stopping agents that are no longer needed. Agent management spans two dashboard areas: the dedicated Agents page for runtime operations and the Settings > Agents tab for persistent configuration.

## Decision

The Agents page (`/agents`) provides a MudDataGrid of running agent instances with spawn and delete actions. The SpawnAgentDialog collects Agent ID, Provider, Model, System Prompt, and Max Turns. The Settings > Agents tab manages persistent agent definitions that auto-spawn on gateway startup.

### Components
- **Agents page**: Data grid with columns (ID, Provider, Model, Turns, Created), Spawn Agent button, per-row Delete button
- **SpawnAgentDialog**: Modal with Agent ID (required), Provider, Model, System Prompt (multiline), Max Turns (numeric, default 20)
- **SettingsAgentsTab**: Agent configuration list with save button

### API Dependencies
- `GET /api/agents` → `AgentInfo[]`
- `POST /api/agents` → Spawn agent (body: `AgentDefinition`)
- `DELETE /api/agents/{id}` → Stop agent
- `GET /api/gateway/config` → Agent definitions (Settings tab)
- `PUT /api/gateway/config/agents` → Save agent definitions

## Acceptance Criteria

1. Agents page displays "Agents" heading
2. "Spawn Agent" button is visible with add icon
3. Refresh button is visible
4. Skeleton loading rows (5) shown while agents are loading
5. Empty state displays "No active agents. Spawn one to get started." when no agents exist
6. Agent data grid displays when agents exist
7. Data grid has columns: ID, Provider, Model, Turns, Created
8. Each agent row has a delete button (red trash icon)
9. Clicking "Spawn Agent" opens the spawn dialog
10. Spawn dialog title is "Spawn New Agent"
11. Spawn dialog contains Agent ID field (required)
12. Spawn dialog contains Provider field with helper text
13. Spawn dialog contains Model field with helper text
14. Spawn dialog contains System Prompt multiline field
15. Spawn dialog contains Max Turns numeric field (default 20, min 1, max 100)
16. Spawn dialog has Cancel and Spawn buttons
17. Spawn button shows progress spinner while submitting
18. Successful spawn shows success snackbar and refreshes the agent list
19. Spawn with empty Agent ID does not submit (client-side validation)
20. Spawn API failure shows error snackbar with message
21. Clicking delete on an agent shows confirmation dialog "Stop agent '{id}'?"
22. Confirming delete removes agent and shows success snackbar
23. Delete API failure shows error snackbar
24. Settings > Agents tab displays agent configuration
25. Settings > Agents tab has a save button
26. [Planned] Agent grid shows expandable rows with system prompt and active sessions
27. [Planned] Test message action sends a probe message to a running agent

## Test Mapping

| AC# | Feature File Scenario |
|-----|-----------------------|
| 1 | AgentsPage.feature: "Displays agent page heading" |
| 2 | AgentsPage.feature: "Spawn button is visible" |
| 3 | AgentsPage.feature: "Refresh button is visible" |
| 4 | AgentsPage.feature: "Skeleton rows shown while loading" |
| 5 | AgentsPage.feature: "Empty state shown when no agents" |
| 6-7 | AgentsPage.feature: "Agent data grid displays with correct columns" |
| 8 | AgentsPage.feature: "Each agent row has delete button" |
| 9-16 | AgentsPage.feature: "Spawn dialog opens with all fields" |
| 17-18 | AgentsPage.feature: "Successful spawn shows snackbar and refreshes list" |
| 19 | AgentsPage.feature: "Spawn with empty ID does not submit" |
| 20 | AgentsPage.feature: "Spawn API failure shows error snackbar" |
| 21-22 | AgentsPage.feature: "Delete agent with confirmation" |
| 23 | AgentsPage.feature: "Delete API failure shows error" |
| 24-25 | SettingsPage.feature: "Agents tab accessible with save" |
| 26-27 | AgentsPage.feature: @planned scenarios |

## Consequences

- Spawn dialog does not validate provider/model against available providers — operators can enter any string. This is intentional for flexibility.
- Delete is immediate after confirmation (no soft-delete or grace period).
- Agent grid does not auto-refresh via SignalR; operators must manually refresh or the list refreshes after spawn/delete actions.
