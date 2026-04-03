---
title: Gateway Administration
description: "Administer the JD.AI Gateway at scale — configuration management, horizontal scaling, channel and agent management, SignalR diagnostics, and backup procedures."
---

# Gateway Administration

This guide covers day-to-day administration of the JD.AI Gateway for operations teams. It addresses configuration management, scaling, channel and agent pool management, monitoring, and backup procedures.

## Configuration

### Configuration files

The gateway reads configuration from `appsettings.json` in the application directory. Environment-specific overrides are loaded automatically:

```text
appsettings.json                  # Base configuration
appsettings.Development.json      # Development overrides
appsettings.Production.json       # Production overrides
```

### Hot reload

Configuration changes to `appsettings.json` are detected and applied without restarting the gateway. Supported hot-reload settings include:

- Rate limiting thresholds
- Channel adapter settings
- Provider configurations
- Telemetry exporter settings

> [!NOTE]
> Changes to `Server.Host`, `Server.Port`, and `Auth.ApiKeys` require a gateway restart.

### Environment-specific settings

Use environment variables or per-environment config files for sensitive or environment-specific values:

```bash
# Production environment
export ASPNETCORE_ENVIRONMENT=Production
export Gateway__Auth__Enabled=true
export OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317

jdai-daemon run
```

The ASP.NET Core configuration system supports `__` (double underscore) as a hierarchy separator in environment variables.

## Scaling

### Horizontal scaling

JD.AI Gateway instances are stateless for REST API requests but maintain in-memory state for active agents and SignalR connections. For horizontal scaling:

- **Load balancer** — place multiple gateway instances behind a load balancer
- **Session affinity** — enable sticky sessions to route SignalR WebSocket connections to the same instance
- **Shared storage** — point all instances at a shared `sessions.db` location or use separate databases per instance

### Load balancer configuration

Example Nginx upstream configuration:

```nginx
upstream jdai_cluster {
    ip_hash;  # Session affinity
    server gateway-1:18789;
    server gateway-2:18789;
    server gateway-3:18789;
}

server {
    listen 443 ssl;
    server_name jdai.example.com;

    location / {
        proxy_pass http://jdai_cluster;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
}
```

### Session affinity

SignalR connections require session affinity (sticky sessions) to maintain WebSocket state. Configure your load balancer to route based on:

- Client IP (`ip_hash` in Nginx)
- Cookie-based affinity
- Connection ID header

## Channel Management

### Enabling and disabling channels

Control channels via the REST API or dashboard:

```bash
# List all channels
curl http://localhost:18789/api/channels

# Connect a channel
curl -X POST http://localhost:18789/api/channels/discord/connect

# Disconnect a channel
curl -X POST http://localhost:18789/api/channels/discord/disconnect
```

### Channel health

Monitor channel connection status through the `/api/channels` endpoint. Each channel reports:

```json
[
  { "channelType": "discord", "displayName": "Discord", "isConnected": true },
  { "channelType": "slack", "displayName": "Slack", "isConnected": false },
  { "channelType": "web", "displayName": "WebChat", "isConnected": true }
]
```

### Reconnection behavior

Channels automatically reconnect on transient failures. The reconnection strategy varies by channel type:

| Channel | Reconnection | Backoff |
|---------|-------------|---------|
| Discord | Automatic (Discord.Net) | Exponential |
| Signal | Automatic (JSON-RPC) | Fixed 5s |
| Slack | Automatic (Socket Mode) | Exponential |
| Telegram | Automatic (Long polling) | Fixed 3s |
| WebChat | Automatic (SignalR) | Exponential |

## Agent Management

### Agent pool configuration

Configure agent pool limits in `appsettings.json`:

