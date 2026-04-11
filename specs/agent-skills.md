# Agent > Skills

**Route:** `/agent/skills`  
**Nav Path:** Agent > Skills  
**Description:** Browse, search, and manage installed skills with setup status, dependencies, and configuration controls.

**Verified:** Live UI [2026-04-11] via session-persistent auth

## Overview

The Skills page provides a comprehensive registry view of all installed agent skills with filtering by readiness state, search functionality, and the ability to browse and install new skills from ClawHub.

## Main Components

### 1. Skills Summary Bar

Quick overview of skill installation status with filter tabs:

- **All** — Total skill count: 71 skills available
- **Ready** — Fully configured and operational: 35 skills
- **Needs Setup** — Require configuration: 11 skills
- **Disabled** — Inactive: 25 skills
- **Refresh** — Button to reload skill registry and status

### 2. ClawHub Integration Section

- **ClawHub** — Header/link to external skill registry
- **Search and install skills from the registry** — Description/call-to-action

### 3. Built-in Skills Directory

**Section header:** "BUILT-IN SKILLS" with count: 50

**Skill List Format:**

Each skill is displayed as a card/row with:
- **Icon** — Visual indicator (emoji or icon)
- **Name** — Skill identifier (e.g., `1password`, `apple-notes`, `bear-notes`)
- **Description** — One-line summary of skill purpose and use context

**Example Built-in Skills Observed:**

1. **🔐 1password** — "Set up and use 1Password CLI. Use when installing the CLI, enabling desktop app integration, signing in (single or multi-account)…"
2. **📝 apple-notes** — "Manage Apple Notes via the memo CLI on macOS (create, view, edit, delete, search, move, and export notes)."
3. **⏰ apple-reminders** — "Manage Apple Reminders via remindctl CLI (list, add, edit, complete, delete). Supports lists, date filters, and JSON/plain output."
4. **🐻 bear-notes** — "Create, search, and manage Bear notes via grizzly CLI."
5. **📰 blogwatcher** — "Monitor blogs and RSS/Atom feeds for updates using the blogwatcher CLI."
6. **🫐 blucli** — "BluOS CLI (blu) for discovery, playback, grouping, and volume."
7. **🫧 bluebubbles** — "Send or manage iMessages via BlueBubbles (recommended iMessage integration)."
8. **📸 camsnap** — "Capture frames or clips from RTSP/ONVIF cameras." (Has missing dependencies warning)
9. **clawhub** — "Use the ClawHub CLI to search, install, update, and publish agent skills from clawhub.com."
10. **🧩 coding-agent** — "Delegate coding tasks to Codex, Claude Code, or Pi agents via background process."
11. **🎮 discord** — "Discord ops via the message tool. Use for channel messages, notifications, and interactions." (Has missing dependencies warning)
12. **🛌 eightctl** — "Control Eight Sleep pods (status, temperature, alarms, schedules)."
13. **✨ gemini** — "Gemini CLI for one-shot Q&A, summaries, and generation."
14. **gh-issues** — "Fetch GitHub issues, spawn sub-agents to implement fixes and open PRs, then monitor PR review comments."
15. **🧲 gifgrep** — "Search GIF providers with CLI/TUI, download results, and extract stills/sheets."
16. **🐙 github** — "GitHub operations via gh CLI: issues, PRs, CI runs, code review, API queries."
17. **🎮 gog** — "Google Workspace CLI for Gmail, Calendar, Drive, Contacts, Sheets, and Docs."
18. **📍 goplaces** — "Query Google Places API via goplaces CLI for text search, place details, resolve, and reviews."
19. **healthcheck** — "Host security hardening and risk-tolerance configuration for OpenClaw deployments."
20. **📧 himalaya** — "CLI to manage emails via IMAP/SMTP (list, read, write, reply, forward, search, organize)."
21. **📨 imsg** — "iMessage/SMS CLI for listing chats, history, and sending messages via Messages.app."
22. **📦 mcporter** — "Use mcporter CLI to list, configure, auth, and call MCP servers/tools directly (HTTP or stdio)."
23. **📊 model-usage** — "Summarize per-model usage for Codex or Claude with CodexBar CLI local cost usage." (Has missing dependencies warning)
24. **📄 nano-pdf** — "Edit PDFs with natural-language instructions using the nano-pdf CLI."
25. **node-connect** — "Diagnose OpenClaw node connection and pairing failures for Android, iOS, and macOS companion apps."
26. **📝 notion** — "Notion API for creating and managing pages, databases, and blocks."
27. **💎 obsidian** — "Work with Obsidian vaults (plain Markdown notes) and automate via obsidian-cli."
28. **🎤 openai-whisper** — "Local speech-to-text with the Whisper CLI (no API key required)."
29. **🌐 openai-whisper-api** — "Transcribe audio via OpenAI Audio Transcriptions API (Whisper)."
30. **💡 openhue** — "Control Philips Hue lights and scenes via the OpenHue CLI."
31. **🧿 oracle** — "Best practices for using the oracle CLI (prompt + file bundling, engines, sessions)…" (truncated)

... (additional skills continue)

## Layout

- **Header Section** — Summary filter buttons (All, Ready, Needs Setup, Disabled)
- **Search/Install Section** — ClawHub integration area
- **Skills List** — Scrollable/paginated list of skills with icons and descriptions
- **Truncated Text** — Descriptions are truncated with "…" if they exceed display width
- **No visible status badges or setup buttons** — Status is indicated only by filter category and count

## Filtering & Navigation

- **Click "All"** → Show all 71 skills
- **Click "Ready"** → Show 35 configured/working skills
- **Click "Needs Setup"** → Show 11 skills awaiting configuration
- **Click "Disabled"** → Show 25 inactive skills
- **Click on a skill** → (Likely) Opens detail view or setup modal for that skill
- **ClawHub link/button** → Navigate to skill marketplace

## State / Data

- **Initial Load** — Displays all 71 skills by default
- **Filter Applied** — Count updates based on selected filter
- **Search** — (Functionality not observed in basic page text, but likely available)
- **Skill Cards** — Display icon, name, and truncated description

## Key Observations

- **Total Skills:** 71 built-in skills across multiple categories
- **Status Distribution:** 35 Ready, 11 Needs Setup, 25 Disabled
- **Missing Dependencies Alert:** "Skills with missing dependencies: camsnap, discord, model-usage +8 more"
- **Skills marked with emojis** for visual distinction (1password, apple-notes, bear-notes, blucli, bluebubbles, camsnap, discord, eightctl, gemini, gifgrep, github, gog, goplaces, himalaya, imsg, mcporter, model-usage, nano-pdf, notion, obsidian, openai-whisper, openhue, oracle, etc.)
- **Truncated descriptions** — Long descriptions are cut off with "…" in display

> **Enriched:** Real field names/values from live UI [2026-04-11]
