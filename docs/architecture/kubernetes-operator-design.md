# JD.AI Kubernetes Operator — v2 Design

**Status**: Planned (v2 scope — not required for initial Helm-based deployment)

---

## Overview

The Kubernetes operator extends the JD.AI deployment model to support `JdAiAgent` custom resources. Agents become first-class Kubernetes objects — versioned, policy-enforced, and lifecycle-managed by the cluster.

This document captures the design intent for the operator so the API surface can be validated before implementation begins.

---

## Custom Resource: `JdAiAgent`

```yaml
apiVersion: jdai.io/v1
kind: JdAiAgent
metadata:
  name: code-reviewer
  namespace: jdai
spec:
  # References a versioned agent definition from AgentDefinitionRegistry
  agentDefinition: code-reviewer@1.2.0

  # Number of concurrent agent replicas
  replicas: 2

  # Resource requests/limits per replica
  resources:
    requests:
      memory: "512Mi"
      cpu: "250m"
    limits:
      memory: "1Gi"
      cpu: "500m"

  # Tool loadout (referenced by name from ConnectorRegistry)
  loadout: jira-readonly

  # Policy override (references a JdAiPolicy resource)
  policyRef:
    name: strict-code-review

  # Telemetry configuration
  telemetry:
    otlpEndpoint: http://otel-collector:4317

status:
  ready: true
  availableReplicas: 2
  conditions:
    - type: Ready
      status: "True"
      lastTransitionTime: "2025-07-01T00:00:00Z"
```

## Custom Resource: `JdAiPolicy`

```yaml
apiVersion: jdai.io/v1
kind: JdAiPolicy
metadata:
  name: strict-code-review
  namespace: jdai
spec:
  rules:
    - id: no-external-calls
      description: Block outbound HTTP to non-allowlisted hosts
      enforce: true
    - id: no-secret-output
      description: Redact secrets from all agent outputs
      enforce: true
  auditRetentionDays: 90
```

---

## Operator Architecture

```
┌──────────────────────────────────────────────────────────────┐
│  JD.AI Operator (controller-runtime)                         │
│                                                               │
│  ┌──────────────────┐  ┌──────────────────────────────────┐ │
│  │ JdAiAgentReconciler │  JdAiPolicyReconciler             │ │
│  │  - Watch JdAiAgent │  - Watch JdAiPolicy                │ │
│  │  - Create Deployment│  - Sync to PolicyRegistry          │ │
│  │  - Create Service  │  - Emit audit events               │ │
│  │  - Manage HPA      │                                    │ │
│  └──────────────────┘  └──────────────────────────────────┘ │
│                                                               │
│  Watches: Deployments, Services, HPAs (owned resources)      │
└──────────────────────────────────────────────────────────────┘
```

The operator is implemented using the [controller-runtime](https://github.com/kubernetes-sigs/controller-runtime) library in Go, or alternatively as a .NET Kubernetes operator using [KubeOps](https://buehler.github.io/dotnet-operator-sdk/).

---

## Reconciliation Loop

For each `JdAiAgent`:

1. Resolve `agentDefinition` from `AgentDefinitionRegistry` (version pinned)
2. Render agent configuration (tools, policies, loadout)
3. Create or update a `Deployment` with the resolved configuration
4. Attach a `Service` if the agent exposes an HTTP interface
5. Attach an `HPA` if `replicas` is a range
6. Emit an `AuditEvent` for any configuration change

---

## Implementation Phases

| Phase | Scope |
|-------|-------|
| v1 | Helm chart + manual Deployment management (current) |
| v2a | CRD definitions + basic reconciler (create Deployment from JdAiAgent) |
| v2b | Policy CRD + reconciler + audit integration |
| v2c | HPA management, status conditions, leader election |
| v3 | Multi-cluster federation, GitOps integration |

---

## References

- [controller-runtime](https://github.com/kubernetes-sigs/controller-runtime) — standard Go operator framework
- [KubeOps](https://buehler.github.io/dotnet-operator-sdk/) — .NET operator SDK
- [Operator Pattern](https://kubernetes.io/docs/concepts/extend-kubernetes/operator/) — Kubernetes documentation
- [JD.AI Architecture — Kubernetes Integration](../README.md#kubernetes-integration)
