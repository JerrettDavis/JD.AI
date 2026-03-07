---
title: UPSS Specification Catalog
description: "Complete reference for all 18 UPSS specification types — fields, validation rules, examples, and assigned agents."
---

# Specification Catalog

Complete reference for all 18 UPSS specification types. Each entry documents the spec's purpose, required fields, validation rules, and assigned agent.

For common fields shared by all specs (`apiVersion`, `kind`, `id`, `version`, `status`, `metadata`, `trace`), see [Authoring Guide](authoring.md).

---

## Foundation Layer

### Vision

**Kind:** `Vision` | **ID pattern:** `vision.<name>` | **Agent:** `upss-vision-architect`

The canonical source of product intent. Anchors all downstream specs.

| Field | Type | Description |
|-------|------|-------------|
| `problemStatement` | string | The problem this product solves |
| `mission` | string | Product mission statement |
| `targetUsers` | object[] | Users and their needs (`name`, `needs`) |
| `valueProposition` | string | Core value delivered |
| `successMetrics` | string[] | Measurable success criteria |
| `constraints` | string[] | Technical or business constraints |
| `nonGoals` | string[] | Explicitly out-of-scope items |

**Downstream trace:** `capabilities[]`

**Validation:** Non-empty problem statement, mission, at least one target user, at least one success metric. Downstream capability references checked against `specs/capabilities/index.yaml`.

---

### Capability

**Kind:** `Capability` | **ID pattern:** `capability.<name>` | **Agent:** `upss-capability-map-architect`

Maps product abilities to actors and dependencies.

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Human-readable capability name |
| `description` | string | What this capability does |
| `maturity` | enum | `emerging`, `beta`, `ga`, `deprecated` |
| `actors` | string[] | Who uses this capability |
| `dependencies` | string[] | Other capabilities this depends on |
| `relatedUseCases` | string[] | Use case IDs that exercise this capability |

**Upstream trace:** `visionRefs[]`
**Downstream trace:** `useCases[]`, `architecture[]`, `testing[]`

**Validation:** Non-blank name and description. Maturity must be from allowed values. Actor and dependency arrays validated for non-empty strings.

---

### Persona

**Kind:** `Persona` | **ID pattern:** `persona.<name>` | **Agent:** `upss-persona-role-architect`

Defines actors with trust boundaries and permissions.

| Field | Type | Description |
|-------|------|-------------|
| `actorType` | enum | `user`, `administrator`, `external_system`, `automated_agent` |
| `roleName` | string | Human-readable role name |
| `description` | string | Role description |
| `permissions` | object | `allowed[]` and `denied[]` permission lists |
| `trustBoundaries` | object[] | Trust zones with `name` and `accessLevel` |
| `responsibilities` | string[] | What this actor is responsible for |

**Trust boundary access levels:** `standard`, `elevated`, `restricted`, `read_only`

**Downstream trace:** `capabilities[]`, `policies[]`, `security[]`, `useCases[]`

**Validation:** Valid actor type. Non-blank role name. Trust boundary access levels from allowed set.

---

## Workflow Layer

### Use Case

**Kind:** `UseCase` | **ID pattern:** `usecase.<name>` | **Agent:** `upss-usecase-architect`

Operationalizes capabilities into actor-driven workflows.

| Field | Type | Description |
|-------|------|-------------|
| `actor` | string | Primary actor performing this use case |
| `capabilityRef` | string | Capability ID this use case exercises |
| `preconditions` | string[] | Required state before execution |
| `workflowSteps` | string[] | Ordered steps in the workflow |
| `expectedOutcomes` | string[] | What success looks like |
| `failureScenarios` | string[] | Known failure modes |

**Downstream trace:** `behavior[]`, `testing[]`, `interfaces[]`

**Validation:** Non-blank actor. Capability reference must match a known capability ID. Non-empty workflow steps and expected outcomes.

---

### Behavior

**Kind:** `Behavior` | **ID pattern:** `behavior.<name>` | **Agent:** `upss-behavioral-spec-architect`

Executable BDD scenarios and state machines tied to use cases.

