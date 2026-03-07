# Domain Model Specification System

The Domain Model Specification captures entities, value objects, aggregates, invariants, and relationships that define the core domain model of a bounded context.

## Purpose

Use `domain.v1` to describe:

- entities with their identifying properties
- value objects that represent immutable domain concepts
- aggregates that enforce consistency boundaries
- invariants that must hold across the domain model
- downstream links to tests, interfaces, and implementation artifacts

## Repository Layout

`/specs/domain/README.md`
`/specs/domain/schema/domain.schema.json`
`/specs/domain/examples/domain.example.yaml`
`/specs/domain/index.yaml`

## Authoring Contract

Domain specs use YAML with these required concepts:

- `apiVersion`: `jdai.upss/v1`
- `kind`: `Domain`
- `id`: `domain.<name>` identifier
- `version`: integer >= 1
- `status`: draft | active | deprecated | retired
- `metadata.owners[]`
- `metadata.reviewers[]`
- `metadata.lastReviewed`
- `metadata.changeReason`
- `boundedContext`
- `entities[]`
- `valueObjects[]`
- `aggregates[]`
- `invariants[]`
- `trace.upstream[]`
- `trace.downstream.data[]`
- `trace.downstream.interfaces[]`
- `trace.downstream.architecture[]`

Each entity must include:

- `name`
- `description`
- `properties[]`

Each value object must include:

- `name`
- `description`
- `properties[]`

Each aggregate must include:

- `name`
- `rootEntity`
- `members[]`

## Validation

Validation is enforced by:

1. `specs/domain/schema/domain.schema.json` for machine-readable contract shape.
2. `JD.AI.Core.Specifications.DomainSpecificationValidator` for repo-native enforcement, including:
   - required domain fields and status values
   - bounded context non-blank constraint
   - entity and aggregate structural integrity
   - repository file validation for upstream and all downstream links

Invalid domain specs fail the existing repository test gate.

## Agent Workflow

Assigned agent: `upss-domain-model-architect`

1. Read the upstream capability context.
2. Model entities, value objects, and aggregates for the bounded context.
3. Express domain invariants as concrete, testable statements.
4. Link only stable repository artifacts in downstream data, interfaces, and architecture references.
5. Run repository validation before opening a PR.
