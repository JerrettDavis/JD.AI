# Policy Specification System

The Policy Specification defines enforceable security, compliance, and validation constraints that govern repository artifacts and agent workflows.

## Purpose

Use `policies.v1` to describe:

- security baselines and access-control constraints
- compliance rules that must hold before code ships
- quality gates and code-hygiene policies
- operational guardrails for deployment and runtime behavior

## Repository Layout

`/specs/policies/README.md`
`/specs/policies/schema/policies.schema.json`
`/specs/policies/examples/policies.example.yaml`
`/specs/policies/index.yaml`

## Authoring Contract

Policy specs use YAML with these required concepts:

- `apiVersion`: `jdai.upss/v1`
- `kind`: `Policy`
- `id`: `policy.<name>` identifier
- `policyType`: one of `security`, `compliance`, `quality`, `operational`
- `severity`: one of `critical`, `high`, `medium`, `low`
- `scope[]`
- `rules[]` each with `id`, `description`, `expression`
- `exceptions[]` each with `ruleId`, `reason`, `expiresAt`
- `enforcement.mode`: one of `enforce`, `warn`, `audit`
- `trace.upstream[]`
- `trace.downstream.ci[]`
- `trace.downstream.enforcement[]`
- `trace.downstream.operations[]`

## Validation

Validation is enforced by:

1. `specs/policies/schema/policies.schema.json` for machine-readable contract shape.
2. `JD.AI.Core.Specifications.PolicySpecificationValidator` for repo-native enforcement, including:
   - required policy fields and status values
   - policyType and severity membership checks
   - scope must contain at least one item
   - rules must contain at least one rule with id and description
   - enforcement mode membership check
   - repository file validation for upstream, ci, enforcement, and operations links

Invalid policy specs fail the existing repository test gate.

## Agent Workflow

Assigned agent: `upss-policy-rules-architect`

1. Read the upstream vision and capability context.
2. Express constraints as typed rules with concrete expressions.
3. Define enforcement mode and severity to signal CI behavior.
4. Link only stable repository artifacts in downstream ci, enforcement, and operations references.
5. Run repository validation before opening a PR.
