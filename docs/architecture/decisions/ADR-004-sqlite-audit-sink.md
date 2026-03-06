# ADR-004: SQLite Queryable Audit Sink

**Status**: Accepted  
**Date**: 2025  
**Closes**: GitHub issue #206 (partial — SQLite sink)

---

## Context

The existing `AuditService` dispatches events to four sinks:
- `FileAuditSink` — append-only JSONL file
- `ElasticsearchAuditSink` — Elasticsearch index
- `WebhookAuditSink` — HTTP POST
- `InMemoryAuditSink` — in-process list (testing)

None of these sinks implement `IQueryableAuditSink`, so there is no way to
query audit events programmatically (e.g., "show me all ToolCall events from
session X in the last hour"). For developer and compliance workflows, a local
queryable audit store is essential.

SQLite was chosen over PostgreSQL for the initial implementation because:
- No external dependency (Microsoft.Data.Sqlite is already referenced)
- Works on all platforms including local developer machines
- Supports all required query filters via SQL
- WAL mode provides adequate write throughput for audit volumes

---

## Decision

### `SqliteAuditSink` implements `IQueryableAuditSink`

The sink stores audit events in a SQLite database with:

**Schema:**
```sql
CREATE TABLE audit_events (
    rowid        INTEGER PRIMARY KEY AUTOINCREMENT,
    event_id     TEXT    NOT NULL UNIQUE,
    timestamp    TEXT    NOT NULL,   -- ISO 8601
    user_id      TEXT,
    session_id   TEXT,
    trace_id     TEXT,
    action       TEXT    NOT NULL,
    resource     TEXT,
    detail       TEXT,
    severity     INTEGER NOT NULL,   -- enum ordinal
    policy_result TEXT,
    tool_name    TEXT,
    tool_args    TEXT,
    tool_result  TEXT,
    duration_ms  INTEGER,
    previous_hash TEXT,
    payload      TEXT    NOT NULL    -- full JSON
);
```

**Indexes:** timestamp (DESC), session_id, user_id, action (NOCASE), severity

**Design choices:**
- `event_id UNIQUE` — duplicate events are silently ignored (idempotent writes)
- Full JSON payload stored alongside indexed columns to support deserializing
  complete `AuditEvent` objects from queries
- WAL + NORMAL synchronous mode: reduces fsync overhead while maintaining
  crash-consistent writes for non-critical local audit storage
- `IAsyncDisposable` implemented but no-op: SQLite connections are opened and
  closed per-operation to avoid connection pool issues in async contexts

### SQL injection prevention

`BuildWhereClause` generates only parameterized conditions. `LIMIT` and `OFFSET`
are passed as sanitized integers (Math.Min/Max bounds), which cannot contain
injection. CA2100 suppressions are documented at each site.

### DI registration

`SqliteAuditSink` should be registered as a named `IAuditSink` option when
`AuditPolicy.Sink == "sqlite"`. The `ConnectionString` or `DataSource` path
from `AuditPolicy` provides the database file location.

---

## Consequences

### Positive

- Developers get a zero-infrastructure queryable audit trail
- `IQueryableAuditSink` can be used by a future CLI `jdai audit query` command
- Tamper-evident chaining (PreviousHash) is preserved in the payload JSON
- All existing sinks continue to work unchanged

### Negative / Trade-offs

- WAL mode + shared cache requires SQLite >= 3.7.0 (guaranteed by NuGet package)
- Not suitable for high-throughput multi-process concurrent writes (use Elasticsearch for that)
- No built-in retention/pruning — callers must manage database size

### Future work

- PostgreSQL `IQueryableAuditSink` using Npgsql for production deployments
- `jdai audit query` CLI command using `IQueryableAuditSink`
- `JdAiWorkflowAuditBridge` to forward WorkflowFramework audit events to `AuditService`
- Automatic database pruning based on `SessionPolicy.RetentionDays`