| Field | Type | Description |
|-------|------|-------------|
| `useCaseRef` | string | Use case ID this behavior implements |
| `bddScenarios` | object[] | BDD scenarios (see below) |
| `stateMachine` | object | State machine definition (see below) |
| `assertions` | string[] | Behavioral assertions for test generation |

**BDD scenario fields:**

| Field | Type | Description |
|-------|------|-------------|
| `title` | string | Scenario name |
| `given` | string[] | Preconditions |
| `when` | string[] | Actions |
| `then` | string[] | Expected outcomes |

**State machine fields:**

| Field | Type | Description |
|-------|------|-------------|
| `initialState` | string | Starting state |
| `states` | object[] | State definitions (`id`, optional `terminal: true`) |
| `transitions` | object[] | Transitions (`from`, `to`, `on`, optional `guards[]`, `actions[]`) |

**Downstream trace:** `testing[]`, `interfaces[]`, `code[]`

**Validation:** Use case reference checked against use case index. Each BDD scenario must have non-empty given, when, and then arrays. State machine initial state must exist in states list. All transition `from` and `to` values must reference defined states. State IDs must be unique.

---

### ADR

**Kind:** `Adr` | **ID pattern:** `adr.<name>` | **Agent:** `upss-adr-curator`

Architecture Decision Records capturing decisions with full context.

| Field | Type | Description |
|-------|------|-------------|
| `date` | string | Decision date (YYYY-MM-DD) |
| `context` | string | Why this decision was needed |
| `decision` | string | What was decided |
| `alternatives` | object[] | Options considered (see below) |
| `consequences` | string[] | Impact of this decision |
| `supersedes` | string[] | ADR IDs this replaces |
| `conflictsWith` | string[] | ADR IDs this conflicts with |

**Alternative fields:**

| Field | Type | Description |
|-------|------|-------------|
| `title` | string | Option name |
| `description` | string | Option description |
| `pros` | string[] | Advantages |
| `cons` | string[] | Disadvantages |

**Downstream trace:** `implementation[]`, `governance[]`

**Validation:** Non-blank context and decision. Valid date format. At least one alternative with non-empty pros and cons. Supersedes and conflicts references must match ADR ID patterns.

---

## Structure Layer

### Architecture

**Kind:** `Architecture` | **ID pattern:** `architecture.<name>` | **Agent:** `upss-architecture-c4-architect`

C4-compatible system decomposition with dependency rules.

| Field | Type | Description |
|-------|------|-------------|
| `architectureStyle` | enum | `layered`, `microservices`, `event-driven`, `modular-monolith`, `hexagonal` |
| `systems` | object[] | System boundaries (`name`, `description`, `type`) |
| `containers` | object[] | Deployable units (`name`, `technology`, `system`) |
| `components` | object[] | Internal components (`name`, `container`, `responsibility`) |
| `dependencyRules` | object[] | Allowed/denied dependencies (`from`, `to`, `allowed`) |

**Downstream trace:** `deployment[]`, `security[]`, `operations[]`

**Validation:** Architecture style from allowed set. Non-blank system, container, and component names. Dependency rule `from` and `to` must reference defined systems, containers, or components.

---

### Domain

**Kind:** `Domain` | **ID pattern:** `domain.<name>` | **Agent:** `upss-domain-model-architect`

Domain-driven design models for bounded contexts.

| Field | Type | Description |
|-------|------|-------------|
| `boundedContext` | string | The bounded context this domain belongs to |
| `entities` | object[] | Domain entities (`name`, `description`, `properties[]`) |
| `valueObjects` | object[] | Value objects (`name`, `description`, `properties[]`) |
| `aggregates` | object[] | Aggregates (`name`, `rootEntity`, `members[]`) |
| `invariants` | string[] | Business rules that must always hold |

**Downstream trace:** `data[]`, `interfaces[]`, `architecture[]`

**Validation:** Non-blank bounded context. Entity and value object names must be unique. Aggregate root entity must reference a defined entity. At least one invariant.

---

### Interface

