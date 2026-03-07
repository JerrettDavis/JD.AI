# Vision Specification System

The Vision Specification is the canonical source of product intent for JD.AI. It captures why a product exists, who it serves, what value it creates, how success is measured, and which constraints and non-goals bound future design work.

## Purpose

Use `vision.v1` to anchor downstream specifications and implementation work. A valid vision spec should:

- state the product problem and mission clearly
- identify target users and their needs
- define value proposition and measurable success metrics
- record constraints and explicit non-goals
- trace upstream context and downstream capability references

## Repository Layout

`/specs/vision/README.md`
`/specs/vision/schema/vision.schema.json`
`/specs/vision/examples/vision.example.yaml`
`/specs/vision/index.yaml`

## Authoring Contract

Vision specs use YAML with the following top-level shape:

- `apiVersion`: must be `jdai.upss/v1`
- `kind`: must be `Vision`
- `id`: `vision.<domain>` style identifier
- `version`: integer revision of the spec payload
- `status`: `draft`, `active`, `deprecated`, or `retired`
- `metadata`: owners, reviewers, review date, and change reason
- `problemStatement`
- `mission`
- `targetUsers[]`
- `valueProposition`
- `successMetrics[]`
- `constraints[]`
- `nonGoals[]`
- `trace.upstream[]`
- `trace.downstream.capabilities[]`

## Validation

Validation is enforced in two layers:

1. `specs/vision/schema/vision.schema.json` provides the machine-readable contract for humans, agents, and external tooling.
2. `JD.AI.Core.Specifications.VisionSpecificationValidator` enforces repo-native rules in CI, including:
   - required fields and allowed status values
   - ID conventions for vision, user, metric, and capability references
   - upstream file reference existence
   - downstream capability referential integrity when `/specs/capabilities/index.yaml` exists
   - index-to-file consistency for checked-in vision specs

The merge gate is the repository test suite. Invalid vision specs fail `dotnet test JD.AI.slnx`.

## Agent Workflow

Assigned agent: `upss-vision-architect`

Expected workflow:

1. Read the current repository context and any upstream strategy documents.
2. Update `index.yaml` and the target vision spec file together.
3. Keep success metrics measurable and non-goals explicit.
4. Add downstream capability references once the corresponding capability artifacts exist.
5. Run repository validation before opening a PR.

## Example

See `specs/vision/examples/vision.example.yaml` for the canonical `vision.v1` example used by repository validation.
