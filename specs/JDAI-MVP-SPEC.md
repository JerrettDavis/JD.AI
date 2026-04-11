# JD.AI — Minimum Target Application Specification

> **Source:** Derived from OpenClaw UI specs in `C:\git\JD.AI\specs\` (reverse-engineered 2026-04-11)  
> **Status:** MVP definition — not a build plan

---

## Vision

JD.AI is a personal AI agent platform built on Semantic Kernel, designed for a single power user who operates multiple AI agents across Discord, terminal, and web. Unlike OpenClaw — which is a general-purpose self-hosted gateway — JD.AI is tuned for a solo operator: one person managing their own agent fleet, skills, and integrations without multi-tenant overhead. The dashboard MVP is a lightweight control surface for the JD.AI Gateway: chat with agents, confirm they're alive, configure providers and communication channels, and view logs. Everything else waits until the core loop is proven.

---

## Scope Decision Framework

A feature is **MVP** if it satisfies at least two of these:

1. **Core loop essential** — Without it, the user cannot interact with agents at all
2. **Setup prerequisite** — Without it, agents cannot be configured or started
3. **Operational visibility** — Without it, the user cannot know if the system is healthy
4. **Low complexity** — Builds directly on existing JD.AI Gateway API without novel infra

A feature is **deferred** if it is useful but not blocking the core loop, or if it requires significant novel infrastructure not yet in JD.AI.

A feature is **out of scope** if it addresses multi-tenant or enterprise concerns irrelevant to a solo operator, or if OpenClaw's version is too tightly coupled to OpenClaw-specific concepts.

---

## Navigation Structure (MVP)

```
[JD.AI Logo]
│
├── Chat                          ← Default/home
│
├── Control                       ← Expandable
│   └── Overview
│
├── Agents                        ← Expandable
│   ├── Agents
│   └── Skills
│
└── Settings                      ← Expandable
    ├── AI & Agents
    ├── Communication
    ├── Config
    └── Logs
```

**Naming changes from OpenClaw:**
- "Agent" section renamed to "Agents" (cleaner label)
- Settings reordered: AI & Agents first (most critical setup step), then Communication, Config, Logs
- Control section trimmed to Overview only for MVP; full Control sub-pages deferred

---

## Pages — IN SCOPE (MVP)

### Chat

- **Purpose:** Primary interaction point — lets the user test any agent directly from the dashboard without needing Discord or a terminal session. This is the core loop.
- **Must-have features:**
  - Agent selector dropdown (populated from `GET /api/agents`)
  - Message bubble layout: user messages right (indigo), agent messages left (gray)
  - Real-time streaming via SignalR `StreamChatAsync`
  - Streaming cursor indicator during active response
  - Enter-to-send, Shift+Enter for newline
  - Clear chat button
  - Error snackbar for agent/connection errors
  - Empty state: "Start a conversation" when no messages
  - "No agents running" warning when agent list is empty
- **Deferred:**
  - Stop/cancel streaming button (backend supports it; UI can wait)
  - Scroll position restoration across sessions
  - Chat history persistence (deliberately not in OpenClaw either — in-memory only is fine for MVP)
- **JD.AI adaptations:**
  - Remove session key URL parameter complexity — JD.AI auth model is simpler (single gateway, single user)
  - Agent selector labels should show model name prominently (JD.AI users care which model they're hitting)

---

### Control > Overview

- **Purpose:** System health at a glance — confirm the gateway is alive, agents are running, and channels are connected before starting work. The operator's "are we good?" page.
- **Must-have features:**
  - Gateway connection status indicator (Connected / Disconnected / Error)
  - Active agent count
  - Active session count
  - Connected channel count
  - Uptime display
  - Manual refresh button
  - Basic auth gate if gateway requires WebSocket token (reuse OpenClaw pattern: WS URL + token input)
- **Deferred:**
  - CPU/memory gauges (useful but not blocking)
  - Message queue depth display
  - Event timeline / recent activity log
  - Quick action shortcuts (navigate to Config, Logs, etc.)
  - Language selector (English-only for MVP)
  - Tick interval display
- **JD.AI adaptations:**
  - Auth gate can be simplified: JD.AI Gateway defaults to local (`ws://127.0.0.1:18789`), so pre-fill endpoint and hide if already connected
  - Remove OpenClaw-specific "Default Session Key" field from this page — session management is handled elsewhere in JD.AI

---

### Agents > Agents

