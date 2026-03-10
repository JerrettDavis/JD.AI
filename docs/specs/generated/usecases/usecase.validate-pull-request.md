# Validate Pull Request

- **Type:** `usecases`
- **Kind:** `UseCaseIndex`
- **ID:** `usecase.validate-pull-request`
- **Status:** `draft`
- **Source:** `specs/usecases/examples/usecases.example.yaml`

## YAML

```yaml
apiVersion: jdai.upss/v1
kind: UseCase
id: usecase.validate-pull-request
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-usecase-architect
  lastReviewed: 2026-03-07
  changeReason: Define the first canonical use case for JD.AI validation workflows.
actor: persona.delivery-agent
capabilityRef: capability.spec-validation
preconditions:
  - Pull request includes modified repository specifications.
workflowSteps:
  - Run schema and policy validators.
  - Publish traceability report.
expectedOutcomes:
  - Pull request is blocked on validation failures.
failureScenarios:
  - Missing upstream capability reference causes validation failure.
trace:
  upstream:
    - specs/capabilities/examples/capabilities.example.yaml
  downstream:
    behavior:
      - docs/reference/commands.md
    testing:
      - tests/JD.AI.Tests/Specifications/UseCaseSpecificationRepositoryTests.cs
    interfaces: []
```
