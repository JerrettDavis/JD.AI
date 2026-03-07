# ADR-009: Cross-Cutting Concerns

**Status:** Accepted
**Date:** 2026-03-05
**Relates to:** `Layout/MainLayout.razor`, `Layout/NavMenu.razor`, `Services/SignalRService.cs`, all pages

## Context

Several behaviors span the entire dashboard and are not specific to any single page: navigation, theming, real-time connection management, loading states, error handling patterns, and page title conventions.

## Decision

Cross-cutting concerns are handled by the layout layer (MainLayout, NavMenu) and the shared SignalRService. The dashboard uses a dark theme exclusively with MudBlazor's theming system. SignalR auto-reconnection uses exponential backoff.

### Components
- **MainLayout**: App bar (logo, SignalR status indicator), drawer (collapsible sidebar), main content area
- **NavMenu**: 8 navigation links with Material Design icons
- **SignalRService**: Manages EventHub and AgentHub connections, exposes `IsConnected` state, fires `OnStateChanged`
- **MudTheme**: Custom dark palette with indigo/purple primary colors

### Navigation Links
| Label | Route | Icon |
|-------|-------|------|
| Overview | `/` | Dashboard |
| Chat | `/chat` | Chat |
| Channels | `/channels` | Cable |
| Agents | `/agents` | SmartToy |
| Sessions | `/sessions` | Forum |
| Providers | `/providers` | Hub |
| Routing | `/routing` | AltRoute |
| Settings | `/settings` | Settings |

### Page Title Convention
All pages follow the pattern: `{PageName} — JD.AI` (using em dash). Home page: `JD.AI — Gateway`.

### Error Handling Patterns
- **API errors**: Caught in `try/catch`, shown as MudSnackbar with `Severity.Error` and `$"Failed: {ex.Message}"`
- **Missing data**: Pages set data to `null` on catch and show empty states
- **No blocking error dialogs**: Errors are non-modal snackbar notifications

## Acceptance Criteria

1. Dark theme is applied consistently across all pages
2. App bar displays "JD.AI Gateway" logo text
3. App bar hamburger menu toggles the sidebar drawer
4. SignalR connection indicator shows "Live" (green) when connected
5. SignalR connection indicator shows "Offline" (red) when disconnected
6. Sidebar contains 8 navigation links: Overview, Chat, Channels, Agents, Sessions, Providers, Routing, Settings
7. Each nav link has the correct icon
8. Clicking a nav link navigates to the correct route
9. Active page's nav link is visually highlighted
10. Sidebar can be collapsed via hamburger menu
11. All pages show loading skeletons during initial data fetch
12. API errors display as non-modal snackbar notifications (not blocking dialogs)
13. Home page title is "JD.AI — Gateway"
14. Chat page title is "Chat — JD.AI"
15. Agents page title is "Agents — JD.AI"
16. Channels page title is "Channels — JD.AI"
17. Sessions page title is "Sessions — JD.AI"
18. Providers page title is "Providers — JD.AI"
19. Routing page title is "Routing — JD.AI"
20. Settings page title is "Settings — JD.AI"
21. Browser back/forward navigation works correctly with Blazor routing
22. 404/unknown routes display the NotFound page

## Test Mapping

| AC# | Feature File Scenario |
|-----|-----------------------|
| 1 | Cross-cutting tests in each page's feature file |
| 2-3 | HomePage.feature: "App bar displays logo and hamburger menu" |
| 4-5 | HomePage.feature: "SignalR connection indicator" |
| 6-8 | HomePage.feature: "Sidebar navigation links" |
| 9 | Each page feature: "Active nav link is highlighted" |
| 10 | HomePage.feature: "Sidebar can be collapsed" |
| 11 | Each page feature: skeleton scenarios |
| 12 | Each page feature: error handling scenarios |
| 13-20 | Each page feature: "Page title is correct" |
| 21-22 | HomePage.feature: "Browser navigation and 404" |

## Consequences

- Dark theme only — no light mode toggle. This is intentional for the operator-focused dashboard aesthetic.
- SignalR reconnection backoff means there can be 10-30 seconds where the dashboard shows "Offline" after a brief network interruption.
- Snackbar-based errors can be missed if the operator is not watching; no persistent error log in the UI.
