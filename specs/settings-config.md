# Settings > Config

**Route:** `/settings/config`  
**Nav Path:** Settings > Config  
**Description:** Centralized configuration management interface displaying all system settings organized by category with form and raw JSON editing modes.

## Layout

The Config page uses a two-panel layout:

- **Left Sidebar:** Hierarchical navigation with categories and sections (collapsible)
- **Main Content Area:** Configuration form/raw editor with top action bar and status indicators

The interface supports two view modes toggled via buttons:
- **Form Mode:** Organized form fields grouped by section
- **Raw Mode:** JSON/JSON5 editor for direct configuration editing

## Components

### Top Action Bar
- **Mode Toggle:** Buttons to switch between Form and Raw editing modes
- **Changes Badge:** Shows count of unsaved changes (e.g., "3 changes")
- **Status Indicator:** Connection status, config file path, auto-sync state
- **Action Buttons:** Save, Apply, Update, Reset, Reload config
- **File Opener:** Button to open config file in external editor (when `configPath` available)

### Left Sidebar Navigation
- **Category Headers:** Core, AI & Agents, Communication, Automation, Infrastructure, Appearance
- **Section Items:** Within each category, list of configuration sections:
  - **Core** → Environment, Authentication, Updates, Meta, Logging, Diagnostics, CLI, Secrets
  - **AI & Agents** → Agents, Models, Skills, Tools, Memory, Session
  - **Communication** → Channels, Messages, Broadcast, Talk, Audio
  - **Automation** → Commands, Hooks, Bindings, Cron, Approvals, Plugins
  - **Infrastructure** → Gateway, Web, Browser, NodeHost, CanvasHost, Discovery, Media, ACP, MCP
  - **Appearance** → Theme, UI, Setup Wizard
- **Search Box:** Global search to filter sections and fields
- **Icons:** Visual indicator for each section (settings, gears, models, channels, etc.)

### Form View Components
- **Grouped Field Sections:** Fields organized by subsection with collapsible headers
- **Field Types:**
  - Text inputs (strings)
  - Number inputs (integers, decimals)
  - Boolean toggles (checkboxes/switches)
  - Select dropdowns (enums, predefined choices)
  - Color pickers (for theme/appearance settings)
  - Text areas (multiline configuration blocks)
  - Array editors (add/remove items for lists)
  - Nested object editors (for nested config structures)
- **Field Metadata:**
  - Label (human-readable name)
  - Help text / description
  - Default value indicator
  - Required/optional badge
  - Validation error messages
  - Sensitive field redaction (password/token fields show as `***`)
- **Tab Navigation:** Tabs for each active section when multiple sections are displayed

### Raw Editor
- **JSON/JSON5 Editor:** Syntax-highlighted code editor with:
  - Line numbers
  - Bracket matching
  - Validation error highlighting
  - Copy/paste support
- **Validity Indicator:** Shows if JSON is valid or displays parsing errors
- **Comparison View:** Option to show original vs. modified side-by-side

### Status Messages
- **Connection Status:** "Connected", "Disconnected", "Reconnecting"
- **Validation Status:** Green checkmark for valid, red X with error list for invalid
- **Save Feedback:** "Saved successfully", "Save failed", "Unsaved changes"
- **Config Info:** Version number, generation timestamp, last updated timestamp

## Sections & Configuration Fields

### Core Category

#### Environment (env)
- Environment variables configuration
- Path overrides
- System locale settings
- Timezone configuration

#### Authentication (auth)
- API key management
- OAuth token storage
- Service account credentials
- Authentication strategy selection

#### Updates (update)
- Auto-update enabled/disabled toggle
- Update check interval (minutes)
- Release channel (stable/beta/nightly)
- Update notification preferences

#### Meta (meta)
- System metadata
- Deployment environment
- Instance name/identifier
- Custom metadata fields

#### Logging (logging)
- Log level (debug, info, warn, error)
- Log file paths
- Log rotation settings
- Log format (JSON, plaintext, etc.)
- Max file size before rotation
- Retention days

#### Diagnostics (diagnostics)
- Performance monitoring enabled/disabled
- Metrics collection opt-in/out
- Debug mode toggle
- Trace collection settings

#### CLI (cli)
- CLI command aliases
- Default output format (json, table, text)
- Interactive mode settings
- Color output toggle