**Kind:** `Interface` | **ID pattern:** `interface.<name>` | **Agent:** `upss-interface-contract-architect`

API contracts for inter-service and external communication.

| Field | Type | Description |
|-------|------|-------------|
| `interfaceType` | enum | `rest`, `graphql`, `grpc`, `event`, `websocket` |
| `operations` | object[] | API operations (`name`, `method`, `path`, `description`) |
| `messageSchemas` | object[] | Message formats (`name`, `format`, `description`) |
| `compatibilityRules` | string[] | Versioning and backward-compatibility constraints |

**Downstream trace:** `code[]`, `testing[]`, `deployment[]`

**Validation:** Interface type from allowed set. Non-blank operation names and paths. At least one operation or message schema.

---

### Data

**Kind:** `Data` | **ID pattern:** `data.<name>` | **Agent:** `upss-data-spec-architect`

Logical data models, migrations, and persistence constraints.

| Field | Type | Description |
|-------|------|-------------|
| `modelType` | enum | `relational`, `document`, `event`, `graph` |
| `schemas` | object[] | Data schemas (`name`, `description`, `fields[]`) |
| `migrations` | object[] | Schema migrations (`version`, `description`, `reversible`) |
| `indexes` | object[] | Database indexes (`name`, `table`, `columns[]`) |
| `constraints` | string[] | Integrity constraints |

**Downstream trace:** `deployment[]`, `operations[]`, `testing[]`

**Validation:** Model type from allowed set. Non-blank schema names. Migration versions must be unique and ordered. Index columns must reference defined schema fields.

---

## Operations Layer

### Deployment

**Kind:** `Deployment` | **ID pattern:** `deployment.<name>` | **Agent:** `upss-deployment-topology-agent`

Environments, CI/CD pipelines, promotion gates, and rollback strategies.

| Field | Type | Description |
|-------|------|-------------|
| `environments` | object[] | Deployment targets (see below) |
| `pipelineStages` | object[] | CI/CD stages (`name`, `order`, `automated`) |
| `promotionGates` | object[] | Promotion criteria (`fromEnv`, `toEnv`, `criteria[]`) |
| `infrastructureRefs` | string[] | Infrastructure artifact references |
| `rollbackStrategy` | enum | `blue-green`, `canary`, `rolling`, `recreate`, `manual` |

**Environment fields:**

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Environment name |
| `type` | enum | `development`, `staging`, `production`, `dr` |
| `region` | string | Deployment region |

**Downstream trace:** `operations[]`, `observability[]`

**Validation:** At least one environment. Environment types from allowed set. Pipeline stages must have unique names and sequential order. Rollback strategy from allowed set.

---

### Operations

**Kind:** `Operations` | **ID pattern:** `operations.<name>` | **Agent:** `upss-operations-runbook-agent`

Operational runbooks, incident response, and escalation policies.

| Field | Type | Description |
|-------|------|-------------|
| `service` | string | Service this operations spec covers |
| `runbooks` | object[] | Operational runbooks (see below) |
| `incidentLevels` | object[] | Severity definitions (`level`, `description`, `responseTime`) |
| `responseSlos` | object[] | Response SLOs (`level`, `acknowledgeWithin`, `resolveWithin`) |
| `escalationPaths` | object[] | Escalation chains (`level`, `contacts[]`) |

**Runbook fields:**

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Runbook name |
| `description` | string | When to use this runbook |
| `triggerCondition` | string | What triggers this runbook |
| `steps` | string[] | Ordered resolution steps |

**Incident levels:** `sev1`, `sev2`, `sev3`, `sev4`

**Downstream trace:** `governance[]`, `audits[]`

**Validation:** Non-blank service name. At least one runbook with non-empty steps. Incident levels from allowed set. Escalation paths must reference defined incident levels.

---

### Observability

**Kind:** `Observability` | **ID pattern:** `observability.<name>` | **Agent:** `upss-observability-agent`

Metrics, logging, distributed tracing, and alerting requirements.

