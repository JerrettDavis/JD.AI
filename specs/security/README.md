# Security Specification System

The Security Specification captures authentication, authorization, trust zones, threat models, and controls that agents and test runners can consume directly.

## Purpose

Use `security.v1` to describe:

- authentication models for system boundaries
- authorization strategies and access control patterns
- trust zones and their classification levels
- threat models with severity and mitigation mappings
- security controls and their categorization
- residual risks with justification
- downstream links to tests, deployment, operations, and implementation artifacts

## Repository Layout

`/specs/security/README.md`
`/specs/security/schema/security.schema.json`
`/specs/security/examples/security.example.yaml`
`/specs/security/index.yaml`

## Authoring Contract

Security specs use YAML with these required concepts:

- `apiVersion`: `jdai.upss/v1`
- `kind`: `Security`
- `id`: `security.<name>` identifier
- `authnModel`
- `authzModel`
- `trustZones[]`
- `threats[]`
- `controls[]`
- `residualRisks[]`
- `trace.upstream[]`
- `trace.downstream.deployment[]`
- `trace.downstream.operations[]`
- `trace.downstream.testing[]`

Each trust zone must include:

- `name`
- `level`

Each threat must include:

- `id`
- `description`
- `severity`
- `mitigatedBy[]`

Each control must include:

- `id`
- `description`
- `type`

Each residual risk must include:

- `threatId`
- `justification`

## Validation

Validation is enforced by:

1. `specs/security/schema/security.schema.json` for machine-readable contract shape.
2. `JD.AI.Core.Specifications.SecuritySpecificationValidator` for repo-native enforcement, including:
   - required security fields and status values
   - authentication and authorization model validation against allowed sets
   - trust zone, threat, and control integrity checks
   - repository file validation for upstream, deployment, operations, and testing links

Invalid security specs fail the existing repository test gate.

## Agent Workflow

Assigned agent: `upss-security-architecture-agent`

1. Read the upstream capability context.
2. Define authentication and authorization models for the system boundary.
3. Enumerate trust zones, threats, and controls with concrete mappings.
4. Document residual risks with clear justification.
5. Link only stable repository artifacts in downstream deployment, operations, and testing references.
6. Run repository validation before opening a PR.
