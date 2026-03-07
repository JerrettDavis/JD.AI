# Governance Specification System

The Governance Specification captures contribution workflows, release policies, approval gates, and change management rules that agents and human reviewers enforce across the product lifecycle.

## Purpose

Use `governance.v1` to describe:

- ownership models for repository areas
- change processes with approval requirements
- approval gates (automated, manual, or hybrid) with criteria
- release policies including cadence, branch strategy, and hotfix processes
- audit requirements that must be met before changes ship

## Repository Layout

`/specs/governance/README.md`
`/specs/governance/schema/governance.schema.json`
`/specs/governance/examples/governance.example.yaml`
`/specs/governance/index.yaml`

## Authoring Contract

Governance specs use YAML with these required concepts:

- `apiVersion`: `jdai.upss/v1`
- `kind`: `Governance`
- `id`: `governance.<name>` identifier
- `ownershipModel`
- `changeProcess[]`
- `approvalGates[]`
- `releasePolicy`
- `auditRequirements[]`
- `trace.upstream[]`
- `trace.downstream.allSpecTypes[]`

Each change process entry must include:

- `name`
- `description`
- `requiredApprovals`

Each approval gate must include:

- `name`
- `type`
- `criteria[]`

## Validation

Validation is enforced by:

1. `specs/governance/schema/governance.schema.json` for machine-readable contract shape.
2. `JD.AI.Core.Specifications.GovernanceSpecificationValidator` for repo-native enforcement, including:
   - required governance fields and status values
   - ownership model value validation
   - change process completeness
   - approval gate type and criteria validation
   - release policy cadence and branch strategy validation
   - audit requirements presence
   - repository file validation for upstream and downstream links

Invalid governance specs fail the existing repository test gate.

## Agent Workflow

Assigned agent: `upss-governance-agent`

1. Read the upstream vision and capability context.
2. Define the ownership model appropriate for the repository area.
3. Express change processes with clear approval thresholds.
4. Configure approval gates with concrete, verifiable criteria.
5. Set release policy to match the project delivery cadence.
6. Run repository validation before opening a PR.