```json
{
  "Gateway": {
    "Agents": {
      "MaxConcurrentAgents": 10,
      "DefaultProvider": "claude-code",
      "DefaultModel": "claude-sonnet-4-20250514",
      "IdleTimeout": "01:00:00",
      "TurnTimeout": "00:05:00"
    }
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `MaxConcurrentAgents` | `10` | Maximum simultaneous agent instances |
| `DefaultProvider` | First available | Provider for new agents when none specified |
| `DefaultModel` | Provider default | Model for new agents when none specified |
| `IdleTimeout` | `01:00:00` | Time before idle agents are automatically stopped |
| `TurnTimeout` | `00:05:00` | Maximum time for a single agent turn |

### Resource limits

Monitor agent resource usage through the REST API:

```bash
# List all active agents with resource info
curl http://localhost:18789/api/agents
```

Each agent reports turn count, creation time, provider, and model. Use the `MaxConcurrentAgents` setting to prevent resource exhaustion.

### Agent lifecycle

Agents follow this lifecycle:

1. **Spawn** — created via REST API, dashboard, or channel message
2. **Active** — processing turns and maintaining conversation state
3. **Idle** — no activity for the configured timeout period
4. **Stopped** — explicitly stopped or auto-removed after idle timeout

## Monitoring

### SignalR hub diagnostics

Monitor SignalR hub connections through the Event Hub:

```bash
# Stream all gateway events
curl -N http://localhost:18789/hubs/events
```

Key events to monitor:

| Event | Description |
|-------|-------------|
| `agent.spawned` | New agent instance created |
| `agent.turn_complete` | Agent completed a conversational turn |
| `agent.stopped` | Agent was stopped and removed |
| `channel.connected` | Channel adapter connected |
| `channel.disconnected` | Channel adapter disconnected |
| `gateway.update.available` | New version detected |

### Connection tracking

The dashboard shows active SignalR connections in real-time. For programmatic monitoring, use the health endpoint:

```bash
curl http://localhost:18789/health
```

The `gateway` health check reports `activeAgents` and `uptime`. The `providers` check reports reachable and unreachable providers.

### REST API health

Combine health endpoints with your monitoring system:

```bash
# Readiness check (returns 503 when unhealthy)
curl -f http://localhost:18789/health/ready

# Liveness check (always 200 while running)
curl -f http://localhost:18789/health/live
```

See [Observability](observability.md) for full health check configuration and alerting integration.

## Backup & Recovery

### Session database backup

The session database is a single SQLite file at `~/.jdai/sessions.db`. Back it up with standard file copy:

```bash
# Stop the gateway or use SQLite online backup
sqlite3 ~/.jdai/sessions.db ".backup /backup/sessions-$(date +%Y%m%d).db"

# Or simple file copy (safe when gateway is stopped)
cp ~/.jdai/sessions.db /backup/sessions-$(date +%Y%m%d).db
```

### Configuration backup

Back up the complete configuration:

```bash
# Back up all JD.AI state
tar czf jdai-backup-$(date +%Y%m%d).tar.gz \
  ~/.jdai/config.json \
  ~/.jdai/credentials/ \
  /etc/jdai/appsettings.json
```

> [!IMPORTANT]
> The `credentials/` directory contains encrypted secrets. Store backups securely and restrict access.

### Disaster recovery

To restore from backup:

1. Install JD.AI Gateway and daemon tools
2. Restore the `~/.jdai/` directory from backup
3. Restore `appsettings.json` to the application directory
4. Start the gateway: `jdai-daemon start`
5. Verify health: `curl http://localhost:18789/health`

```bash
# Full restore procedure
dotnet tool install -g JD.AI.Daemon
tar xzf jdai-backup-20250115.tar.gz -C /
cp /backup/sessions-20250115.db ~/.jdai/sessions.db
jdai-daemon start
jdai-daemon restart
jdai-daemon status
```

### Scheduled backups

Automate backups with cron (Linux) or Task Scheduler (Windows):

```bash
# crontab entry — daily backup at 2 AM
0 2 * * * sqlite3 ~/.jdai/sessions.db ".backup /backup/sessions-$(date +\%Y\%m\%d).db"
```

## See also

- [Deployment](deployment.md) — service installation and auto-updates
- [Observability](observability.md) — telemetry and health checks
- [Security](security.md) — authentication and credential management
- [Dashboard](dashboard.md) — web-based administration UI