- **Purpose:** The user needs to see what agents are running, select a default, and inspect agent configuration. Without this, the system is a black box.
- **Must-have features:**
  - Agent list with status indicators (active/inactive/error)
  - Agent selector dropdown with Copy ID and Set Default actions
  - Refresh button
  - Per-agent detail view with tabs:
    - **Overview tab:** Agent ID, provider, model, system prompt
    - **Tools tab:** Effective tool list, allow/deny overrides
    - **Skills tab:** Installed skills per agent with toggle
  - Spawn new agent dialog (ID, provider, model, system prompt, max turns)
  - Delete agent with confirmation
- **Deferred:**
  - Files tab (config file editor) — useful for power users, complex to implement safely
  - Channels tab — channel assignment lives in Communication settings for MVP
  - Cron tab — agent-level cron deferred until cron management is built
- **JD.AI adaptations:**
  - Version footer with update indicator is JD.AI-specific — include it (JD.AI already has version.json)
  - Command palette (⌘K) trigger in header — include if global search is built, otherwise omit

---

### Agents > Skills

- **Purpose:** Skills are the primary way to extend agent capabilities. The user needs to enable/disable and configure skills without touching config files.
- **Must-have features:**
  - Skills grouped by category with status tabs: All / Ready / Needs Setup / Disabled
  - Per-skill toggle (enable/disable, immediate effect)
  - Per-skill configure button (opens modal with config form for API keys etc.)
  - Skill status badges: Ready / Needs Setup / Disabled with reason
  - Search/filter input
- **Deferred:**
  - ClawHub registry integration — JD.AI does not have an equivalent public registry; build internal skill discovery first
  - Batch enable/disable actions
- **JD.AI adaptations:**
  - Replace "ClawHub" with JD.AI's own skill discovery mechanism when one exists
  - Skills in JD.AI are system-wide (same model as OpenClaw) — no per-agent skill install for MVP

---

### Settings > AI & Agents

- **Purpose:** Without configuring AI providers and defining agent definitions, nothing works. This is the first thing a new user configures.
- **Must-have features:**
  - **Providers tab:**
    - Card per provider with enable toggle and test button
    - Test provider fetches available models and displays them
    - Status indicator (connected / not tested / error)
  - **Agents tab:**
    - Expandable card per agent definition
    - Fields: Agent ID, Provider (dropdown), Model (dropdown populated from tested provider), Max Turns, Auto-Spawn toggle, System Prompt textarea
    - Collapsible model parameters panel: Temperature, Top-P, Max Tokens (others deferred)
    - Add Agent Definition button
    - Save button
- **Deferred:**
  - Full 10-parameter model parameter panel (Top-K, Context Window, Frequency/Presence/Repeat Penalty, Seed, Stop Sequences) — expose Temperature, Top-P, Max Tokens for MVP
  - Context Window field (Ollama-specific, niche)
- **JD.AI adaptations:**
  - JD.AI supports 15 AI providers vs OpenClaw's smaller set — provider cards must be extensible
  - Provider list should be driven by JD.AI's registered providers, not hardcoded OpenClaw set

---

### Settings > Communication

- **Purpose:** JD.AI is multi-channel by design — Discord, terminal, web. The user needs to configure at least one messaging provider (Discord is primary for JD.AI) to connect agents to real channels.
- **Must-have features:**
  - Two-panel layout: provider directory (left) + config form (right)
  - Per-provider enable/disable toggle
  - Per-provider config form with account tabs (multi-account support)
  - Test connection button per provider
  - Global message settings: message prefix, acknowledgment reactions, status reactions (thinking, tool, coding, done, error)
  - Sensitive field masking with eye toggle
- **Deferred:**
  - 13-provider coverage — MVP needs Discord (primary), Slack, Telegram; others can be listed but not form-completed
  - Text-to-speech / Audio / Talk configuration
  - Broadcast settings (advanced multi-agent routing)
  - Authorization allowlist UI (can be set via Config > Raw for now)
- **JD.AI adaptations:**
  - JD.AI already has 6 channel adapters — form coverage should match what's actually implemented
  - Status reaction emoji must include JD.AI-specific states if they differ from OpenClaw's set

---

### Settings > Config

