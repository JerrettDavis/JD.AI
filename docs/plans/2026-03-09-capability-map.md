# UPSS Capability Map Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create 22 capability specs covering all JD.AI subsystems, update the capability index, and update the vision downstream refs.

**Architecture:** Each capability is a standalone YAML file in `specs/capabilities/` following the `capabilities.schema.json` contract. All capabilities trace to `vision.jdai.product`. Dependencies between capabilities are declared explicitly and validated against the capability index. The existing `CapabilitySpecificationValidator` in CI validates all specs on every build.

**Tech Stack:** YAML (UPSS v1 schema), existing C# spec validators (no code changes needed)

---

## Capability Inventory (22 specs)

| # | ID | Category | File |
|---|---|---|---|
| 1 | capability.agent-conversation | Core Runtime | agent-conversation.yaml |
| 2 | capability.session-management | Core Runtime | session-management.yaml |
| 3 | capability.context-transformation | Core Runtime | context-transformation.yaml |
| 4 | capability.provider-management | AI Infrastructure | provider-management.yaml |
| 5 | capability.model-selection | AI Infrastructure | model-selection.yaml |
| 6 | capability.prompt-execution | AI Infrastructure | prompt-execution.yaml |
| 7 | capability.tool-execution | Tool Ecosystem | tool-execution.yaml |
| 8 | capability.tool-registry | Tool Ecosystem | tool-registry.yaml |
| 9 | capability.tool-loadouts | Tool Ecosystem | tool-loadouts.yaml |
| 10 | capability.workflow-authoring | Automation | workflow-authoring.yaml |
| 11 | capability.workflow-execution | Automation | workflow-execution.yaml |
| 12 | capability.subagents | Orchestration | subagents.yaml |
| 13 | capability.team-orchestration | Orchestration | team-orchestration.yaml |
| 14 | capability.mcp-integration | Integrations | mcp-integration.yaml |
| 15 | capability.plugin-system | Integrations | plugin-system.yaml |
| 16 | capability.multi-channel-interaction | Communication | multi-channel-interaction.yaml |
| 17 | capability.messaging-adapters | Communication | messaging-adapters.yaml |
| 18 | capability.gateway-control-plane | Platform | gateway-control-plane.yaml |
| 19 | capability.dashboard-ui | Platform | dashboard-ui.yaml |
| 20 | capability.telemetry | Platform | telemetry.yaml |
| 21 | capability.governance | Cross-cutting | governance.yaml |
| 22 | capability.memory | Cross-cutting | memory.yaml |

## Validation Rules (from CapabilitySpecificationValidator)

Every spec must pass these checks:
- `apiVersion` = `jdai.upss/v1`, `kind` = `Capability`
- `id` matches `^capability\.[a-z0-9]+(?:[.-][a-z0-9]+)*$`
- `version` >= 1, `status` in {draft, active, deprecated, retired}
- `metadata.owners` non-empty, `metadata.reviewers` non-empty
- `metadata.lastReviewed` is valid ISO-8601 date
- `maturity` in {emerging, beta, ga, deprecated}
- `actors[]` each matches `^persona\.[a-z0-9]+(?:[.-][a-z0-9]+)*$`
- `dependencies[]` each matches capability ID pattern AND exists in index
- `trace.visionRefs[]` each exists in `specs/vision/index.yaml`
- `trace.upstream[]` each resolves to a real repo file
- `trace.downstream.architecture[]` each resolves to a real repo file
- `trace.downstream.testing[]` each resolves to a real repo file
- `relatedUseCases[]` left empty (no usecase index exists yet)
- Index entry `id` and `status` must match the spec file

## Available Persona IDs

- `persona.terminal-operator`
- `persona.platform-engineer`
- `persona.workflow-author`
- `persona.tool-developer`
- `persona.governance-admin`
- `persona.channel-adapter`
- `persona.autonomous-agent`

---

### Task 1: Create feature branch

**Step 1: Create and checkout the feature branch**

Run: `git checkout -b feat/325-upss-capability-map`

Expected: Switched to new branch `feat/325-upss-capability-map`

---

### Task 2: Create Core Runtime capabilities (3 files)

**Files:**
- Create: `specs/capabilities/agent-conversation.yaml`
- Create: `specs/capabilities/session-management.yaml`
- Create: `specs/capabilities/context-transformation.yaml`

