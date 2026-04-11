# Settings > Infrastructure

**Route:** `/settings/infrastructure`  
**Nav Path:** Settings > Infrastructure  
**Description:** Configure gateway, web, browser, and media infrastructure settings with fine-grained network, access, and reliability controls.

**Verified:** Live UI [2026-04-11] via session-persistent auth

## Overview

The Infrastructure settings page is a comprehensive configuration interface for gateway networking, web hosting, browser automation, media handling, and external service integrations. It features a **tabbed interface** (Form/Raw views) with extensive field controls organized by subsection.

## Main Tabs

- **Form** — Visual form editor (currently active)
- **Raw** — Raw JSON/text editor (available but raw mode disabled for safe round-tripping)

## Page Controls

- **"No changes"** — Status indicator showing unsaved state
- **"Raw mode disabled (snapshot cannot safely round-trip raw text)"** — Notice about raw editing limitations
- **Open** — Button to expand/view something
- **Reload** — Button to discard unsaved changes
- **Save** — Button to persist configuration
- **Apply** — Button to apply changes
- **Update** — Button for immediate update

## Main Configuration Sections

### 1. Infrastructure (Main Header)

Subsections:
- Gateway
- Web
- Browser
- NodeHost
- CanvasHost
- Discovery
- Media
- Acp
- Mcp

### 2. Gateway Configuration

**Description:** "Gateway server settings (port, auth, binding)"

**Fields:**

1. **Gateway Allow x-real-ip Fallback** — Toggle
   - Description: "Enables x-real-ip fallback when x-forwarded-for is missing in proxy scenarios. Keep disabled unless your ingress stack requires this compatibility behavior."
   - Tags: access, network, reliability

2. **Gateway Auth Mode** — Dropdown
   - Description: "Gateway auth mode: none, token, password, or trusted-proxy depending on your edge architecture."
   - Options: none, token, password, trusted-proxy
   - Tags: network

3. **Gateway Auth Allow Tailscale Identity** — Toggle
   - Description: "Allows trusted Tailscale identity paths to satisfy gateway auth checks when configured."
   - Tags: access, network

4. **Gateway Password** — Text input
   - Description: "Required for Tailscale funnel."
   - Tags: security, auth, access, network

5. **Gateway Auth Rate Limit** — Numeric field
   - Tags: network, performance

6. **Gateway Token** — Text input (password-masked)
   - Description: "Required by default for gateway access (unless using Tailscale Serve identity); required for non-loopback binds."
   - Tags: security, auth, access, network

7. **Gateway Trusted Proxy Auth** — Configuration field
   - Tags: network

8. **Gateway Bind Mode** — Dropdown
   - Description: "Network bind profile: auto, lan, loopback, custom, or tailnet to control interface exposure."
   - Options: auto, lan, loopback, custom, tailnet
   - Tags: network

10. **Gateway Channel Health Check Interval (min)** — Numeric spinner
    - Description: "Interval in minutes for automatic channel health probing and status updates. Use lower intervals for faster detection, or higher intervals to reduce periodic probe noise."
    - Tags: network, reliability
    - Controls: +/- buttons

11. **Gateway Channel Max Restarts Per Hour** — Numeric spinner
    - Description: "Maximum number of health-monitor-initiated channel restarts allowed within a rolling one-hour window. Once hit, further restarts are skipped until the window expires. Default: 10."
    - Tags: network, performance
    - Controls: +/- buttons

12. **Gateway Channel Stale Event Threshold (min)** — Numeric spinner
    - Description: "How many minutes a connected channel can go without receiving any event before the health monitor treats it as a stale socket and triggers a restart. Default: 30."
    - Tags: network
    - Controls: +/- buttons

### 3. Control UI Configuration

**Description:** "Control UI hosting settings including enablement, pathing, and browser-origin/auth hardening behavior. Keep UI exposure minimal and pair with strong auth controls before internet-facing deployments."

**Fields:**

1. **Control UI Allowed Origins** — List input
   - Description: "Allowed browser origins for Control UI/WebChat websocket connections (full origins only, e.g. https://control.example.com). Required for non-loopback Control UI deployments unless dangerous Host-header fallback is explicitly enabled. Setting ["*"] means allow any browser origin and should be avoided outside tightly controlled local testing."
   - Current value: 1 item (shown with "Add" button)
   - Tags: access, network

2. **Insecure Control UI Auth Toggle** — Toggle
   - Description: "Loosens strict browser auth checks for Control UI when you must run a non-standard setup. Keep this off unless you trust your network and proxy path, because impersonation r…" (truncated in UI)

## Additional Sections (Observed but Truncated)

The page continues with more subsections including:
- **Web** — Additional web hosting configuration
- **Browser** — Browser-related settings
- **NodeHost**, **CanvasHost**, **Discovery** — Host/discovery configuration
- **Media** — Media processing settings
- **Acp**, **Mcp** — Additional service configurations

## Layout

- **Top Controls** — Status + Open, Reload, Save, Apply, Update buttons
- **Tabbed Interface** — Form tab (active) and Raw tab
- **Collapsible Sections** — Each major configuration area (Gateway, Web, etc.) can be expanded/collapsed
- **Field Grouping** — Related fields grouped under subsection headers
- **Descriptive Labels** — Extensive help text and warnings for each field
- **Metadata Tags** — Tags indicating field impact (network, security, access, performance, reliability, observability, etc.)
- **Type-specific Controls** — Toggles, dropdowns, text inputs, numeric spinners with +/- buttons

## Interactions

- **Click section header** → Expand/collapse that section
- **Toggle switches** → Enable/disable features
- **Dropdown fields** → Select from predefined options
- **Text input** → Enter custom values (IPs, URLs, passwords)
- **Numeric spinners** → Adjust numbers via +/- buttons or direct input
- **"Add" button in lists** → Add new item to list (e.g., allowed origins)
- **Click "Save"** → Persist changes to backend
- **Click "Apply"** → Apply changes immediately
- **Click "Reload"** → Discard unsaved changes and reload from server
- **Click "Open"** → Open/expand additional details

## State / Data

- **Unsaved Changes** — "No changes" indicator visible when pristine
- **Form Validation** — Fields validate on blur or on save attempt
- **Raw Mode** — Disabled to prevent unsafe round-trip of configuration
- **Metadata Filtering** — Users can filter fields by tag (network, security, access, performance, reliability, observability, etc.)
- **Tabbed Interface** — Form tab (default, visual editor) and Raw tab (JSON editor, currently disabled)

> **Enriched:** Real field names/values from live UI [2026-04-11]
