# Dashboard Comprehensive Specs Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create 9 functional-area ADR/spec documents and expand 8 per-page Reqnroll+Playwright feature files to comprehensively cover all dashboard functionality.

**Architecture:** Two-tier specification approach: (1) Functional-area ADR documents in `docs/specs/dashboard/` define acceptance criteria, context, and planned enhancements. (2) Per-page `.feature` files in `tests/JD.AI.Specs.UI/Features/Dashboard/` contain Gherkin scenarios that map to ADR acceptance criteria. Page objects and step definitions are expanded to support the new scenarios.

**Tech Stack:** Reqnroll (BDD/Gherkin), Playwright (.NET), xUnit, Blazor WASM (MudBlazor), ASP.NET Core Gateway

---

## Context & Conventions

### Project Layout
```
docs/specs/dashboard/          ← NEW: ADR documents (9 files)
tests/JD.AI.Specs.UI/
  Features/Dashboard/          ← EXISTING: Feature files (8 files, to be expanded)
  PageObjects/                 ← EXISTING: Page objects (9 files, to be expanded)
  StepDefinitions/             ← EXISTING: Step definitions (8 files, to be expanded)
  Support/                     ← EXISTING: DashboardFixture.cs, PlaywrightHooks.cs
```

### ADR Document Format
Each ADR follows this structure:
```markdown
# ADR-NNN: [Title]

**Status:** Accepted | Proposed
**Date:** 2026-03-05
**Relates to:** [pages/components covered]

## Context
Why this feature exists and what problem it solves.

## Decision
What was built and how it works.

## Acceptance Criteria
Numbered list. Each criterion maps to one or more Gherkin scenarios.
Items tagged `[Planned]` are not yet implemented.

## Test Mapping
Table mapping acceptance criteria numbers to feature file scenarios.

## Consequences
Trade-offs, dependencies, operational concerns.
```

### Feature File Conventions
- All scenarios tagged `@ui`
- Fast subset tagged `@smoke` (heading visible, key elements, navigation)
- Unimplemented features tagged `@planned`
- Precondition-dependent tests tagged `@requires-agents`, `@requires-channels`, etc.
- Scenarios grouped by category: Rendering, Loading, Empty States, Data Display, User Actions, Validation, Error Handling, Navigation, Real-Time
- Tests run against a live Gateway instance (env var `DASHBOARD_BASE_URL`)

### Page Object Conventions
- Extend `BasePage` (provides `NavigateAsync()`, `WaitForLoadAsync()`, `PageTitle`, `RefreshButton`, `NavMenu`)
- Locators prefer `data-testid` attributes, fall back to CSS classes (`.jd-*`)
- Helper methods for multi-step interactions (e.g., `SendMessageAsync()`)

### Step Definition Conventions
- One step definition class per page
- `[BeforeScenario("@ui", Order = 10)]` initializes page object from `ScenarioContext`
- Use `static Microsoft.Playwright.Assertions` for `Expect()` assertions
- Parameterized steps use regex captures

### Running Tests
```bash
# Build only (default CI — skipped because IsTestProject=false)
dotnet build tests/JD.AI.Specs.UI/JD.AI.Specs.UI.csproj

# Run all UI tests (requires live Gateway + Playwright browsers installed)
dotnet test tests/JD.AI.Specs.UI/JD.AI.Specs.UI.csproj

# Run smoke tests only
dotnet test tests/JD.AI.Specs.UI/JD.AI.Specs.UI.csproj --filter "Category=smoke"

# Skip planned/unimplemented
dotnet test tests/JD.AI.Specs.UI/JD.AI.Specs.UI.csproj --filter "Category!=planned"
```

---

## Task 1: Create `docs/specs/dashboard/` directory and ADR-001 Gateway Overview

**Files:**
- Create: `docs/specs/dashboard/001-gateway-overview.md`

**Step 1: Write ADR-001**

```markdown
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
```

**Step 2: Commit**

```bash
git add docs/specs/dashboard/001-gateway-overview.md
git commit -m "docs: add ADR-001 gateway overview specification"
```

---

## Task 2: ADR-002 Agent Lifecycle

**Files:**
- Create: `docs/specs/dashboard/002-agent-lifecycle.md`

**Step 1: Write ADR-002**

```markdown
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
```

**Step 2: Commit**

```bash
git add docs/specs/dashboard/002-agent-lifecycle.md
git commit -m "docs: add ADR-002 agent lifecycle specification"
```

---

## Task 3: ADR-003 Channel Management

**Files:**
- Create: `docs/specs/dashboard/003-channel-management.md`

**Step 1: Write ADR-003**

```markdown
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
```

**Step 2: Commit**

```bash
git add docs/specs/dashboard/003-channel-management.md
git commit -m "docs: add ADR-003 channel management specification"
```

---

## Task 4: ADR-004 Web Chat

**Files:**
- Create: `docs/specs/dashboard/004-web-chat.md`

**Step 1: Write ADR-004**

