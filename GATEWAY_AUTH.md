# OpenClaw Gateway Authentication

## Token Acquisition

The OpenClaw dashboard token is **dynamically generated** at runtime using:

```bash
openclaw dashboard
```

This command:
1. Starts the gateway (if not already running)
2. Generates a fresh tokenized dashboard URL
3. Copies URL to clipboard
4. Opens in default browser

Output format:
```
Dashboard URL: http://127.0.0.1:18789/#token=<TOKEN>
```

**Token is not stored in configuration files** — it's generated fresh on each invocation for security.

## Usage in Automation

To programmatically authenticate:

```bash
# Get tokenized URL and extract token
TOKEN=$(openclaw dashboard 2>&1 | grep "token=" | sed 's/.*token=//' | tr -d '\n')

# Use token in web requests or Playwright
curl "http://127.0.0.1:18789/api/endpoint#token=$TOKEN"
```

Or in Playwright:
```javascript
const token = 'a878da53d4ec7e4f045d8a10a4fb9474c7e4bfa6247bcd5c';
await page.goto(`http://127.0.0.1:18789/control/usage#token=${token}`);
```

## Dashboard Routes

All authenticated routes support the `#token=<TOKEN>` URL fragment:

- `/` — Chat (main page)
- `/control/overview` — Control Overview
- `/control/channels` — Channels
- `/control/instances` — Instances
- `/control/sessions` — Sessions
- `/control/usage` — Usage Analytics
- `/cron` — Cron Jobs
- `/agent/agents` — Agents
- `/agent/skills` — Skills
- `/agent/nodes` — Nodes
- `/agent/dreaming` — Dreaming
- `/settings/*` — Settings pages

## Notes

- Token is valid for the current session
- Token persists across page navigation within the same tab
- Dashboard expects token in URL fragment (not query parameter)
- Gateway must be running at `127.0.0.1:18789` (default port)
