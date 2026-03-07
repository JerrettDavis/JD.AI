# Interface Specification System

The Interface Specification defines canonical interface contracts for REST, GraphQL, gRPC, event, and WebSocket APIs that agents and services expose or consume.

## Purpose

Use `interfaces.v1` to describe:

- interface type and transport protocol
- operations exposed by the API surface
- message schemas exchanged over the interface
- compatibility rules governing contract evolution
- downstream links to tests, implementation, and deployment artifacts

## Repository Layout

`/specs/interfaces/README.md`
`/specs/interfaces/schema/interfaces.schema.json`
`/specs/interfaces/examples/interfaces.example.yaml`
`/specs/interfaces/index.yaml`

## Authoring Contract

Interface specs use YAML with these required concepts:

- `apiVersion`: `jdai.upss/v1`
- `kind`: `Interface`
- `id`: `interface.<name>` identifier
- `interfaceType`: one of `rest`, `graphql`, `grpc`, `event`, `websocket`
- `operations[]`
- `messageSchemas[]`
- `compatibilityRules[]`
- `trace.upstream[]`
- `trace.downstream.code[]`
- `trace.downstream.testing[]`
- `trace.downstream.deployment[]`

Each operation must include:

- `name`
- `method`
- `path`
- `description`

Each message schema must include:

- `name`
- `format`
- `description`

## Validation

Validation is enforced by:

1. `specs/interfaces/schema/interfaces.schema.json` for machine-readable contract shape.
2. `JD.AI.Core.Specifications.InterfaceSpecificationValidator` for repo-native enforcement, including:
   - required interface fields and status values
   - interface type enumeration validation
   - operation and message schema structural integrity
   - compatibility rule presence
   - repository file validation for upstream and all downstream arrays

Invalid interface specs fail the existing repository test gate.

## Agent Workflow

Assigned agent: `upss-interface-contract-architect`

1. Read the upstream use case and behavioral context.
2. Define the interface type and operations that realize the upstream contracts.
3. Declare message schemas with enough detail to generate or validate payloads.
4. Establish compatibility rules governing how the contract may evolve.
5. Link only stable repository artifacts in downstream testing, code, and deployment references.
6. Run repository validation before opening a PR.
