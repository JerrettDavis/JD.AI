# Settings > Communication
n> **Verified:** Real UI via authenticated playwright [2026-04-11]

>**Verified:** Gateway authentication required. Session params do not bypass WebSocket auth for admin panel.

**Route:** `/settings/communication`  
**Nav Path:** Settings > Communication  
**Description:** Configuration interface for all communication channels and messaging integrations including Discord, Slack, Telegram, WhatsApp, and other chat providers.

## Layout

The Communication page uses a two-panel layout:

- **Left Panel:** Channel provider directory (all integrated platforms)
- **Right Panel:** Configuration form for selected channel

The interface supports:
- **Channel Selection:** Click any provider to configure that channel
- **Multi-Instance:** Some providers support multiple accounts (e.g., multiple Discord servers)
- **Tabbed Configuration:** When a channel supports multiple accounts, tabs for each account

## Components

### Left Panel: Channel Directory

#### Provider Categories
- **Messaging Apps:** Discord, Slack, Telegram, WhatsApp, Signal, iMessage, Mattermost, Microsoft Teams, Feishu, Matrix, IRC, Zalo, Google Chat
- **Broadcasting:** Broadcast/Multicast configuration
- **Voice/Audio:** Talk (voice interaction configuration)

#### Provider Items
- **Provider Name:** (e.g., "Discord", "Slack")
- **Status Badge:** Connected/Disconnected/Unconfigured
- **Account Count:** Number of configured accounts for this provider
- **Enable/Disable Toggle:** Quick toggle without opening configuration
- **Config Icon:** Click to open this provider's settings
- **Unread Count:** (Optional) Number of unread messages if applicable

#### Search/Filter
- **Search Box:** Filter providers by name
- **Status Filter:** Show all / connected only / disconnected only

### Right Panel: Provider Configuration

#### Account Selection Tabs
When a provider supports multiple accounts:
- **Tab per Account:** Account name/ID shown in tabs
- **Add Account Button:** Create new account configuration
- **Remove Button:** Delete account configuration (with confirmation)
- **Default Account Badge:** Mark which account is the default

#### Configuration Form Fields

##### Common Fields (all providers)
- **Enabled:** Boolean toggle to enable/disable this channel
- **Default Account:** Select which account is used by default
- **Allowed From:** List of sender IDs permitted to interact (allowlist)
- **DM Policy:** Policy for direct messages (always, blocked, specific users)
- **Group Policy:** Policy for group messages (allow, block, specific groups)
- **Context Visibility:** Who can see conversation context (public, private, agent-specific)
- **Default Delivery Target:** Where to send replies if not specified
- **Callback Base URL:** Webhook endpoint for inbound events (if provider supports)
- **Media Max MB:** Maximum file size for media uploads/downloads

##### Discord-Specific Fields
- **Bot Token:** Discord bot token (sensitive field)
- **Server ID / Guild ID:** Discord server/guild identifier(s)
- **Channel ID:** Default channel for replies
- **Presence Settings:**
  - Activity Type (Playing, Streaming, Listening, etc.)
  - Activity Status Text
  - Status (Online, Idle, DND, Invisible)
- **Agent Components:** Settings for agent-specific roles/permissions
- **DM Policy:** Behavior for direct messages

##### Slack-Specific Fields
- **Bot Token:** Slack bot/app token (sensitive field)
- **Signing Secret:** Slack request signing key (sensitive field)
- **Default Workspace:** Which workspace if multiple
- **Channel Routing:** Map channels to agents
- **Thread Behavior:** Reply in thread vs. new message
- **Block Kit Support:** Enable/disable advanced message formatting

##### Telegram-Specific Fields
- **Bot Token:** Telegram bot token (sensitive field)
- **API ID:** Telegram API credentials (sensitive field)
- **API Hash:** Telegram API credentials (sensitive field)
- **Topic ID:** Default topic for replies (if using topics)
- **Audio Settings:**
  - Transcription enabled
  - Transcription command/endpoint
  - Audio format
- **Custom Commands:** Define custom slash commands
- **DM Policy:** Behavior for direct messages

##### WhatsApp-Specific Fields
- **API Token:** WhatsApp API token (sensitive field)
- **Phone Number:** WhatsApp phone number
- **Business Account ID:** (Optional)
- **Webhook URL:** Inbound message webhook
- **Message Prefix:** Prefix for WhatsApp-specific formatting
- **Media Auto-Download:** Download media files automatically
- **DM Policy:** Direct message behavior
- **Group DM Settings:** Group vs. individual DM routing

##### Signal-Specific Fields
- **Signal CLI Path:** Path to signal-cli executable
- **Phone Number:** Signal phone number
- **DM Policy:** Direct message behavior

##### iMessage-Specific Fields
- **Apple ID:** iMessage account (sensitive field)
- **Device Settings:** Which Mac/device to use
- **DM Policy:** Conversation routing

