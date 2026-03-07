# ADR-003: Role-Based Access Control (RBAC) in PolicyEvaluator

**Status**: Accepted  
**Date**: 2025  
**Closes**: GitHub issue #203

---

## Context

The existing `PolicyEvaluator` supports tool, provider, model, and workflow publish
evaluation based on allowed/denied lists in `PolicySpec`. However, `PolicyContext`
only carried `UserId`, `ProjectPath`, `ProviderName`, and `ModelId`.

This meant all users were subject to the same policy regardless of role. Organizations
deploying JD.AI in multi-user environments needed the ability to:

- Grant elevated permissions to admin or superuser roles
- Restrict junior developers to a subset of tools and providers
- Override individual base-policy denials for specific roles (e.g., allow a security
  engineer to access network tools even when the global policy denies them)

---

## Decision

### 1. Extend `PolicyContext` with role fields

Added two optional fields to the `PolicyContext` record:

```csharp
string? RoleName = null
IReadOnlyList<string>? Groups = null
```

These fields are populated at call-time by the caller (AgentLoop, CLI, Gateway)
using either a static assignment or a pluggable `IRoleResolver`.

### 2. Add `RolePolicy` to `PolicySpec`

Added a `Roles` property of type `RolePolicy` to `PolicySpec`. A `RolePolicy` holds
a dictionary of named `RoleDefinition` entries:

```yaml
spec:
  roles:
    definitions:
      developer:
        allowTools:
          - ReadFile
          - WriteFile
        denyProviders:
          - OpenAI
      admin:
        inherits:
          - developer
        allowTools:
          - '*'
```

### 3. Role evaluation order in `PolicyEvaluator`

Evaluation follows this precedence:

1. **Role-level deny** — always wins, checked first
2. **Base policy deny** → can be overridden by role-level allow
3. **Base policy allowed-list exclusion** → can be extended by role-level allow
4. **Allow** (default when no restrictions match)

### 4. Role inheritance

`RoleDefinition.Inherits` supports transitive role inheritance with cycle detection.
A derived role additively accumulates the parent's allow/deny lists.

### 5. `IRoleResolver` + `FileRoleResolver`

Introduced `IRoleResolver` for pluggable role/group resolution. `FileRoleResolver`
loads a YAML file mapping user IDs to roles and groups:

```yaml
users:
  alice:
    role: admin
    groups:
      - engineering
  bob:
    role: developer
    groups:
      - engineering
```

### 6. `PolicyResolver` merges `RolePolicy` across scopes

When multiple policy documents (Global → User) are resolved, their role definitions
are additively merged — later scopes can add tools to existing roles without losing
global-scope definitions.

---

## Consequences

### Positive

- Organizations can define role hierarchies in policy YAML with no code changes
- Base denials can be selectively overridden for privileged roles
- `PolicyContext` is backward-compatible — all existing callers continue working
  with `RoleName = null` (role evaluation is skipped when no role is set)
- Role inheritance with cycle guard prevents infinite loops

### Negative / Trade-offs

- Role resolution must be wired at the call site; it does not happen automatically
  inside `PolicyEvaluator` (by design — evaluator is stateless)
- `Groups` are carried in context but not yet used in evaluation (reserved for
  future group-based allow/deny policies)

### Future work

- Wire `IRoleResolver` into `AgentLoop` and the Gateway session setup
- Implement group-based evaluation (allow/deny by `Groups` membership)
- Add `RequireApproval` decisions that can be triggered by role restrictions
