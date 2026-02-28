---
description: "Connect JD.AI to Discord, Signal, Slack, Telegram, WebChat, and OpenClaw with pluggable channel adapters."
---

# Channel Adapters

Channel adapters connect the JD.AI Gateway to external messaging platforms. Each adapter implements a single interface — `IChannel` — and translates platform-specific messaging into a unified `ChannelMessage` format that the gateway can route to agents.

JD.AI ships with six adapters:

| Channel | Package | Platform | Transport |
|---------|---------|----------|-----------|
| [Discord](#discord) | `JD.AI.Channels.Discord` | Discord | WebSocket (Discord.Net) |
| [Signal](#signal) | `JD.AI.Channels.Signal` | Signal | JSON-RPC via signal-cli |
| [Slack](#slack) | `JD.AI.Channels.Slack` | Slack | Socket Mode (SlackNet) |
| [Telegram](#telegram) | `JD.AI.Channels.Telegram` | Telegram | Long polling (Telegram.Bot) |
| [WebChat](#webchat) | `JD.AI.Channels.Web` | Browser | SignalR bridge |
| [OpenClaw](#openclaw) | `JD.AI.Channels.OpenClaw` | OpenClaw | HTTP polling |

## The IChannel interface

Every channel adapter implements `IChannel` from `JD.AI.Core`:

```csharp
public interface IChannel : IAsyncDisposable
{
    string ChannelType { get; }
    string DisplayName { get; }
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default);

    event Func<ChannelMessage, Task>? MessageReceived;
}
```

| Member | Purpose |
|--------|---------|
| `ChannelType` | Unique identifier string (`"discord"`, `"signal"`, `"slack"`, `"telegram"`, `"web"`, `"openclaw"`) |
| `DisplayName` | Human-readable name shown in the gateway UI and API |
| `IsConnected` | Live connection status |
| `ConnectAsync` | Establishes the connection to the external platform |
| `DisconnectAsync` | Gracefully tears down the connection |
| `SendMessageAsync` | Sends an outbound message to a specific conversation |
| `MessageReceived` | Event raised when an inbound message arrives |

## ChannelMessage format

All inbound messages are normalized into a `ChannelMessage` record:

```csharp
public record ChannelMessage
{
    public required string Id { get; init; }
    public required string ChannelId { get; init; }
    public required string SenderId { get; init; }
    public string? SenderDisplayName { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string? ThreadId { get; init; }
    public string? ReplyToMessageId { get; init; }
    public IReadOnlyList<ChannelAttachment> Attachments { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
}
```

Attachments use lazy streaming — the file content is only downloaded when `OpenReadAsync` is called:

```csharp
public record ChannelAttachment(
    string FileName,
    string ContentType,
    long SizeBytes,
    Func<CancellationToken, Task<Stream>> OpenReadAsync);
```

## Channel registry

The gateway manages adapters through `IChannelRegistry`, a thread-safe in-memory registry:

```csharp
public interface IChannelRegistry
{
    IReadOnlyList<IChannel> Channels { get; }
    void Register(IChannel channel);
    void Unregister(string channelType);
    IChannel? GetChannel(string channelType);
}
```

The registry is registered as a singleton in the gateway's DI container. Channel endpoints (`/api/channels/*`) use the registry to list, connect, disconnect, and send messages.

### Registering a channel at startup

```csharp
var registry = app.Services.GetRequiredService<IChannelRegistry>();
registry.Register(new DiscordChannel(botToken: "your-bot-token"));
registry.Register(new TelegramChannel(botToken: "your-telegram-token"));
```

Or via configuration in `appsettings.json`:

```json
{
  "Gateway": {
    "Channels": [
      {
        "Type": "discord",
        "Name": "My Discord Bot",
        "Settings": { "BotToken": "..." }
      },
      {
        "Type": "telegram",
        "Name": "Support Bot",
        "Settings": { "BotToken": "..." }
      }
    ]
  }
}
```

---

## Discord

The Discord adapter uses [Discord.Net](https://discordnet.dev/) and supports guild messages, direct messages, threads, and file attachments.

### Prerequisites

1. Create a Discord application at <https://discord.com/developers/applications>
2. Add a **Bot** and copy the bot token
3. Enable the **Message Content** privileged intent
4. Invite the bot to your server with the `bot` and `applications.commands` scopes

### Required intents

The adapter requests these gateway intents:

- `Guilds` — access server and channel metadata
- `GuildMessages` — receive messages in server channels
- `DirectMessages` — receive DMs
- `MessageContent` — read message text (privileged intent)

### Setup

```csharp
var discord = new DiscordChannel(botToken: "your-discord-bot-token");
registry.Register(discord);
await discord.ConnectAsync();
```

Or via gateway configuration:

```json
{
  "Type": "discord",
  "Name": "My Discord Bot",
  "Settings": { "BotToken": "MTIzNDU2Nzg5..." }
}
```

### Behavior

- **Inbound:** The adapter listens for all non-bot messages. Each message is converted to a `ChannelMessage` with the Discord channel ID as `ChannelId`, user ID as `SenderId`, and thread ID (if applicable) as `ThreadId`. File attachments are included with lazy HTTP streaming.
- **Outbound:** `SendMessageAsync` takes a Discord channel ID (as a string) and posts the message to that channel.
- **Connection:** The adapter waits for the `Ready` event from Discord before `ConnectAsync` returns, ensuring the bot is fully online.

### Message flow

```
Discord Server → Discord.Net WebSocket → DiscordChannel.OnMessageReceivedAsync
    → ChannelMessage → MessageReceived event → Gateway Agent Router
```

---

## Signal

The Signal adapter bridges to [signal-cli](https://github.com/AsamK/signal-cli) via its JSON-RPC interface over stdin/stdout. Signal-cli runs as a child process managed by the adapter.

### Prerequisites

1. Install signal-cli: `brew install signal-cli` (macOS) or download from [GitHub releases](https://github.com/AsamK/signal-cli/releases)
2. Register a phone number: `signal-cli -a +1234567890 register`
3. Verify: `signal-cli -a +1234567890 verify CODE`

### Setup

```csharp
var signal = new SignalChannel(
    account: "+1234567890",
    signalCliPath: "/usr/local/bin/signal-cli"  // optional, defaults to "signal-cli"
);
registry.Register(signal);
await signal.ConnectAsync();
```

Or via gateway configuration:

```json
{
  "Type": "signal",
  "Name": "Signal Bot",
  "Settings": {
    "Account": "+1234567890",
    "SignalCliPath": "/usr/local/bin/signal-cli"
  }
}
```

### Behavior

- **Process management:** `ConnectAsync` spawns a `signal-cli -a {account} jsonRpc` process. The adapter reads JSON-RPC notifications from stdout and sends commands via stdin.
- **Inbound:** The adapter parses `receive` method notifications, extracting the envelope's `source` (phone number) as both `ChannelId` and `SenderId`, and the `dataMessage.message` field as `Content`.
- **Outbound:** `SendMessageAsync` writes a JSON-RPC `send` request to stdin with the recipient phone number and message content.
- **Disconnection:** `DisconnectAsync` cancels the read loop and kills the signal-cli process.

### Message flow

```
Signal Network → signal-cli (JSON-RPC stdout) → SignalChannel.ReadLoopAsync
    → ChannelMessage → MessageReceived event → Gateway Agent Router
```

---

## Slack

The Slack adapter uses [SlackNet](https://github.com/soxtoby/SlackNet) with Socket Mode for real-time event delivery without a public endpoint.

### Prerequisites

1. Create a Slack app at <https://api.slack.com/apps>
2. Enable **Socket Mode** and generate an **App-Level Token** (`xapp-...`)
3. Add a **Bot Token** (`xoxb-...`) with these OAuth scopes:
   - `chat:write` — send messages
   - `channels:history` — read channel messages
   - `groups:history` — read private channel messages
   - `im:history` — read DMs
4. Subscribe to the `message.channels`, `message.groups`, and `message.im` events
5. Install the app to your workspace

### Setup

```csharp
var slack = new SlackChannel(
    botToken: "xoxb-your-bot-token",
    appToken: "xapp-your-app-token"
);
registry.Register(slack);
await slack.ConnectAsync();
```

Or via gateway configuration:

```json
{
  "Type": "slack",
  "Name": "Slack Bot",
  "Settings": {
    "BotToken": "xoxb-...",
    "AppToken": "xapp-..."
  }
}
```

### Behavior

- **Inbound:** The adapter receives `MessageEvent` instances through the SlackNet event handler registration. Each event is mapped to a `ChannelMessage` with the Slack channel ID, user ID, message text, and thread timestamp (for threaded conversations).
- **Outbound:** `SendMessageAsync` posts a message to the specified Slack channel using the Web API's `chat.postMessage` method.
- **Thread support:** Slack's `ThreadTs` field maps to `ChannelMessage.ThreadId`, enabling threaded conversations with agents.

### Message flow

```
Slack Workspace → SlackNet Socket Mode → SlackChannel.HandleMessageAsync
    → ChannelMessage → MessageReceived event → Gateway Agent Router
```

---

## Telegram

The Telegram adapter uses the [Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot) SDK with long polling.

### Prerequisites

1. Create a bot via [BotFather](https://t.me/BotFather): send `/newbot` and follow the prompts
2. Copy the bot token
3. Optionally set bot commands via `/setcommands`

### Setup

```csharp
var telegram = new TelegramChannel(botToken: "123456:ABC-DEF...");
registry.Register(telegram);
await telegram.ConnectAsync();
```

Or via gateway configuration:

```json
{
  "Type": "telegram",
  "Name": "Telegram Bot",
  "Settings": { "BotToken": "123456:ABC-DEF..." }
}
```

### Behavior

- **Inbound:** The adapter uses `StartReceiving` with a filter for `UpdateType.Message`. Each text message is converted to a `ChannelMessage` with the Telegram chat ID as `ChannelId`, user ID as `SenderId`, display name from the user's first name, and `MessageThreadId` for topic-based groups.
- **Outbound:** `SendMessageAsync` sends a text message to the specified chat ID using `SendMessage`.
- **Group support:** The adapter works in private chats, groups, and supergroups. In topic-enabled supergroups, the thread ID is preserved.
- **Error handling:** Polling errors are logged to stderr and do not stop the adapter.

### Message flow

```
Telegram API → Telegram.Bot Polling → TelegramChannel.HandleUpdateAsync
    → ChannelMessage → MessageReceived event → Gateway Agent Router
```

---

## WebChat

The WebChat adapter bridges browser clients to the JD.AI gateway via SignalR. Unlike other adapters that connect to external services, WebChat is an internal adapter that wraps the gateway's own SignalR infrastructure.

### How it works

The WebChat adapter does not connect to any external service. Instead, it acts as a bridge between the gateway's SignalR hub and the `IChannel` abstraction:

1. A browser client connects to the gateway's AgentHub via SignalR
2. The hub calls `WebChannel.IngestMessageAsync` with the connection ID, user ID, and message content
3. The adapter raises `MessageReceived`, which routes the message to an agent
4. Agent responses are sent back through the SignalR hub directly (not through `SendMessageAsync`)

### Setup

```csharp
var web = new WebChannel();
registry.Register(web);
await web.ConnectAsync();  // Immediately ready — no external connection needed
```

### Browser client example

```html
<script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"></script>
<script>
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:18789/hubs/agent")
    .build();

async function sendMessage(agentId, text) {
    const stream = connection.stream("StreamChat", agentId, text);
    stream.subscribe({
        next: (chunk) => {
            if (chunk.type === "content") {
                document.getElementById("output").textContent += chunk.content;
            }
        },
        complete: () => console.log("Response complete"),
        error: (err) => console.error("Stream error:", err)
    });
}

connection.start().then(() => console.log("Connected"));
</script>
```

### Behavior

- **Inbound:** Messages are ingested programmatically via `IngestMessageAsync`, not from an external event stream.
- **Outbound:** `SendMessageAsync` stores messages in an in-memory conversation log. The actual delivery to the browser happens through the SignalR hub.
- **State:** The adapter maintains a `ConcurrentDictionary` of conversation histories keyed by connection ID.

---

## OpenClaw

The OpenClaw adapter bridges JD.AI to an [OpenClaw](https://github.com/openclaw/openclaw) gateway instance, enabling cross-platform multi-AI orchestration. See [OpenClaw Integration](openclaw-integration.md) for a comprehensive guide.

### Prerequisites

1. A running OpenClaw instance (default: `http://localhost:3000`)
2. An API key if the OpenClaw instance requires authentication

### Setup

Use the DI extension method for full configuration:

```csharp
services.AddOpenClawBridge(config =>
{
    config.BaseUrl = "http://localhost:3000";
    config.InstanceName = "production";
    config.ApiKey = "your-openclaw-api-key";
    config.TargetChannel = "jdai-outbound";
    config.SourceChannel = "jdai-inbound";
    config.PollIntervalMs = 1000;
});
```

Or via gateway configuration:

```json
{
  "Type": "openclaw",
  "Name": "OpenClaw Bridge",
  "Settings": {
    "BaseUrl": "http://localhost:3000",
    "InstanceName": "production",
    "ApiKey": "your-openclaw-api-key",
    "TargetChannel": "jdai-outbound",
    "SourceChannel": "jdai-inbound",
    "PollIntervalMs": "1000"
  }
}
```

### Configuration reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BaseUrl` | `string` | `http://localhost:3000` | OpenClaw HTTP API base URL |
| `InstanceName` | `string` | `local` | Friendly name (appears in `DisplayName`) |
| `ApiKey` | `string?` | `null` | Optional API key for OpenClaw authentication |
| `TargetChannel` | `string` | `default` | OpenClaw channel for outbound messages |
| `SourceChannel` | `string` | `default` | OpenClaw channel to poll for inbound messages |
| `PollIntervalMs` | `int` | `1000` | Polling interval in milliseconds |

### Behavior

- **Connection:** `ConnectAsync` verifies the OpenClaw instance is reachable via a health check (`GET /api/health`), then starts a background polling loop.
- **Inbound:** The adapter polls `GET /api/messages?since={timestamp}&channel={sourceChannel}` at the configured interval. Each message is converted to a `ChannelMessage` with the channel ID set to `openclaw-{instanceName}`.
- **Outbound:** `SendMessageAsync` posts an `OpenClawOutboundMessage` to `POST /api/messages` with metadata tagging the source as `jdai-gateway`.
- **Error resilience:** Poll failures are logged and trigger a 5-second backoff before retrying.

### Message flow

```
OpenClaw Gateway → HTTP Poll → OpenClawBridgeChannel.PollMessagesAsync
    → ChannelMessage → MessageReceived event → Gateway Agent Router

Gateway Agent → OpenClawBridgeChannel.SendMessageAsync → HTTP POST → OpenClaw Gateway
```

---

## Routing messages to agents

Once a channel delivers a `ChannelMessage`, the gateway routes it to an agent. A typical routing setup subscribes to the `MessageReceived` event:

```csharp
var discord = registry.GetChannel("discord")!;
var agentPool = app.Services.GetRequiredService<AgentPoolService>();

discord.MessageReceived += async (msg) =>
{
    // Route to a specific agent
    var response = await agentPool.SendMessageAsync("a3f8b2e1c4d7", msg.Content);
    if (response is not null)
    {
        await discord.SendMessageAsync(msg.ChannelId, response);
    }
};
```

## Writing a custom channel adapter

To add support for a new messaging platform:

1. Create a new class library referencing `JD.AI.Core`
2. Implement `IChannel`
3. Register the adapter with the channel registry

```csharp
public sealed class MyChannel : IChannel
{
    public string ChannelType => "my-platform";
    public string DisplayName => "My Platform";
    public bool IsConnected { get; private set; }

    public event Func<ChannelMessage, Task>? MessageReceived;

    public Task ConnectAsync(CancellationToken ct = default)
    {
        // Connect to your platform
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default)
    {
        // Send message to your platform
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
```

## Channel comparison

| Feature | Discord | Signal | Slack | Telegram | WebChat | OpenClaw |
|---------|:-------:|:------:|:-----:|:--------:|:-------:|:--------:|
| **Threads** | ✓ | — | ✓ | ✓ | — | — |
| **Attachments** | ✓ | — | — | — | — | — |
| **Group chat** | ✓ | ✓ | ✓ | ✓ | — | — |
| **DMs** | ✓ | ✓ | ✓ | ✓ | — | — |
| **External dependency** | Discord.Net | signal-cli | SlackNet | Telegram.Bot | None | HttpClient |
| **Transport** | WebSocket | Process I/O | Socket Mode | Long poll | In-process | HTTP poll |
| **Auth required** | Bot token | Phone number | Bot + App tokens | Bot token | None | Optional API key |

## See also

- [Gateway API Reference](gateway-api.md) — REST endpoints for managing channels
- [OpenClaw Integration](openclaw-integration.md) — cross-gateway orchestration with OpenClaw
- [Extending JD.AI](extending.md) — writing custom tools and providers
