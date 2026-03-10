# API Security Baseline Policy

- **Type:** `policies`
- **Kind:** `PolicyIndex`
- **ID:** `policy.api-security-baseline`
- **Status:** `draft`
- **Source:** `specs/policies/examples/policies.example.yaml`

## YAML

```yaml
apiVersion: jdai.upss/v1
kind: Policy
id: policy.api-security-baseline
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-policy-rules-architect
  lastReviewed: 2026-03-07
  changeReason: Establish the first canonical policy specification for API security baselines.
policyType: security
severity: high
scope:
  - src/JD.AI.Core
  - src/JD.AI.Cli
rules:
  - id: require-auth-on-endpoints
    description: All public API endpoints must require authentication.
    expression: endpoint.auth != null
  - id: no-plaintext-secrets
    description: Configuration values must not contain plaintext secrets.
    expression: config.values.none(v => v.matches('password|secret|key'))
exceptions: []
enforcement:
  mode: enforce
trace:
  upstream:
    - specs/vision/examples/vision.example.yaml
  downstream:
    ci:
      - tests/JD.AI.Tests/Specifications/PolicySpecificationRepositoryTests.cs
    enforcement:
      - src/JD.AI.Core/Specifications/PolicySpecification.cs
    operations: []
```
