# Settings > Infrastructure
>**Verified:** Gateway authentication required. Session params do not bypass WebSocket auth for admin panel.

**Route:** `/settings/infrastructure`  
**Nav Path:** Settings > Infrastructure  
**Description:** Configure server infrastructure, connection settings, resource limits, and deployment/scaling parameters.

## Status
**⚠️ Authentication Required** — This page requires gateway authentication via WebSocket connection before the UI content is accessible.

## Authentication Gate (Current State)

The Infrastructure settings page is protected by a login gate with the following fields:

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

- **Server configuration fields** — Server address, port, hostname, protocol settings
- **Connection settings** — Connection timeout, retry policies, pooling configuration
- **Resource limits** — Memory limits, CPU allocation, concurrent connection limits, request timeouts
- **Environment configuration** — Environment variables, runtime configuration, feature flags
- **Deployment/scale settings** — Number of instances, load balancer configuration, scaling policies, health check configuration

## Likely Sections

### Server Configuration
- Server address/hostname input
- Port configuration
- Protocol selection (HTTP/HTTPS)
- SSL/TLS certificate settings

### Connection & Network
- Connection timeout (seconds)
- Read/write timeout
- Max retries
- Retry backoff strategy
- Connection pool size

### Resource Limits
- Memory allocation (MB/GB)
- CPU cores
- Max concurrent connections
- Request queue size
- Session timeout

### Environment
- Environment variable input (key=value pairs)
- Runtime mode selection (development/staging/production)
- Debug logging toggle
- Custom configuration fields

### Deployment & Scaling
- Number of worker instances
- Load balancer configuration
- Auto-scaling policy (min/max instances)
- Health check endpoint
- Graceful shutdown timeout
- Blue-green deployment toggle

## Interactions (Estimated)

- Click WebSocket URL field → Enter/modify gateway connection string
- Click Gateway Token field → Enter optional token credential
- Click token visibility toggle → Reveal/mask token input
- Click password visibility toggle → Reveal/mask password input
- Click Connect button → Validate credentials and establish WebSocket connection
- (After auth) Modify server address → Validate hostname/IP format
- (After auth) Adjust resource limits → Show impact estimate or warning if approaching limits
- (After auth) Add environment variable → Open input for key and value
- (After auth) Toggle auto-scaling → Show/hide min/max instance controls
- (After auth) Click "Save" or "Apply Changes" → POST configuration to gateway
- (After auth) Click "Restart Services" → Trigger service restart with changes
- (After auth) Click "Test Connection" → Verify connection with new settings

## State / Data

- **Connection state:** Not connected (login gate showing) | Connected (authenticated content visible)
- **Infrastructure configuration:** Current server settings including:
  - Server endpoint
  - Connection parameters
  - Resource allocations
  - Environment variables
  - Deployment strategy
- **Server status:** Running/Stopped, current resource usage, uptime
- **Deployment state:** Current number of active instances, version running
- **Health status:** Last health check result, percentage healthy instances

## API / WebSocket Calls

- **WebSocket connection:** Initiated on Connect button submit to the URL specified in WebSocket URL input
- **Authentication payload:** Gateway token and password (if provided) transmitted on initial connection
- **Fetch infrastructure config:** GET or WS subscribe to current infrastructure settings after auth
- **Update server config:** POST/PUT to update server connection settings
- **Update resource limits:** POST/PUT to modify resource constraints
- **Update environment variables:** POST/PUT to set environment configuration
- **Restart services:** POST to trigger service restart with new configuration
- **Get server status:** GET or WS stream of current server health and resource metrics
- **Test connection:** POST to verify connectivity with proposed settings before applying

## Notes

- Infrastructure changes may require service restart (shown as confirmation dialog)
- Some settings may be read-only if managed by orchestration layer (Kubernetes, Docker Swarm)
- Resource limits are typically enforced at the OS/container level
- Changes to server endpoint may affect WebSocket connection itself — client may need to reconnect
- Environment variables should be validated before saving (no empty keys, special characters)
- Infrastructure page may show real-time metrics (memory, CPU, connections) via WebSocket stream
- Deployment configuration might support multiple strategies (rolling update, blue-green, canary)