```markdown
# ADR-004: Web Chat

**Status:** Accepted
**Date:** 2026-03-05
**Relates to:** Chat page (`Pages/Chat.razor`)

## Context

Operators and developers need a way to test agent responses directly from the dashboard without connecting external messaging platforms. The web chat provides a real-time streaming conversation interface similar to consumer AI chat products.

## Decision

The Chat page (`/chat`) implements a full-screen chat layout with three zones: header (agent selector), message area (scrollable bubbles), and input bar. Messages stream via SignalR's `StreamChatAsync` hub method, with chunked content delivery and a blinking cursor indicator during streaming.

### Components
- **Chat header**: "Web Chat" title, agent selector dropdown (or "No agents running" warning chip), clear chat button
- **Message area**: Scrollable container with user bubbles (right-aligned, indigo tint) and agent bubbles (left-aligned, gray tint), empty state with "Start a conversation" message
- **Input bar**: Text field with "Type a message…" placeholder, send adornment icon, Enter-to-send (Shift+Enter for newline)
- **Streaming indicator**: Blinking cursor (▋) in agent bubble during active stream

### API Dependencies
- `GET /api/agents` → Available agents for selector
- SignalR Hub `/hubs/agent` → `StreamChatAsync(agentId, message)` → `IAsyncEnumerable<ChatChunk>`
- `ChatChunk`: `{ Type: "content"|"error", Content: string }`

## Acceptance Criteria

1. Chat page displays "Web Chat" header with chat icon
2. Agent selector dropdown populates from running agents
3. Each agent option shows ID with provider/model in parentheses
4. "No agents running" warning chip shown when no agents exist
5. Empty state displays "Start a conversation" heading
6. Empty state displays helper text "Type a message below to chat with the selected agent."
7. Message input has placeholder "Type a message…"
8. Message input has send icon adornment
9. Input is disabled when no agent is selected
10. Input is disabled while streaming a response
11. Pressing Enter sends the message
12. Pressing Shift+Enter does not send (allows multiline)
13. Sending adds a user bubble right-aligned with "You" label
14. User bubble displays the sent message text
15. User bubble displays timestamp (HH:mm:ss)
16. Agent response streams with a blinking cursor indicator (▋)
17. Streaming bubble shows agent ID as label
18. Completed response becomes a permanent agent bubble (left-aligned)
19. Agent bubble displays timestamp (HH:mm:ss)
20. Messages preserve whitespace (pre-wrap CSS)
21. Messages area auto-scrolls to bottom on new messages
22. Clear chat button (trash icon) removes all messages
23. Clear chat button is disabled when no messages exist
24. Agent error during streaming shows error snackbar with "Agent error: {message}"
25. Connection error during chat shows error snackbar
26. Sending empty or whitespace-only input does nothing
27. Chat page title is "Chat — JD.AI"

## Test Mapping

| AC# | Feature File Scenario |
|-----|-----------------------|
| 1 | ChatPage.feature: "Displays chat header with title" |
| 2-3 | ChatPage.feature: "Agent selector populates from running agents" |
| 4 | ChatPage.feature: "No agents warning when none running" |
| 5-6 | ChatPage.feature: "Empty state shown when no messages" |
| 7-8 | ChatPage.feature: "Message input with placeholder and send icon" |
| 9-10 | ChatPage.feature: "Input disabled when no agent or streaming" |
| 11-12 | ChatPage.feature: "Enter sends message, Shift+Enter does not" |
| 13-15 | ChatPage.feature: "User message bubble appears after sending" |
| 16-19 | ChatPage.feature: "Agent response streams with cursor indicator" |
| 20 | ChatPage.feature: "Messages preserve whitespace" |
| 21 | ChatPage.feature: "Chat auto-scrolls on new messages" |
| 22-23 | ChatPage.feature: "Clear chat removes all messages" |
| 24-25 | ChatPage.feature: "Error handling during chat" |
| 26 | ChatPage.feature: "Empty input does not send" |
| 27 | ChatPage.feature: "Page title is correct" |

## Consequences

- Chat state is entirely client-side (in-memory `List<ChatMessage>`); refreshing the page loses all history.
- Only one conversation at a time; no multi-session support in the web chat.
- Streaming cancellation is supported via CancellationTokenSource but not exposed in the UI (no stop button).
```

**Step 2: Commit**

```bash
git add docs/specs/dashboard/004-web-chat.md
git commit -m "docs: add ADR-004 web chat specification"
```

---

## Task 5: ADR-005 Session Management

**Files:**
- Create: `docs/specs/dashboard/005-session-management.md`

**Step 1: Write ADR-005**

```markdown
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
```

**Step 2: Commit**

```bash
git add docs/specs/dashboard/005-session-management.md
git commit -m "docs: add ADR-005 session management specification"
```

---

## Task 6: ADR-006 Provider Catalog

**Files:**
- Create: `docs/specs/dashboard/006-provider-catalog.md`

**Step 1: Write ADR-006**

```markdown
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
```

**Step 2: Commit**

```bash
git add docs/specs/dashboard/006-provider-catalog.md
git commit -m "docs: add ADR-006 provider catalog specification"
```

---

## Task 7: ADR-007 Routing Configuration

**Files:**
- Create: `docs/specs/dashboard/007-routing-configuration.md`

**Step 1: Write ADR-007**

```markdown
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
```

**Step 2: Commit**

```bash
git add docs/specs/dashboard/007-routing-configuration.md
git commit -m "docs: add ADR-007 routing configuration specification"
```

