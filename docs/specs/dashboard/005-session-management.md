# ADR-005: Session Management

**Status:** Accepted
**Date:** 2026-03-05
**Relates to:** Sessions page (`Pages/Sessions.razor`)

## Context

Operators need to review conversation history, monitor active sessions, and export or close sessions. Session data is persisted in SQLite by the gateway and includes turn-by-turn details with token counts and duration metrics.

## Decision

The Sessions page (`/sessions`) uses a MudDataGrid with filtering and multi-column sorting. A selected session expands an inline turn viewer below the grid. Each turn shows role (user/assistant), content, input/output token counts, and response duration.

### Components
- **Session data grid**: Columns (Session ID, Model, Provider, Messages, Tokens, Status chip, Created), filterable, sortable
- **Action buttons per row**: View (eye icon), Export (download icon), Close (X icon, only for active sessions)
- **Turn viewer panel**: Inline expandable panel below grid, shows conversation turns with role chips, content, token/duration metadata
- **Status chips**: "Active" (green) / "Closed" (default)

### API Dependencies
- `GET /api/sessions` → `SessionInfo[]`
- `GET /api/sessions/{id}` → `SessionInfo` (with Turns populated)
- `POST /api/sessions/{id}/export` → Export session data
- `POST /api/sessions/{id}/close` → Close active session

## Acceptance Criteria

1. Sessions page displays "Sessions" heading
2. Refresh button is visible
3. Skeleton loading rows (5) shown while sessions load
4. Empty state displays "No sessions found." when no sessions exist
5. Session data grid displays when sessions exist
6. Data grid has columns: Session ID, Model, Provider, Messages, Tokens, Status, Created
7. Data grid supports filtering
8. Data grid supports multi-column sorting
9. Status column shows "Active" chip (green) for active sessions
10. Status column shows "Closed" chip (default) for closed sessions
11. Each row has a view button (eye icon)
12. Each row has an export button (download icon)
13. Active session rows have a close button (X icon, warning color)
14. Closed session rows do not show a close button
15. Clicking view opens the turn viewer panel below the grid
16. Turn viewer displays "Conversation: {sessionId}" heading
17. Turn viewer has a close button to dismiss the panel
18. Turn viewer shows "No turns in this session." for empty sessions
19. Each turn shows a role chip (user=Primary, assistant=Secondary)
20. Each turn shows the message content with whitespace preserved
21. Each turn shows input/output token counts ("{in}/{out} tokens")
22. Each turn shows response duration ("{ms}ms")
23. Turn entries have a colored left border (primary for user, secondary for assistant)
24. Export action shows success snackbar
25. Export failure shows error snackbar
26. Close action terminates session and shows success snackbar
27. Close action refreshes the session list
28. Close failure shows error snackbar
29. View failure shows error snackbar
30. [Planned] Filters for agent, channel, and date range

## Test Mapping

| AC# | Feature File Scenario |
|-----|-----------------------|
| 1 | SessionsPage.feature: "Displays sessions page heading" |
| 2 | SessionsPage.feature: "Refresh button is visible" |
| 3 | SessionsPage.feature: "Skeleton rows shown while loading" |
| 4 | SessionsPage.feature: "Empty state when no sessions" |
| 5-8 | SessionsPage.feature: "Session data grid with columns, filtering, sorting" |
| 9-10 | SessionsPage.feature: "Session status chips" |
| 11-14 | SessionsPage.feature: "Row action buttons" |
| 15-23 | SessionsPage.feature: "Turn viewer displays conversation details" |
| 24-25 | SessionsPage.feature: "Export session actions" |
| 26-29 | SessionsPage.feature: "Close session actions" |
| 30 | SessionsPage.feature: @planned scenario |

## Consequences

- Turn viewer is inline (not a modal or separate page), which means viewing long conversations pushes the grid up.
- Only one session can be viewed at a time; selecting another replaces the current viewer.
- Export triggers a server-side action but does not download a file to the browser directly.
