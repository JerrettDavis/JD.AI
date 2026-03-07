# Operational Specification System

The Operational Specification captures runbooks, incident response procedures, escalation policies, and response SLOs that agents and operators can consume directly.

## Purpose

Use `operations.v1` to describe:

- runbooks with trigger conditions and step-by-step procedures
- incident severity levels and expected response times
- response SLOs for acknowledgement and resolution windows
- escalation paths with contact chains
- downstream links to governance and audit artifacts

## Repository Layout

`/specs/operations/README.md`
`/specs/operations/schema/operations.schema.json`
`/specs/operations/examples/operations.example.yaml`
`/specs/operations/index.yaml`

## Authoring Contract

Operations specs use YAML with these required concepts:

- `apiVersion`: `jdai.upss/v1`
- `kind`: `Operations`
- `id`: `operations.<name>` identifier
- `service`
- `runbooks[]`
- `incidentLevels[]`
- `responseSlos[]`
- `escalationPaths[]`
- `trace.upstream[]`
- `trace.downstream.governance[]`
- `trace.downstream.audits[]`

Each runbook must include:

- `name`
- `triggerCondition`
- `steps[]`

Each incident level must include:

- `level` (one of: sev1, sev2, sev3, sev4)
- `description`
- `responseTime`

Each escalation path must include:

- `level`
- `contacts[]`

## Validation

Validation is enforced by:

1. `specs/operations/schema/operations.schema.json` for machine-readable contract shape.
2. `JD.AI.Core.Specifications.OperationsSpecificationValidator` for repo-native enforcement, including:
   - required operations fields and status values
   - service field must not be blank
   - runbook integrity for name, trigger condition, and steps
   - incident level validation against allowed severity levels
   - escalation path integrity for level and contacts
   - repository file validation for upstream, governance, and audit links

Invalid operations specs fail the existing repository test gate.

## Agent Workflow

Assigned agent: `upss-operations-runbook-agent`

1. Read the upstream vision and capability context.
2. Define runbooks with clear trigger conditions and actionable steps.
3. Map incident severity levels to response time expectations.
4. Establish escalation paths with concrete contact chains.
5. Run repository validation before opening a PR.