---

## Task 8: ADR-008 Settings Gateway

**Files:**
- Create: `docs/specs/dashboard/008-settings-gateway.md`

**Step 1: Write ADR-008**

```markdown
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
```

**Step 2: Commit**

```bash
git add docs/specs/dashboard/008-settings-gateway.md
git commit -m "docs: add ADR-008 settings gateway specification"
```

---

## Task 9: ADR-009 Cross-Cutting Concerns

**Files:**
- Create: `docs/specs/dashboard/009-cross-cutting.md`

**Step 1: Write ADR-009**

```markdown
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
```

**Step 2: Commit**

```bash
git add docs/specs/dashboard/009-cross-cutting.md
git commit -m "docs: add ADR-009 cross-cutting concerns specification"
```

---

## Task 10: Expand HomePage.feature

**Files:**
- Modify: `tests/JD.AI.Specs.UI/Features/Dashboard/HomePage.feature`

**Step 1: Replace feature file with expanded version**

```gherkin
@ui
Feature: Home Page - Gateway Overview
    As a gateway operator
    I want to see an overview of the system status
    So that I can quickly assess the health of my AI gateway

    Background:
        Given I am on the home page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays overview heading
        Then I should see the heading "Gateway Overview"

    @smoke
    Scenario: Displays four stat cards
        Then I should see 4 stat cards
        And I should see a stat card labeled "Agents"
        And I should see a stat card labeled "Channels"
        And I should see a stat card labeled "Sessions"
        And I should see a stat card labeled "OpenClaw"

    Scenario: App bar displays logo
        Then the app bar should display "JD.AI"
        And the app bar should display "Gateway"

    # ── Data display ──────────────────────────────────────
    Scenario: Stat cards show numeric counts from gateway status
        Then the "Agents" stat card should display a numeric value
        And the "Channels" stat card should display a numeric value
        And the "Sessions" stat card should display a numeric value

    Scenario: OpenClaw card shows connection status
        Then the "OpenClaw" stat card should display "Connected" or "Offline"

    @requires-openclaw
    Scenario: OpenClaw bridge table shown when bridge data exists
        Given the OpenClaw bridge is configured
        Then I should see the OpenClaw Bridge details table
        And the table should show the "Enabled" property
        And the table should show "Registered Agents"

    # ── Loading states ────────────────────────────────────
    Scenario: Skeleton cards shown while loading
        Given the gateway status is loading
        Then I should see skeleton stat card placeholders
        And I should see a skeleton activity section

    # ── Activity feed ─────────────────────────────────────
    Scenario: Recent Activity section with heading and refresh
        Then I should see the "Recent Activity" section heading
        And I should see a refresh button in the activity section

    Scenario: Activity feed empty state
        Given there are no recent activity events
        Then I should see "No recent activity" in the activity feed

    @requires-agents
    Scenario: Activity events display event details
        Given there are recent activity events
        Then each activity event should show an event type chip
        And each activity event should show a message
        And each activity event should show a timestamp

    @requires-agents
    Scenario: Activity feed shows most recent events first
        Given there are multiple activity events
        Then the activity feed should display events in reverse chronological order
        And the feed should show at most 20 items

    # ── Real-time ─────────────────────────────────────────
    @requires-agents
    Scenario: New activity events appear without page refresh
        Given I am observing the activity feed
        When a new gateway event occurs
        Then the event should appear in the activity feed

    # ── Error handling ────────────────────────────────────
    Scenario: Graceful degradation when API is unreachable
        Given the gateway API is unavailable
        Then the stat cards should display zero counts
        And no error dialog should block the page

    # ── Navigation ────────────────────────────────────────
    @smoke
    Scenario: Sidebar navigates to agents page
        When I click the "Agents" navigation link
        Then I should be on the "/agents" page

    @smoke
    Scenario: Sidebar navigates to chat page
        When I click the "Chat" navigation link
        Then I should be on the "/chat" page

    Scenario: Sidebar navigates to channels page
        When I click the "Channels" navigation link
        Then I should be on the "/channels" page

    Scenario: Sidebar navigates to sessions page
        When I click the "Sessions" navigation link
        Then I should be on the "/sessions" page

    Scenario: Sidebar navigates to providers page
        When I click the "Providers" navigation link
        Then I should be on the "/providers" page

    Scenario: Sidebar navigates to routing page
        When I click the "Routing" navigation link
        Then I should be on the "/routing" page

    Scenario: Sidebar navigates to settings page
        When I click the "Settings" navigation link
        Then I should be on the "/settings" page

    # ── SignalR connection ────────────────────────────────
    Scenario: SignalR connection indicator shows status
        Then the app bar should show a connection status indicator
        And the indicator should display "Live" or "Offline"

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "JD.AI — Gateway"
```

**Step 2: Commit**

```bash
git add tests/JD.AI.Specs.UI/Features/Dashboard/HomePage.feature
git commit -m "test: expand HomePage.feature with comprehensive scenarios"
```

---

## Task 11: Expand AgentsPage.feature

**Files:**
- Modify: `tests/JD.AI.Specs.UI/Features/Dashboard/AgentsPage.feature`

**Step 1: Replace feature file with expanded version**

