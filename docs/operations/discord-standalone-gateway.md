# Standalone Discord Gateway

Run JD.AI as a dedicated Discord gateway without OpenClaw override mode. This supports multiple Discord bot instances in one JD.AI process, each routed to a different JD.AI agent.

## Goal

- Keep OpenClaw and Claude Code running independently.
- Run JD.AI as a third local gateway process.
- Attach one or more JD.AI Discord bots/channels with dedicated routing.

## 1) Prepare config

1. Copy `src/JD.AI.Gateway/appsettings.discord-standalone.example.json` to `src/JD.AI.Gateway/appsettings.Standalone.json`.
2. Keep `Gateway:OpenClaw:Enabled` set to `false`.
3. Ensure each Discord channel entry has a unique `Name` value.

Routing rules can now target specific channel instances using `ChannelName`:

```json
{ "ChannelType": "discord", "ChannelName": "support-discord", "AgentId": "support-agent" }
```

## 2) Set bot tokens

PowerShell example:

```powershell
$env:DISCORD_BOT_TOKEN_SUPPORT = "<support-bot-token>"
$env:DISCORD_BOT_TOKEN_LAB = "<lab-bot-token>"
```

## 3) Start dedicated gateway

From repo root:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Standalone"
$env:ASPNETCORE_URLS = "http://127.0.0.1:15795"
dotnet run --project src/JD.AI.Gateway
```

## 4) Validate runtime

In a second terminal:

```powershell
curl http://127.0.0.1:15795/api/channels
curl http://127.0.0.1:15795/api/routes
curl http://127.0.0.1:15795/api/gateway/status
```

Expected:

- both Discord channels show connected
- route table includes separate keys (for duplicate type channels) like:
  - `discord:support-discord`
  - `discord:lab-discord`
- OpenClaw status is disabled in this gateway instance

## 5) Coexistence checks

- OpenClaw gateway still runs on its own port/process and keeps its own agents/workspaces.
- JD.AI standalone gateway runs on `15795` and uses only its configured channels/tokens.
- No OpenClaw bridge registration is attempted when `Gateway:OpenClaw:Enabled` is `false`.
