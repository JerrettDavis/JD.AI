# ADR-008: Settings — Gateway Configuration

**Status:** Accepted
**Date:** 2026-03-05
**Relates to:** Settings page (`Pages/Settings.razor`), all `Components/Settings*Tab.razor`

## Context

The Settings page centralizes all gateway configuration in a single tabbed interface. While some settings overlap with dedicated pages (Agents, Channels, Providers, Routing), the Settings page provides the canonical configuration editor.

## Decision

The Settings page (`/settings`) uses MudTabs with 6 tabs, each delegating to a dedicated tab component. The page fetches the full gateway configuration on load and passes relevant sections to each tab. Each tab has an independent save button.

### Components
- **Tab strip**: Server, Providers, Agents, Channels, Routing, OpenClaw (each with icon and tooltip)
- **SettingsServerTab**: Network, auth, and rate-limit configuration
- **SettingsProvidersTab**: Provider enable/disable, test connectivity, model display
- **SettingsAgentsTab**: Agent name, provider, model, parameters configuration
- **SettingsChannelsTab**: Channel credentials and connection settings
- **SettingsRoutingTab**: Routing strategy and channel-to-agent mappings
- **SettingsOpenClawTab**: OpenClaw bridge URL, credentials, and diagnostics

### API Dependencies
- `GET /api/gateway/config` → `GatewayConfigModel` (all sections)
- `PUT /api/gateway/config/server` → Save server settings
- `PUT /api/gateway/config/providers` → Save provider settings
- `PUT /api/gateway/config/agents` → Save agent settings
- `PUT /api/gateway/config/channels` → Save channel settings
- `PUT /api/gateway/config/routing` → Save routing settings
- `PUT /api/gateway/config/openclaw` → Save OpenClaw settings

## Acceptance Criteria

1. Settings page displays "Settings" heading
2. Refresh button is visible in the header
3. Loading skeleton shown while configuration is being fetched
4. Error alert "Unable to load gateway configuration. Is the gateway running?" shown when API fails
5. Tab strip displays 6 tabs: Server, Providers, Agents, Channels, Routing, OpenClaw
6. Server tab has DNS icon and tooltip "Network, auth, and rate-limit settings"
7. Providers tab has Hub icon and tooltip "AI model provider configuration"
8. Agents tab has SmartToy icon and tooltip "Agent definitions and auto-spawn settings"
9. Channels tab has Cable icon and tooltip "Messaging channel connections"
10. Routing tab has AltRoute icon and tooltip "Channel-to-agent routing rules"
11. OpenClaw tab has Cloud icon and tooltip "OpenClaw bridge integration"
12. Clicking "Server" tab shows server settings panel
13. Clicking "Providers" tab shows providers settings panel with save button
14. Clicking "Agents" tab shows agents settings panel with save button
15. Clicking "Channels" tab shows channels settings panel with save button
16. Clicking "Routing" tab shows routing settings panel
17. Clicking "OpenClaw" tab shows OpenClaw settings panel
18. Tab switching does not lose unsaved changes within a session
19. Settings page title is "Settings — JD.AI"

## Test Mapping

| AC# | Feature File Scenario |
|-----|-----------------------|
| 1 | SettingsPage.feature: "Displays settings page heading" |
| 2 | SettingsPage.feature: "Refresh button is visible" |
| 3 | SettingsPage.feature: "Loading skeleton shown while fetching" |
| 4 | SettingsPage.feature: "Error alert when gateway unreachable" |
| 5-11 | SettingsPage.feature: "Tab strip with correct tabs, icons, tooltips" |
| 12 | SettingsPage.feature: "Server tab is accessible" |
| 13 | SettingsPage.feature: "Providers tab accessible with save" |
| 14 | SettingsPage.feature: "Agents tab accessible with save" |
| 15 | SettingsPage.feature: "Channels tab accessible with save" |
| 16 | SettingsPage.feature: "Routing tab is accessible" |
| 17 | SettingsPage.feature: "OpenClaw tab is accessible" |
| 18 | SettingsPage.feature: "Tab switching preserves state" |
| 19 | SettingsPage.feature: "Page title is correct" |

## Consequences

- Full config is fetched in one API call, which is efficient but means all sections load together or fail together.
- Tab components receive config sections as parameters and handle their own save independently.
- No real-time config sync; changes made via CLI or API won't appear until refresh.