```gherkin
@ui
Feature: Agents Page
    As a gateway operator
    I want to manage AI agents from the dashboard
    So that I can spawn, monitor, and remove agents

    Background:
        Given I am on the agents page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays agent page heading
        Then I should see the heading "Agents"

    @smoke
    Scenario: Spawn button is visible
        Then I should see the "Spawn Agent" button with add icon

    Scenario: Refresh button is visible
        Then I should see the refresh button

    # ── Loading states ────────────────────────────────────
    Scenario: Skeleton rows shown while loading
        Given the agents list is loading
        Then I should see 5 skeleton loading rows

    # ── Empty state ───────────────────────────────────────
    Scenario: Empty state shown when no agents
        Given there are no active agents
        Then I should see the agents empty state
        And the empty state should display "No active agents"

    # ── Data display ──────────────────────────────────────
    @requires-agents
    Scenario: Agent data grid displays with correct columns
        Given there are active agents
        Then I should see the agents data grid
        And the data grid should have an "ID" column
        And the data grid should have a "Provider" column
        And the data grid should have a "Model" column
        And the data grid should have a "Turns" column
        And the data grid should have a "Created" column

    @requires-agents
    Scenario: Each agent row has delete button
        Given there are active agents
        Then each agent row should have a red delete button

    # ── Spawn dialog ──────────────────────────────────────
    Scenario: Spawn dialog opens with all fields
        When I click the "Spawn Agent" button
        Then the spawn agent dialog should be visible
        And the dialog title should be "Spawn New Agent"
        And the dialog should contain an "Agent ID" input
        And the dialog should contain a "Provider" input
        And the dialog should contain a "Model" input
        And the dialog should contain a "System Prompt" multiline input
        And the dialog should contain a "Max Turns" numeric input
        And the dialog should have a "Cancel" button
        And the dialog should have a "Spawn" button

    @requires-agents
    Scenario: Successful spawn shows snackbar and refreshes list
        When I click the "Spawn Agent" button
        And I fill in the agent ID with a unique value
        And I fill in the provider with "ollama"
        And I fill in the model with "test-model"
        And I click the "Spawn" button
        Then a success snackbar should appear with "spawned"
        And the agents data grid should refresh

    Scenario: Spawn with empty ID does not submit
        When I click the "Spawn Agent" button
        And I leave the agent ID empty
        And I click the "Spawn" button
        Then the dialog should remain open

    Scenario: Spawn dialog can be cancelled
        When I click the "Spawn Agent" button
        And I click the "Cancel" button
        Then the dialog should close

    # ── Delete agent ──────────────────────────────────────
    @requires-agents
    Scenario: Delete agent with confirmation
        Given there are active agents
        When I click the delete button on the first agent
        Then a confirmation dialog should appear with "Stop agent"
        When I confirm the deletion
        Then a success snackbar should appear with "stopped"

    @requires-agents
    Scenario: Delete agent can be cancelled
        Given there are active agents
        When I click the delete button on the first agent
        Then a confirmation dialog should appear
        When I cancel the deletion
        Then the agent should still be in the list

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "Agents — JD.AI"

    # ── Planned ───────────────────────────────────────────
    @planned
    Scenario: Agent grid shows expandable details
        Given there are active agents
        When I expand the first agent row
        Then I should see the agent's system prompt
        And I should see the agent's active sessions

    @planned
    Scenario: Test message action
        Given there are active agents
        When I click the test message button on the first agent
        Then a test response should appear
```

**Step 2: Commit**

```bash
git add tests/JD.AI.Specs.UI/Features/Dashboard/AgentsPage.feature
git commit -m "test: expand AgentsPage.feature with comprehensive scenarios"
```

---

## Task 12: Expand ChatPage.feature

**Files:**
- Modify: `tests/JD.AI.Specs.UI/Features/Dashboard/ChatPage.feature`

**Step 1: Replace feature file with expanded version**

```gherkin
@ui
Feature: Chat Page
    As a gateway operator
    I want to interact with AI agents through a web chat interface
    So that I can test agent responses and have conversations

    Background:
        Given I am on the chat page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays chat header with title
        Then I should see the chat header
        And the header should display "Web Chat"
        And the header should have a chat icon

    @smoke
    Scenario: Displays message input area
        Then I should see the message input field
        And the message input should have placeholder "Type a message…"
        And the message input should have a send icon

    # ── Agent selector ────────────────────────────────────
    @requires-agents
    Scenario: Agent selector populates from running agents
        Then I should see the agent selector dropdown
        And the dropdown should contain at least one agent option

    Scenario: No agents warning when none running
        Given there are no running agents
        Then I should see "No agents running" warning chip

    # ── Empty state ───────────────────────────────────────
    Scenario: Empty state shown when no messages
        Then I should see the chat empty state
        And the empty state should display "Start a conversation"
        And the empty state should display "Type a message below"

    # ── Input behavior ────────────────────────────────────
    Scenario: Input disabled when no agent selected
        Given there are no running agents
        Then the message input should be disabled

    Scenario: Empty input does not send
        Given an agent is selected
        When I type "" in the message input
        And I press Enter in the message input
        Then no message bubble should appear

    # ── Sending messages ──────────────────────────────────
    @requires-agents
    Scenario: User message bubble appears after sending
        Given an agent is selected
        When I type "Hello" in the message input
        And I send the message
        Then a user message bubble should appear on the right
        And the message bubble should contain "Hello"
        And the user bubble should show "You" label
        And the user bubble should show a timestamp

    @requires-agents
    Scenario: Agent response streams with cursor indicator
        Given an agent is selected
        When I type "Hi there" in the message input
        And I send the message
        Then a streaming indicator should appear
        And an agent response bubble should eventually appear on the left

    # ── Clear chat ────────────────────────────────────────
    Scenario: Clear chat button disabled when empty
        Then the clear chat button should be disabled

    @requires-agents
    Scenario: Clear chat removes all messages
        Given an agent is selected
        And I have sent a message "test"
        When I click the clear chat button
        Then no message bubbles should be visible
        And the empty state should be displayed

    # ── Error handling ────────────────────────────────────
    @requires-agents
    Scenario: Error during chat shows snackbar
        Given an agent is selected
        When a chat error occurs
        Then an error snackbar should appear

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "Chat — JD.AI"
```