- **Purpose:** Escape hatch for advanced configuration. Power users and debugging require direct access to the full config tree without touching files on disk.
- **Must-have features:**
  - Two-panel layout: hierarchical sidebar nav (left) + editor (right)
  - Form mode with field types: text, number, boolean toggle, select dropdown, textarea, array editor
  - Raw JSON mode with syntax highlighting and validation
  - Form/Raw mode toggle
  - Save (persist to disk), Apply (live reload without persist), Reset (discard changes), Reload (re-fetch from server) actions
  - Changes badge (unsaved count)
  - Sensitive field masking with eye toggle
  - Search box to filter sections and fields
  - Schema-driven: all fields, types, validation from JSON schema
- **Deferred:**
  - External file opener (open config in system editor) — nice-to-have, not blocking
  - Comparison view (original vs modified side-by-side) — deferred
  - Color picker fields (Appearance section) — deferred until Appearance page is built
- **JD.AI adaptations:**
  - Config schema must reflect JD.AI's actual config structure, not OpenClaw's
  - Section categories should map to JD.AI's config namespace (Core, AI & Agents, Communication, Automation, Infrastructure, Appearance)

---

### Settings > Logs

- **Purpose:** When something goes wrong, the user needs to see what happened. Logs are the primary debugging tool for a solo operator before reaching for Debug tooling.
- **Must-have features:**
  - Audit event log grid: Timestamp, Level (color-coded), Source, Event, Message
  - Filter panel: Severity (Debug/Info/Warning/Error/Critical), Event Type, Source text, Search text
  - Apply button to reload with new filters
  - Auto-refresh toggle (5-second polling)
  - Click row → expand detail panel with full JSON payload
  - Manual refresh button
  - Severity color coding: Error=red, Warning=orange, Info=blue, Debug=gray
- **Deferred:**
  - Multi-column sortable headers — default sort (newest first) is fine for MVP
  - Export/download logs
  - SignalR real-time push (polling is acceptable for MVP)
- **JD.AI adaptations:**
  - JD.AI uses OpenTelemetry for tracing — log viewer should accept OTEL-structured events if that's what JD.AI emits, not OpenClaw's AuditEvent schema

---

## Pages — DEFERRED (Post-MVP)

| Page | Reason Deferred | Target Phase |
|------|----------------|--------------|
| Control > Channels | Channel management is possible via Communication settings for MVP; a dedicated CRUD table adds value later | Phase 2 |
| Control > Instances | JD.AI runs as a single self-hosted instance for now; multi-instance management is future scope | Phase 3 |
| Control > Sessions | Session termination is an admin edge case; solo operator rarely needs this | Phase 2 |
| Control > Usage | Analytics are useful but require chart infrastructure; deferred until observability layer is richer | Phase 3 |
| Control > Cron Jobs | Cron management is valuable but not blocking core agent usage; manageable via Config > Raw for MVP | Phase 2 |
| Agents > Nodes | Node/device pairing and execution approval rules are complex; JD.AI node management may differ from OpenClaw | Phase 3 |
| Agents > Dreaming | Memory consolidation is a JD.AI-specific feature (SQLite vectors); the animated UI is a nice touch but not core | Phase 2 |
| Settings > Appearance | Theme switching works via localStorage without a dedicated page; build it when the design system is stable | Phase 2 |
| Settings > Automation | Trigger/action/condition rule builders are complex; automation via Config for MVP | Phase 3 |
| Settings > Infrastructure | Server/resource/deployment config is for advanced users; environment vars cover MVP needs | Phase 3 |
| Settings > Debug | Debug tooling is valuable but can be replaced by Logs + direct API access for solo operator MVP | Phase 2 |

---

## Pages — OUT OF SCOPE

| Page | Rationale |
|------|-----------|
| Any multi-tenant user management | JD.AI is single-user; no user roles, no account management |
| Session revocation / Terminate All Sessions | Enterprise security feature not relevant to solo operator |
| ClawHub registry integration | JD.AI has no public skill registry; replace with internal skill discovery when needed |
| Device pairing / token rotation (Nodes) | OpenClaw-specific distributed execution model; JD.AI architecture may differ significantly |
| Instance deployment wizard | JD.AI is deployed as a single gateway; no dynamic instance provisioning |
| Geolocation / IP tracking in Sessions | Privacy-invasive enterprise feature with no value for solo use |

---

## MVP Feature Checklist

The following must be built and working for JD.AI dashboard MVP to be shippable:

