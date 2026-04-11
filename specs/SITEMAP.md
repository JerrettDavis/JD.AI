# OpenClaw UI — Complete Sitemap

## Route Hierarchy

```
/ (root → redirect to /chat?session=...)
├── /chat — Chat Interface (default landing page)
│   └── ?session=<session-key> — Session context (global, persists across navigation)
├── /control — Control Plane
│   ├── /control/overview — Gateway & System Status Dashboard
│   ├── /control/channels — Channel Management (Discord, etc.)
│   ├── /control/instances — Instance Lifecycle Management
│   ├── /control/sessions — Session Tracking & Management
│   ├── /control/usage — Usage Analytics & Resource Metrics
│   └── /control/cron-jobs — Scheduled Job Management
├── /agent — Agent Framework
│   ├── /agent/agents — Agent Management & Inspection
│   ├── /agent/skills — Skill Library & ClawHub Registry
│   ├── /agent/nodes — Node Management, Devices & Bindings
│   └── /agent/dreaming — Memory Consolidation Engine (alias: /dreams)
└── /settings — System Settings
    ├── /settings/config — Core Configuration (Form + Raw JSON)
    ├── /settings/communication — Messaging Providers & Channel Setup
    ├── /settings/appearance — Theme & UI Preferences
    ├── /settings/automation — Rules, Triggers & Actions
    ├── /settings/infrastructure — Server & Deployment Config
    ├── /settings/ai-agents — AI Providers & Agent Definitions
    ├── /settings/debug — Diagnostics & Troubleshooting
    └── /settings/logs — Audit Log Viewer (also accessible at /logs)
```

## Page Inventory

| Route | Title | Category | Key Purpose | Primary Components |
|-------|-------|----------|-------------|-------------------|
| `/chat` | Web Chat | Chat | Real-time agent conversation testing | Agent selector dropdown, message bubbles (user/agent), streaming input, clear chat |
| `/control/overview` | System Overview | Control | Gateway health & connection status | Auth gate, gateway access form, snapshots grid (uptime, CPU, memory, queue), status indicators |
| `/control/channels` | Channels | Control | Manage connected Discord channels | Channel table (name, server, status, activity), add/edit/delete channel, search/filter |
| `/control/instances` | Instances | Control | Agent instance lifecycle | Instance table (name, type, status, CPU, memory, uptime), deploy/restart/stop/delete, real-time metrics |
| `/control/sessions` | Sessions | Control | Active session monitoring | Session table (user, platform, IP, status, duration), terminate/revoke, summary cards |
| `/control/usage` | Usage | Control | Resource consumption analytics | *Spec pending — not yet documented* |
| `/control/cron-jobs` | Cron Jobs | Control | Scheduled task management | *Spec pending — not yet documented* |
| `/agent/agents` | Agents | Agent | Manage spawned AI agents | Agent selector, tabbed detail panel (Overview, Files, Tools, Skills, Channels, Cron), spawn dialog |
| `/agent/skills` | Skills | Agent | Skill library & ClawHub | Status tabs (All/Ready/Needs Setup/Disabled), skill cards with toggles, ClawHub search + install |
| `/agent/nodes` | Nodes | Agent | Device & node management | Execution approvals, agent-node bindings, device pairing (approve/reject), token management, node list |
| `/agent/dreaming` | Dreaming | Agent | Memory consolidation engine | Scene view (animated starfield + lobster), dream diary viewer (markdown), phase controls (Light/Deep/REM) |
| `/settings/config` | Configuration | Settings | Centralized config management | Two-panel (sidebar nav + form/raw editor), 6 categories, ~30 sections, save/apply/reset/reload |
| `/settings/communication` | Communication | Settings | Channel provider setup | Two-panel (provider directory + config form), multi-account tabs, 13+ providers, message/broadcast/command settings |
| `/settings/appearance` | Appearance | Settings | Theme & UI customization | Auth gate, theme selector (Claw/Knot/Dash), mode toggle (Light/Dark/System), color/font/layout settings |
| `/settings/automation` | Automation | Settings | Workflow automation rules | Auth gate, automation rules list, trigger/action/condition builders, create/edit/toggle rules |
| `/settings/infrastructure` | Infrastructure | Settings | Server & deployment config | Auth gate, server config, connection settings, resource limits, environment vars, scaling policies |
| `/settings/ai-agents` | Providers & Agents | Settings | AI provider & agent config | Tabbed interface (Providers tab with cards + Agents tab with expandable cards), model parameters, test connectivity |
| `/settings/debug` | Debug | Settings | Diagnostics & troubleshooting | Connection inspector, state inspector, error log viewer, diagnostic actions (test, clear cache, export bundle) |
| `/settings/logs` | Logs | Settings | Audit event viewer | Filter panel (severity, event type, source, search), log grid with detail panel, auto-refresh toggle |

## Navigation Structure

### Sidebar Hierarchy

```
[OpenClaw Logo]
│
├── Chat                          (single page, default/home)
│
├── Control                       (expandable section)
│   ├── Overview
│   ├── Channels
│   ├── Instances
│   ├── Sessions
│   ├── Usage
│   └── Cron Jobs
│
├── Agent                         (expandable section)
│   ├── Agents
│   ├── Skills
│   ├── Nodes
│   └── Dreaming
│
└── Settings                      (expandable section)
    ├── Config
    ├── Communication
    ├── Appearance
    ├── Automation
    ├── Infrastructure
    ├── AI & Agents
    ├── Debug
    └── Logs
```

### Navigation Behavior
- Sidebar is persistent (always visible)
- Sections expand/collapse on click
- Active page highlighted with visual distinction
- Sidebar supports collapse to icon-only mode
- Breadcrumbs update to reflect current location
- Session context (`?session=...`) persists across all navigation

## Session Identifier Format

```
agent:<agent-name>:<provider>:<resource-type>:<resource-id>
```

Example: `agent:jdai-default:discord:channel:1466622912307007690`

## Page Count Summary

| Section | Pages | Notes |
|---------|-------|-------|
| Chat | 1 | Single page, no sub-routes |
| Control | 6 | Usage and Cron Jobs specs pending |
| Agent | 4 | Dreaming has /dreams alias |
| Settings | 8 | AI & Agents uses tabbed sub-views |
| **Total** | **19** | |

## Authentication Model

- **Open pages:** Chat, Settings > Config, Settings > Communication, Settings > AI & Agents, Settings > Logs
- **Auth-gated pages:** Control > Overview, Settings > Appearance, Settings > Automation, Settings > Infrastructure (require WebSocket gateway auth)
- **Auth mechanism:** WebSocket URL + optional gateway token + optional password
- **Auth gate UI:** Centered card with OpenClaw logo, "Gateway Dashboard" subtitle, connect button