| Field | Type | Description |
|-------|------|-------------|
| `serviceRefs` | string[] | Services this observability spec covers |
| `metrics` | object[] | Metric definitions (`name`, `type`, `description`) |
| `logs` | object[] | Log requirements (`name`, `level`, `format`) |
| `traces` | object[] | Trace span definitions (`name`, `spanKind`, `attributes[]`) |
| `alerts` | object[] | Alert definitions (`name`, `condition`, `severity`) |

**Metric types:** `counter`, `gauge`, `histogram`, `summary`

**Log levels:** `debug`, `info`, `warning`, `error`, `critical`

**Span kinds:** `server`, `client`, `producer`, `consumer`, `internal`

**Alert severities:** `critical`, `warning`, `info`

**Downstream trace:** `operations[]`, `governance[]`

**Validation:** At least one service reference. Metric types, log levels, span kinds, and alert severities from allowed sets. Non-blank names on all items.

---

### Security

**Kind:** `Security` | **ID pattern:** `security.<name>` | **Agent:** `upss-security-architecture-agent`

Authentication, authorization, trust zones, threat modeling, and security controls.

| Field | Type | Description |
|-------|------|-------------|
| `authnModel` | enum | `oauth2`, `oidc`, `api-key`, `mtls`, `saml`, `none` |
| `authzModel` | enum | `rbac`, `abac`, `pbac`, `acl`, `none` |
| `trustZones` | object[] | Security zones (`name`, `level`) |
| `threats` | object[] | Threat model entries (see below) |
| `controls` | object[] | Security controls (`id`, `description`, `type`) |
| `residualRisks` | object[] | Accepted risks (`threatId`, `justification`) |

**Trust zone levels:** `public`, `dmz`, `internal`, `restricted`

**Threat fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Threat identifier |
| `description` | string | Threat description |
| `severity` | enum | `critical`, `high`, `medium`, `low` |
| `mitigatedBy` | string[] | Control IDs that mitigate this threat |

**Control types:** `preventive`, `detective`, `corrective`

**Downstream trace:** `deployment[]`, `operations[]`, `testing[]`

**Validation:** AuthN and AuthZ models from allowed sets. Trust zone levels from allowed set. Threat severity and control types from allowed sets. Residual risk `threatId` must reference a defined threat.

---

## Governance Layer

### Quality

**Kind:** `Quality` | **ID pattern:** `quality.<name>` | **Agent:** `upss-quality-nfr-agent`

Non-functional requirements with SLO/SLI targets, error budgets, and scalability expectations.

| Field | Type | Description |
|-------|------|-------------|
| `category` | enum | `performance`, `availability`, `reliability`, `scalability`, `security` |
| `slos` | object[] | Service Level Objectives (`name`, `target`, `description`) |
| `slis` | object[] | Service Level Indicators (`name`, `metric`, `unit`) |
| `errorBudgets` | object[] | Error budgets (`sloRef`, `budget`, `window`) |
| `scalabilityExpectations` | object[] | Scale targets (`dimension`, `current`, `target`) |

**Downstream trace:** `testing[]`, `observability[]`, `operations[]`

**Validation:** Category from allowed set. At least one SLO with non-blank name and target. SLI names must be non-blank. Error budget `sloRef` must reference a defined SLO.

---

### Testing

**Kind:** `Testing` | **ID pattern:** `testing.<name>` | **Agent:** `upss-testing-strategy-agent`

Verification strategies tied to behavioral specs and quality targets.

| Field | Type | Description |
|-------|------|-------------|
| `verificationLevels` | string[] | Testing levels to apply |
| `behaviorRefs` | string[] | Behavior spec IDs to verify |
| `qualityRefs` | string[] | Quality spec IDs to verify |
| `coverageTargets` | object[] | Coverage requirements (`scope`, `target`, optional `metric`) |
| `generationRules` | object[] | Test generation strategy (`source`, `strategy`) |

**Verification levels:** `unit`, `integration`, `acceptance`, `performance`, `security`, `e2e`

**Generation strategies:** `generated`, `manual`, `hybrid`

**Downstream trace:** `ci[]`, `release[]`

