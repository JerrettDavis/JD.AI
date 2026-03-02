# Service Deployment

JD.AI Gateway can run as a native system service on both Windows and Linux, providing always-on AI assistant capabilities with automatic updates.

## Installation

### Prerequisites

- .NET 10.0 SDK or Runtime
- `jdai-daemon` dotnet tool installed globally

```bash
dotnet tool install -g JD.AI.Daemon
```

### Windows Service

Install the daemon as a Windows Service:

```powershell
# Install the service (requires Administrator)
jdai-daemon install

# Start the service
jdai-daemon start

# Check status
jdai-daemon status
```

This creates a Windows Service named **JDAIDaemon** with:
- **Automatic startup** — starts with Windows
- **Recovery policy** — restarts on failure (5s, 10s, 30s backoff)
- **Display name** — "JD.AI Gateway Daemon"

### Linux (systemd)

Install as a systemd unit:

```bash
# Install the service (requires sudo for /etc/systemd/system/)
sudo jdai-daemon install

# Start the service
sudo jdai-daemon start

# Check status
jdai-daemon status
```

The generated unit file (`/etc/systemd/system/jdai-daemon.service`) includes:
- **Type=notify** — proper systemd integration
- **Restart=on-failure** — automatic restart with 5-second delay
- **Security hardening** — `ProtectSystem=strict`, `PrivateTmp=true`, `NoNewPrivileges=true`
- **Network dependency** — waits for `network-online.target`

## CLI Reference

| Command | Description |
|---------|-------------|
| `jdai-daemon run` | Start the daemon in foreground (default) |
| `jdai-daemon install` | Install as Windows Service or systemd unit |
| `jdai-daemon uninstall` | Remove the system service |
| `jdai-daemon start` | Start the installed service |
| `jdai-daemon stop` | Stop the running service |
| `jdai-daemon status` | Show service state, version, uptime |
| `jdai-daemon update` | Check for and apply NuGet updates |
| `jdai-daemon update --check-only` | Check for updates without applying |
| `jdai-daemon logs [-n 50]` | Show recent service logs |

## Auto-Updates

The daemon includes a built-in update service that periodically checks NuGet for newer versions.

### Configuration

Add an `Updates` section to your `appsettings.json`:

```json
{
  "Updates": {
    "CheckInterval": "24:00:00",
    "AutoApply": false,
    "NotifyChannels": true,
    "PreRelease": false,
    "DrainTimeout": "00:00:30",
    "PackageId": "JD.AI.Daemon",
    "NuGetFeedUrl": "https://api.nuget.org/v3-flatcontainer/"
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `CheckInterval` | `24:00:00` | How often to check for updates |
| `AutoApply` | `false` | Automatically apply updates when found |
| `NotifyChannels` | `true` | Notify Discord/Slack/Signal when updates are available |
| `PreRelease` | `false` | Consider pre-release NuGet versions |
| `DrainTimeout` | `00:00:30` | Max wait time for in-flight requests during update |
| `PackageId` | `JD.AI.Daemon` | NuGet package to check |

### Update Flow

1. **Detection** — The update service checks NuGet on the configured interval
2. **Notification** — If `NotifyChannels` is true, connected channels receive an update alert
3. **Drain** — Active agents finish in-flight work (up to `DrainTimeout`)
4. **Apply** — Runs `dotnet tool update -g JD.AI.Daemon`
5. **Restart** — The service exits; Windows Service recovery / systemd restart brings it back on the new version

### Manual Update

```bash
# Check only
jdai-daemon update --check-only

# Check and apply
jdai-daemon update
```

### EventBus Events

The update service publishes events that channels and the dashboard can subscribe to:

| Event | Payload | When |
|-------|---------|------|
| `gateway.update.available` | `{ Current, Latest }` | New version detected |
| `gateway.update.draining` | `{ Update, DrainTimeout }` | Draining before update |
| `gateway.update.applying` | `{ Update }` | Running `dotnet tool update` |
| `gateway.update.restarting` | `{ Update }` | About to restart |

## Uninstalling

```bash
# Windows
jdai-daemon uninstall

# Linux
sudo jdai-daemon uninstall
```

This stops the service (if running), removes the Windows Service entry or systemd unit file, and reloads the daemon.

## Troubleshooting

### "Permission denied" on Linux

Service installation writes to `/etc/systemd/system/` which requires root. Use `sudo`:

```bash
sudo jdai-daemon install
```

### Service won't start

Check logs for configuration errors:

```bash
jdai-daemon logs -n 100
```

### Update fails

Ensure `dotnet` is on the service user's PATH and the NuGet feed is accessible:

```bash
jdai-daemon update --check-only
```

If behind a proxy, configure NuGet authentication via `nuget.config`.
