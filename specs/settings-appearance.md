# Settings > Appearance
>**Verified:** Gateway authentication required. Session params do not bypass WebSocket auth for admin panel.

**Route:** `/settings/appearance`  
**Nav Path:** Settings > Appearance  
**Description:** User interface theme, color palette, font, and layout customization settings.

## Status
**⚠️ Authentication Required** — This page requires gateway authentication via WebSocket connection before the UI content is accessible.

## Authentication Gate (Current State)

The Appearance settings page is protected by a login gate with the following fields:

### Layout
- Centered card-based form on a full-screen background
- Header with OpenClaw logo and "Gateway Dashboard" subtitle

### Components

- **WebSocket URL input** — Text field with placeholder `ws://127.0.0.1:18789`, stores the gateway connection endpoint
- **Gateway Token input** — Password field with placeholder `OPENCLAW_GATEWAY_TOKEN (optional)`, allows optional token authentication
- **Password input** — Password field labeled "Password (not stored)", secondary auth mechanism
- **Token visibility toggle button** — Icon button to show/hide Gateway Token field
- **Password visibility toggle button** — Icon button to show/hide Password field
- **Connect button** — Submit button to establish WebSocket connection and proceed to authenticated content

## Expected Content (Awaiting Auth)

Based on the task requirements, once authenticated, this page should contain:

- **Theme selection** — Light/Dark/Custom theme options (likely radio buttons or toggle)
- **Color palette options** — Primary, secondary, accent color pickers or preset palettes
- **Font settings** — Font family selection, size controls
- **Layout density options** — Compact/Normal/Spacious view modes
- **Preview functionality** — Real-time theme preview showing changes before saving

## Interactions (Estimated)

- Click WebSocket URL field → Enter/modify gateway connection string
- Click Gateway Token field → Enter optional token credential
- Click token visibility toggle → Reveal/mask token input
- Click password visibility toggle → Reveal/mask password input
- Click Connect button → Validate credentials and establish WebSocket connection
- (After auth) Select theme → Apply theme and save preference
- (After auth) Adjust colors → Show preview of changes
- (After auth) Change fonts → Update display and save preference

## State / Data

- **Connection state:** Not connected (login gate showing) | Connected (authenticated content visible)
- **Theme preference:** Stored in localStorage under `openclaw.control.settings.v1` key
- **Current theme:** Detected from `data-theme` and `data-theme-mode` HTML attributes (currently "light")
- **Theme options:** "claw", "knot", "dash" (see theme initialization script)
- **Theme modes:** "system", "light", "dark"

## API / WebSocket Calls

- **WebSocket connection:** Initiated on Connect button submit to the URL specified in WebSocket URL input
- **Authentication payload:** Gateway token and password (if provided) transmitted on initial connection
- **Settings sync:** After successful auth, fetch current theme and appearance preferences from gateway
- **Theme update:** POST changes to gateway to persist preferences

## Notes

- The application detects theme preference on page load via a script that reads localStorage and applies `data-theme` attributes to the HTML root
- Legacy theme naming (`openknot`, `fieldmanual`, `clawdash`) is supported for backwards compatibility
- Themes include Claw, Knot, and Dash with light/dark mode variants
- The connect gate prevents access to all Settings pages (/settings/*) until authenticated
- Password field explicitly states "(not stored)" — likely only used for session authentication, not persisted