**Validation:** Verification levels from allowed set. Behavior and quality references must match ID patterns. Coverage targets must have non-blank scope and target. Generation strategies from allowed set.

---

### Policy

**Kind:** `Policy` | **ID pattern:** `policy.<name>` | **Agent:** `upss-policy-rules-architect`

Security, compliance, quality, and operational policy constraints.

| Field | Type | Description |
|-------|------|-------------|
| `policyType` | enum | `security`, `compliance`, `quality`, `operational` |
| `severity` | enum | `critical`, `high`, `medium`, `low` |
| `scope` | string[] | What this policy applies to |
| `rules` | object[] | Policy rules (`id`, `description`, `expression`) |
| `exceptions` | object[] | Granted exceptions (`ruleId`, `reason`, `expiresAt`) |
| `enforcement` | object | Enforcement configuration (`mode`) |

**Enforcement modes:** `enforce`, `warn`, `audit`

**Downstream trace:** `ci[]`, `enforcement[]`, `operations[]`

**Validation:** Policy type and severity from allowed sets. At least one rule with non-blank id and description. Exception `ruleId` must reference a defined rule. Enforcement mode from allowed set.

---

### Governance

**Kind:** `Governance` | **ID pattern:** `governance.<name>` | **Agent:** `upss-governance-agent`

Ownership models, change processes, approval gates, and release policies.

| Field | Type | Description |
|-------|------|-------------|
| `ownershipModel` | enum | `codeowners`, `team-based`, `individual`, `shared` |
| `changeProcess` | object[] | Change management workflows (see below) |
| `approvalGates` | object[] | Approval requirements (see below) |
| `releasePolicy` | object | Release configuration (see below) |
| `auditRequirements` | string[] | Audit trail requirements |

**Change process fields:**

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Process name |
| `description` | string | Process description |
| `requiredApprovals` | integer | Number of approvals needed |

**Approval gate fields:**

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Gate name |
| `type` | enum | `automated`, `manual`, `hybrid` |
| `criteria` | string[] | Gate pass criteria |

**Release policy fields:**

| Field | Type | Description |
|-------|------|-------------|
| `cadence` | enum | `continuous`, `scheduled`, `manual` |
| `branchStrategy` | string | Branching model |
| `hotfixProcess` | string | Emergency fix process |

**Downstream trace:** References to any spec type (governance spans the entire system)

**Validation:** Ownership model from allowed set. At least one change process. Approval gate types from allowed set. Release cadence from allowed set. At least one audit requirement.

---

## Quick reference

| Spec | Kind | ID Prefix | Agent |
|------|------|-----------|-------|
| Vision | `Vision` | `vision.` | `upss-vision-architect` |
| Capability | `Capability` | `capability.` | `upss-capability-map-architect` |
| Persona | `Persona` | `persona.` | `upss-persona-role-architect` |
| Use Case | `UseCase` | `usecase.` | `upss-usecase-architect` |
| Behavior | `Behavior` | `behavior.` | `upss-behavioral-spec-architect` |
| ADR | `Adr` | `adr.` | `upss-adr-curator` |
| Architecture | `Architecture` | `architecture.` | `upss-architecture-c4-architect` |
| Domain | `Domain` | `domain.` | `upss-domain-model-architect` |
| Interface | `Interface` | `interface.` | `upss-interface-contract-architect` |
| Data | `Data` | `data.` | `upss-data-spec-architect` |
| Deployment | `Deployment` | `deployment.` | `upss-deployment-topology-agent` |
| Operations | `Operations` | `operations.` | `upss-operations-runbook-agent` |
| Observability | `Observability` | `observability.` | `upss-observability-agent` |
| Security | `Security` | `security.` | `upss-security-architecture-agent` |
| Quality | `Quality` | `quality.` | `upss-quality-nfr-agent` |
| Testing | `Testing` | `testing.` | `upss-testing-strategy-agent` |
| Policy | `Policy` | `policy.` | `upss-policy-rules-architect` |
| Governance | `Governance` | `governance.` | `upss-governance-agent` |
