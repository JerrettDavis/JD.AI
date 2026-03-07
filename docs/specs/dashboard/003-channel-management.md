# ADR-003: Channel Management

**Status:** Accepted
**Date:** 2026-03-05
**Relates to:** Channels page (`Pages/Channels.razor`), `Components/OverrideDialog.razor`, `Components/SettingsChannelsTab.razor`

## Context

The gateway connects to multiple messaging platforms (Discord, Signal, Slack, Telegram, Web). Operators need to monitor channel connection status, connect/disconnect channels, configure per-channel routing overrides, and sync routing with OpenClaw.

## Decision

The Channels page (`/channels`) displays channels as card-based layout (MudCard in a MudGrid) rather than a data grid, since each channel is a distinct entity with visual status. Channel cards show name, type, connection status badge, connect/disconnect action, and an Override button that opens the OverrideDialog.

### Components
- **Channel cards**: Avatar (color-coded by status), DisplayName, ChannelType subtitle, Online/Offline chip, Connect/Disconnect button, Override button
- **OverrideDialog**: Agent ID, Model, Routing Mode dropdown (Passthrough/Sidecar/Intercept), Override Enabled switch, Cancel/Save buttons
- **Sync OpenClaw button**: Triggers `POST /api/gateway/openclaw/sync`
- **SettingsChannelsTab**: Channel credentials with save button

### API Dependencies
- `GET /api/channels` → `ChannelInfo[]` (ChannelType, DisplayName, IsConnected)
- `POST /api/channels/{type}` → Connect channel
- `DELETE /api/channels/{type}` → Disconnect channel
- `POST /api/routing/map` → Save routing override
- `POST /api/gateway/openclaw/sync` → Sync with OpenClaw

## Acceptance Criteria

1. Channels page displays "Channels" heading
2. "Sync OpenClaw" button is visible with sync icon
3. Refresh button is visible
4. Skeleton channel cards (3) shown while loading
5. Empty state displays "No channels configured. Check your Gateway configuration." when no channels exist
6. Channel cards display when channels exist
7. Each channel card shows the channel display name
8. Each channel card shows the channel type as subtitle
9. Each channel card shows a status badge: "Online" (green) or "Offline" (default)
10. Channel avatar is color-coded: green (Success) when connected, default when disconnected
11. Connected channels show a "Disconnect" button (warning color)
12. Disconnected channels show a "Connect" button (success color)
13. Each channel card has an "Override" button with edit icon
14. Clicking Connect triggers API call and shows success snackbar
15. Clicking Disconnect triggers API call and shows warning snackbar
16. Connect/Disconnect API failure shows error snackbar
17. Clicking Override opens the override dialog with channel name in title
18. Override dialog contains Agent ID field
19. Override dialog contains Model field
20. Override dialog contains Routing Mode dropdown with Passthrough/Sidecar/Intercept options
21. Override dialog contains Override Enabled switch
22. Override dialog has Cancel and Save buttons
23. Save button shows progress spinner while submitting
24. Successful override save shows success snackbar and refreshes channel list
25. Override save failure shows error snackbar
26. Sync OpenClaw shows success snackbar on completion
27. Sync OpenClaw failure shows error snackbar
28. Channel icons match channel type (Discord=Forum, Signal=Security, Slack=Tag, Telegram=Send, Web=Language)
29. Settings > Channels tab shows channel configuration with save button
30. [Planned] Real-time channel status changes update badges without page refresh
31. [Planned] Test Channel action verifies channel connectivity

## Test Mapping

| AC# | Feature File Scenario |
|-----|-----------------------|
| 1 | ChannelsPage.feature: "Displays channels page heading" |
| 2 | ChannelsPage.feature: "Sync OpenClaw button is visible" |
| 3 | ChannelsPage.feature: "Refresh button is visible" |
| 4 | ChannelsPage.feature: "Skeleton cards shown while loading" |
| 5 | ChannelsPage.feature: "Empty state when no channels configured" |
| 6-8 | ChannelsPage.feature: "Channel cards display with name and type" |
| 9-10 | ChannelsPage.feature: "Channel status badges show Online or Offline" |
| 11-12 | ChannelsPage.feature: "Connect and disconnect buttons shown by status" |
| 13 | ChannelsPage.feature: "Override button available on each channel" |
| 14-16 | ChannelsPage.feature: "Connect and disconnect trigger API calls" |
| 17-25 | ChannelsPage.feature: "Override dialog workflow" |
| 26-27 | ChannelsPage.feature: "Sync OpenClaw button triggers sync" |
| 28 | ChannelsPage.feature: "Channel icons match channel type" |
| 29 | SettingsPage.feature: "Channels tab accessible with save" |
| 30-31 | ChannelsPage.feature: @planned scenarios |

## Consequences

- Channel cards are laid out in a responsive grid (xs=12, md=6, lg=4), which means on mobile all cards stack vertically.
- Override dialog currently only saves the agent ID via `MapRoutingAsync`; the model, routing mode, and enabled toggle are UI-present but the API integration for those fields may need backend expansion.
- Connect/Disconnect are toggle actions with immediate effect — no confirmation dialog (unlike agent delete).
