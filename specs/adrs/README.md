# Decision Specification System (ADR)

The Decision Specification captures canonical architecture decision records with machine-readable decision metadata and conflict semantics that agents and governance tools can consume directly.

## Purpose

Use `adrs.v1` to describe:

- architecture decisions with structured context, rationale, and consequences
- alternative options evaluated with pros and cons
- conflict and supersession semantics between decisions
- downstream links to implementation and governance artifacts

## Repository Layout

`/specs/adrs/README.md`
`/specs/adrs/schema/adrs.schema.json`
`/specs/adrs/examples/adrs.example.yaml`
`/specs/adrs/index.yaml`

## Authoring Contract

ADR specs use YAML with these required concepts:

- `apiVersion`: `jdai.upss/v1`
- `kind`: `Adr`
- `id`: `adr.<name>` identifier
- `date`
- `context`
- `decision`
- `alternatives[]`
- `consequences[]`
- `supersedes[]`
- `conflictsWith[]`
- `trace.upstream[]`
- `trace.downstream.implementation[]`
- `trace.downstream.governance[]`

Each alternative must include:

- `title`
- `description`
- `pros[]`
- `cons[]`

## Validation

Validation is enforced by:

1. `specs/adrs/schema/adrs.schema.json` for machine-readable contract shape.
2. `JD.AI.Core.Specifications.AdrSpecificationValidator` for repo-native enforcement, including:
   - required ADR fields and status values
   - date format validation
   - context and decision content requirements
   - alternatives structure with non-blank titles
   - consequences with at least one entry
   - supersedes and conflictsWith entries matching the ADR ID pattern
   - repository file validation for upstream, implementation, and governance links

Invalid ADR specs fail the existing repository test gate.

## Agent Workflow

Assigned agent: `upss-adr-curator`

1. Read the upstream product context and architectural drivers.
2. Document the decision context and rationale clearly.
3. Enumerate alternatives with concrete pros and cons.
4. State consequences that are testable or observable.
5. Link only stable repository artifacts in downstream implementation and governance references.
6. Run repository validation before opening a PR.