**Step 2: Commit**

```bash
git add tests/JD.AI.Specs.UI/Features/Dashboard/ChatPage.feature
git commit -m "test: expand ChatPage.feature with comprehensive scenarios"
```

---

## Task 13: Expand ChannelsPage.feature

**Files:**
- Modify: `tests/JD.AI.Specs.UI/Features/Dashboard/ChannelsPage.feature`

**Step 1: Replace feature file with expanded version**

```gherkin
@ui
Feature: Channels Page
    As a gateway operator
    I want to view and manage messaging channel connections
    So that I can control which platforms my agents communicate through

    Background:
        Given I am on the channels page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays channels page heading
        Then I should see the heading "Channels"

    @smoke
    Scenario: Sync OpenClaw button is visible
        Then I should see the "Sync OpenClaw" button with sync icon

    Scenario: Refresh button is visible
        Then I should see the refresh button

    # ── Loading states ────────────────────────────────────
    Scenario: Skeleton cards shown while loading
        Given the channels are loading
        Then I should see 3 skeleton channel cards

    # ── Empty state ───────────────────────────────────────
    Scenario: Empty state when no channels configured
        Given there are no configured channels
        Then I should see the channels empty state
        And the empty state should display "No channels configured"

    # ── Data display ──────────────────────────────────────
    @requires-channels
    Scenario: Channel cards display with name and type
        Given there are configured channels
        Then I should see channel cards
        And each channel card should display a display name
        And each channel card should display the channel type

    @requires-channels
    Scenario: Channel status badges show Online or Offline
        Given there are configured channels
        Then each channel card should show a status badge
        And status badges should display "Online" or "Offline"

    @requires-channels
    Scenario: Channel avatar reflects connection status
        Given there are configured channels
        Then connected channel avatars should be green
        And disconnected channel avatars should be default color

    # ── Connect/Disconnect ────────────────────────────────
    @requires-channels
    Scenario: Connect and disconnect buttons shown by status
        Given there are configured channels
        Then connected channels should show a "Disconnect" button
        And disconnected channels should show a "Connect" button

    @requires-channels
    Scenario: Connect channel triggers API and shows snackbar
        Given there is a disconnected channel
        When I click the "Connect" button on the channel
        Then a success snackbar should appear with "connected"

    @requires-channels
    Scenario: Disconnect channel triggers API and shows snackbar
        Given there is a connected channel
        When I click the "Disconnect" button on the channel
        Then a warning snackbar should appear with "disconnected"

    # ── Override dialog ───────────────────────────────────
    @requires-channels
    Scenario: Override button available on each channel
        Given there are configured channels
        Then each channel card should have an "Override" button with edit icon

    @requires-channels
    Scenario: Override dialog opens with channel name
        Given there are configured channels
        When I click the "Override" button on a channel
        Then the override dialog should be visible
        And the dialog title should contain the channel name

    @requires-channels
    Scenario: Override dialog contains all configuration fields
        Given there are configured channels
        When I click the "Override" button on a channel
        Then the dialog should contain an "Agent ID" field
        And the dialog should contain a "Model" field
        And the dialog should contain a "Routing Mode" dropdown
        And the "Routing Mode" dropdown should have options "Passthrough", "Sidecar", "Intercept"
        And the dialog should contain an "Override Enabled" switch
        And the dialog should have "Cancel" and "Save" buttons

    @requires-channels
    Scenario: Override dialog can be cancelled
        Given there are configured channels
        When I click the "Override" button on a channel
        And I click "Cancel" in the override dialog
        Then the override dialog should close

    @requires-channels
    Scenario: Override save shows success snackbar
        Given there are configured channels
        When I click the "Override" button on a channel
        And I fill in the agent ID
        And I click "Save" in the override dialog
        Then a success snackbar should appear with "Override saved"

    # ── Sync OpenClaw ─────────────────────────────────────
    Scenario: Sync OpenClaw triggers sync and shows snackbar
        When I click the "Sync OpenClaw" button
        Then a success snackbar should appear with "sync complete"

    # ── Channel icons ─────────────────────────────────────
    @requires-channels
    Scenario: Channel icons match channel type
        Given there are configured channels
        Then each channel card should display an icon matching its type

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "Channels — JD.AI"

    # ── Planned ───────────────────────────────────────────
    @planned
    Scenario: Real-time channel status updates
        Given there are configured channels
        When a channel status changes via SignalR
        Then the status badge should update without page refresh

    @planned
    Scenario: Test channel connectivity
        Given there are configured channels
        When I click the "Test" button on a channel
        Then a connectivity test result should appear
```