#### Secrets (secrets)
- Secret backend selection (vault, env, file)
- Secret prefix patterns
- Redaction rules
- Encryption key management

### AI & Agents Category

#### Agents (agents)
- Default agent configuration
- Agent list/directory paths
- Concurrency limits
- Timeout settings
- Default model assignments
- Agent identity/name customization

#### Models (models)
- Model provider configuration (Anthropic, OpenAI, etc.)
- Model alias definitions
- API endpoint overrides
- Default model selection per provider
- Model-specific parameters (temperature, max_tokens, etc.)
- Token counting strategies

#### Skills (skills)
- Available skills registry
- Skill loading paths
- Skill enablement toggles
- Skill-specific configuration

#### Tools (tools)
- Tool registry
- Tool access policies
- Tool execution timeouts
- Web search provider selection

#### Memory (memory)
- Memory backend type (in-memory, redis, database)
- Memory retention policy
- Conversation history limits
- Session storage settings

#### Session (session)
- Session timeout (minutes)
- Session persistence
- Session key encryption
- Max concurrent sessions

### Communication Category

#### Channels (channels)
- Channel defaults (heartbeat visibility, group policy, context visibility)
- Per-channel model overrides
- Channel-specific settings for each provider (Discord, Slack, Telegram, etc.)
- Message queueing strategy
- Default delivery targets

#### Messages (messages)
- Message prefix configuration
- Message queue mode (parallel/sequential)
- Debounce settings for rapid inbound messages
- Acknowledgment reaction emoji
- Status reaction configuration (thinking, tool, coding, done, error emojis)
- Text-to-speech settings
- Tool error suppression toggle
- Reply prefix templates with variable substitution

#### Broadcast (broadcast)
- Broadcast strategy (parallel/sequential)
- Peer ID to agent routing
- Broadcast-specific timeouts

#### Talk (talk)
- Audio transcription settings
- Voice/speech settings
- Audio codec selection

#### Audio (audio)
- Audio transcription command
- Transcription timeout
- Audio format preferences
- Volume normalization settings

### Automation Category

#### Commands (commands)
- Native command registration enabled/disabled
- Text command parsing enabled/disabled
- Bash command access toggle
- Config command access toggle
- MCP command access toggle
- Plugin command access toggle
- Debug command access toggle
- Owner/admin allowlist
- Per-channel command access policies
- Command execution timeout
- Command output size limits

#### Hooks (hooks)
- Hook file paths
- Hook module loading settings
- Pre/post execution hooks
- Event-based hook configuration

#### Bindings (bindings)
- Keyboard/command bindings
- Custom shortcut definitions
- Binding conflict resolution

#### Cron (cron)
- Cron job definitions
- Cron execution timeout
- Cron retention policy
- Max concurrent cron jobs

#### Approvals (approvals)
- Approval workflow enabled/disabled
- Approval timeout (minutes)
- Approver list configuration
- Approval notification channels

#### Plugins (plugins)
- Plugin directories
- Plugin enablement toggle
- Plugin auto-discovery
- Plugin configuration overrides
- Bundled plugin settings

### Infrastructure Category

#### Gateway (gateway)
- Gateway port number
- Bind address
- TLS/SSL configuration
- Reverse proxy settings
- CORS policy
- Rate limiting configuration
- Tailscale integration settings

#### Web (web)
- Web server port
- Static file serving paths
- Middleware configuration
- Session cookie settings
- HTTPS enforcement

#### Browser (browser)
- Headless browser path
- Browser launch arguments
- Browser timeout settings
- Viewport dimensions
- Screenshot quality

#### NodeHost (nodeHost)
- Node host connection URL
- Worker pool size
- Task queue configuration
- Timeout settings

#### CanvasHost (canvasHost)
- Canvas host endpoint
- Canvas worker configuration
- Rendering timeout
- Memory limits

#### Discovery (discovery)
- Service discovery method
- Service registry endpoint
- Health check interval
- DNS configuration

#### Media (media)
- Media processing backend
- Image processing settings
- Audio processing settings
- Video codec support
- Max file size limits

#### ACP (acp)
- ACP server configuration
- ACP authentication
- ACP routing policies
- ACP timeout settings

#### MCP (mcp)
- MCP server configuration
- MCP protocol version
- MCP client settings
- MCP feature flags

