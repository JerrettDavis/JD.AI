---
title: Unified Product Specification System (UPSS)
description: "Living specification framework that captures product intent, architecture, behavior, operations, and governance as machine-verifiable YAML artifacts."
---

# Unified Product Specification System (UPSS)

The Unified Product Specification System (UPSS) is JD.AI's living specification framework. It captures product intent, architecture, behavior, deployment, security, and governance as structured YAML artifacts that are version-controlled, validated in CI, and linked through a traceability graph.

## Why UPSS exists

Traditional documentation drifts from reality. Requirements live in wikis, architecture decisions in Confluence, operational runbooks in shared drives, and none of them are connected to the code they describe. When a developer changes behavior, the spec doesn't update. When a spec changes, no test breaks.

UPSS solves this by making specifications **executable infrastructure**:

- **Machine-readable** — YAML specs validated against JSON Schemas in CI
- **Repository-native** — Specs live alongside code in `specs/`, not in an external tool
- **Traceable** — Every spec links upstream to its rationale and downstream to its implementation
- **Validated** — C# validators run as unit tests; broken links and invalid specs fail the build
- **Agent-consumable** — AI agents read specs to understand context, generate tests, and enforce constraints

## The specification dependency graph

UPSS specs form a directed acyclic graph from product intent down to operational concerns:

```
                        ┌──────────┐
                        │  Vision  │
                        └────┬─────┘
                             │
                  ┌──────────┼──────────┐
                  ▼          ▼          ▼
            ┌──────────┐ ┌────────┐ ┌─────────┐
            │Capability│ │Persona │ │   ADR   │
            └────┬─────┘ └───┬────┘ └────┬────┘
                 │           │           │
                 ▼           │           │
            ┌──────────┐    │           │
            │ Use Case │◄───┘           │
            └────┬─────┘               │
                 │                      │
          ┌──────┼──────┐              │
          ▼      ▼      ▼              ▼
     ┌────────┐┌──────┐┌──────────┐┌──────────────┐
     │Behavior││Domain││ Interface││ Architecture │
     └───┬────┘└──┬───┘└────┬─────┘└──────┬───────┘
         │        │         │             │
         ▼        ▼         │             ▼
    ┌────────┐┌──────┐      │      ┌────────────┐
    │Testing ││ Data │      │      │ Deployment │
    └────────┘└──────┘      │      └─────┬──────┘
                            │            │
              ┌─────────────┼────────────┤
              ▼             ▼            ▼
        ┌──────────┐ ┌──────────┐ ┌────────────┐
        │ Security │ │ Quality  │ │ Operations │
        └──────────┘ └──────────┘ └─────┬──────┘
                                        │
                          ┌─────────────┼──────────────┐
                          ▼             ▼              ▼
                   ┌──────────┐ ┌──────────────┐ ┌────────────┐
                   │  Policy  │ │Observability │ │ Governance │
                   └──────────┘ └──────────────┘ └────────────┘
```

**Reading the graph:** Vision defines what the product is. Capabilities break it into what the product can do. Use cases describe how actors interact. Behavior specs make those interactions executable. Architecture, domain, and interface specs describe the structural implementation. Deployment, operations, security, quality, testing, policy, observability, and governance specs capture cross-cutting production concerns. ADRs record the decisions made along the way.

## The 18 spec types

UPSS includes 18 specification types organized in five layers:

### Foundation

