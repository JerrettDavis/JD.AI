# Use Case Specification System

The Use Case Specification operationalizes capabilities into actor-driven workflows with preconditions, outcomes, and failure paths.

## Purpose

Use `usecases.v1` to describe:

- which actor exercises a capability
- what must already be true before the workflow starts
- the workflow steps that define the scenario
- expected outcomes and failure scenarios
- downstream links to behavior, testing, and interfaces

## Repository Layout

`/specs/usecases/README.md`
`/specs/usecases/schema/usecases.schema.json`
`/specs/usecases/examples/usecases.example.yaml`
`/specs/usecases/index.yaml`

## Authoring Contract

Use case specs use YAML with these required concepts:

- `apiVersion`: `jdai.upss/v1`
- `kind`: `UseCase`
- `id`: `usecase.<name>` identifier
- `actor`
- `capabilityRef`
- `preconditions[]`
- `workflowSteps[]`
- `expectedOutcomes[]`
- `failureScenarios[]`
- `trace.upstream[]`
- `trace.downstream.behavior[]`
- `trace.downstream.testing[]`
- `trace.downstream.interfaces[]`

## Validation

Validation is enforced by:

1. `specs/usecases/schema/usecases.schema.json` for machine-readable contract shape.
2. `JD.AI.Core.Specifications.UseCaseSpecificationValidator` for repo-native enforcement, including:
   - required workflow fields and status values
   - actor and capability ID conventions
   - capability referential integrity against `specs/capabilities/index.yaml`
   - repository file validation for upstream, behavior, testing, and interface links

Invalid use case specs fail the existing repository test gate.

## Agent Workflow

Assigned agent: `upss-usecase-architect`

1. Read the relevant capability and actor context.
2. Update use case index and use case documents together.
3. Keep workflow steps concrete enough to become behavioral specs later.
4. Link testing and interface artifacts only when they are stable repository files.
5. Run repository validation before opening a PR.