##### Other Provider Fields (Microsoft Teams, Mattermost, IRC, etc.)
- Provider-specific tokens and credentials
- Server/workspace configuration
- Channel routing rules
- Protocol-specific settings

### Messaging Settings (Global)

#### Broadcast Configuration
- **Broadcast Strategy:** parallel (all agents process) or sequential (first agent only)
- **Peer Configuration:** Map peer IDs to arrays of agent IDs
- **Broadcast Timeout:** How long to wait for agent responses

#### Messages Configuration
- **Message Prefix:** Prefix added to all outbound replies
- **Prefix Mode:** Static text, "auto" (derives from agent name), or templated with variables
  - Supported variables: `{model}`, `{modelFull}`, `{provider}`, `{thinkingLevel}`, `{identity.name}`
- **Response Prefix:** Separate prefix specifically for agent responses
- **Group Chat Settings:**
  - Mention patterns (regex patterns that trigger @mention response)
  - History limit (how many prior messages to consider)
- **DM Settings:**
  - DM history limit
- **Message Queue:**
  - Queue mode (parallel or sequential processing)
  - Debounce interval (ms) for rapid inbound messages
  - Per-channel debounce overrides
  - Queue capacity (max pending messages)
  - Drop policy (drop oldest, drop newest, or reject)

#### Acknowledgment Settings
- **Ack Reaction:** Emoji to acknowledge inbound messages (empty disables)
- **Ack Scope:** When to send acknowledgment:
  - `group-mentions` — group chats with @mention
  - `group-all` — all group chat messages
  - `direct` — direct messages only
  - `all` — all messages
  - `off` / `none` — disabled
- **Remove After Reply:** Auto-remove ack emoji once reply is sent

#### Status Reactions
- **Enabled:** Toggle status reaction indicator emojis
- **Custom Emojis:** Override defaults for:
  - Thinking (thinking state)
  - Tool (tool execution)
  - Coding (code generation)
  - Web (web search)
  - Done (completion)
  - Error (error state)
  - Stall Soft (timeout warning)
  - Stall Hard (hard timeout)
  - Compacting (memory/context compaction)
- **Timing:**
  - Debounce interval (ms) for status updates
  - Soft stall timeout (when to show soft stall warning)
  - Hard stall timeout (when to show hard stall warning)
  - Done hold duration (how long to show done emoji)
  - Error hold duration (how long to show error emoji)

#### Tool Error Handling
- **Suppress Tool Errors:** Hide ⚠️ tool-error warnings from users

#### Text-to-Speech
- **TTS Enabled:** Boolean toggle
- **TTS Provider:** Select audio synthesis backend
- **Voice/Speaker:** Select specific voice for output
- **Language:** Language for TTS
- **Speed:** Playback speed multiplier

### Commands Configuration

#### Global Command Settings
- **Native Commands:** Enable/disable native platform commands (slash commands)
  - Toggle: `true`, `false`, or `"auto"` (auto-detect)
- **Native Skill Commands:** Enable/disable skill-based command registration
- **Text Commands:** Enable/disable text-based command parsing
- **Bash Command:** Allow `!` bash chat command (default: false)
- **Config Command:** Allow `/config` command (default: false)
- **MCP Command:** Allow `/mcp` command for MCP settings (default: false)
- **Plugins Command:** Allow `/plugins` command (default: false)
- **Debug Command:** Allow `/debug` command (default: false)
- **Restart Commands:** Allow restart/reload commands

#### Authorization
- **Use Access Groups:** Enforce access group policies (default: true)
- **Owner Allowlist:** Explicit list of owner/admin IDs for owner-only tools
- **Owner Display:** How owner IDs are shown in system prompts:
  - `raw` — show actual ID
  - `hash` — show hashed ID (requires ownerDisplaySecret)
- **Owner Display Secret:** Secret key for hashing owner IDs (sensitive field)
- **Per-Channel Allowlist:** Map channel IDs to allowed sender IDs
  - Global default with `*` key
  - Provider-specific overrides (e.g., `discord`, `slack`)

### Audio/Talk Configuration

#### Transcription Settings
- **Transcription Command:** CLI command to convert audio to text
- **Transcription Timeout:** How long to wait for transcription (seconds)
- **Transcription Format:** Input/output audio format

#### Voice Settings
- **Voice Enabled:** Toggle voice/audio support
- **Voice Codec:** Audio codec (opus, pcm, aac, etc.)
- **Sample Rate:** Audio sample rate (16000 Hz, etc.)
- **Channels:** Mono/stereo configuration

#### Speech Recognition
- **STT Provider:** Speech-to-text backend (Google, Whisper, etc.)
- **STT Language:** Default language for recognition

## Interactions

