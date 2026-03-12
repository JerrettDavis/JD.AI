# UPSS Issue #349 Promotion Matrix

Date: 2026-03-11
Issue: https://github.com/JerrettDavis/JD.AI/issues/349
Source commit reviewed: `5d2298d`

## Review Summary

- Promoted the issue `#327-#342` draft specification set from `draft` to `active` across spec files and their corresponding index entries.
- Reconciled overlap between composite capability IDs and existing granular capability-map entries by encoding explicit dependency links.
- Re-validated repository-level UPSS consistency with specification test gates.

## Capability Overlap Reconciliation

- `capability.control-plane-management` now depends on:
  - `capability.gateway-control-plane`
  - `capability.dashboard-ui`
  - `capability.multi-channel-platform`
- `capability.multi-channel-platform` now depends on:
  - `capability.multi-channel-interaction`
  - `capability.messaging-adapters`
  - `capability.provider-model-management`
- `capability.provider-model-management` now depends on:
  - `capability.provider-management`
  - `capability.model-selection`
  - `capability.model-routing`
- `capability.subagent-team-orchestration` now depends on:
  - `capability.subagents`
  - `capability.team-orchestration`
- `capability.tool-execution-loadouts` now depends on:
  - `capability.tool-execution`
  - `capability.tool-loadouts`
  - `capability.tool-registry`
  - `capability.provider-tool-calling`
- `capability.workflow-authoring-execution` now depends on:
  - `capability.workflow-authoring`
  - `capability.workflow-execution`

## Promotion Decisions

Status decision for all issue `#327-#342` artifacts in commit `5d2298d`: `active`.

Promoted spec groups:

1. Capability
2. UseCase
3. Behavior
4. Architecture
5. Domain
6. Interface
7. Policy
8. Security
9. Testing
10. Quality
11. Deployment
12. Operations
13. Observability
14. Governance

## Validation Gates

- `dotnet test tests/JD.AI.Tests --filter "FullyQualifiedName~SpecificationRepositoryTests"`
- `dotnet test tests/JD.AI.Tests --filter "FullyQualifiedName~Specification"`
- `dotnet husky run --group pre-commit --no-partial`
