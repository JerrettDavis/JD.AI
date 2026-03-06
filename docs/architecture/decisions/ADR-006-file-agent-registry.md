# ADR-006: FileAgentDefinitionRegistry — Versioned, Environment-Scoped Agent Storage

## Status

Accepted

## Context

Issue #211 extends the `AgentDefinition` format introduced in #201 with a registry
that supports:

1. **Versioning** — multiple versions of an agent coexist; `latest` resolves the
   highest semantic version.
2. **Environment scopes** — `dev`, `staging`, `prod` form a promotion pipeline.
3. **Integrity** — a SHA-256 checksum stored alongside each YAML prevents silent
   corruption or tampering.
4. **CLI operations** — list, tag, promote, remove.

The existing in-memory `AgentDefinitionRegistry` supports fast synchronous lookups
but has no persistence or promotion mechanics.

## Decision

### Directory Layout

```text
~/.jdai/agents/
  dev/
    pr-reviewer@1.0.agent.yaml
    pr-reviewer@1.0.sha256
    pr-reviewer@2.0.agent.yaml
    pr-reviewer@2.0.sha256
  staging/
    pr-reviewer@1.0.agent.yaml
    pr-reviewer@1.0.sha256
  prod/
```

Files are named `{name}@{version}.agent.yaml`.
Non-alphanumeric characters other than `.` and `-` are replaced with `_` in
the file name.

### Versioning

- Multiple versions of the same agent name may coexist within an environment.
- `ResolveAsync(name, "latest")` or `ResolveAsync(name, null)` returns the
  entry with the highest `System.Version` (after normalizing single-segment
  versions to `n.0`).
- Exact version strings are matched case-insensitively.

### Environment Promotion

`PromoteAsync(name, version, from, to)` copies (re-registers) the definition
into the target environment. The source definition is preserved — promotion is
non-destructive.

The promotion chain `dev → staging → prod` is encoded in `AgentEnvironments.NextAfter()`.

### Checksum Verification

On `RegisterAsync`:
1. The YAML content is serialized.
2. `SHA256.HashData()` computes a hex checksum.
3. The checksum is written to `{name}@{version}.sha256`.

On `LoadAndVerifyAsync` (called by `ListAsync`):
1. If a companion `.sha256` file exists, the stored and computed checksums are
   compared.
2. Mismatch → the file is silently skipped (not surfaced to callers).
3. Missing `.sha256` → file is loaded without verification (backwards compat
   with hand-authored files).

### Interface Design

`FileAgentDefinitionRegistry` implements both:

| Interface | Used by |
|-----------|---------|
| `IAgentDefinitionRegistry` | Synchronous in-session lookups (existing callers) |
| `IVersionedAgentDefinitionRegistry` | CLI, governance, promotion workflows |

The sync `IAgentDefinitionRegistry` methods delegate to an internal
`AgentDefinitionRegistry` (in-memory, ConcurrentDictionary) that is pre-warmed
at construction time by reading all `dev/` definitions asynchronously.

### CLI Commands

```text
jdai agents list [--env dev|staging|prod] [-v]
jdai agents tag <name> <version> [--env <env>]
jdai agents promote <name> [<version>] [--from <env>] [--to <env>]
jdai agents remove <name> <version> [--env <env>]
```

## Consequences

### Positive

- Agents are durable across sessions.
- Multiple versions coexist without conflict.
- Environment promotion mirrors standard infrastructure promotion workflows.
- Integrity checks prevent loading tampered definitions.
- Backwards compatible: hand-authored YAML files (no `.sha256`) still load.

### Negative / Trade-offs

- `ListAsync` performs file I/O on every call — no cache invalidation or
  file-system watcher. For large registries (>100 agents), callers should cache.
- The in-memory sync cache is pre-warmed from `dev/` only.  Definitions in other
  environments require async resolution.
- SHA-256 covers the serialized YAML content only.  Agent Name/Version fields
  could be altered by re-serializing from a tampered object.  Deep validation
  (e.g., signing) is deferred to a future issue.

## Related

- ADR-002: Tool Safety Tiers
- ADR-003: RBAC Policy Evaluation
- ADR-005: IApprovalService
- Issue #201 — Agent Definition Format (`.agent.yaml` schema)
- Issue #211 — Agent Registry (this ADR)
