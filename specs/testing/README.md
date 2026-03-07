# Testing Specification System

The Testing Specification maps verification strategies to behavioral specifications, interface contracts, and quality targets so that agents and CI pipelines know exactly what to test and how.

## Purpose

Use `testing.v1` to describe:

- verification levels required for a feature or workflow
- references to the behavioral specs and quality specs being verified
- coverage targets with scope, metric, and threshold
- generation rules that declare whether tests are generated, manual, or hybrid
- downstream links to CI test files and release implementation artifacts

## Repository Layout

`/specs/testing/README.md`
`/specs/testing/schema/testing.schema.json`
`/specs/testing/examples/testing.example.yaml`
`/specs/testing/index.yaml`

## Authoring Contract

Testing specs use YAML with these required concepts:

- `apiVersion`: `jdai.upss/v1`
- `kind`: `Testing`
- `id`: `testing.<name>` identifier
- `verificationLevels[]`
- `behaviorRefs[]`
- `qualityRefs[]`
- `coverageTargets[]`
- `generationRules[]`
- `trace.upstream[]`
- `trace.downstream.ci[]`
- `trace.downstream.release[]`

Each coverage target must include:

- `scope`
- `target`
- `metric` (optional)

Each generation rule must include:

- `source`
- `strategy` (one of `generated`, `manual`, `hybrid`)

## Validation

Validation is enforced by:

1. `specs/testing/schema/testing.schema.json` for machine-readable contract shape.
2. `JD.AI.Core.Specifications.TestingSpecificationValidator` for repo-native enforcement, including:
   - required testing fields and status values
   - verification level values constrained to the allowed set
   - behavioral and quality reference pattern integrity
   - coverage target non-blank scope and target
   - generation rule strategy constrained to the allowed set
   - repository file validation for upstream, ci, and release links

Invalid testing specs fail the existing repository test gate.

## Agent Workflow

Assigned agent: `upss-testing-strategy-agent`

1. Read the upstream behavior and quality specification context.
2. Select verification levels appropriate for the workflow under test.
3. Define coverage targets with concrete scopes and thresholds.
4. Declare generation rules for each test source.
5. Link only stable repository artifacts in downstream ci and release references.
6. Run repository validation before opening a PR.
