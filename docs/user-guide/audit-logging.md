---
description: "Audit logging for JD.AI: event schema, file, Elasticsearch, and webhook sinks, configuration reference, and log query examples."
---

# Audit logging

JD.AI's audit system records a structured event for every significant action the agent takes: tool invocations, policy denials, session lifecycle, and user-driven denials. Events are written to one or more configurable sinks — a local JSONL file by default — and each event carries a trace identifier that links it to a distributed tracing system if one is in use.

Audit logging serves compliance, incident investigation, and operational visibility. Because failures in the audit subsystem never propagate to callers, a sink outage cannot block the agent from running.

## Quick start

Enable the file sink by adding an `audit` section to any policy file:

```yaml
apiVersion: jdai/v1
kind: Policy
metadata:
  name: audit-enabled
  scope: Project
spec:
  audit:
    enabled: true
    sink: file
```

Audit files are written to `~/.jdai/audit/audit-{yyyy-MM-dd}.jsonl`. Each line is a self-contained JSON object. A sample entry from a successful `read_file` call:

```json
{
  "eventId": "4a9f1b3c8e2d47a6b5c0d9e1f2a3b4c5",
  "timestamp": "2026-03-03T14:22:07.341+00:00",
  "userId": null,
  "sessionId": "sess_abc123",
  "traceId": null,
  "action": "tool.invoke",
  "resource": "read_file",
  "detail": "status=ok; args=path=src/Auth/TokenService.cs",
  "severity": "Debug",
  "policyResult": "Allow"
}
```

A policy denial looks like this:

```json
{
  "eventId": "7d2e0a4f9b1c3e5a7d9f0b2c4e6a8b0c",
  "timestamp": "2026-03-03T14:23:01.882+00:00",
  "userId": null,
  "sessionId": "sess_abc123",
  "traceId": null,
  "action": "tool.invoke",
  "resource": "run_command",
  "detail": "status=denied; args=command=curl http://internal-api/reset",
  "severity": "Warning",
  "policyResult": "Deny"
}
```

## Audit events

The `AuditService` dispatches events emitted by `ToolConfirmationFilter` and session management. All events share the same `AuditEvent` schema.

### Event actions

| Action | When emitted | Typical severity |
|---|---|---|
| `tool.invoke` | Every tool call, regardless of outcome. `detail` contains `status=ok`, `status=denied`, or `status=user_denied` and the tool arguments. | `Debug` (ok), `Info` (user_denied), `Warning` (denied) |
| `session.create` | A new session is created. | `Info` |
| `session.close` | A session ends. `detail` includes turn count and token estimate. | `Info` |
| `policy.deny` | A provider or model evaluation returns `Deny`. `resource` contains the provider or model ID. | `Warning` |

> [!NOTE]
> `tool.invoke` is the most frequent event type. For high-volume deployments, consider filtering to `severity >= Warning` in your sink query to reduce storage costs while retaining all policy denials.

## Audit event schema

Every `AuditEvent` has the following fields:

