# ADR-004: Web Chat

**Status:** Accepted
**Date:** 2026-03-05
**Relates to:** Chat page (`Pages/Chat.razor`)

## Context

Operators and developers need a way to test agent responses directly from the dashboard without connecting external messaging platforms. The web chat provides a real-time streaming conversation interface similar to consumer AI chat products.

## Decision

The Chat page (`/chat`) implements a full-screen chat layout with three zones: header (agent selector), message area (scrollable bubbles), and input bar. Messages stream via SignalR's `StreamChatAsync` hub method, with chunked content delivery and a blinking cursor indicator during streaming.

### Components
- **Chat header**: "Web Chat" title, agent selector dropdown (or "No agents running" warning chip), clear chat button
- **Message area**: Scrollable container with user bubbles (right-aligned, indigo tint) and agent bubbles (left-aligned, gray tint), empty state with "Start a conversation" message
- **Input bar**: Text field with "Type a message..." placeholder, send adornment icon, Enter-to-send (Shift+Enter for newline)
- **Streaming indicator**: Blinking cursor (▋) in agent bubble during active stream

### API Dependencies
- `GET /api/agents` → Available agents for selector
- SignalR Hub `/hubs/agent` → `StreamChatAsync(agentId, message)` → `IAsyncEnumerable<ChatChunk>`
- `ChatChunk`: `{ Type: "content"|"error", Content: string }`

## Acceptance Criteria

1. Chat page displays "Web Chat" header with chat icon
2. Agent selector dropdown populates from running agents
3. Each agent option shows ID with provider/model in parentheses
4. "No agents running" warning chip shown when no agents exist
5. Empty state displays "Start a conversation" heading
6. Empty state displays helper text "Type a message below to chat with the selected agent."
7. Message input has placeholder "Type a message..."
8. Message input has send icon adornment
9. Input is disabled when no agent is selected
10. Input is disabled while streaming a response
11. Pressing Enter sends the message
12. Pressing Shift+Enter does not send (allows multiline)
13. Sending adds a user bubble right-aligned with "You" label
14. User bubble displays the sent message text
15. User bubble displays timestamp (HH:mm:ss)
16. Agent response streams with a blinking cursor indicator (▋)
17. Streaming bubble shows agent ID as label
18. Completed response becomes a permanent agent bubble (left-aligned)
19. Agent bubble displays timestamp (HH:mm:ss)
20. Messages preserve whitespace (pre-wrap CSS)
21. Messages area auto-scrolls to bottom on new messages
22. Clear chat button (trash icon) removes all messages
23. Clear chat button is disabled when no messages exist
24. Agent error during streaming shows error snackbar with "Agent error: {message}"
25. Connection error during chat shows error snackbar
26. Sending empty or whitespace-only input does nothing
27. Chat page title is "Chat — JD.AI"

## Test Mapping

| AC# | Feature File Scenario |
|-----|-----------------------|
| 1 | ChatPage.feature: "Displays chat header with title" |
| 2-3 | ChatPage.feature: "Agent selector populates from running agents" |
| 4 | ChatPage.feature: "No agents warning when none running" |
| 5-6 | ChatPage.feature: "Empty state shown when no messages" |
| 7-8 | ChatPage.feature: "Message input with placeholder and send icon" |
| 9-10 | ChatPage.feature: "Input disabled when no agent or streaming" |
| 11-12 | ChatPage.feature: "Enter sends message, Shift+Enter does not" |
| 13-15 | ChatPage.feature: "User message bubble appears after sending" |
| 16-19 | ChatPage.feature: "Agent response streams with cursor indicator" |
| 20 | ChatPage.feature: "Messages preserve whitespace" |
| 21 | ChatPage.feature: "Chat auto-scrolls on new messages" |
| 22-23 | ChatPage.feature: "Clear chat removes all messages" |
| 24-25 | ChatPage.feature: "Error handling during chat" |
| 26 | ChatPage.feature: "Empty input does not send" |
| 27 | ChatPage.feature: "Page title is correct" |

## Consequences

- Chat state is entirely client-side (in-memory `List<ChatMessage>`); refreshing the page loses all history.
- Only one conversation at a time; no multi-session support in the web chat.
- Streaming cancellation is supported via CancellationTokenSource but not exposed in the UI (no stop button).
