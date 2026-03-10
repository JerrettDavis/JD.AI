# JD.AI Gateway Deployment

- **Type:** `deployment`
- **Kind:** `DeploymentIndex`
- **ID:** `deployment.jdai-gateway`
- **Status:** `draft`
- **Source:** `specs/deployment/examples/deployment.example.yaml`

## YAML

```yaml
apiVersion: jdai.upss/v1
kind: Deployment
id: deployment.jdai-gateway
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-deployment-topology-agent
  lastReviewed: 2026-03-07
  changeReason: Establish the first canonical deployment specification for the JD.AI gateway service.
environments:
  - name: staging
    type: staging
    region: us-east-1
  - name: production
    type: production
    region: us-east-1
pipelineStages:
  - name: Build and Test
    order: 1
    automated: true
  - name: Deploy to Staging
    order: 2
    automated: true
promotionGates:
  - fromEnv: staging
    toEnv: production
    criteria:
      - All integration tests pass in staging environment.
infrastructureRefs:
  - infra/gateway/main.tf
rollbackStrategy: blue-green
trace:
  upstream:
    - specs/vision/examples/vision.example.yaml
  downstream:
    operations:
      - tests/JD.AI.Tests/Specifications/DeploymentSpecificationRepositoryTests.cs
    observability:
      - src/JD.AI.Core/Specifications/DeploymentSpecification.cs
```