### Foundation
- [ ] SPA framework with client-side routing (10 routes)
- [ ] Theme system: light/dark/system with localStorage persistence, no flash on load
- [ ] Global shell: persistent collapsible sidebar with active state, breadcrumb
- [ ] Toast/snackbar notification system (bottom-positioned, auto-dismiss)
- [ ] Empty states, loading skeletons, and error boundaries for all pages
- [ ] WebSocket/SignalR client setup for real-time subscriptions
- [ ] Gateway authentication gate (WebSocket URL + token, reusable component)

### Chat
- [ ] Agent selector dropdown from `GET /api/agents`
- [ ] User/agent message bubble layout with timestamps
- [ ] SignalR streaming via `StreamChatAsync` with real-time chunk append
- [ ] Streaming cursor indicator (blinking `▋`)
- [ ] Enter-to-send, Shift+Enter for newline
- [ ] Input disabled during streaming
- [ ] Clear chat button
- [ ] Error handling: agent error, connection error snackbars

### Control > Overview
- [ ] Gateway connection status card
- [ ] System snapshot cards: active agents, sessions, channels, uptime
- [ ] Manual refresh button
- [ ] Auth gate component (WebSocket URL + token form)

### Agents > Agents
- [ ] Agent list with status indicators (active/error/inactive)
- [ ] Agent selector dropdown with Copy ID, Set Default, Refresh
- [ ] Detail panel with Overview, Tools, Skills tabs
- [ ] Spawn agent dialog
- [ ] Delete agent with confirmation dialog

### Agents > Skills
- [ ] Skills list grouped by category with status tabs (All/Ready/Needs Setup/Disabled)
- [ ] Search/filter input
- [ ] Per-skill toggle (enable/disable)
- [ ] Per-skill configure modal with API key forms
- [ ] Skill status badges with eligibility reasons

### Settings > AI & Agents
- [ ] Providers tab: card per provider, enable toggle, test button, model table
- [ ] Agents tab: expandable agent definition cards
- [ ] Agent fields: ID, provider/model dropdowns, max turns, auto-spawn, system prompt
- [ ] Model parameters panel: Temperature, Top-P, Max Tokens
- [ ] Add Agent / Save agents actions

### Settings > Communication
- [ ] Provider directory (left) + config form (right) two-panel layout
- [ ] Discord provider: full config form with account tabs
- [ ] Slack, Telegram providers: config forms
- [ ] Enable/disable toggle and test connection per provider
- [ ] Global message settings: prefix, acknowledgment reactions, status reactions
- [ ] Sensitive field masking

### Settings > Config
- [ ] Two-panel layout: sidebar nav + form/raw editor
- [ ] Hierarchical category navigation with search
- [ ] Form mode with text, number, boolean, select, array field types
- [ ] Raw JSON mode with syntax highlighting
- [ ] Save, Apply, Reset, Reload actions
- [ ] Changes badge
- [ ] Sensitive field masking with eye toggle

### Settings > Logs
- [ ] Audit log grid: Timestamp, Level, Source, Event, Message
- [ ] Filter panel: severity, event type, source, search text
- [ ] Apply filters button
- [ ] Auto-refresh toggle (5-second polling)
- [ ] Row expand → JSON detail panel
- [ ] Severity color coding

---

## Open Questions

1. **Auth model:** Does JD.AI Gateway use the same WebSocket token auth as OpenClaw, or does it have a simpler auth mechanism? The auth gate design depends on this.

2. **API compatibility:** Are JD.AI Gateway REST endpoints compatible with OpenClaw's `/api/*` patterns documented in the specs, or does JD.AI use different routes/schemas?

3. **SignalR hub:** Does JD.AI expose the same `/hubs/agent` SignalR hub with `StreamChatAsync`? If so, the Chat page is straightforward to wire up.

4. **Config schema format:** Does JD.AI's config expose a JSON schema at `/api/config/schema`? The Config page is schema-driven and requires this.

5. **Skill system:** JD.AI has a plugin/skill system (MemStack skills, etc.) — how does it map to the OpenClaw skills model? Does `GET /api/skills/status` exist?

6. **Log format:** JD.AI uses OpenTelemetry — does it emit structured audit events compatible with what OpenClaw's Logs page expects, or does the log viewer need to consume OTEL spans/events directly?

7. **Tech stack decision:** OpenClaw uses Web Components + Lit. JD.AI dashboard tech stack is not specified. Decision needed before any implementation starts (React/Next.js recommended given JD.AI's .NET backend and existing TypeScript tooling).

8. **Dreaming priority:** JD.AI has SQLite vector memory — is the Dreaming UI higher priority than indicated here, given that memory consolidation is an active JD.AI feature?
