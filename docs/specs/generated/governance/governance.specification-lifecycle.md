# Specification Lifecycle Governance

- **Type:** `governance`
- **Kind:** `GovernanceIndex`
- **ID:** `governance.specification-lifecycle`
- **Status:** `draft`
- **Source:** `specs/governance/examples/governance.example.yaml`

## YAML

```yaml
apiVersion: jdai.upss/v1
kind: Governance
id: governance.specification-lifecycle
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-governance-agent
  lastReviewed: 2026-03-07
  changeReason: Establish the first canonical governance specification for specification lifecycle management.
ownershipModel: codeowners
changeProcess:
  - name: Specification change proposal
    description: All specification changes must be submitted as pull requests with upstream traceability links.
    requiredApprovals: 1
approvalGates:
  - name: CI validation gate
    type: automated
    criteria:
      - All repository specification validators pass without errors.
releasePolicy:
  cadence: continuous
  branchStrategy: trunk-based
  hotfixProcess: Fast-track PR with owner approval bypassing scheduled review.
auditRequirements:
  - Every specification change must include trace links to upstream and downstream artifacts.
trace:
  upstream:
    - specs/vision/examples/vision.example.yaml
  downstream:
    allSpecTypes:
      - tests/JD.AI.Tests/Specifications/GovernanceSpecificationRepositoryTests.cs
      - src/JD.AI.Core/Specifications/GovernanceSpecification.cs
```
