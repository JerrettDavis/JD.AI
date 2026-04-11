# Chat

**Route:** `/chat`  
**Nav Path:** Chat (sidebar)  
**Description:** Real-time conversational interface for testing agent responses directly from the dashboard without requiring external messaging platform connections.

## Layout

The Chat page uses a **full-screen chat application layout** with three distinct zones:

- **Header Zone** — Fixed-height header containing page title, agent selector, and clear chat action
- **Message Area** — Scrollable central zone displaying conversation history as message bubbles
- **Input Zone** — Fixed-height footer containing text input field with send action

The layout is responsive and maintains consistent spacing across desktop and tablet viewports.

## Components

### Header Section

**Web Chat Title** — Text label  
- Display text: "Web Chat"
- Positioned left in header, paired with optional chat icon
- Semantic header role (h1 equivalent in accessibility tree)

**Agent Selector** — Dropdown menu  
- Label: "Select Agent" or similar
- Type: Dropdown/combobox listing all running agents
- Options populated from `GET /api/agents`
- Each option displays: `AgentID (Provider/Model)` format
- Disabled state: Grayed out if no agents running
- Selected state: Shows currently selected agent ID
- Default behavior: No agent selected on page load (input disabled)
- Empty state chip: "No agents running" warning badge if agent list is empty

**Clear Chat Button** — Icon button  
- Icon: Trash icon or equivalent
- Aria label: "Clear chat history"
- Behavior: Removes all messages from the message area
- Disabled state: Grayed out when no messages exist (conversation is empty)
- Position: Right-aligned in header

### Message Area

**Empty State** — Container with centered content  
- Heading: "Start a conversation"
- Helper text: "Type a message below to chat with the selected agent."
- Display condition: Shown only when message list is empty and no agent error present

**Message Bubbles** — Scrollable list of conversation messages

**User Message Bubble** — Right-aligned message container
- Alignment: Floated/justified to right side
- Background color: Indigo or blue tint (distinct from agent messages)
- Label: "You" (typically as small text above or integrated with bubble)
- Content: User's sent message text
- Timestamp: HH:mm:ss format (bottom right of bubble)
- Text rendering: Preserves whitespace (CSS `white-space: pre-wrap`)
- Padding: Standard message bubble padding
- Added after: User presses Enter in input field

**Agent Message Bubble** — Left-aligned message container
- Alignment: Floated/justified to left side
- Background color: Gray or neutral tint (distinct from user messages)
- Label: Agent ID (typically as small text above or integrated with bubble)
- Content: Agent's response text
- Timestamp: HH:mm:ss format (bottom left of bubble)
- Text rendering: Preserves whitespace (CSS `white-space: pre-wrap`)
- Padding: Standard message bubble padding

**Streaming Agent Bubble** — Active response during stream
- Appearance: Identical to Agent Message Bubble
- Content: Chunks appended in real-time as they arrive via SignalR
- Cursor indicator: Blinking vertical bar (▋) at end of content during stream
- Conversion: Upon stream completion, becomes a permanent Agent Message Bubble

**Error Message Bubble** (If shown inline):
- Background color: Red or error tint
- Content: Error message text (e.g., "Agent encountered an error")
- Timestamp: When error occurred
- Position: Displayed like a normal agent bubble

### Input Zone

**Message Input Field** — Text area  
- Type: Single-line or multiline text input (`<textarea>` or `<input type="text">`)
- Placeholder text: "Type a message..."
- Max characters: No hard limit specified (suggest 4096 for practical limit)
- Disabled state: When no agent selected OR while streaming a response
- Focus: Auto-focuses on page load (if agent available)
- Keyboard behavior:
  - Enter key: Sends message (even in multiline mode)
  - Shift+Enter: Adds newline (does not send)
  - Arrow keys, Tab, etc: Standard text navigation

**Send Button/Adornment** — Icon button or visual indicator
- Icon: Send icon (paper airplane, arrow, or equivalent)
- Position: Right side of input field (as input adornment or adjacent button)
- Aria label: "Send message"
- Disabled state: When input is empty, whitespace-only, no agent selected, or streaming active
- Interaction: Click sends message (same effect as pressing Enter)
- Visual feedback: May show hover state or loading spinner during send

## Interactions

### Initial Page Load
1. Page loads at `/chat`
2. Agent selector populates from `GET /api/agents` response
3. If agents available: dropdown enabled, show agent list
4. If no agents: show "No agents running" warning chip, disable input and clear button
5. Message area shows empty state ("Start a conversation")

### Agent Selection Flow
1. User clicks agent selector dropdown
2. List of running agents displays (Agent ID with provider/model)
3. User selects an agent
4. Input field becomes enabled
5. Focus can shift to input field or remain on dropdown (UX decision)

### Message Sending Flow
1. User types message in input field (multiline allowed)
2. User presses Enter OR clicks send button
3. Empty/whitespace-only input: Do nothing (silent ignore)
4. Non-empty input:
   - User bubble appears immediately (right-aligned with "You" label, timestamp)
   - Input field clears
   - Input becomes disabled (until response completes)
   - Send button becomes disabled
   - Message area auto-scrolls to bottom to show user bubble

