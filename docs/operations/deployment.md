---
title: Service Deployment
description: "Install and run JD.AI Gateway as a Windows Service, systemd unit, or Docker container with auto-updates and reverse proxy support."
---

# Service Deployment

JD.AI Gateway can run as a native system service on both Windows and Linux, or as a Docker container, providing always-on AI assistant capabilities with automatic updates and health monitoring.

![Daemon and CLI service workflow](../images/demo-cli.png)

## Prerequisites

- .NET 10.0 SDK or Runtime
- `jdai-daemon` dotnet tool installed globally

```bash
dotnet tool install -g JD.AI.Daemon
```

## Windows Service

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
- **OpenClaw gateway task** — scheduled task `\OpenClaw Gateway` (runs as `SYSTEM` at startup)
- **OpenClaw watchdog task** — scheduled task `\OpenClaw Gateway Watchdog` (runs every minute)
- **OpenClaw launcher scripts** — `~/.openclaw/gateway-service.cmd` and `~/.openclaw/gateway-watchdog.cmd`

`jdai-daemon install` is idempotent on Windows: running it again updates the existing service definition and re-applies scheduled task configuration.

## Linux (systemd)

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

## Docker / Container Deployment

Run JD.AI Gateway in a container for reproducible deployments:

```bash
# Run with default settings
docker run -d \
  --name jdai-gateway \
  -p 18789:18789 \
  -v jdai-data:/root/.jdai \
  jdai/gateway:latest

# Run with custom configuration
docker run -d \
  --name jdai-gateway \
  -p 18789:18789 \
  -v jdai-data:/root/.jdai \
  -v ./appsettings.json:/app/appsettings.json:ro \
  -e OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317 \
  jdai/gateway:latest
```

Mount the `~/.jdai` directory as a volume to persist sessions, credentials, and local models across container restarts.

Container build definitions are versioned in this repository:

- `deploy/docker/Dockerfile.gateway`
- `deploy/docker/Dockerfile.daemon`
- `deploy/docker/Dockerfile.tui`

For Kubernetes + Helm deployment, see [Kubernetes Deployment](kubernetes.md).

## Reverse Proxy Configuration

### Nginx

```nginx
server {
    listen 443 ssl;
    server_name jdai.example.com;

    ssl_certificate     /etc/ssl/certs/jdai.crt;
    ssl_certificate_key /etc/ssl/private/jdai.key;

    location / {
        proxy_pass http://localhost:18789;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # WebSocket support for SignalR
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

### Caddy

```text
jdai.example.com {
    reverse_proxy localhost:18789
}
```

Caddy automatically provisions TLS certificates and handles WebSocket upgrades.

## Daemon CLI Reference

| Command | Description |
|---------|-------------|
| `jdai-daemon run` | Start the daemon in foreground (default) |
| `jdai-daemon install` | Install as Windows Service or systemd unit |
| `jdai-daemon uninstall` | Remove the system service |
| `jdai-daemon start` | Start the installed service |
| `jdai-daemon stop` | Stop the running service |
| `jdai-daemon restart` | Restart the installed service |
| `jdai-daemon status` | Show service state, version, uptime |
| `jdai-daemon update` | Check/apply NuGet updates and refresh installed service/task config |
| `jdai-daemon update --check-only` | Check for updates without applying |
| `jdai-daemon logs [-n 50]` | Show recent service logs |
| `jdai-daemon bridge status` | Show OpenClaw bridge enablement and override state |
| `jdai-daemon bridge enable` | Enable OpenClaw bridge and auto-connect |
| `jdai-daemon bridge disable` | Disable OpenClaw bridge and auto-connect |
| `jdai-daemon bridge passthrough` | Keep bridge enabled but force passthrough mode on default + channels |

### OpenClaw bridge emergency controls

When OpenClaw traffic is being overridden by JD.AI (`Intercept`, `Proxy`, or `Sidecar`), use:

```bash
jdai-daemon bridge status
```

To immediately stop bridge routing entirely:

```bash
jdai-daemon bridge disable
```

To keep diagnostics/visibility but disable response hijacking:

```bash
jdai-daemon bridge passthrough
```

## Auto-Updates

The daemon includes a built-in update service that periodically checks NuGet for newer versions.

### Update configuration

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
| `NotifyChannels` | `true` | Notify connected channels when updates are available |
| `PreRelease` | `false` | Consider pre-release NuGet versions |
| `DrainTimeout` | `00:00:30` | Max wait time for in-flight requests during update |

### Update flow

1. **Detection** — The update service checks NuGet on the configured interval
2. **Notification** — If `NotifyChannels` is true, connected channels receive an update alert
3. **Drain** — Active agents finish in-flight work (up to `DrainTimeout`)
4. **Apply** — Runs `dotnet tool update -g JD.AI.Daemon`
5. **Reconcile** — If the daemon service is installed, `jdai-daemon update` re-applies service/task definitions
6. **Restart** — The service exits; Windows Service recovery or systemd restart brings it back on the new version

### Manual update

```bash
# Check only
jdai-daemon update --check-only

# Check and apply
jdai-daemon update
```

## Health Check Endpoints

The gateway exposes health endpoints for load balancers and orchestrators:

| Endpoint | Description | Status codes |
|----------|-------------|--------------|
| `GET /health` | Full JSON health report | `200` always |
| `GET /health/ready` | Readiness probe | `200` / `503` |
| `GET /health/live` | Liveness probe | `200` always |

```bash
curl http://localhost:18789/health
```

See [Observability](observability.md) for full health check configuration and Kubernetes probe examples.

## Uninstalling

```bash
# Windows
jdai-daemon uninstall

# Linux
sudo jdai-daemon uninstall
```

This stops the service (if running), removes the Windows Service entry or systemd unit file, and reloads the daemon.

On Windows, uninstall also removes:

- Scheduled tasks `\OpenClaw Gateway` and `\OpenClaw Gateway Watchdog`
- `~/.openclaw/gateway-service.cmd`
- `~/.openclaw/gateway-watchdog.cmd`

## Troubleshooting

### "Permission denied" on Linux

Service installation writes to `/etc/systemd/system/` which requires root:

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

## See also

- [Observability](observability.md) — monitoring and health checks
- [Gateway Administration](gateway-admin.md) — scaling and configuration management
- [Security](security.md) — securing the gateway in production
