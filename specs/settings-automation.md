# Settings > Automation
n> **Verified:** Real UI via authenticated playwright [2026-04-11]

>**Verified:** Gateway authentication required. Session params do not bypass WebSocket auth for admin panel.

**Route:** `/settings/automation`  
**Nav Path:** Settings > Automation  
**Description:** Configure automation rules, triggers, and actions for gateway event handling.

## Status
**⚠️ Authentication Required** — This page requires gateway authentication via WebSocket connection before the UI content is accessible.

## Authentication Gate (Current State)

The Automation settings page is protected by a login gate with the following fields:

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

- **Automation rules list** — Table or card view showing existing automation rules with status indicators
- **Trigger types selector** — Configuration for event-based triggers (e.g., message received, timer, status change) and schedule-based triggers (cron-like expressions)
- **Action types selector** — Options for actions to execute (e.g., send notification, log event, call webhook, update state)
- **Condition/filter logic** — UI for building conditional expressions (if X and Y then Z)
- **Create new automation form** — Button and modal/panel to define new automation rules with:
  - Name and description fields
  - Trigger configuration (event type + conditions)
  - Action configuration (action type + parameters)
  - Enable/disable toggle
  - Save/apply button

## Interactions (Estimated)

- Click WebSocket URL field → Enter/modify gateway connection string
- Click Gateway Token field → Enter optional token credential
- Click token visibility toggle → Reveal/mask token input
- Click password visibility toggle → Reveal/mask password input
- Click Connect button → Validate credentials and establish WebSocket connection
- (After auth) Click "New Automation" button → Open create form
- (After auth) Select trigger type → Show trigger-specific configuration options
- (After auth) Select action type → Show action-specific parameter fields
- (After auth) Add condition → Add row to condition builder
- (After auth) Save automation → POST rule to gateway
- (After auth) Click automation row → Open details/edit view
- (After auth) Toggle enable/disable → Update rule status

## State / Data

- **Connection state:** Not connected (login gate showing) | Connected (authenticated content visible)
- **Automation rules:** List of configured rules with their current enabled/disabled status
- **Rule properties:**
  - ID (unique identifier)
  - Name (human-readable label)
  - Description
  - Trigger configuration (type + parameters)
  - Conditions (filters/logic)
  - Actions (operation(s) to execute)
  - Enabled status
  - Last execution time (if applicable)
  - Execution count or stats
- **Trigger types:** Event-based (message received, status change, webhook), schedule-based (cron expressions, intervals)
- **Action types:** Notification, logging, webhook call, state update, conditional branches

## API / WebSocket Calls

- **WebSocket connection:** Initiated on Connect button submit to the URL specified in WebSocket URL input
- **Authentication payload:** Gateway token and password (if provided) transmitted on initial connection
- **Fetch rules:** GET or WS subscribe to list all automation rules with current status after auth
- **Create rule:** POST to gateway with rule definition (trigger, actions, conditions)
- **Update rule:** PUT/PATCH to modify existing rule
- **Delete rule:** DELETE to remove a rule
- **Toggle rule:** PATCH to enable/disable without modifying configuration
- **Test rule:** Optional endpoint to simulate trigger execution for testing

## Notes

- Automation rules are likely stored server-side in the gateway and synced to the UI on connection
- Rules may support templating or variable substitution for dynamic action parameters
- Condition logic might support complex expressions (AND, OR, NOT, parentheses)
- Trigger types could be extensible based on gateway capabilities
- Performance consideration: Large numbers of rules may require pagination or filtering
