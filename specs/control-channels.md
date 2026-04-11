# Control > Channels

**Route:** `/control/channels`  
**Nav Path:** Control > Channels  
**Description:** Comprehensive channel configuration interface for WhatsApp, Telegram, and Discord integrations with detailed setup forms and toggles.

**Verified:** Live UI [2026-04-11] via session-persistent auth

## Overview

The Channels page is a **tabbed interface** with per-channel-provider configuration sections. Each provider (WhatsApp, Telegram, etc.) is presented as an independent collapsible/tabbed section with its own configuration controls, credentials, and status fields.

## Page Sections

### 1. WhatsApp Configuration Section

**Status Display:**
- `Configured` — n/a
- `Linked` — No
- `Running` — No
- `Connected` — No
- `Last connect` — n/a
- `Last message` — n/a
- `Auth age` — n/a

**Configuration Fields:**

1. **Accounts** — Empty list (0 items), with "Add" button
2. **Ack Reaction** — Dropdown selector
3. **Actions** — Collapsible section with "Allow From" list (0 items) and "Add" button
4. **Block Streaming** — Toggle/checkbox
5. **Block Streaming Coalesce** — Toggle/checkbox
6. **Capabilities** — Empty list (0 items) with "Add" button
7. **Chunk Mode** — Radio buttons: "length", "newline"
8. **WhatsApp Config Writes** — Checkbox, "Allow WhatsApp to write config in response to channel events/commands (default: true)"
9. **Context Visibility** — Dropdown: "all", "allowlist", "allowlist_quote"
10. **WhatsApp Message Debounce (ms)** — Numeric input with +/- buttons (with metadata tags: "network", "performance", "channels")
11. **Default Account** — Dropdown selector
12. **Default To** — Input field
13. **Dm History Limit** — Numeric spinner (+/-)
14. **WhatsApp DM Policy** — Dropdown with access control options:
    - "pairing" (recommended)
    - "allowlist"
    - "open"
    - "disabled"
15. **Dms Enabled** — Toggle
16. **Group Allow From** — List (0 items) with "Add" button
17. **Group Policy** — Dropdown: "open", "disabled", "allowlist"
18. **Groups** — Section header
19. **Health Monitor** — Section
20. **Heartbeat** — Configuration option
21. **History Limit** — Numeric spinner
22. **Markdown** — Toggle
23. **Media Max Mb** — Numeric spinner
24. **Message Prefix** — Text input
25. **Reaction Level** — Radio buttons: "off", "ack", "minimal", "extensive"
26. **Response Prefix** — Text input
27. **WhatsApp Self-Phone Mode** — Toggle with description: "Same-phone setup (bot uses your personal WhatsApp number)"
28. **Send Read Receipts** — Toggle
29. **Text Chunk Limit** — Numeric spinner

**Action Buttons:**
- **Save** — Persist configuration changes
- **Reload** — Refresh configuration from server
- **Show QR** — Display QR code for scanning
- **Relink** — Re-establish WhatsApp connection
- **Wait for scan** — Status indicator during QR auth
- **Logout** — Disconnect WhatsApp account
- **Refresh** — Update status

### 2. Telegram Configuration Section

**Status Display:**
- `Configured` — n/a
- `Running` — No
- `Mode` — n/a
- `Last start` — n/a
- `Last probe` — n/a

**Configuration Fields:**

1. **Accounts** — Empty list with "Add" button
2. **Ack Reaction** — Dropdown
3. **Actions** — Collapsible with "Allow From" list
4. **Telegram API Root URL** — Text input for custom Bot API endpoint
5. **Telegram Auto Topic Label** — Toggle with description: "Auto-rename DM forum topics on first message using LLM"
6. **Telegram Bot Token** — Password input field (marked as "security", "auth", "network", "channels")
7. **Capabilities** — Collapsible section with "Commands" subsection
8. **Telegram Config Writes** — Toggle
9. **Context Visibility** — Dropdown
10. **Telegram Custom Commands** — List (0 items) with "Add" button for custom menu commands
11. **Default Account** — Dropdown
12. **Default To** — Input
13. **Direct** — Toggle
14. **Dm History Limit** — Numeric spinner
15. **Telegram DM Policy** — Dropdown with options: "pairing", "allowlist", "open", "disabled"
16. **Dms Enabled** — Toggle
17. **Error Cooldown Ms** — Numeric spinner
18. **Error Policy** — Dropdown: "always", "once", "silent"
19. **Telegram Exec Approvals** — Toggle/field
20. **Group Allow From** — List with "Add"
21. **Group Policy** — Dropdown
22. **Groups** — Section
23. **Health Monitor** — Section
24. **Heartbeat** — Field
25. **History Limit** — Spinner
26. **Link Preview** — Toggle
27. **Markdown** — Toggle
28. **Media Max Mb** — Spinner
29. **Name** — Text input
30. **Network** — Field
31. **Proxy** — Input field
32. **Reaction Level** — Radio: "off", "ack", "minimal", "extensive"
33. **Reaction Notifications** — Radio: "off", "own", "all"
34. **Reply To Mode** — Radio: "off", "first", "all", "batched"
35. **Response Prefix** — Text input
36. **Retry** — Field
37. **Telegram Silent Error Replies** — Toggle with description: "When true, Telegram bot replies marked as errors are sent silently (no notification sound)"
38. **Telegram Streaming Mode** — Toggle/selector
39. **Text Chunk Limit** — Spinner
40. **Thread Bindings** — Section
41. **Telegram API Timeout (seconds)** — Numeric input for request timeout

**Pattern:**
Configuration continues with metadata tags indicating scope (network, access, performance, channels, security, auth, reliability, observability, advanced, automation).

## Layout

- **Tabbed/Collapsible Interface** — Each provider (WhatsApp, Telegram, Discord, etc.) is a separate expandable section
- **Form-heavy Design** — Majority of page is configuration input fields
- **Metadata Badges** — Tags shown next to fields indicating impact area (network, security, performance, etc.)
- **No main table view** — Unlike other pages, Channels is purely configuration-focused
- **Status cards at top of each provider** — Quick reference for current state
- **Action buttons at section end** — Save, Reload, Connect/Disconnect actions per provider

## Interactions

- **Expand/collapse provider section** → Toggle visibility of that provider's configuration form
- **Toggle fields** → Enable/disable features per provider
- **Numeric spinners (+/-)** → Adjust numerical parameters
- **Dropdown selection** → Choose from predefined options
- **Text input** → Enter custom values (URLs, tokens, prefixes)
- **Click "Add" in list fields** → Open modal or inline form to add item to list
- **Click "Save"** → Persist changes for this provider
- **Click "Reload"** → Discard unsaved changes, refresh from server
- **Click status refresh buttons** — Update connection/configuration status indicators

## State / Data

- **Unsaved Changes** — Page likely shows "No changes" indicator or requires explicit save
- **Configuration Persistence** — Settings saved per provider
- **Raw Mode Toggle** (observed in UI) — "Raw mode disabled (snapshot cannot safely round-trip raw text)"
- **Form/Raw Tabs** — "Form" tab (shown) and "Raw" tab available for JSON editing