### Appearance Category

#### Theme
- Theme selection (claw, knot, dash)
- Light/dark mode toggle
- System preference detection
- Custom theme override
- Color customization

#### UI
- Border radius preference (None/Slight/Default/Round/Full)
- Font size scaling
- Compact/spacious mode
- Animation preferences
- Sidebar collapse default

#### Setup Wizard
- First-run wizard enabled/disabled
- Wizard step completion tracking
- Onboarding checklist state

## Interactions

1. **Navigation:** Click section in sidebar → loads that section's fields in main area
2. **Search:** Type in search box → filters sections and fields by name/description
3. **Mode Toggle:** Click Form/Raw button → switches between editing modes (validates on switch)
4. **Field Editing:** Modify field → marks as changed, updates changes badge
5. **Save:** Click Save button → validates config, writes to server, shows confirmation
6. **Apply:** Click Apply button → applies config without saving to disk (live reload)
7. **Reset:** Click Reset button → discards unsaved changes, reverts to last saved state
8. **Reload:** Click Reload button → fetches fresh config from server
9. **Update:** Click Update button → checks for new schema/version updates
10. **File Open:** Click folder icon → opens config file in system editor
11. **Copy/Paste:** In raw mode, select + Ctrl+C/Ctrl+V works normally
12. **Sensitive Field Toggle:** Click eye icon on password/token fields to show/redact
13. **Default Value:** Click "reset to default" link on individual fields

## State / Data

### Loaded on Page Entry:
- **Config Schema:** JSON schema defining all possible fields, their types, validation rules
- **Current Config Values:** Actual system configuration values
- **UI Hints:** Labels, help text, placeholders, field grouping information
- **Sensitive Field Metadata:** Which paths contain secrets/sensitive data

### Loading States:
- **Initial Load:** Spinner while schema and config are fetched
- **Saving:** Disabled save button with "Saving..." text
- **Applying:** Spinner overlay on form indicating live changes
- **Updating:** Version check in progress

### Empty States:
- **No Config Loaded:** "Unable to connect to config server" message with retry
- **Section Empty:** "No fields in this section" if schema section is empty

### Error States:
- **Validation Errors:** Red highlights on invalid fields with error messages
- **Save Failure:** Toast notification with error details
- **Connection Lost:** "Disconnected" status badge, disabled save

### Special States:
- **Form Unsafe:** If raw JSON contains structures that can't safely render in form, show warning
- **Changes Pending:** Save/Reset buttons enabled, changes badge visible
- **Dirty Form:** Indicator when user has unsaved changes (prevents loss)

## API / WebSocket Calls

### REST Endpoints:
- **GET `/api/config/schema`** — Fetch JSON schema of all config fields
- **GET `/api/config/current`** — Fetch current configuration values
- **GET `/api/config/ui-hints`** — Fetch UI metadata (labels, help text, grouping)
- **POST `/api/config/save`** — Save configuration changes to disk
- **POST `/api/config/apply`** — Apply configuration without persisting (live reload)
- **POST `/api/config/validate`** — Validate config without saving
- **POST `/api/config/reset`** — Reset to last saved state
- **GET `/api/config/file`** — Open config file path (optional)

### WebSocket Events:
- **config.changed** — Config was modified externally, reload needed
- **config.validated** — Validation result for unsaved changes
- **schema.updated** — Schema version changed, refresh schema

## Notes

- **Sensitive Field Redaction:** All fields marked as "sensitive" (passwords, tokens, API keys) display `***` in both form and raw modes. Users must explicitly acknowledge they're editing sensitive data.
- **Nested Configuration:** Deep nested objects (e.g., `channels.discord.accounts`) may render as collapsible sub-sections or inline editors.
- **Enum/Select Fields:** Dropdowns auto-generate from schema's `enum` property; additional values may be forbidden unless `additionalProperties: true`.
- **Array Fields:** List fields (e.g., `agents.allowFrom`) show add/remove buttons for each item.
- **Form Safety:** If raw JSON is malformed or contains structures the form doesn't support, form mode becomes read-only with warning.
- **Auto-save Not Available:** Changes must be explicitly saved; no auto-save to prevent unintended writes.
- **Schema Evolution:** Schema updates (new fields, deprecations) are handled gracefully — deprecated fields shown with warning badge.