### Message Streaming Flow
1. User's message sends to SignalR hub: `StreamChatAsync(agentId, message)`
2. Agent bubble appears (left-aligned with agent ID label) with streaming cursor (▋)
3. As `ChatChunk` messages arrive via SignalR:
   - Chunk content appended to agent bubble in real-time
   - Cursor indicator remains blinking
4. When stream completes:
   - Cursor indicator removed
   - Agent bubble becomes permanent (shows timestamp)
   - Input field re-enabled
   - Send button re-enabled
   - Message area auto-scrolls to show completed response

### Error Handling Flow
1. **Agent Error during stream**: Show error snackbar with "Agent error: {error message}"
   - Agent bubble may show partial content received before error
   - Input re-enabled so user can send another message
2. **Connection Error**: Show error snackbar "Connection error: Unable to reach agent"
   - Input disabled until reconnection
3. **Empty input submission**: Silent fail (no error notification)

### Clear Chat Flow
1. User clicks clear chat button (trash icon)
2. All message bubbles removed
3. Message area shows empty state again
4. Clear button becomes disabled (until new messages added)
5. Input field remains enabled (if agent selected)

### Auto-Scroll Behavior
- After user message added: Scroll to bottom of message area
- After agent chunk added: Scroll to bottom (maintaining focus on streaming response)
- Manual scroll up: Does not auto-scroll (user explicitly viewing history)
- Scroll position restored: When new message arrives that is currently scrolled past (soft auto-scroll at bottom only)

## State / Data

### Component State
- **Selected Agent**: Current agent ID (null if none selected)
- **Message History**: List of `ChatMessage` objects with:
  - `sender`: "user" | "agent" | "system"
  - `content`: String (may be empty during streaming)
  - `timestamp`: ISO string or HH:mm:ss
  - `agentId`: Agent ID (for agent messages)
  - `isStreaming`: Boolean (true while chunks arriving, false when complete)
- **Is Streaming**: Boolean (true while agent response is actively being received)
- **Input Value**: Current text in input field
- **Agent List**: Array of available agents from `GET /api/agents`
- **Error State**: Current error message (if any), null when no error

### Persisted Data
- **Chat history**: NOT persisted; lost on page refresh (client-side in-memory only)
- **Selected Agent**: May be stored in sessionStorage or localStorage (optional UX enhancement)
- **Scroll Position**: Not persisted between sessions

### Real-time Data
- **Message Chunks**: Streamed via SignalR `StreamChatAsync` as `IAsyncEnumerable<ChatChunk>`
- **Chunk Structure**: `{ Type: "content" | "error", Content: string }`
- **Agent List**: Fetched once on page load via REST API

### Loading/Empty States
- **No agents**: "No agents running" chip, input disabled, clear button disabled
- **No messages**: "Start a conversation" heading with helper text
- **Streaming**: Cursor indicator in agent bubble, input disabled, send button disabled
- **Connection error**: Snackbar notification with error message

## API / WebSocket Calls

### Get Available Agents
- **Method**: GET
- **Endpoint**: `/api/agents`
- **Response**: Array of agent objects
- **Response schema**:
  ```
  [
    {
      "id": "agent-id-1",
      "provider": "anthropic",
      "model": "claude-3-sonnet"
    },
    ...
  ]
  ```
- **Called on**: Page load
- **Error handling**: Show "Unable to load agents" snackbar if request fails

### Stream Chat Messages (SignalR)
- **Hub**: `/hubs/agent`
- **Method**: `StreamChatAsync(agentId: string, message: string)`
- **Returns**: `IAsyncEnumerable<ChatChunk>`
- **Chunk schema**:
  ```
  {
    "type": "content" | "error",
    "content": "Text chunk or error message"
  }
  ```
- **Behavior**:
  - Called when user sends a message (after agent selected)
  - Chunks received in real-time; each chunk appended to agent bubble
  - Stream ends when last chunk received or error occurs
  - CancellationToken supported (not exposed in UI)
- **Error handling**: If chunk type is "error", show snackbar and stop streaming

## Notes

- **No Message History Persistence**: Chat state is entirely client-side (in-memory `List<ChatMessage>`). Refreshing the page loses all history. This is by design for testing purposes.
- **Single Conversation**: No multi-session or conversation threading support. Only one active chat at a time.
- **Streaming Cancellation**: Supported via backend CancellationTokenSource but not exposed in UI (no stop button visible to user). User can select different agent or refresh page to interrupt streaming.
- **Whitespace Preservation**: All message text uses `white-space: pre-wrap` CSS to preserve user formatting (newlines, spaces).
- **Keyboard Accessibility**: Input field must be fully keyboard-navigable (Tab, Enter, Shift+Enter, Escape). Agent selector must support arrow keys for navigation.
- **Focus Management**: When agent error occurs or connection drops, focus should remain or return to input field with clear error message.
- **Message Timestamps**: Format as HH:mm:ss (24-hour or 12-hour based on user locale preference from Settings > Appearance).
- **Agent Offline Handling**: If selected agent goes offline mid-chat, show error snackbar "Agent disconnected" and allow user to select another agent or retry.
- **Empty Input**: Whitespace-only input (spaces, tabs, newlines) should be treated as empty and not sent.
- **Responsive Design**: On mobile viewports:
  - Header may stack vertically (title, then agent selector on second line)
  - Message bubbles may adjust width to fit screen
  - Input field remains at bottom with soft keyboard integration
