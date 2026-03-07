# ADR-006: Provider Catalog

**Status:** Accepted
**Date:** 2026-03-05
**Relates to:** Providers page (`Pages/Providers.razor`), `Components/SettingsProvidersTab.razor`

## Context

Operators need visibility into which AI providers are configured, whether they're reachable, and what models each offers. This helps with troubleshooting and selecting models when spawning agents.

## Decision

The Providers page (`/providers`) displays providers as cards in a responsive grid. Each card shows the provider name, availability status, model count, and an expandable model list table. The Settings > Providers tab allows enabling/disabling providers and testing connectivity.

### Components
- **Provider cards**: Avatar (green=available, red=unavailable), provider name, model count or status message, Online/Offline chip
- **Model table**: Inline table within card showing Model ID (monospace) and Display Name
- **SettingsProvidersTab**: Enable/disable toggles, test connectivity button, save button

### API Dependencies
- `GET /api/providers` → `ProviderInfo[]` (Name, IsAvailable, StatusMessage, Models[])
- `GET /api/gateway/config` → Provider configuration (Settings tab)
- `PUT /api/gateway/config/providers` → Save provider configuration

## Acceptance Criteria

1. Providers page displays "Model Providers" heading
2. Refresh button is visible
3. Skeleton provider cards (2) shown while loading
4. Empty state displays "No providers configured." when no providers exist
5. Provider cards display when providers exist
6. Each provider card shows the provider name (bold)
7. Available providers show "{N} models" subtitle
8. Unavailable providers show status message or "Unavailable" subtitle
9. Each provider card shows Online (green) or Offline (red) status chip
10. Provider avatar is green when available, red when unavailable
11. Available providers with models show an inline model table
12. Model table has columns: Model ID (monospace font), Display Name
13. Providers with no models do not show the model table
14. Settings > Providers tab displays provider configuration
15. Settings > Providers tab has a save button
16. [Planned] Ollama provider shows model size and context window in model table

## Test Mapping

| AC# | Feature File Scenario |
|-----|-----------------------|
| 1 | ProvidersPage.feature: "Displays providers page heading" |
| 2 | ProvidersPage.feature: "Refresh button is visible" |
| 3 | ProvidersPage.feature: "Skeleton cards shown while loading" |
| 4 | ProvidersPage.feature: "Empty state when no providers" |
| 5-8 | ProvidersPage.feature: "Provider cards show name and availability" |
| 9-10 | ProvidersPage.feature: "Provider status badges" |
| 11-13 | ProvidersPage.feature: "Model table shown for available providers" |
| 14-15 | SettingsPage.feature: "Providers tab accessible with save" |
| 16 | ProvidersPage.feature: @planned scenario |

## Consequences

- Provider availability is point-in-time (snapshot on page load); no auto-refresh or SignalR subscription for provider health changes.
- Model list comes from the provider API; Ollama may take several seconds to enumerate local models on first load.
