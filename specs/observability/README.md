# Observability Specification System

The Observability Specification captures metrics, logging, tracing, alerting, and dashboard requirements so that agents and operational tooling can enforce and validate observability coverage.

## Purpose

Use `observability.v1` to describe:

- metrics definitions (counters, gauges, histograms, summaries) for a service
- structured log entries with level and format requirements
- distributed trace spans with span kinds and attributes
- alerting rules with conditions and severity classifications
- traceability links to upstream vision artifacts and downstream tests, governance, and operations

## Repository Layout

`/specs/observability/README.md`
`/specs/observability/schema/observability.schema.json`
`/specs/observability/examples/observability.example.yaml`
`/specs/observability/index.yaml`

## Authoring Contract

Observability specs use YAML with these required concepts:

- `apiVersion`: `jdai.upss/v1`
- `kind`: `Observability`
- `id`: `observability.<name>` identifier
- `serviceRefs[]`
- `metrics[]`
- `logs[]`
- `traces[]`
- `alerts[]`
- `trace.upstream[]`
- `trace.downstream.operations[]`
- `trace.downstream.governance[]`

Each metric must include:

- `name`
- `type` (counter, gauge, histogram, summary)
- `description`

Each log entry must include:

- `name`
- `level` (debug, info, warning, error, critical)
- `format`

Each trace span must include:

- `name`
- `spanKind` (server, client, producer, consumer, internal)
- `attributes[]`

Each alert must include:

- `name`
- `condition`
- `severity` (critical, warning, info)

## Validation

Validation is enforced by:

1. `specs/observability/schema/observability.schema.json` for machine-readable contract shape.
2. `JD.AI.Core.Specifications.ObservabilitySpecificationValidator` for repo-native enforcement, including:
   - required observability fields and status values
   - service reference integrity
   - metric type and log level enumeration validation
   - trace span kind enumeration validation
   - alert severity enumeration validation
   - repository file validation for upstream, operations, and governance links

Invalid observability specs fail the existing repository test gate.

## Agent Workflow

Assigned agent: `upss-observability-agent`

1. Read the upstream vision and capability context.
2. Define metrics, logs, traces, and alerts for each referenced service.
3. Keep alert conditions concrete enough to map to monitoring rules.
4. Link only stable repository artifacts in downstream operations and governance references.
5. Run repository validation before opening a PR.