**Step 1: Create agent-conversation.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.agent-conversation
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define agent conversation capability as part of the canonical capability map (#325).
name: Agent Conversation
description: >-
  Manage interactive agent conversations including the agent loop, tool calling,
  streaming responses, fallback handling, and fork-point checkpointing.
maturity: beta
actors:
  - persona.terminal-operator
  - persona.autonomous-agent
dependencies: []
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Tests/Agents/AgentLoopTextToolCallTests.cs
```

**Step 2: Create session-management.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.session-management
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define session management capability as part of the canonical capability map (#325).
name: Session Management
description: >-
  Persist, retrieve, replay, and verify integrity of agent sessions including
  session records, export, and checkpointing strategies.
maturity: beta
actors:
  - persona.terminal-operator
  - persona.platform-engineer
dependencies:
  - capability.agent-conversation
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Tests/Sessions/SessionRecordTests.cs
```

**Step 3: Create context-transformation.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.context-transformation
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define context transformation capability as part of the canonical capability map (#325).
name: Context Transformation
description: >-
  Transform, enrich, and restructure conversation context before prompt execution
  including instruction loading, agent definition resolution, and message shaping.
maturity: beta
actors:
  - persona.terminal-operator
  - persona.autonomous-agent
dependencies:
  - capability.agent-conversation
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Tests/Agents/ConversationTransformerTests.cs
```

**Step 4: Commit**

```bash
git add specs/capabilities/agent-conversation.yaml specs/capabilities/session-management.yaml specs/capabilities/context-transformation.yaml
git commit -m "feat(specs): add Core Runtime capability specs (#325)"
```

---

### Task 3: Create AI Infrastructure capabilities (3 files)

**Files:**
- Create: `specs/capabilities/provider-management.yaml`
- Create: `specs/capabilities/model-selection.yaml`
- Create: `specs/capabilities/prompt-execution.yaml`

**Step 1: Create provider-management.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.provider-management
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define provider management capability as part of the canonical capability map (#325).
name: Provider Management
description: >-
  Register, detect, and manage AI provider backends including cloud APIs,
  local models, and credential lifecycle with encrypted storage and vault integration.
maturity: beta
actors:
  - persona.platform-engineer
  - persona.terminal-operator
dependencies: []
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Tests/Providers/ApiKeyDetectorTests.cs
```

**Step 2: Create model-selection.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.model-selection
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define model selection capability as part of the canonical capability map (#325).
name: Model Selection
description: >-
  Discover model capabilities, search across providers, and select optimal models
  based on metadata, pricing, context window, and feature support.
maturity: beta
actors:
  - persona.terminal-operator
  - persona.platform-engineer
dependencies:
  - capability.provider-management
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Tests/Providers/Metadata/ModelMetadataProviderTests.cs
```

**Step 3: Create prompt-execution.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.prompt-execution
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define prompt execution capability as part of the canonical capability map (#325).
name: Prompt Execution
description: >-
  Execute prompts against AI providers with streaming, prompt caching,
  token budgeting, and provider-agnostic request/response handling.
maturity: beta
actors:
  - persona.terminal-operator
  - persona.autonomous-agent
dependencies:
  - capability.provider-management
  - capability.model-selection
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Tests/Providers/AnthropicPromptCachingChatClientTests.cs
```

**Step 4: Commit**

```bash
git add specs/capabilities/provider-management.yaml specs/capabilities/model-selection.yaml specs/capabilities/prompt-execution.yaml
git commit -m "feat(specs): add AI Infrastructure capability specs (#325)"
```

---

### Task 4: Create Tool Ecosystem capabilities (3 files)

**Files:**
- Create: `specs/capabilities/tool-execution.yaml`
- Create: `specs/capabilities/tool-registry.yaml`
- Create: `specs/capabilities/tool-loadouts.yaml`

**Step 1: Create tool-execution.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.tool-execution
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define tool execution capability as part of the canonical capability map (#325).
name: Tool Execution
description: >-
  Invoke tools within agent conversations including built-in tool suites,
  process execution, file operations, git integration, and MCP tool proxying.
maturity: beta
actors:
  - persona.autonomous-agent
  - persona.terminal-operator
dependencies:
  - capability.tool-registry
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Tests/Agents/AgentLoopTextToolExecutionTests.cs
```

**Step 2: Create tool-registry.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.tool-registry
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define tool registry capability as part of the canonical capability map (#325).
name: Tool Registry
description: >-
  Scan, register, and discover tools from assemblies, plugins, and MCP servers
  providing a unified catalog of available agent capabilities.
maturity: beta
actors:
  - persona.tool-developer
  - persona.platform-engineer
dependencies: []
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Tests/Tools/CompositeToolLoadoutRegistryTests.cs
```

**Step 3: Create tool-loadouts.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.tool-loadouts
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define tool loadouts capability as part of the canonical capability map (#325).
name: Tool Loadouts
description: >-
  Define, validate, and resolve named tool loadout profiles that scope which tools
  are available to agents in different contexts and environments.
maturity: beta
actors:
  - persona.platform-engineer
  - persona.workflow-author
dependencies:
  - capability.tool-registry
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/decisions/ADR-002-tool-loadout-yaml.md
    testing:
      - tests/JD.AI.Tests/Tools/FileToolLoadoutRegistryTests.cs
```

**Step 4: Commit**

```bash
git add specs/capabilities/tool-execution.yaml specs/capabilities/tool-registry.yaml specs/capabilities/tool-loadouts.yaml
git commit -m "feat(specs): add Tool Ecosystem capability specs (#325)"
```

---

### Task 5: Create Automation capabilities (2 files)

**Files:**
- Create: `specs/capabilities/workflow-authoring.yaml`
- Create: `specs/capabilities/workflow-execution.yaml`

**Step 1: Create workflow-authoring.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.workflow-authoring
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define workflow authoring capability as part of the canonical capability map (#325).
name: Workflow Authoring
description: >-
  Create, detect, and validate multi-step agent workflows using a fluent builder API,
  YAML-based definitions, and a file-backed workflow catalog.
maturity: beta
actors:
  - persona.workflow-author
  - persona.platform-engineer
dependencies: []
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Tests/Workflows/AgentWorkflowDetectorTests.cs
```

**Step 2: Create workflow-execution.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.workflow-execution
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define workflow execution capability as part of the canonical capability map (#325).
name: Workflow Execution
description: >-
  Execute multi-step workflows with consensus, conflict detection, distributed locking,
  run history tracking, and pluggable transports including in-memory, Redis, and Azure Service Bus.
maturity: emerging
actors:
  - persona.workflow-author
  - persona.autonomous-agent
dependencies:
  - capability.workflow-authoring
  - capability.agent-conversation
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/decisions/ADR-011-distributed-workflow-execution.md
    testing:
      - tests/JD.AI.Tests/Workflows/WorkflowConsensusTests.cs
```

**Step 3: Commit**

```bash
git add specs/capabilities/workflow-authoring.yaml specs/capabilities/workflow-execution.yaml
git commit -m "feat(specs): add Automation capability specs (#325)"
```

---

### Task 6: Create Orchestration capabilities (2 files)

**Files:**
- Create: `specs/capabilities/subagents.yaml`
- Create: `specs/capabilities/team-orchestration.yaml`

**Step 1: Create subagents.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.subagents
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define subagents capability as part of the canonical capability map (#325).
name: Subagents
description: >-
  Spawn and manage subordinate agent instances with single-turn and multi-turn executors,
  subagent prompt generation, and event-driven communication.
maturity: beta
actors:
  - persona.autonomous-agent
  - persona.workflow-author
dependencies:
  - capability.agent-conversation
  - capability.prompt-execution
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Tests/Orchestration/StrategyIntegrationTests.cs
```

**Step 2: Create team-orchestration.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.team-orchestration
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define team orchestration capability as part of the canonical capability map (#325).
name: Team Orchestration
description: >-
  Coordinate multi-agent teams using pluggable orchestration strategies including
  sequential, fan-out, pipeline, supervisor, debate, blackboard, map-reduce, relay, and voting patterns.
maturity: emerging
actors:
  - persona.workflow-author
  - persona.platform-engineer
dependencies:
  - capability.subagents
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Tests/Orchestration/CoordinationStrategyTests.cs
```

**Step 3: Commit**

```bash
git add specs/capabilities/subagents.yaml specs/capabilities/team-orchestration.yaml
git commit -m "feat(specs): add Orchestration capability specs (#325)"
```

---

### Task 7: Create Integrations capabilities (2 files)

**Files:**
- Create: `specs/capabilities/mcp-integration.yaml`
- Create: `specs/capabilities/plugin-system.yaml`

**Step 1: Create mcp-integration.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.mcp-integration
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define MCP integration capability as part of the canonical capability map (#325).
name: MCP Integration
description: >-
  Manage Model Context Protocol server lifecycle including discovery, connection,
  status tracking, and curated MCP catalog with tool proxying into the agent runtime.
maturity: beta
actors:
  - persona.tool-developer
  - persona.platform-engineer
dependencies:
  - capability.tool-registry
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Tests/Mcp/McpManagerMergeTests.cs
```

**Step 2: Create plugin-system.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.plugin-system
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define plugin system capability as part of the canonical capability map (#325).
name: Plugin System
description: >-
  Load, install, verify, and manage plugins with integrity checking, permission-scoped
  contexts, lifecycle management, and service-provider integration.
maturity: beta
actors:
  - persona.tool-developer
  - persona.platform-engineer
dependencies: []
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Tests/Plugins/PluginLoaderTests.cs
```

**Step 3: Commit**

```bash
git add specs/capabilities/mcp-integration.yaml specs/capabilities/plugin-system.yaml
git commit -m "feat(specs): add Integrations capability specs (#325)"
```

---

### Task 8: Create Communication capabilities (2 files)

**Files:**
- Create: `specs/capabilities/multi-channel-interaction.yaml`
- Create: `specs/capabilities/messaging-adapters.yaml`

**Step 1: Create multi-channel-interaction.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.multi-channel-interaction
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define multi-channel interaction capability as part of the canonical capability map (#325).
name: Multi-Channel Interaction
description: >-
  Deliver agent capabilities across multiple communication channels with a unified
  channel abstraction, routing, and feature-parity management across terminal,
  Discord, Slack, Signal, Telegram, and web interfaces.
maturity: beta
actors:
  - persona.terminal-operator
  - persona.channel-adapter
dependencies:
  - capability.agent-conversation
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Tests/Channels/ChannelRegistryTests.cs
```

**Step 2: Create messaging-adapters.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.messaging-adapters
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define messaging adapters capability as part of the canonical capability map (#325).
name: Messaging Adapters
description: >-
  Implement channel-specific adapters for Discord, Slack, Telegram, Signal, Web,
  and OpenClaw bridge providing protocol translation and platform-native interactions.
maturity: beta
actors:
  - persona.channel-adapter
  - persona.platform-engineer
dependencies:
  - capability.multi-channel-interaction
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/decisions/ADR-010-connector-sdk.md
    testing:
      - tests/JD.AI.Tests/Channels/Discord/DiscordChannelTests.cs
```

**Step 3: Commit**

```bash
git add specs/capabilities/multi-channel-interaction.yaml specs/capabilities/messaging-adapters.yaml
git commit -m "feat(specs): add Communication capability specs (#325)"
```

---

### Task 9: Create Platform capabilities (3 files)

**Files:**
- Create: `specs/capabilities/gateway-control-plane.yaml`
- Create: `specs/capabilities/dashboard-ui.yaml`
- Create: `specs/capabilities/telemetry.yaml`

**Step 1: Create gateway-control-plane.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.gateway-control-plane
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define gateway control plane capability as part of the canonical capability map (#325).
name: Gateway Control Plane
description: >-
  Expose REST API endpoints and real-time SignalR hubs for managing agents, sessions,
  providers, channels, plugins, routing, audit logs, and platform configuration
  with API key authentication and versioned routing.
maturity: beta
actors:
  - persona.platform-engineer
  - persona.terminal-operator
dependencies:
  - capability.agent-conversation
  - capability.session-management
  - capability.provider-management
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Gateway.Tests/AgentEndpointTests.cs
```

**Step 2: Create dashboard-ui.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.dashboard-ui
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define dashboard UI capability as part of the canonical capability map (#325).
name: Dashboard UI
description: >-
  Provide a Blazor WebAssembly dashboard for visualizing agent status, session history,
  provider health, channel mappings, routing configuration, and gateway status.
maturity: emerging
actors:
  - persona.platform-engineer
  - persona.terminal-operator
dependencies:
  - capability.gateway-control-plane
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Gateway.Tests/DashboardModelIntegrationTests.cs
```

**Step 3: Create telemetry.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.telemetry
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define telemetry capability as part of the canonical capability map (#325).
name: Telemetry
description: >-
  Instrument the platform with OpenTelemetry tracing, metrics, and health checks
  including GenAI-specific attributes, provider health monitoring, disk space,
  and memory utilization checks.
maturity: beta
actors:
  - persona.platform-engineer
  - persona.governance-admin
dependencies: []
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/decisions/ADR-001-otel-instrumentation.md
    testing:
      - tests/JD.AI.Tests/Telemetry/OtelInstrumentationTests.cs
```

**Step 4: Commit**

```bash
git add specs/capabilities/gateway-control-plane.yaml specs/capabilities/dashboard-ui.yaml specs/capabilities/telemetry.yaml
git commit -m "feat(specs): add Platform capability specs (#325)"
```

---

### Task 10: Create Cross-cutting capabilities (2 files)

**Files:**
- Create: `specs/capabilities/governance.yaml`
- Create: `specs/capabilities/memory.yaml`

**Step 1: Create governance.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.governance
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define governance capability as part of the canonical capability map (#325).
name: Governance
description: >-
  Enforce policies, approval workflows, budget tracking, data classification and
  redaction, RBAC role resolution, compliance profiles, and auditable event logging
  with pluggable sinks including SQLite, Elasticsearch, file, and webhook backends.
maturity: beta
actors:
  - persona.governance-admin
  - persona.platform-engineer
dependencies: []
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/decisions/ADR-003-policy-rbac.md
    testing:
      - tests/JD.AI.Tests/Governance/PolicyEvaluatorTests.cs
```

**Step 2: Create memory.yaml**

```yaml
apiVersion: jdai.upss/v1
kind: Capability
id: capability.memory
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-capability-map-architect
  lastReviewed: "2026-03-09"
  changeReason: Define memory capability as part of the canonical capability map (#325).
name: Memory
description: >-
  Store and retrieve agent knowledge using vector stores, batch embedding pipelines,
  text chunking, and semantic search with in-memory and SQLite backends.
maturity: emerging
actors:
  - persona.autonomous-agent
  - persona.terminal-operator
dependencies: []
relatedUseCases: []
trace:
  visionRefs:
    - vision.jdai.product
  upstream:
    - specs/vision/jdai-product-vision.yaml
  downstream:
    useCases: []
    architecture:
      - docs/architecture/README.md
    testing:
      - tests/JD.AI.Tests/Memory/MemoryManagerTests.cs
```

**Step 3: Commit**

```bash
git add specs/capabilities/governance.yaml specs/capabilities/memory.yaml
git commit -m "feat(specs): add Cross-cutting capability specs (#325)"
```

---

### Task 11: Update capability index

**Files:**
- Modify: `specs/capabilities/index.yaml`

**Step 1: Replace index.yaml with all 23 entries (22 new + 1 existing)**

```yaml
apiVersion: jdai.upss/v1
kind: CapabilityIndex
entries:
  # Existing
  - id: capability.spec-validation
    title: Specification Validation
    path: specs/capabilities/examples/capabilities.example.yaml
    status: draft

  # Core Runtime
  - id: capability.agent-conversation
    title: Agent Conversation
    path: specs/capabilities/agent-conversation.yaml
    status: draft
  - id: capability.session-management
    title: Session Management
    path: specs/capabilities/session-management.yaml
    status: draft
  - id: capability.context-transformation
    title: Context Transformation
    path: specs/capabilities/context-transformation.yaml
    status: draft

  # AI Infrastructure
  - id: capability.provider-management
    title: Provider Management
    path: specs/capabilities/provider-management.yaml
    status: draft
  - id: capability.model-selection
    title: Model Selection
    path: specs/capabilities/model-selection.yaml
    status: draft
  - id: capability.prompt-execution
    title: Prompt Execution
    path: specs/capabilities/prompt-execution.yaml
    status: draft

  # Tool Ecosystem
  - id: capability.tool-execution
    title: Tool Execution
    path: specs/capabilities/tool-execution.yaml
    status: draft
  - id: capability.tool-registry
    title: Tool Registry
    path: specs/capabilities/tool-registry.yaml
    status: draft
  - id: capability.tool-loadouts
    title: Tool Loadouts
    path: specs/capabilities/tool-loadouts.yaml
    status: draft

  # Automation
  - id: capability.workflow-authoring
    title: Workflow Authoring
    path: specs/capabilities/workflow-authoring.yaml
    status: draft
  - id: capability.workflow-execution
    title: Workflow Execution
    path: specs/capabilities/workflow-execution.yaml
    status: draft

  # Orchestration
  - id: capability.subagents
    title: Subagents
    path: specs/capabilities/subagents.yaml
    status: draft
  - id: capability.team-orchestration
    title: Team Orchestration
    path: specs/capabilities/team-orchestration.yaml
    status: draft

  # Integrations
  - id: capability.mcp-integration
    title: MCP Integration
    path: specs/capabilities/mcp-integration.yaml
    status: draft
  - id: capability.plugin-system
    title: Plugin System
    path: specs/capabilities/plugin-system.yaml
    status: draft

  # Communication
  - id: capability.multi-channel-interaction
    title: Multi-Channel Interaction
    path: specs/capabilities/multi-channel-interaction.yaml
    status: draft
  - id: capability.messaging-adapters
    title: Messaging Adapters
    path: specs/capabilities/messaging-adapters.yaml
    status: draft

  # Platform
  - id: capability.gateway-control-plane
    title: Gateway Control Plane
    path: specs/capabilities/gateway-control-plane.yaml
    status: draft
  - id: capability.dashboard-ui
    title: Dashboard UI
    path: specs/capabilities/dashboard-ui.yaml
    status: draft
  - id: capability.telemetry
    title: Telemetry
    path: specs/capabilities/telemetry.yaml
    status: draft

  # Cross-cutting
  - id: capability.governance
    title: Governance
    path: specs/capabilities/governance.yaml
    status: draft
  - id: capability.memory
    title: Memory
    path: specs/capabilities/memory.yaml
    status: draft
```

**Step 2: Commit**

```bash
git add specs/capabilities/index.yaml
git commit -m "feat(specs): update capability index with all 22 new entries (#325)"
```

---

### Task 12: Update vision downstream capabilities

**Files:**
- Modify: `specs/vision/jdai-product-vision.yaml` (line 111)

**Step 1: Replace the empty downstream capabilities list with all 23 capability IDs**

Change `capabilities: []` at the end of the file to:

```yaml
    capabilities:
      - capability.spec-validation
      - capability.agent-conversation
      - capability.session-management
      - capability.context-transformation
      - capability.provider-management
      - capability.model-selection
      - capability.prompt-execution
      - capability.tool-execution
      - capability.tool-registry
      - capability.tool-loadouts
      - capability.workflow-authoring
      - capability.workflow-execution
      - capability.subagents
      - capability.team-orchestration
      - capability.mcp-integration
      - capability.plugin-system
      - capability.multi-channel-interaction
      - capability.messaging-adapters
      - capability.gateway-control-plane
      - capability.dashboard-ui
      - capability.telemetry
      - capability.governance
      - capability.memory
```

**Step 2: Commit**

```bash
git add specs/vision/jdai-product-vision.yaml
git commit -m "feat(specs): link vision to all capabilities in downstream refs (#325)"
```

---

### Task 13: Run validation

**Step 1: Run the capability specification repository tests**

Run: `dotnet test tests/JD.AI.Tests --filter "FullyQualifiedName~CapabilitySpecification" --no-build -v minimal`

Expected: All tests pass (3 tests in RepositoryTests + 5 in ValidatorTests)

If build is needed first:

Run: `dotnet build JD.AI.slnx`

Then re-run the tests.

**Step 2: Run the vision specification tests too (to verify downstream update)**

Run: `dotnet test tests/JD.AI.Tests --filter "FullyQualifiedName~VisionSpecification" --no-build -v minimal`

Expected: All pass

**Step 3: Fix any validation errors and re-commit**

If errors occur, fix the offending YAML and amend the relevant commit.

---

### Task 14: Final commit and push

**Step 1: Push the branch**

Run: `git push -u origin feat/325-upss-capability-map`

---

## Dependency Graph

```
provider-management
├── model-selection
│   └── prompt-execution ← (also depends on provider-management)
│
agent-conversation
├── session-management
├── context-transformation
├── multi-channel-interaction
│   └── messaging-adapters
├── workflow-execution ← (also depends on workflow-authoring)
├── subagents ← (also depends on prompt-execution)
│   └── team-orchestration
└── gateway-control-plane ← (also depends on session-management, provider-management)
    └── dashboard-ui

tool-registry
├── tool-execution
├── tool-loadouts
└── mcp-integration

workflow-authoring → workflow-execution

(Independent roots: telemetry, governance, memory, plugin-system)
```
