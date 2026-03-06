# ADR-001: Gateway Overview

**Status:** Accepted
**Date:** 2026-03-05
**Relates to:** Home page (`Pages/Home.razor`), `MainLayout.razor`

## Context

Operators need an at-a-glance view of the JD.AI Gateway's health: how many agents are running, which channels are connected, active session count, and OpenClaw bridge status. The overview page is the default landing page and doubles as a real-time activity monitor.

## Decision

The home page (`/`) displays four stat cards fetched from `GET /api/gateway/status` and a real-time activity feed powered by the SignalR EventHub. The OpenClaw card uses color-coded iconography (green=connected, red=offline) rather than a numeric count. An OpenClaw Bridge detail table appears when bridge data is present.

### Components
- **Stat cards**: Agents (count), Channels (count), Sessions (count), OpenClaw (status text)
- **OpenClaw Bridge table**: Enabled flag, Registered Agents list (shown only when bridge data exists)
- **Recent Activity feed**: Real-time event list from SignalR `ActivityEvent`, capped at 20 items, reverse chronological
- **Skeleton loaders**: 4 placeholder cards + activity skeleton shown during initial fetch

### API Dependencies
- `GET /api/gateway/status` → `GatewayStatus` (ActiveAgents, ActiveChannels, ActiveSessions, OpenClaw)
- SignalR Hub `/hubs/events` → `ActivityEvent` (EventType, Message, Timestamp)

## Acceptance Criteria

1. Home page displays "Gateway Overview" heading
2. Four stat cards are rendered: Agents, Channels, Sessions, OpenClaw
3. Agents stat card displays a numeric count from API response
4. Channels stat card displays a numeric count from API response
5. Sessions stat card displays a numeric count from API response
6. OpenClaw stat card displays "Connected" or "Offline" text
7. OpenClaw stat card icon is green when connected, red when offline
8. Skeleton placeholder cards (4) are shown while the API response is loading
9. Skeleton activity section is shown while loading
10. OpenClaw Bridge table appears when bridge data exists in the API response
11. Bridge table shows "Enabled" property row
12. Bridge table shows "Registered Agents" property row
13. Recent Activity section heading "Recent Activity" is visible
14. Refresh button is present in the activity section
15. Activity feed empty state shows "No recent activity" message when no events exist
16. Activity events display an event type chip
17. Activity events display a message text
18. Activity events display a timestamp (HH:mm:ss format)
19. Activity feed shows at most 20 items
20. Activity feed displays events in reverse chronological order (newest first)
21. New activity events from SignalR appear in the feed without page refresh
22. When the gateway API is unreachable, stat cards display zero counts gracefully (no error dialog)
23. Sidebar "Agents" link navigates to `/agents`
24. Sidebar "Chat" link navigates to `/chat`

## Test Mapping

| AC# | Feature File Scenario |
|-----|-----------------------|
| 1 | HomePage.feature: "Displays overview heading" |
| 2 | HomePage.feature: "Displays four stat cards" |
| 3-5 | HomePage.feature: "Stat cards show numeric counts from gateway status" |
| 6-7 | HomePage.feature: "OpenClaw card shows connection status" |
| 8-9 | HomePage.feature: "Skeleton cards shown while loading" |
| 10-12 | HomePage.feature: "OpenClaw bridge table shown when bridge data exists" |
| 13-14 | HomePage.feature: "Recent Activity section with heading and refresh" |
| 15 | HomePage.feature: "Activity feed empty state" |
| 16-18 | HomePage.feature: "Activity events display event details" |
| 19-20 | HomePage.feature: "Activity feed shows most recent events first" |
| 21 | HomePage.feature: "New activity events appear without page refresh" |
| 22 | HomePage.feature: "Graceful degradation when API is unreachable" |
| 23-24 | HomePage.feature: "Sidebar navigates to agents/chat page" |

## Consequences

- The overview page makes a single API call (`/api/gateway/status`) on load; if the gateway is slow to respond, skeleton cards provide visual feedback.
- Activity feed is unbounded in memory (capped at 100 in the client list, 20 displayed); for high-throughput gateways this is acceptable since only the latest events matter.
- No authentication gating on this page — all data is operational, not sensitive.
