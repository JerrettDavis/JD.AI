# Agent > Nodes Page Specification
n> **Verified:** Real UI via authenticated playwright [2026-04-11]


> **Verified:** Gateway authentication required. Session params do not bypass WebSocket auth for admin panel.

**Route:** `/agent/nodes`  
**Controller:** `controllers/nodes.ts` (OpenClaw UI)  
**View:** `views/nodes.ts`

## Overview

The Nodes page is a comprehensive device and network management interface within the Agent section. It displays:

1. **Execution Approvals** - Approval rules for agent execution (gateway vs node targets)
2. **Bindings** - Agent-to-node bindings for distributed execution
3. **Devices** - Paired devices and role-based token management
4. **Nodes** - Live list of connected nodes and their metadata

## Page Structure

### Sections (in order)

#### 1. Execution Approvals Section
**Purpose:** Control which agents can execute on gateways vs remote nodes

- **Target Selection:** Radio/dropdown choice between "Gateway" and "Node"
  - Gateway: Policies for agent execution on main server
  - Node: Per-node execution policies with node ID selector
- **Agent Selection:** Dropdown to select which agent to configure
- **Approval Rules:** Editable list of approval configurations per agent/target
  - Load from snapshot file format
  - Support for form mode and raw JSON editing
  - Save/discard changes with validation

**State Props:**
- `execApprovalsLoading` - Loading indicator
- `execApprovalsSaving` - Save in progress
- `execApprovalsDirty` - Has unsaved changes
- `execApprovalsSnapshot` - Current file snapshot
- `execApprovalsForm` - Form-mode data
- `execApprovalsSelectedAgent` - Selected agent
- `execApprovalsTarget` - "gateway" or "node"
- `execApprovalsTargetNodeId` - Selected node ID

#### 2. Bindings Section
**Purpose:** Configure which nodes agents default to

- **Default Node Binding:** Single selector for system default execution node
  - Shows available nodes from `nodeTargets`
  - Resolves agent list from config
- **Per-Agent Bindings:** Table showing each agent and its bound node
  - Agent ID | Current Node | Action buttons
  - Bind to node selector
  - "Bind Default" button to reset to system default
- **Save/Discard:** Apply or cancel binding changes with config update

**State Props:**
- `configForm` - Current config being edited
- `configLoading/configSaving/configDirty` - Config state
- `configFormMode` - "form" or "raw" JSON editor

#### 3. Devices Section
**Purpose:** Device pairing and token lifecycle management

**Pending Devices Subsection:**
- List of pairing requests from remote devices
- Per-request display:
  - Device ID and display name
  - Remote IP address
  - Requested role(s) and scopes
  - Time since requested
  - "Repair" badge if this is a repair request
- **Actions:**
  - `Approve` button → accepts pairing, creates initial token
  - `Reject` button → denies pairing request

**Paired Devices Subsection:**
- List of successfully paired devices
- Per-device display:
  - Device ID and display name
  - Remote IP address
  - Assigned roles and scopes
- **Token Management** (if tokens exist):
  - Per-token row showing:
    - Role identifier
    - Status (active/revoked)
    - Scopes assigned
    - Relative timestamp (creation, rotation, or last used)
  - **Token Actions:**
    - `Rotate` button → create new token for same role
    - `Revoke` button → deactivate token (hidden if already revoked)

**Empty State:**
- "No paired devices." when both pending and paired lists are empty

**State Props:**
- `devicesList` - Structure: `{ pending: PendingDevice[], paired: PairedDevice[] }`
- `devicesLoading` - Loading indicator
- `devicesError` - Error message display

#### 4. Nodes Section
**Purpose:** Display all connected nodes and their metadata

- **Header:**
  - Title: "Nodes"
  - Subtitle: "Paired devices and live links."
  - Refresh button (disabled while loading)
- **List Display:**
  - Each node renders with key metadata
  - Node ID, connection status, uptime, etc.
  - Details depend on node data structure (variable by system)
- **Empty State:**
  - "No nodes found." when node list is empty

**State Props:**
- `nodes` - Array of node objects (structure: `Array<Record<string, unknown>>`)
- `loading` - Loading indicator
- `onRefresh` - Callback to reload nodes

## Data Flow & State Management

### Node Controller (`controllers/nodes.ts`)

