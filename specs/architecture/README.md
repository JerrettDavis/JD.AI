# Architecture Specification System

The Architecture Specification captures C4-compatible systems, containers, components, and dependency boundaries that define the structural blueprint for the product.

## Purpose

Use `architecture.v1` to describe:

- systems and their high-level responsibilities
- containers within each system and their technology stacks
- components within each container and their responsibilities
- dependency rules that enforce allowed and disallowed relationships
- downstream links to deployment, security, and operations artifacts

## Repository Layout

`/specs/architecture/README.md`
`/specs/architecture/schema/architecture.schema.json`
`/specs/architecture/examples/architecture.example.yaml`
`/specs/architecture/index.yaml`

## Authoring Contract

Architecture specs use YAML with these required concepts:

- `apiVersion`: `jdai.upss/v1`
- `kind`: `Architecture`
- `id`: `architecture.<name>` identifier
- `architectureStyle`
- `systems[]`
- `containers[]`
- `components[]`
- `dependencyRules[]`
- `trace.upstream[]`
- `trace.downstream.deployment[]`
- `trace.downstream.security[]`
- `trace.downstream.operations[]`

Each system must include:

- `name`
- `description`
- `type`

Each container must include:

- `name`
- `technology`
- `system`

Each component must include:

- `name`
- `container`
- `responsibility`

Each dependency rule must include:

- `from`
- `to`
- `allowed`

## Validation

Validation is enforced by:

1. `specs/architecture/schema/architecture.schema.json` for machine-readable contract shape.
2. `JD.AI.Core.Specifications.ArchitectureSpecificationValidator` for repo-native enforcement, including:
   - required architecture fields and status values
   - architecture style must be one of: layered, microservices, event-driven, modular-monolith, hexagonal
   - systems, containers, components, and dependency rules must each have at least one entry
   - repository file validation for upstream, deployment, security, and operations links

Invalid architecture specs fail the existing repository test gate.

## Agent Workflow

Assigned agent: `upss-architecture-c4-architect`

1. Read the upstream vision and capability context.
2. Define systems, containers, and components following C4 conventions.
3. Express dependency rules that enforce architectural boundaries.
4. Link only stable repository artifacts in downstream deployment, security, and operations references.
5. Run repository validation before opening a PR.
