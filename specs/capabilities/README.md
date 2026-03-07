# Capability Specification System

The Capability Specification maps what a JD.AI product can do, who exercises that ability, what it depends on, and how it traces back to product intent.

## Purpose

Use `capabilities.v1` to describe:

- product abilities in a stable canonical map
- the actors associated with each capability
- dependency relationships between capabilities
- related use cases and downstream implementation references

## Repository Layout

`/specs/capabilities/README.md`
`/specs/capabilities/schema/capabilities.schema.json`
`/specs/capabilities/examples/capabilities.example.yaml`
`/specs/capabilities/index.yaml`

## Authoring Contract

Capability specs use YAML with these required concepts:

- `apiVersion`: `jdai.upss/v1`
- `kind`: `Capability`
- `id`: `capability.<name>` identifier
- `name`
- `description`
- `maturity`: `emerging`, `beta`, `ga`, or `deprecated`
- `actors[]`
- `dependencies[]`
- `relatedUseCases[]`
- `trace.visionRefs[]`
- `trace.upstream[]`
- `trace.downstream.useCases[]`
- `trace.downstream.architecture[]`
- `trace.downstream.testing[]`

## Validation

Validation is enforced by:

1. `specs/capabilities/schema/capabilities.schema.json` for machine-readable contract shape.
2. `JD.AI.Core.Specifications.CapabilitySpecificationValidator` for repo-native enforcement, including:
   - required fields, status values, and maturity values
   - ID conventions for capability, persona, vision, and use case references
   - vision referential integrity against `specs/vision/index.yaml`
   - capability dependency validation against `specs/capabilities/index.yaml`
   - file reference validation for upstream, architecture, and testing links

Invalid capability specs fail the existing repository test gate.

## Agent Workflow

Assigned agent: `upss-capability-map-architect`

1. Read the current vision and actor context for the product.
2. Update capability index and capability documents together.
3. Keep capability IDs stable and dependencies explicit.
4. Add downstream use case references only when canonical use case artifacts exist.
5. Run repository validation before opening a PR.