```typescript
export type NodesState = {
  client: GatewayBrowserClient | null;      // WebSocket/API connection
  connected: boolean;                        // Connection status
  nodesLoading: boolean;                     // Loading flag
  nodes: Array<Record<string, unknown>>;     // List of nodes
  lastError: string | null;                  // Last error message
};

// Async load via: client.request("node.list", {})
// Response: { nodes?: Record<string, unknown> }
```

### Initialization
1. Page mounts, calls `loadNodes(state, { quiet: true })`
2. Polling via `startNodesPolling(host)` on lifecycle connect
3. Polling interval can be stopped via `stopNodesPolling(host)`

### Refresh Flow
- User clicks "Refresh" button
- Calls `props.onRefresh()`
- Triggers `loadNodes()` again
- Updates `nodesLoading` and `lastError` state

## Type Definitions (from source)

```typescript
// Device pairing request
type PendingDevice = {
  requestId: string;
  deviceId: string;
  displayName?: string;
  remoteIp?: string;
  role?: string;
  roles: string[];
  scopes: string[];
  ts?: number;           // Timestamp in ms
  isRepair?: boolean;
};

// Successfully paired device
type PairedDevice = {
  deviceId: string;
  displayName?: string;
  remoteIp?: string;
  roles: string[];
  scopes: string[];
  tokens: DeviceTokenSummary[];
};

// Token in a paired device
type DeviceTokenSummary = {
  role: string;
  scopes: string[];
  createdAtMs?: number;
  rotatedAtMs?: number;
  lastUsedAtMs?: number;
  revokedAtMs?: number;
};

// Pairing list structure
type DevicePairingList = {
  pending: PendingDevice[];
  paired: PairedDevice[];
};

// Node target option for binding
type NodeTargetOption = {
  label: string;    // Display name
  value: string;    // Node ID or system identifier
};
```

## Event Handlers

| Handler | Triggered | Responsibility |
|---------|-----------|-----------------|
| `onRefresh()` | Refresh button (Nodes section) | Reload node list |
| `onDevicesRefresh()` | Refresh button (Devices section) | Reload device/pairing list |
| `onDeviceApprove(requestId)` | Approve button on pending device | Accept pairing request |
| `onDeviceReject(requestId)` | Reject button on pending device | Deny pairing request |
| `onDeviceRotate(deviceId, role, scopes)` | Rotate button on token | Create new token for role |
| `onDeviceRevoke(deviceId, role)` | Revoke button on token | Deactivate token |
| `onLoadConfig()` | Binding section mount | Fetch current config |
| `onLoadExecApprovals()` | Approvals section mount | Fetch approval rules file |
| `onBindDefault(nodeId \| null)` | Default node selector change | Update system default |
| `onBindAgent(agentIndex, nodeId \| null)` | Per-agent binding change | Bind/unbind agent |
| `onSaveBindings()` | Save button (Bindings section) | Apply config changes |
| `onExecApprovalsTargetChange(kind, nodeId)` | Target radio/dropdown change | Switch approval target |
| `onExecApprovalsSelectAgent(agentId)` | Agent dropdown change | Select agent to configure |
| `onExecApprovalsPatch(path, value)` | Form field edit | Update approval rule |
| `onExecApprovalsRemove(path)` | Delete button on rule | Remove approval entry |
| `onSaveExecApprovals()` | Save button (Approvals section) | Apply approval changes |

## Loading States

- **Nodes Loading:** Shows loading indicator, disables refresh button
- **Devices Loading:** Shows loading indicator on devices section
- **Config Loading:** Disables binding controls, shows loading on save button
- **Approvals Loading:** Disables approval form, shows loading on save button

## Error Handling

- **Device Errors:** `devicesError` prop displays error callout above device list
- **Config Errors:** Shown inline in bindings section
- **Approval Errors:** Shown inline in approvals section
- **Node Errors:** `lastError` prop stored but not displayed in Nodes section itself

## Visual Conventions

- **Card Layout:** Each major section wrapped in `.card` element
- **List Items:** Device/node entries use `.list` and `.list-item` classes
- **Status Badges:** "Repair", "active", "revoked" shown as text inline
- **Buttons:** Primary (blue) for approval/rotation; danger (red) for revoke
- **Typography:** Title, subtitle, and muted text for hierarchy
- **Empty States:** Muted text saying "No X found."

## Related Pages

- **Agent > Agents** - Agent list that Nodes bindings reference
- **Settings > Config** - Config editing that bindings and approvals affect
- **Control > Logs** - Device connection/pairing logs

---

**Last Updated:** April 2026  
**Status:** Documented from OpenClaw UI source v2026.4.6