**Step 2: Commit**

```bash
git add tests/JD.AI.Specs.UI/Features/Dashboard/ChannelsPage.feature
git commit -m "test: expand ChannelsPage.feature with comprehensive scenarios"
```

---

## Task 14: Expand SessionsPage.feature

**Files:**
- Modify: `tests/JD.AI.Specs.UI/Features/Dashboard/SessionsPage.feature`

**Step 1: Replace feature file with expanded version**

```gherkin
@ui
Feature: Sessions Page
    As a gateway operator
    I want to view and manage conversation sessions
    So that I can monitor active conversations and review past ones

    Background:
        Given I am on the sessions page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays sessions page heading
        Then I should see the heading "Sessions"

    Scenario: Refresh button is visible
        Then I should see the refresh button

    # ── Loading states ────────────────────────────────────
    Scenario: Skeleton rows shown while loading
        Given the sessions list is loading
        Then I should see 5 skeleton loading rows

    # ── Empty state ───────────────────────────────────────
    Scenario: Empty state when no sessions
        Given there are no sessions
        Then I should see the sessions empty state
        And the empty state should display "No sessions found"

    # ── Data display ──────────────────────────────────────
    @requires-sessions
    Scenario: Session data grid displays with correct columns
        Given there are sessions
        Then I should see the sessions data grid
        And the grid should have a "Session ID" column
        And the grid should have a "Model" column
        And the grid should have a "Provider" column
        And the grid should have a "Messages" column
        And the grid should have a "Tokens" column
        And the grid should have a "Status" column
        And the grid should have a "Created" column

    @requires-sessions
    Scenario: Session status chips display correctly
        Given there are sessions
        Then active sessions should show "Active" chip in green
        And closed sessions should show "Closed" chip in default color

    @requires-sessions
    Scenario: Data grid supports filtering
        Given there are sessions
        Then the data grid should have filter controls

    @requires-sessions
    Scenario: Data grid supports sorting
        Given there are sessions
        When I click a column header
        Then the grid should sort by that column

    # ── Row actions ───────────────────────────────────────
    @requires-sessions
    Scenario: Row action buttons are present
        Given there are sessions
        Then each session row should have a view button
        And each session row should have an export button

    @requires-sessions
    Scenario: Active sessions have close button
        Given there are active sessions
        Then active session rows should have a close button in warning color

    @requires-sessions
    Scenario: Closed sessions do not have close button
        Given there are closed sessions
        Then closed session rows should not have a close button

    # ── Turn viewer ───────────────────────────────────────
    @requires-sessions
    Scenario: View session opens turn viewer
        Given there are sessions
        When I click the view button on a session
        Then the turn viewer panel should appear below the grid
        And the turn viewer should show "Conversation:" with the session ID

    @requires-sessions
    Scenario: Turn viewer displays conversation turns
        Given there are sessions with turns
        When I click the view button on a session
        Then each turn should show a role chip
        And each turn should show message content
        And each turn should show token counts
        And each turn should show response duration
        And each turn should have a colored left border

    @requires-sessions
    Scenario: Turn viewer can be dismissed
        Given I am viewing a session's turns
        When I click the close button on the turn viewer
        Then the turn viewer should disappear

    @requires-sessions
    Scenario: Empty session shows no turns message
        Given there is a session with no turns
        When I click the view button on that session
        Then the turn viewer should show "No turns in this session"

    # ── Export ─────────────────────────────────────────────
    @requires-sessions
    Scenario: Export session shows success snackbar
        Given there are sessions
        When I click the export button on a session
        Then a success snackbar should appear with "exported"

    # ── Close session ─────────────────────────────────────
    @requires-sessions
    Scenario: Close session shows success and refreshes
        Given there are active sessions
        When I click the close button on an active session
        Then a success snackbar should appear with "closed"
        And the session list should refresh

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "Sessions — JD.AI"

    # ── Planned ───────────────────────────────────────────
    @planned
    Scenario: Filter sessions by agent, channel, and date range
        Given there are sessions
        Then I should see filter controls for agent, channel, and date range
        When I apply a filter
        Then only matching sessions should be displayed
```

**Step 2: Commit**

```bash
git add tests/JD.AI.Specs.UI/Features/Dashboard/SessionsPage.feature
git commit -m "test: expand SessionsPage.feature with comprehensive scenarios"
```

---

## Task 15: Expand ProvidersPage.feature

**Files:**
- Modify: `tests/JD.AI.Specs.UI/Features/Dashboard/ProvidersPage.feature`

**Step 1: Replace feature file with expanded version**

