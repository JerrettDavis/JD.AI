# Control > Channels

**Route:** `/control/channels`  
**Nav Path:** Control > Channels  
**Description:** Manage and monitor Discord channels connected to the system, including configuration and activity tracking.

## Layout
The Channels page displays a list/table view of connected Discord channels with filtering and management controls. Likely includes:
- Header with page title and "Add Channel" or "Connect Channel" button
- Search/filter bar for channel lookup by name
- Main table displaying channel details and actions
- Right-side panel or modal for channel configuration details

## Components

- **Page Header** — Title "Channels" with subtitle describing the view purpose
- **Add/Connect Channel Button** — Primary action button to create new channel connection
- **Search Bar** — Text input for filtering channels by name or ID
- **Filter/Sort Controls** — Dropdowns or toggles for status filters (active, inactive), server/guild, or sort order
- **Channels Table** — List view showing:
  - **Channel Name** — Discord channel name with icon/indicator
  - **Server/Guild** — Parent Discord server name
  - **Status** — Active/Inactive/Error indicator with visual badge
  - **Member Count** — Number of users in the channel (if applicable)
  - **Last Activity** — Timestamp of most recent message or interaction
  - **Created/Connected** — Date channel was added to the system
  - **Actions** — Button group with Edit, View Logs, Test, Delete, or settings icon
- **Pagination Controls** — If table has many rows, next/previous buttons or page indicator
- **Empty State** — Message when no channels connected, with link to "Connect first channel"

## Interactions

- **Click Channel Row** → Opens channel detail view or right panel with full configuration
- **Click Add Channel** → Opens modal form to:
  - Search and select Discord server/guild
  - Select channel from that server's channel list
  - Configure notification preferences (if applicable)
  - Set channel as "primary" or assign role/group
  - Submit to connect
- **Click Edit** → Opens inline editor or modal to modify channel settings (name alias, notification rules, etc.)
- **Click Delete** → Shows confirmation dialog before removing channel connection
- **Search Input** → Filters table rows in real-time or on Enter
- **Click Status Badge** → May show reason for error or detailed status
- **Click Last Activity** → May show recent message preview or activity log

## State / Data

- **Initial Load** — Fetches all connected channels and displays in table
- **Loading State** — Spinner/skeleton while channels are being fetched
- **Empty State** — Shows when zero channels are connected
- **Error State** — Shows error message if fetch fails (e.g., Discord API unavailable)
- **Filter Applied** — Table updates immediately as filters change
- **Confirmation Modal** — Before destructive actions (delete, disconnect)

## API / WebSocket Calls

Expected endpoints or messages:
- `GET /api/channels` — Fetch list of connected channels
- `POST /api/channels` — Create/connect new channel
- `GET /api/channels/:id` — Fetch single channel details
- `PUT /api/channels/:id` — Update channel configuration
- `DELETE /api/channels/:id` — Disconnect channel
- `GET /api/servers` or `/api/discord/guilds` — Fetch available Discord servers (for connect modal)
- `GET /api/discord/guilds/:guildId/channels` — Fetch channels within a specific server

WebSocket messages (if real-time updates are supported):
- `channel.created` — New channel connected
- `channel.updated` — Channel settings changed
- `channel.deleted` — Channel disconnected
- `channel.status_changed` — Channel went online/offline

## Notes

- Need to verify exact column names and table structure by inspecting rendered UI
- Check if there's a "bulk actions" checkbox for multi-select delete/edit
- Confirm whether channel configuration is inline or in a separate detail view
- Verify pagination vs. infinite scroll behavior
- Check if "Last Activity" timestamp is clickable to show message preview
- Determine if channels can be grouped by server/guild in the table view
- Confirm tooltip/hover behavior on status badges and "Last Activity"
