# ADR-007: Routing Configuration

**Status:** Accepted
**Date:** 2026-03-05
**Relates to:** Routing page (`Pages/Routing.razor`), `Components/SettingsRoutingTab.razor`

## Context

The gateway routes incoming messages from channels to agents. Operators need to view and modify these mappings, visualize the routing pipeline, and sync routing state with OpenClaw.

## Decision

The Routing page (`/routing`) combines an editable MudDataGrid with a MudTimeline visualization. The grid supports inline cell editing for the Agent ID column. The timeline provides a visual representation of each channel-to-agent mapping with directional arrows.

### Components
- **Routing data grid**: Columns (Channel with icon, Agent ID [editable], Status chip), inline cell editing
- **Status chips**: "Override" (Primary) when agent assigned, "Default" when no agent (OpenClaw handles)
- **Routing diagram**: MudTimeline with timeline items per mapping, showing Channel → Agent flow
- **Sync OpenClaw button**: Triggers routing sync

### API Dependencies
- `GET /api/routing/mappings` → `RoutingMapping[]` (ChannelType, AgentId)
- `POST /api/routing/map` → Update single mapping (ChannelType, AgentId)
- `POST /api/gateway/openclaw/sync` → Sync with OpenClaw

## Acceptance Criteria

1. Routing page displays "Channel → Agent Routing" heading
2. "Sync OpenClaw" button is visible with sync icon
3. Refresh button is visible
4. Skeleton loading rows (4) shown while loading
5. Routing data grid displays when mappings exist
6. Grid shows channel name with matching icon per row
7. Grid shows Agent ID column (editable via inline cell editing)
8. Grid shows Status chip: "Override" (primary) when agent assigned, "Default" when empty
9. Editing Agent ID in the grid triggers API call to update the mapping
10. Successful mapping update shows success snackbar with "Routing updated: {channel} → {agent}"
11. Mapping update failure shows error snackbar
12. Routing diagram section displays below the grid
13. Routing diagram has "Routing Diagram" heading
14. Diagram uses MudTimeline with one item per mapping
15. Each timeline item shows Channel chip → arrow → Agent chip
16. Agent chip shows "OpenClaw Default" when no agent is assigned
17. Timeline items are color-coded: Primary when agent assigned, Default when using OpenClaw default
18. Sync OpenClaw shows success snackbar and refreshes mappings
19. Sync failure shows error snackbar
20. Channel icons match type (same mapping as Channels page)
21. Settings > Routing tab shows routing strategy and mappings with save button
22. [Planned] Dropdown-based agent selector replaces free-text Agent ID editing

## Test Mapping

| AC# | Feature File Scenario |
|-----|-----------------------|
| 1 | RoutingPage.feature: "Displays routing page heading" |
| 2 | RoutingPage.feature: "Sync OpenClaw button is available" |
| 3 | RoutingPage.feature: "Refresh button is visible" |
| 4 | RoutingPage.feature: "Skeleton rows shown while loading" |
| 5-8 | RoutingPage.feature: "Routing data grid with columns and status" |
| 9-11 | RoutingPage.feature: "Inline editing updates routing" |
| 12-17 | RoutingPage.feature: "Routing diagram visualization" |
| 18-19 | RoutingPage.feature: "Sync OpenClaw button triggers sync" |
| 20 | RoutingPage.feature: "Channel icons match type" |
| 21 | SettingsPage.feature: "Routing tab accessible with save" |
| 22 | RoutingPage.feature: @planned scenario |

## Consequences

- Inline cell editing means accidental edits can change routing immediately; there's no undo.
- The timeline visualization is linear (not a graph), which works for the current 1:1 channel-to-agent mapping but would need rethinking for many-to-many routing.