```gherkin
@ui
Feature: Providers Page
    As a gateway operator
    I want to view configured AI model providers
    So that I can monitor provider availability and available models

    Background:
        Given I am on the providers page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays providers page heading
        Then I should see the heading "Model Providers"

    @smoke
    Scenario: Refresh button is visible
        Then I should see the refresh button

    # ── Loading states ────────────────────────────────────
    Scenario: Skeleton cards shown while loading
        Given the providers are loading
        Then I should see 2 skeleton provider cards

    # ── Empty state ───────────────────────────────────────
    Scenario: Empty state when no providers
        Given there are no configured providers
        Then I should see the providers empty state
        And the empty state should display "No providers configured"

    # ── Data display ──────────────────────────────────────
    @requires-providers
    Scenario: Provider cards show name and availability
        Given there are configured providers
        Then I should see provider cards
        And each provider card should display the provider name in bold
        And available providers should show model count subtitle
        And unavailable providers should show status message subtitle

    @requires-providers
    Scenario: Provider status badges show Online or Offline
        Given there are configured providers
        Then each provider card should show a status badge
        And available providers should show "Online" badge in green
        And unavailable providers should show "Offline" badge in red

    @requires-providers
    Scenario: Provider avatar color reflects availability
        Given there are configured providers
        Then available provider avatars should be green
        And unavailable provider avatars should be red

    # ── Model table ───────────────────────────────────────
    @requires-providers
    Scenario: Model table shown for available providers with models
        Given there are available providers with models
        Then available provider cards should show a model table
        And the model table should have "Model ID" and "Display Name" columns
        And model IDs should be displayed in monospace font

    @requires-providers
    Scenario: No model table for providers without models
        Given there is a provider with no models
        Then that provider card should not show a model table

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "Providers — JD.AI"

    # ── Planned ───────────────────────────────────────────
    @planned
    Scenario: Ollama provider shows model size and context window
        Given the Ollama provider is available
        Then the Ollama model table should show "Size" and "Context" columns
```

**Step 2: Commit**

```bash
git add tests/JD.AI.Specs.UI/Features/Dashboard/ProvidersPage.feature
git commit -m "test: expand ProvidersPage.feature with comprehensive scenarios"
```

---

## Task 16: Expand RoutingPage.feature

**Files:**
- Modify: `tests/JD.AI.Specs.UI/Features/Dashboard/RoutingPage.feature`

**Step 1: Replace feature file with expanded version**

```gherkin
@ui
Feature: Routing Page
    As a gateway operator
    I want to configure channel-to-agent routing rules
    So that incoming messages are directed to the correct AI agents

    Background:
        Given I am on the routing page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays routing page heading
        Then I should see the heading "Channel → Agent Routing"

    @smoke
    Scenario: Sync OpenClaw button is available
        Then I should see the "Sync OpenClaw" button with sync icon

    Scenario: Refresh button is visible
        Then I should see the refresh button

    # ── Loading states ────────────────────────────────────
    Scenario: Skeleton rows shown while loading
        Given the routing mappings are loading
        Then I should see 4 skeleton loading rows

    # ── Data grid ─────────────────────────────────────────
    @requires-routing
    Scenario: Routing data grid displays with correct columns
        Given there are routing mappings
        Then I should see the routing data grid
        And the grid should have a "Channel" column
        And the grid should have an "Agent ID" column
        And the grid should have a "Status" column

    @requires-routing
    Scenario: Channel column shows icon and name
        Given there are routing mappings
        Then each routing row should display a channel icon
        And each routing row should display the channel type name

    @requires-routing
    Scenario: Status chips show Override or Default
        Given there are routing mappings
        Then rows with an assigned agent should show "Override" chip in primary color
        And rows without an assigned agent should show "Default" chip

    @requires-routing
    Scenario: Agent ID column is editable inline
        Given there are routing mappings
        Then the Agent ID column should support inline cell editing

    @requires-routing
    Scenario: Editing agent ID updates routing via API
        Given there are routing mappings
        When I edit the agent ID on a routing row
        And I commit the cell edit
        Then a success snackbar should appear with "Routing updated"

    # ── Routing diagram ───────────────────────────────────
    @requires-routing
    Scenario: Routing diagram section is displayed
        Given there are routing mappings
        Then I should see the "Routing Diagram" section
        And the diagram should use a timeline layout

    @requires-routing
    Scenario: Diagram shows channel-to-agent flow
        Given there are routing mappings
        Then each timeline item should show a channel chip
        And each timeline item should show an arrow
        And each timeline item should show an agent chip or "OpenClaw Default"

    @requires-routing
    Scenario: Timeline items are color-coded by assignment
        Given there are routing mappings
        Then timeline items with assigned agents should be primary colored
        And timeline items using default should be default colored

    # ── Sync OpenClaw ─────────────────────────────────────
    Scenario: Sync OpenClaw triggers sync and shows snackbar
        When I click the "Sync OpenClaw" button
        Then a success snackbar should appear with "sync complete"
        And the routing data should refresh

    # ── Channel icons ─────────────────────────────────────
    @requires-routing
    Scenario: Channel icons match channel type
        Given there are routing mappings
        Then routing channel icons should match their type

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "Routing — JD.AI"

    # ── Planned ───────────────────────────────────────────
    @planned
    Scenario: Agent selector dropdown replaces free-text editing
        Given there are routing mappings
        When I click to edit an agent ID
        Then a dropdown with available agents should appear
```

**Step 2: Commit**

