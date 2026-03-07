# Deployment Specification System

The Deployment Specification captures environments, CI/CD pipelines, infrastructure configuration, promotion gates, and rollback strategies as repo-native artifacts that agents and automation can consume directly.

## Purpose

Use `deployment.v1` to describe:

- target environments and their types (development, staging, production, DR)
- pipeline stages and their execution order
- promotion gates with criteria for environment-to-environment promotion
- infrastructure references linking specs to provisioning artifacts
- rollback strategies governing failure recovery
- downstream links to tests, implementation, and observability artifacts

## Repository Layout

`/specs/deployment/README.md`
`/specs/deployment/schema/deployment.schema.json`
`/specs/deployment/examples/deployment.example.yaml`
`/specs/deployment/index.yaml`

## Authoring Contract

Deployment specs use YAML with these required concepts:

- `apiVersion`: `jdai.upss/v1`
- `kind`: `Deployment`
- `id`: `deployment.<name>` identifier
- `environments[]`
- `pipelineStages[]`
- `promotionGates[]`
- `infrastructureRefs[]`
- `rollbackStrategy`
- `trace.upstream[]`
- `trace.downstream.operations[]`
- `trace.downstream.observability[]`

Each environment must include:

- `name`
- `type`
- `region`

Each pipeline stage must include:

- `name`
- `order`
- `automated`

Each promotion gate must include:

- `fromEnv`
- `toEnv`
- `criteria[]`

## Validation

Validation is enforced by:

1. `specs/deployment/schema/deployment.schema.json` for machine-readable contract shape.
2. `JD.AI.Core.Specifications.DeploymentSpecificationValidator` for repo-native enforcement, including:
   - required deployment fields and status values
   - environment type validation against allowed types
   - pipeline stage integrity
   - rollback strategy validation against allowed strategies
   - repository file validation for upstream, operations, and observability links

Invalid deployment specs fail the existing repository test gate.

## Agent Workflow

Assigned agent: `upss-deployment-topology-agent`

1. Read the upstream vision and capability context.
2. Define environments, pipeline stages, and promotion gates.
3. Specify rollback strategy appropriate for the deployment topology.
4. Link only stable repository artifacts in downstream operations and observability references.
5. Run repository validation before opening a PR.
