# Control > Sessions

**Route:** `/control/sessions`  
**Nav Path:** Control > Sessions  
**Description:** Track and manage active user sessions, including session details, duration, device/platform information, and session control (terminate, revoke, etc.).

## Layout
The Sessions page displays a list/table of active and recent sessions with filtering and management controls. Includes:
- Header with page title and "Terminate All" or "Clear Sessions" button (if applicable)
- Session summary stats (active sessions, total today, etc.)
- Search/filter bar for session lookup by username, device, or IP
- Main table showing session details with actions
- Optional detail view for session inspection

## Components

- **Page Header** — Title "Sessions" with summary metrics (Active Sessions, Total, Duration Range)
- **Terminate All Sessions Button** — Optional bulk action to clear all sessions (admin only)
- **Summary Cards** — Quick stats showing:
  - Active Sessions count
  - Total Sessions today
  - Longest Session duration
  - Most common platform/device
- **Search Bar** — Text input to filter sessions by:
  - Username or user ID
  - IP address
  - Device type or browser
  - Session token (partial)
- **Filter/Sort Controls** — Dropdowns for:
  - Status (Active, Idle, Terminated, Expired)
  - Platform/Device type (Web, Mobile, Desktop app, API, etc.)
  - Sort by (Start time, Duration, Last activity, IP)
  - Date range (Today, This week, All time)
- **Sessions Table** — Columnar view showing:
  - **Username/User ID** — Person or service owning the session with avatar/icon
  - **Platform/Device** — Browser/OS (Chrome on Windows, Safari on iOS, API Client, etc.) with icon
  - **IP Address** — Source IP with optional geolocation flag
  - **Status** — Active, Idle, Terminated with visual indicator
  - **Started** — Session creation timestamp
  - **Last Activity** — Most recent request time
  - **Duration** — Elapsed time since session started (e.g., "2h 34m")
  - **Location** — Geolocation (country/city) if available
  - **Actions** — Button group: View Details, Terminate, Revoke, or three-dot menu
- **Pagination Controls** — If many sessions exist
- **Empty State** — "No active sessions" message
- **Real-Time Updates** — Duration and "Last Activity" update live

## Interactions

- **Click Session Row** → Opens detail panel/modal showing:
  - Full user information (name, email, profile)
  - Session token (masked for security) and expiration time
  - Detailed device/browser information (User-Agent string, OS version)
  - Full IP address and geolocation details
  - All requests made in this session (if logged)
  - IP change history (if detected)
  - Terminate, Revoke, or "Mark as trusted" options
- **Click Terminate Session** → Shows confirmation, then immediately ends session
  - User is logged out if this is their current session
  - If user is elsewhere, they're disconnected
- **Click Revoke** → May mark session as revoked (cannot be reused) but doesn't disconnect
- **Click View Details** → Opens full session inspection view (logs, IP history, device details)
- **Search Input** → Real-time filtering of sessions
- **Filter Dropdowns** → Table updates immediately when filters change
- **Terminate All Sessions** → Shows confirmation dialog ("This will log out all users"), then clears all sessions
- **Status Badge** → Hovering may show detailed status reason (e.g., "Idle for 30 minutes")
- **IP Address** → Hovering may show geolocation details or full IP info
- **Device/Platform Icon** → Hovering may show full User-Agent or browser details

## State / Data

- **Initial Load** — Fetches active and recent sessions
- **Loading State** — Spinner/skeleton while sessions are being loaded
- **Real-Time Updates** — Duration and Last Activity update without refresh (via WebSocket or polling)
- **Empty State** — When no sessions exist
- **Error State** — If session API unavailable
- **Filtered View** — Table updates as filters change
- **Confirmation Dialogs** — Before destructive actions (terminate, revoke, terminate all)
- **Detail Panel** — Persistent or dismissible view of selected session

## API / WebSocket Calls

Expected endpoints:
- `GET /api/sessions` — Fetch active/recent sessions (may support filtering and pagination)
- `GET /api/sessions/:id` — Fetch single session details
- `POST /api/sessions/:id/terminate` — Immediately end a session
- `POST /api/sessions/:id/revoke` — Revoke a session (prevent reuse)
- `POST /api/sessions/terminate-all` — Clear all active sessions (admin action)
- `GET /api/sessions/current` — Fetch current user's session
- `GET /api/sessions/:id/activity` — Fetch session activity log (requests, page views, etc.)

WebSocket messages (for real-time updates):
- `session.created` — New session initiated
- `session.activity` — Activity recorded in session (updates Last Activity time)
- `session.terminated` — Session ended
- `session.revoked` — Session revoked
- `session.idle` — Session marked as idle
- `session.ip_changed` — Detected IP address change for session

## Notes

- Verify whether "Last Activity" is updated real-time or on polling interval
- Check if sessions are grouped by user or displayed flat
- Confirm whether session tokens are displayed (usually masked for security)
- Verify geolocation accuracy and data source
- Determine if "Revoke" action is different from "Terminate" or if they're the same
- Check if users can see only their own sessions or if admins see all
- Verify whether terminated sessions are kept in history or immediately deleted
- Confirm if there's a "Mark as trusted device" feature that bypasses future MFA
- Check if session count is limited per user (max 5 concurrent, etc.)
- Verify whether API tokens/service accounts have their own session view