```bash
git add tests/JD.AI.Specs.UI/Features/Dashboard/RoutingPage.feature
git commit -m "test: expand RoutingPage.feature with comprehensive scenarios"
```

---

## Task 17: Expand SettingsPage.feature

**Files:**
- Modify: `tests/JD.AI.Specs.UI/Features/Dashboard/SettingsPage.feature`

**Step 1: Replace feature file with expanded version**

```gherkin
@ui
Feature: Settings Page
    As a gateway operator
    I want to configure the gateway through a settings interface
    So that I can manage server, provider, agent, channel, routing, and OpenClaw settings

    Background:
        Given I am on the settings page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays settings page heading
        Then I should see the heading "Settings"

    Scenario: Refresh button is visible
        Then I should see the refresh button

    # ── Loading states ────────────────────────────────────
    Scenario: Loading skeleton shown while fetching config
        Given the settings are loading
        Then I should see a settings loading skeleton

    # ── Error state ───────────────────────────────────────
    Scenario: Error alert when gateway unreachable
        Given the gateway configuration cannot be loaded
        Then I should see an error alert
        And the alert should display "Unable to load gateway configuration"

    # ── Tab strip ─────────────────────────────────────────
    @smoke
    Scenario: Tab strip with all six tabs
        Then I should see the settings tab strip
        And the tab strip should contain a "Server" tab
        And the tab strip should contain a "Providers" tab
        And the tab strip should contain a "Agents" tab
        And the tab strip should contain a "Channels" tab
        And the tab strip should contain a "Routing" tab
        And the tab strip should contain an "OpenClaw" tab

    Scenario: Tabs have correct icons
        Then the "Server" tab should have a server icon
        And the "Providers" tab should have a hub icon
        And the "Agents" tab should have a robot icon
        And the "Channels" tab should have a cable icon
        And the "Routing" tab should have a route icon
        And the "OpenClaw" tab should have a cloud icon

    Scenario: Tabs have tooltips
        Then the "Server" tab should have tooltip "Network, auth, and rate-limit settings"
        And the "Providers" tab should have tooltip "AI model provider configuration"
        And the "Agents" tab should have tooltip "Agent definitions and auto-spawn settings"
        And the "Channels" tab should have tooltip "Messaging channel connections"
        And the "Routing" tab should have tooltip "Channel-to-agent routing rules"
        And the "OpenClaw" tab should have tooltip "OpenClaw bridge integration"

    # ── Tab navigation ────────────────────────────────────
    Scenario: Server tab is accessible
        When I click the "Server" tab
        Then the server settings panel should be visible

    Scenario: Providers tab accessible with save
        When I click the "Providers" tab
        Then the providers settings panel should be visible
        And a save providers button should be available

    Scenario: Agents tab accessible with save
        When I click the "Agents" tab
        Then the agents settings panel should be visible
        And a save agents button should be available

    Scenario: Channels tab accessible with save
        When I click the "Channels" tab
        Then the channels settings panel should be visible
        And a save channels button should be available

    Scenario: Routing tab is accessible
        When I click the "Routing" tab
        Then the routing settings panel should be visible

    Scenario: OpenClaw tab is accessible
        When I click the "OpenClaw" tab
        Then the OpenClaw settings panel should be visible

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "Settings — JD.AI"
```

**Step 2: Commit**

```bash
git add tests/JD.AI.Specs.UI/Features/Dashboard/SettingsPage.feature
git commit -m "test: expand SettingsPage.feature with comprehensive scenarios"
```

---

## Task 18: Final commit — all ADRs and feature files together

This is a verification and cleanup task.

**Step 1: Verify all files exist**

```bash
ls docs/specs/dashboard/
# Expected: 001-gateway-overview.md through 009-cross-cutting.md (9 files)

ls tests/JD.AI.Specs.UI/Features/Dashboard/
# Expected: 8 .feature files (HomePage, ChatPage, AgentsPage, ChannelsPage, SessionsPage, ProvidersPage, RoutingPage, SettingsPage)
```

**Step 2: Verify the project still builds**

```bash
dotnet build tests/JD.AI.Specs.UI/JD.AI.Specs.UI.csproj
```

**Step 3: Count scenarios across all feature files**

```bash
grep -c "Scenario:" tests/JD.AI.Specs.UI/Features/Dashboard/*.feature
# Expected: ~100+ total scenarios across all files (up from ~35 original)
```

---

## Summary

| Deliverable | Count | Location |
|-------------|-------|----------|
| Functional-area ADR documents | 9 | `docs/specs/dashboard/001-*.md` through `009-*.md` |
| Expanded feature files | 8 | `tests/JD.AI.Specs.UI/Features/Dashboard/*.feature` |
| Total acceptance criteria | ~180 | Across all ADRs |
| Total Gherkin scenarios | ~120 | Across all feature files (up from ~35) |
| `@smoke` scenarios | ~16 | Fast CI subset |
| `@planned` scenarios | ~10 | Not yet implemented features |
| `@requires-*` scenarios | ~50 | Need live gateway with data |

Note: This plan covers **spec and feature file creation only**. The corresponding **page object expansions** and **step definition implementations** to make the new scenarios actually runnable are a follow-up effort — the feature files define _what_ to test; the step definitions define _how_.