| Spec | Kind | Purpose |
|------|------|---------|
| [Vision](catalog.md#vision) | `Vision` | Product intent — problem, mission, users, success metrics |
| [Capability](catalog.md#capability) | `Capability` | Product abilities — features, maturity, actor mapping |
| [Persona](catalog.md#persona) | `Persona` | Actors — users, admins, systems, trust boundaries |

### Workflow

| Spec | Kind | Purpose |
|------|------|---------|
| [Use Case](catalog.md#use-case) | `UseCase` | Actor-driven workflows with preconditions and outcomes |
| [Behavior](catalog.md#behavior) | `Behavior` | BDD scenarios and state machines for use cases |
| [ADR](catalog.md#adr) | `Adr` | Architecture decisions with context, alternatives, consequences |

### Structure

| Spec | Kind | Purpose |
|------|------|---------|
| [Architecture](catalog.md#architecture) | `Architecture` | C4 systems, containers, components, dependency rules |
| [Domain](catalog.md#domain) | `Domain` | DDD entities, value objects, aggregates, invariants |
| [Interface](catalog.md#interface) | `Interface` | API contracts — REST, GraphQL, gRPC, events, WebSocket |
| [Data](catalog.md#data) | `Data` | Logical models, migrations, indexes, constraints |

### Operations

| Spec | Kind | Purpose |
|------|------|---------|
| [Deployment](catalog.md#deployment) | `Deployment` | Environments, pipelines, promotion gates, rollback |
| [Operations](catalog.md#operations) | `Operations` | Runbooks, incident response, escalation, SLOs |
| [Observability](catalog.md#observability) | `Observability` | Metrics, logs, traces, alerts |
| [Security](catalog.md#security) | `Security` | AuthN/AuthZ, trust zones, threats, controls |

### Governance

| Spec | Kind | Purpose |
|------|------|---------|
| [Quality](catalog.md#quality) | `Quality` | SLOs, SLIs, error budgets, scalability targets |
| [Testing](catalog.md#testing) | `Testing` | Verification levels, coverage targets, generation rules |
| [Policy](catalog.md#policy) | `Policy` | Security, compliance, operational constraints and rules |
| [Governance](catalog.md#governance) | `Governance` | Ownership, change process, approval gates, release policy |

## Repository layout

All specs live under `/specs/` in the repository root:

```
specs/
├── vision/
│   ├── README.md                          # Human documentation
│   ├── schema/vision.schema.json          # JSON Schema contract
│   ├── examples/vision.example.yaml       # Canonical example
│   └── index.yaml                         # Catalog of all vision specs
├── capabilities/
│   ├── README.md
│   ├── schema/capabilities.schema.json
│   ├── examples/capabilities.example.yaml
│   └── index.yaml
├── behavior/
│   └── ...                                # Same structure
├── architecture/
│   └── ...
└── ... (18 directories total)
```

Each spec directory contains exactly four artifacts:

| File | Purpose |
|------|---------|
| `README.md` | Human-readable documentation for the spec type |
| `schema/*.schema.json` | JSON Schema defining the machine-readable contract |
| `examples/*.example.yaml` | Canonical example used by repository validation |
| `index.yaml` | Catalog of all specs of this type with paths and status |

## How validation works

UPSS validation runs at two levels:

### 1. JSON Schema validation

Each spec's `schema/*.schema.json` defines the structural contract — required fields, types, enumerations, and patterns. All schemas use `"additionalProperties": false` to reject unknown fields.

### 2. C# repository validation

Each spec type has a corresponding validator class in `JD.AI.Core.Specifications`:

```
BehaviorSpecificationValidator.Validate(spec)        → document-level rules
BehaviorSpecificationValidator.ValidateRepository(root) → repo-level integrity
```

Repository validation checks:
- Index entries point to files that exist and parse successfully
- IDs match between the index and the parsed spec
- Status values are consistent
- Cross-references resolve (e.g., a behavior spec's `useCaseRef` exists in the use case index)
- Downstream file references (test files, source files) exist on disk

All validators run as xUnit tests in `tests/JD.AI.Tests/Specifications/`. A broken spec fails the build.

## Traceability

Every spec includes a `trace` field linking it to upstream dependencies and downstream implementation:

```yaml
trace:
  upstream:
    - specs/vision/examples/vision.example.yaml
  downstream:
    testing:
      - tests/JD.AI.Tests/Specifications/BehaviorSpecificationRepositoryTests.cs
    code:
      - src/JD.AI.Core/Specifications/BehaviorSpecification.cs
```

Validators verify that referenced files exist. This creates a live dependency graph where breaking a link breaks the build.

## Agent integration

Each spec type has an assigned AI agent role (e.g., `upss-vision-architect`, `upss-behavioral-spec-architect`). Agents are expected to:

1. Read upstream specs for context before authoring
2. Update the index and spec file together
3. Maintain valid traceability links
4. Run repository validation before opening PRs

## Next steps

- [Authoring Guide](authoring.md) — How to create and maintain UPSS specs
- [Specification Catalog](catalog.md) — Complete reference for all 18 spec types