1. **Select Provider:** Click provider in left panel → loads that provider's config form on right
2. **Account Selection:** Click account tab → switches to that account's config
3. **Add Account:** Click "+" button → form for new account details
4. **Toggle Enable:** Click on/off toggle next to provider → enables/disables without opening form
5. **Field Editing:** Modify field → marks as changed
6. **Save Changes:** Click Save button → validates and persists channel config
7. **Test Connection:** Click "Test" button (if available) → pings provider to verify credentials
8. **Show Sensitive Field:** Click eye icon on password/token fields → toggles visibility
9. **Copy Token:** Select token field → Ctrl+C copies to clipboard
10. **Help Tooltip:** Hover over `?` icon → shows field help text
11. **Remove Account:** Click remove icon on tab → deletes account with confirmation
12. **Global Settings:** Click gear icon → opens broadcast/messages/commands settings modal

## State / Data

### Loaded on Page Entry:
- **Provider List:** All supported channel types
- **Current Configurations:** For each provider, their current settings
- **Connection Status:** Whether each provider is currently connected
- **Account List:** Accounts configured for multi-account providers
- **Global Settings:** Broadcast, messages, commands, audio settings

### Loading States:
- **Initial Load:** Spinner while provider list and configs are fetched
- **Testing Connection:** Spinner on Test button while verifying credentials
- **Saving:** Disabled save button with "Saving..." text

### Empty States:
- **No Providers Configured:** "No channels configured" with link to add first provider
- **Provider Unconfigured:** "Configure this provider" button if credentials missing
- **No Accounts:** "No accounts configured" with add account button

### Error States:
- **Invalid Credentials:** Red error message with troubleshooting link
- **Connection Failed:** "Connection test failed" with error details
- **Validation Error:** Red highlights on required fields missing data
- **Save Failure:** Toast notification with error details

### Special States:
- **Sensitive Field Redaction:** Tokens/passwords show as `****` by default
- **Changes Pending:** Save button enabled with unsaved changes indicator
- **Multiple Accounts:** Account tabs with ability to switch and add new accounts
- **Test Connection Success:** Green checkmark and "Connected" confirmation

## API / WebSocket Calls

### REST Endpoints:

#### Channel Discovery & Status
- **GET `/api/channels/providers`** — List all available channel providers
- **GET `/api/channels/status`** — Get connection status for all configured channels
- **GET `/api/channels/{provider}/status`** — Get status for specific provider

#### Configuration
- **GET `/api/channels/{provider}/config`** — Fetch current config for provider
- **POST `/api/channels/{provider}/config`** — Save config for provider
- **POST `/api/channels/{provider}/config/validate`** — Validate without saving
- **GET `/api/channels/{provider}/accounts`** — List accounts for this provider
- **POST `/api/channels/{provider}/accounts`** — Add new account
- **DELETE `/api/channels/{provider}/accounts/{accountId}`** — Remove account

#### Messaging Settings
- **GET `/api/channels/messages/config`** — Fetch global messages settings
- **POST `/api/channels/messages/config`** — Save messages settings
- **GET `/api/channels/broadcast/config`** — Fetch broadcast settings
- **POST `/api/channels/broadcast/config`** — Save broadcast settings
- **GET `/api/channels/commands/config`** — Fetch commands settings
- **POST `/api/channels/commands/config`** — Save commands settings

#### Testing & Verification
- **POST `/api/channels/{provider}/test`** — Test connection with current config
  - Returns: `{ success: bool, message: string, connectedAccounts: array }`
- **POST `/api/channels/{provider}/test-webhook`** — Test webhook configuration

### WebSocket Events:
- **channel.status-changed** — Provider connection status changed
- **channel.config-updated** — Channel config was modified externally
- **channel.message-received** — New inbound message (real-time delivery)
- **channel.connection-error** — Provider connection failed
- **broadcast.delivered** — Broadcast message delivery status

## Notes

- **Sensitive Field Management:** Tokens, API keys, and passwords are encrypted in transit and stored securely. Users cannot retrieve previously saved tokens; they must provide new ones to update.
- **Multi-Account Support:** Discord, Slack, Telegram, and other platforms support multiple accounts/workspaces. Each account is a separate configuration tab.
- **Webhook Security:** Inbound webhooks require the provider's signing key to verify authenticity. This is enforced server-side.
- **Rate Limiting:** Some providers (Discord, Slack) have built-in rate limits. Settings configure per-provider retry behavior and queue handling.
- **Message Prefix Variables:** Template variables in message prefixes are case-insensitive and resolved at runtime. Unknown variables remain as literal text.
- **Broadcast Peer Routing:** Peers (identifiers like "peer1", "peer2") can be mapped to agent IDs. This enables multi-agent broadcast scenarios.
- **Account Deletion Warning:** Removing an account does not delete historical messages; it only removes the configuration.
- **DM Policy Inheritance:** If not explicitly set, DM policy inherits from channel defaults configuration.
- **Status Reactions Throttling:** Status reaction updates are debounced to avoid spam on platforms with reaction limits.