| Field | Type | Nullable | Description |
|---|---|---|---|
| `eventId` | string | No | A unique 32-character hexadecimal identifier (`Guid.NewGuid().ToString("N")`). |
| `timestamp` | DateTimeOffset | No | UTC timestamp of the event. ISO 8601 format in JSON output. |
| `userId` | string | Yes | Identity of the user, when available. Populated by gateway authentication; null in CLI mode. |
| `sessionId` | string | Yes | The session identifier from `SessionInfo.Id`. Null before a session is established. |
| `traceId` | string | Yes | OpenTelemetry trace ID from the active activity, if present. See [OpenTelemetry correlation](#opentelemetry-correlation). |
| `action` | string | No | The event action string (e.g., `tool.invoke`, `session.create`). |
| `resource` | string | Yes | The primary resource affected: tool name, provider name, or model ID. |
| `detail` | string | Yes | Free-text detail string. For `tool.invoke`: `status={ok|denied|user_denied}; args={...}`. |
| `severity` | AuditSeverity | No | See [Severity levels](#severity-levels). |
| `policyResult` | PolicyDecision? | Yes | The policy evaluation outcome: `Allow`, `Deny`, `RequireApproval`, or `Audit`. Null when no policy was evaluated. |

### AuditSeverity enum

| Value | Numeric | When used |
|---|---|---|
| `Debug` | 0 | Routine successful tool invocations. |
| `Info` | 1 | Session lifecycle events and user-denied tool calls. |
| `Warning` | 2 | Policy denials and budget alert threshold crossings. |
| `Error` | 3 | Unexpected errors in tool execution or sink failures (reserved for future use). |
| `Critical` | 4 | Reserved for future use. |

### PolicyDecision enum

| Value | Description |
|---|---|
| `Allow` | Policy permitted the action. |
| `Deny` | Policy blocked the action. |
| `RequireApproval` | Policy deferred to user confirmation (reserved for future use). |
| `Audit` | Policy logged the action without blocking (reserved for future use). |

## Audit sinks

### File sink (default)

The `FileAuditSink` appends events as JSON lines to a daily-rotated file. No additional configuration is required beyond `sink: file`.

**File path pattern**: `~/.jdai/audit/audit-{yyyy-MM-dd}.jsonl`

Each `.jsonl` file contains one JSON object per line. The file is created on first write each day. Writes are serialized with a semaphore to ensure consistency under concurrent access.

```yaml
spec:
  audit:
    enabled: true
    sink: file
    # No further fields needed for the file sink.
```

| Property | Type | Default | Description |
|---|---|---|---|
| `enabled` | bool | false | Must be `true` to activate logging. |
| `sink` | string | `file` | Sink type. |

> [!TIP]
> The audit directory is created automatically on first write. You do not need to pre-create `~/.jdai/audit/`.

### Elasticsearch sink

The `ElasticsearchAuditSink` posts each event as a JSON document to `{endpoint}/{index}/_doc` via HTTP POST. Authentication uses a Bearer token if provided.

The `index` field supports a `{yyyy.MM}` date substitution that is resolved from the event timestamp, enabling time-based index rotation.

```yaml
spec:
  audit:
    enabled: true
    sink: elasticsearch
    endpoint: https://es.corp.example.com:9200
    index: jdai-audit-{yyyy.MM}
    token: eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
```

| Property | Type | Default | Description |
|---|---|---|---|
| `enabled` | bool | false | Must be `true` to activate logging. |
| `sink` | string | — | Must be `elasticsearch`. |
| `endpoint` | string | — | Base URL of the Elasticsearch cluster. Trailing slash is stripped. |
| `index` | string | — | Index name. `{yyyy.MM}` is substituted with the event's year and month. |
| `token` | string | null | Bearer token sent in the `Authorization` header. Omit for unauthenticated clusters. |

> [!WARNING]
> Store the Elasticsearch bearer token in a secrets manager rather than a policy YAML file checked into source control. Consider using `JDAI_ORG_CONFIG` to point to a policy directory that is not under version control.

### Webhook sink

The `WebhookAuditSink` posts each event as a JSON body to a configured URL via HTTP POST. The content type is `application/json`. Non-success HTTP responses are silently ignored.

```yaml
spec:
  audit:
    enabled: true
    sink: webhook
    url: https://siem.corp.example.com/api/ingest/jdai
```

| Property | Type | Default | Description |
|---|---|---|---|
| `enabled` | bool | false | Must be `true` to activate logging. |
| `sink` | string | — | Must be `webhook`. |
| `url` | string | — | The full URL to POST events to. |

> [!NOTE]
> The webhook sink does not support custom request headers in the current implementation. If your endpoint requires additional authentication headers, consider placing a reverse proxy in front of it or using the Elasticsearch sink with a compatible ingestion endpoint.

### Sink failure behavior

All sinks catch exceptions internally and swallow them. A sink that is unreachable or returns an error does not block tool execution or propagate an exception to the agent loop. This is by design: audit failures must not disrupt the user's workflow.

If you require guaranteed delivery, use the file sink as a primary sink and forward from the JSONL files to your SIEM using a log shipper (Filebeat, Fluent Bit, etc.).

## Configuration

The `audit` section of a policy file is the only configuration surface for the audit system. The resolved policy's audit settings are determined by the scope merge rules:

- `enabled` is `true` if **any** policy in the hierarchy has `enabled: true`.
- All other fields (`sink`, `endpoint`, `index`, `token`, `url`) are taken from the **most specific** policy (the last one in scope/priority order).

### Full configuration examples

**File sink with audit enabled at organization scope:**

```yaml
apiVersion: jdai/v1
kind: Policy
metadata:
  name: org-audit
  scope: Organization
spec:
  audit:
    enabled: true
    sink: file
```

**Elasticsearch sink at project scope (overrides org sink settings):**

```yaml
apiVersion: jdai/v1
kind: Policy
metadata:
  name: project-audit
  scope: Project
spec:
  audit:
    enabled: true
    sink: elasticsearch
    endpoint: https://es.internal:9200
    index: jdai-{yyyy.MM}
    token: Bearer_token_here
```

**Webhook sink for real-time SIEM integration:**

```yaml
apiVersion: jdai/v1
kind: Policy
metadata:
  name: siem-sink
  scope: Organization
spec:
  audit:
    enabled: true
    sink: webhook
    url: https://splunk.corp.example.com:8088/services/collector
```

## Querying audit logs

### File sink: jq examples

The JSONL format is directly queryable with `jq`.

**All policy denials from today:**

```bash
jq 'select(.severity == "Warning" and .action == "tool.invoke")' \
  ~/.jdai/audit/audit-$(date +%Y-%m-%d).jsonl
```

**All events for a specific session:**

```bash
jq 'select(.sessionId == "sess_abc123")' \
  ~/.jdai/audit/audit-2026-03-03.jsonl
```

**Count tool invocations by tool name:**

```bash
jq -r '.resource' ~/.jdai/audit/audit-2026-03-03.jsonl \
  | sort | uniq -c | sort -rn
```

**Events where the user denied a tool call:**

```bash
jq 'select(.detail | test("status=user_denied"))' \
  ~/.jdai/audit/audit-2026-03-03.jsonl
```

**All events in the last 7 days (bash loop):**

```bash
for i in $(seq 0 6); do
  date_str=$(date -d "-$i days" +%Y-%m-%d 2>/dev/null || date -v"-${i}d" +%Y-%m-%d)
  file="$HOME/.jdai/audit/audit-${date_str}.jsonl"
  [ -f "$file" ] && cat "$file"
done | jq 'select(.severity != "Debug")'
```

### Elasticsearch: curl and Kibana examples

**Search for all policy denials in the current month's index:**

```bash
curl -H "Authorization: Bearer $ES_TOKEN" \
  -H "Content-Type: application/json" \
  "https://es.corp.example.com:9200/jdai-audit-2026.03/_search" \
  -d '{
    "query": {
      "bool": {
        "must": [
          { "term": { "severity": "Warning" } },
          { "term": { "policyResult": "Deny" } }
        ]
      }
    },
    "sort": [{ "timestamp": "desc" }],
    "size": 50
  }'
```

**Kibana KQL query for all denied tool calls in the last 24 hours:**

```text
action: "tool.invoke" AND policyResult: "Deny" AND @timestamp > now-24h
```

**Kibana KQL query for a specific session:**

```text
sessionId: "sess_abc123"
```

## Severity levels

The severity of an event reflects how significant the action is from a governance perspective:

| Severity | Events | Notes |
|---|---|---|
| `Debug` | Successful tool invocations (`status=ok`). | High volume in normal use. Consider filtering in production sinks. |
| `Info` | Session create/close events, user-denied tool calls (`status=user_denied`). | Low volume. Useful for session-level forensics. |
| `Warning` | Policy denials (`status=denied`). Budget alert thresholds crossed. | Should be reviewed routinely. Indicates policy enforcement activity. |
| `Error` | (Reserved) Unexpected internal errors. | Not currently emitted by the production code path. |
| `Critical` | (Reserved) | Not currently emitted. |

## OpenTelemetry correlation

Each `AuditEvent` carries a `traceId` field. When JD.AI is running inside a distributed trace (for example, when hosted as a service behind a gateway that injects OpenTelemetry context), the `traceId` from the current `System.Diagnostics.Activity` is included in every audit event emitted during that activity's scope.

This allows you to correlate JD.AI audit events with spans from other services in your observability platform:

```json
{
  "eventId": "9c1a0e2f3b4d5c6e7f8a9b0c1d2e3f4a",
  "timestamp": "2026-03-03T15:01:44.002+00:00",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "action": "tool.invoke",
  "resource": "write_file",
  "severity": "Debug",
  "policyResult": "Allow"
}
```

In a standalone CLI session where no distributed trace is active, `traceId` is null.

## Retention and rotation

The file sink rotates by calendar day — a new `.jsonl` file is created for each UTC date. There is no automatic deletion of old audit files. To enforce a retention policy, schedule a cleanup job:

**Linux/macOS — delete files older than 90 days:**

```bash
find ~/.jdai/audit -name 'audit-*.jsonl' -mtime +90 -delete
```

**Windows PowerShell:**

```powershell
Get-ChildItem "$env:USERPROFILE\.jdai\audit" -Filter 'audit-*.jsonl' |
  Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-90) } |
  Remove-Item
```

For the Elasticsearch sink, use index lifecycle management (ILM) policies to delete or archive old indices automatically.

## Troubleshooting

### No audit files are created

1. Verify that `audit.enabled` is `true` in at least one policy file in the hierarchy.
2. Confirm the policy file is in a scanned directory: `~/.jdai/policies/` or `{project}/.jdai/policies/`.
3. Check that the policy YAML parses correctly. JD.AI silently skips files that fail to parse. Test your file manually:

   ```bash
   # Validate by watching for parse errors at startup, or use a YAML linter
   yamllint ~/.jdai/policies/audit.yaml
   ```

4. Confirm that the `~/.jdai/audit/` directory is writable by the user running JD.AI.

### Elasticsearch sink is not receiving events

1. Verify the `endpoint` URL is reachable from the machine running JD.AI:

   ```bash
   curl -I https://es.corp.example.com:9200
   ```

2. Confirm the bearer token has write permission to the target index.
3. Check that the `index` template is valid. If `{yyyy.MM}` is not substituted correctly, verify the index name does not contain characters that Elasticsearch rejects (spaces, `\`, `/`, `*`, `?`, `"`, `<`, `>`, `|`, `,`).
4. Because the Elasticsearch sink swallows HTTP errors silently, test connectivity by temporarily pointing `url` at a request bin (e.g., `https://httpbin.org/post`) using the webhook sink to confirm events are being emitted.

### Audit events are missing the userId field

The `userId` field is populated only when JD.AI is running in gateway mode with API key authentication. In standalone CLI mode, there is no authenticated identity, so `userId` is always null. Use `sessionId` and `traceId` for session-level correlation in CLI scenarios.

### Policy denials appear in the audit log but the tool still ran

This should not happen. If you observe it, check that the `IPolicyEvaluator` is wired up. In the CLI, the `ToolConfirmationFilter` is constructed with the `policyEvaluator` argument only when a policy is resolved at startup. Confirm that at least one policy file with a `tools.denied` entry is being loaded by checking the startup output for policy load errors.

## See also

- [Policy Engine and Permissions](governance.md) — Configuring policy files, scope hierarchy, and tool restrictions.
- [Configuration](configuration.md) — Data directories, environment variables, and the `~/.jdai/` structure.
