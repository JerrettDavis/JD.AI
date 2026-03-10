# Core Agent Conversation and Session Lifecycle — UPSS Specs

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create the full UPSS specification stack (UseCase, Behavior, Architecture, Testing, Quality) for the core agent conversation and session lifecycle subsystem (issue #326).

**Architecture:** Each spec layer traces to the one above it: capabilities → use cases → behaviors → testing. Quality and architecture specs trace directly to the vision/capabilities. All specs are YAML files validated by existing C# validators and JSON schemas. No code changes — only YAML spec files and index updates.

**Tech Stack:** YAML (UPSS specs), .NET 10 / xUnit (validation tests)

**Traceability chain:**
```
vision.jdai.product
  ├── capability.agent-conversation ──┐
  ├── capability.session-management ──┼── usecases ── behaviors ── testing
  └── capability.context-transformation ┘
                                       └── quality (NFR, traces to capabilities)
                                       └── architecture (structural, traces to capabilities)
```

**Spec inventory (17 files):**

| Layer | ID | File |
|---|---|---|
| UseCase | `usecase.start-session` | `specs/usecases/start-session.yaml` |
| UseCase | `usecase.resume-session` | `specs/usecases/resume-session.yaml` |
| UseCase | `usecase.transform-context` | `specs/usecases/transform-context.yaml` |
| UseCase | `usecase.execute-slash-command` | `specs/usecases/execute-slash-command.yaml` |
| UseCase | `usecase.recover-interrupted-conversation` | `specs/usecases/recover-interrupted-conversation.yaml` |
| Behavior | `behavior.session-persistence` | `specs/behavior/session-persistence.yaml` |
| Behavior | `behavior.streaming-responses` | `specs/behavior/streaming-responses.yaml` |
| Behavior | `behavior.slash-command-routing` | `specs/behavior/slash-command-routing.yaml` |
| Behavior | `behavior.context-transformation` | `specs/behavior/context-transformation.yaml` |
| Architecture | `architecture.session-lifecycle` | `specs/architecture/session-lifecycle.yaml` |
| Testing | `testing.session-lifecycle` | `specs/testing/session-lifecycle.yaml` |
| Testing | `testing.agent-conversation` | `specs/testing/agent-conversation.yaml` |
| Quality | `quality.session-reliability` | `specs/quality/session-reliability.yaml` |
| Quality | `quality.streaming-latency` | `specs/quality/streaming-latency.yaml` |
| Index | — | `specs/usecases/index.yaml` (modify) |
| Index | — | `specs/behavior/index.yaml` (modify) |
| Index | — | `specs/architecture/index.yaml` (modify) |
| Index | — | `specs/testing/index.yaml` (modify) |
| Index | — | `specs/quality/index.yaml` (modify) |

**Validation command:**
```bash
dotnet test tests/JD.AI.Tests/ --filter "FullyQualifiedName~UseCaseSpecification|FullyQualifiedName~BehaviorSpecification|FullyQualifiedName~ArchitectureSpecification|FullyQualifiedName~TestingSpecification|FullyQualifiedName~QualitySpecification"
```

---

## Task 1: Create 5 UseCase specs

**Files:**
- Create: `specs/usecases/start-session.yaml`
- Create: `specs/usecases/resume-session.yaml`
- Create: `specs/usecases/transform-context.yaml`
- Create: `specs/usecases/execute-slash-command.yaml`
- Create: `specs/usecases/recover-interrupted-conversation.yaml`
- Modify: `specs/usecases/index.yaml`

**Context:** Each use case traces upstream to a capability spec and downstream to behavior specs (created in Task 2) and testing specs (created in Task 5). The `actor` field must reference a valid persona ID from `specs/personas/index.yaml`. The `capabilityRef` must reference a valid capability ID from `specs/capabilities/index.yaml`. All `trace.downstream` paths must reference files that exist on disk.

**Reference format (from existing spec `specs/usecases/provider-tool-calling-execution.yaml`):**
```yaml
apiVersion: jdai.upss/v1
kind: UseCase
id: usecase.<name>
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-usecase-architect
  lastReviewed: "2026-03-10"
  changeReason: <why this spec exists, reference #326>
actor: persona.<id>
capabilityRef: capability.<id>
preconditions:
  - <at least one>
workflowSteps:
  - <at least one>
expectedOutcomes:
  - <at least one>
failureScenarios:
  - <at least one>
trace:
  upstream:
    - specs/capabilities/<capability>.yaml   # must exist on disk
  downstream:
    behavior:
      - specs/behavior/<behavior>.yaml       # must exist on disk
    testing:
      - specs/testing/<testing>.yaml         # must exist on disk
    interfaces: []
```

**Spec details:**

### usecase.start-session
- **actor:** `persona.terminal-operator`
- **capabilityRef:** `capability.session-management`
- **preconditions:** The operator launches the JD.AI CLI in a project directory. The SQLite session database is accessible at `~/.jdai/sessions.db`.
- **workflowSteps:** (1) CLI parses arguments and resolves project path. (2) `SessionConfigurator` creates an `AgentSession` with a new `SessionInfo`. (3) `SessionStore` persists the session row with project hash, model, and timestamps. (4) The interactive loop begins accepting user input.
- **expectedOutcomes:** A new session record exists in SQLite with `is_active=1`. The session ID (16-char hex) is available for resume.
- **failureScenarios:** SQLite database is locked or corrupted, preventing session creation. Project path cannot be resolved.
- **trace.upstream:** `specs/capabilities/session-management.yaml`
- **trace.downstream.behavior:** `specs/behavior/session-persistence.yaml`
- **trace.downstream.testing:** `specs/testing/session-lifecycle.yaml`

### usecase.resume-session
- **actor:** `persona.terminal-operator`
- **capabilityRef:** `capability.session-management`
- **preconditions:** A prior session exists in `SessionStore` for the current project hash. The operator supplies a `--resume <id>` or `--continue` flag.
- **workflowSteps:** (1) CLI resolves the session ID (explicit or most-recent via `--continue`). (2) `SessionStore.GetSessionAsync` loads the full session tree (turns, tool calls, file touches). (3) `ChatHistory` is rebuilt from persisted turns. (4) Model switch history and fork points are restored. (5) The interactive loop resumes.
- **expectedOutcomes:** Conversation context is restored to the state at session close. The operator can continue the conversation without re-explaining context.
- **failureScenarios:** Session ID does not exist. Session data is corrupted or incomplete. The model used in the prior session is no longer available.
- **trace.upstream:** `specs/capabilities/session-management.yaml`
- **trace.downstream.behavior:** `specs/behavior/session-persistence.yaml`
- **trace.downstream.testing:** `specs/testing/session-lifecycle.yaml`

### usecase.transform-context
- **actor:** `persona.terminal-operator`
- **capabilityRef:** `capability.context-transformation`
- **preconditions:** An active session exists with conversation history. The operator triggers a provider or model switch.
- **workflowSteps:** (1) The operator selects a new provider/model via `/model` command or API. (2) `ConversationTransformer` is invoked with the selected `SwitchMode` (Preserve, Compact, Transform, Fresh). (3) For Compact/Transform modes, the current LLM summarizes or creates a handoff briefing. (4) `ChatHistory` is replaced with the transformed context. (5) The kernel is rebuilt for the new provider.
- **expectedOutcomes:** Context is adapted to the new model's capabilities. For Transform mode, key decisions, file paths, and pending work are preserved in a briefing.
- **failureScenarios:** The summarization LLM call fails. The operator cancels the switch (Cancel mode).
- **trace.upstream:** `specs/capabilities/context-transformation.yaml`
- **trace.downstream.behavior:** `specs/behavior/context-transformation.yaml`
- **trace.downstream.testing:** `specs/testing/agent-conversation.yaml`

### usecase.execute-slash-command
- **actor:** `persona.terminal-operator`
- **capabilityRef:** `capability.agent-conversation`
- **preconditions:** An active session exists. The operator enters input beginning with `/`.
- **workflowSteps:** (1) `InteractiveLoop` detects the `/` prefix. (2) `SlashCommandRouter.IsSlashCommand` confirms the input. (3) `SlashCommandCatalog.TryResolveDispatch` normalizes and looks up the command (including aliases). (4) The router dispatches to the handler method with the session and arguments. (5) The handler executes and returns a result (or `null` for `/quit`).
- **expectedOutcomes:** The command executes its side effect (e.g., switch model, list sessions, compact history). Non-existent commands produce an error message. `/quit` signals session exit.
- **failureScenarios:** The command token does not match any registered dispatch entry. The handler throws an unhandled exception.
- **trace.upstream:** `specs/capabilities/agent-conversation.yaml`
- **trace.downstream.behavior:** `specs/behavior/slash-command-routing.yaml`
- **trace.downstream.testing:** `specs/testing/agent-conversation.yaml`

### usecase.recover-interrupted-conversation
- **actor:** `persona.terminal-operator`
- **capabilityRef:** `capability.session-management`
- **preconditions:** A session was interrupted (process crash, network failure, manual kill). The session has `is_active=1` but the process is no longer running.
- **workflowSteps:** (1) The operator restarts JD.AI with `--continue` or `/resume`. (2) The system identifies the interrupted session by project hash and active status. (3) `SessionStore.GetSessionAsync` loads the session. All persisted turns are available because turns are saved transactionally at record time. (4) `ChatHistory` is rebuilt. (5) A checkpoint strategy may be used to restore file-system state if needed.
- **expectedOutcomes:** The conversation resumes from the last persisted turn. No data loss occurs for completed turns. The operator is informed of the recovery.
- **failureScenarios:** The SQLite WAL file is corrupted from the crash. File-system state has diverged from the last checkpoint.
- **trace.upstream:** `specs/capabilities/session-management.yaml`
- **trace.downstream.behavior:** `specs/behavior/session-persistence.yaml`
- **trace.downstream.testing:** `specs/testing/session-lifecycle.yaml`

**Step 1: Create all 5 use case YAML files** following the reference format above with the exact content described.

**Step 2: Update `specs/usecases/index.yaml`** — add 5 new entries after the existing `usecase.provider-tool-calling-execution` entry:
```yaml
  - id: usecase.start-session
    title: Start Session
    path: specs/usecases/start-session.yaml
    status: draft
  - id: usecase.resume-session
    title: Resume Session
    path: specs/usecases/resume-session.yaml
    status: draft
  - id: usecase.transform-context
    title: Transform Context
    path: specs/usecases/transform-context.yaml
    status: draft
  - id: usecase.execute-slash-command
    title: Execute Slash Command
    path: specs/usecases/execute-slash-command.yaml
    status: draft
  - id: usecase.recover-interrupted-conversation
    title: Recover Interrupted Conversation
    path: specs/usecases/recover-interrupted-conversation.yaml
    status: draft
```

**Step 3: Validate**
```bash
dotnet test tests/JD.AI.Tests/ --filter "FullyQualifiedName~UseCaseSpecification"
```
Expected: All tests pass (existing + new specs validated by repository-level validator).

**Step 4: Commit**
```bash
git add specs/usecases/
git commit -m "feat(specs): add 5 session lifecycle use case specs (#326)"
```

---

## Task 2: Create 4 Behavior specs

**Files:**
- Create: `specs/behavior/session-persistence.yaml`
- Create: `specs/behavior/streaming-responses.yaml`
- Create: `specs/behavior/slash-command-routing.yaml`
- Create: `specs/behavior/context-transformation.yaml`
- Modify: `specs/behavior/index.yaml`

**Context:** Each behavior spec traces upstream to a use case (from Task 1) and downstream to testing specs and code files. The `useCaseRef` must match an ID in `specs/usecases/index.yaml`. Each behavior needs BDD scenarios, a state machine, and assertions. All `trace.downstream.code` paths must reference files that exist on disk.

**Reference format (from existing spec `specs/behavior/provider-tool-calling-execution.yaml`):**
```yaml
apiVersion: jdai.upss/v1
kind: Behavior
id: behavior.<name>
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-behavioral-spec-architect
  lastReviewed: "2026-03-10"
  changeReason: <why, reference #326>
useCaseRef: usecase.<id>
bddScenarios:
  - title: <scenario title>
    given:
      - <at least one>
    when:
      - <at least one>
    then:
      - <at least one>
stateMachine:
  initialState: <state id>
  states:
    - id: <id>
      description: <desc>
    - id: <id>
      description: <desc>
      terminal: true
  transitions:
    - from: <state>
      to: <state>
      on: <event>
      actions:
        - <action>
assertions:
  - <at least one>
trace:
  upstream:
    - specs/usecases/<usecase>.yaml
  downstream:
    testing:
      - specs/testing/<testing>.yaml
    interfaces: []
    code:
      - src/JD.AI.Core/<path>.cs
```

**Spec details:**

### behavior.session-persistence
- **useCaseRef:** `usecase.start-session`
- **bddScenarios:**
  1. *New session is persisted to SQLite on creation:* Given the operator starts JD.AI in a project directory / When SessionStore.CreateSessionAsync is called / Then a session row exists in sessions.db with is_active=1, project_hash, and timestamps.
  2. *Turn is saved transactionally with tool calls and file touches:* Given an active session exists / When RecordAssistantTurnAsync completes / Then the turn, tool_calls, and files_touched rows are committed in a single SQLite transaction.
  3. *Session is exported to JSON on close:* Given an active session with turns / When CloseSessionAsync is called / Then a JSON file is written to `~/.jdai/projects/{hash}/sessions/{id}.json` and is_active is set to 0.
  4. *Integrity check detects mismatches between SQLite and JSON exports:* Given a session with differing turn counts between SQLite and JSON / When SessionIntegrity.CheckAsync runs / Then the mismatch is reported, and if autoRepair is enabled, the JSON is re-exported from SQLite.
- **stateMachine:** `New → Active → Closing → Closed` (terminal). Also `Active → Interrupted` (non-terminal, can transition to `Active` via resume).
- **assertions:** Turns are never lost if the process exits after RecordUserTurnAsync. SQLite WAL mode ensures crash-safe writes. Session IDs are unique 16-char hex strings. Project hashes are deterministic SHA-256 truncations.
- **trace.downstream.code:** `src/JD.AI.Core/Sessions/SessionStore.cs`, `src/JD.AI.Core/Sessions/SessionRecord.cs`, `src/JD.AI.Core/Sessions/SessionExporter.cs`, `src/JD.AI.Core/Sessions/SessionIntegrity.cs`
- **trace.downstream.testing:** `specs/testing/session-lifecycle.yaml`

### behavior.streaming-responses
- **useCaseRef:** `usecase.start-session`
- **bddScenarios:**
  1. *Streaming chunks are rendered in real time:* Given an active conversation turn / When the model emits streaming chunks / Then each chunk is passed to IAgentOutput.WriteStreamingChunk and rendered immediately.
  2. *Thinking content is parsed from stream:* Given a model that emits `<think>` tags or ReasoningContent metadata / When StreamingContentParser processes chunks / Then thinking segments are routed to WriteThinkingChunk and content segments to WriteStreamingChunk.
  3. *Streaming failure falls back to non-streaming:* Given a streaming request that terminates prematurely / When the error is detected / Then AgentLoop retries the same request using GetChatMessageContentAsync (non-streaming).
- **stateMachine:** `Idle → Streaming → Completed` (terminal). `Streaming → StreamFailed → FallingBack → Completed`. `FallingBack → Failed` (terminal).
- **assertions:** Partial `<think>` tags spanning chunk boundaries are buffered correctly. Streaming and thinking content are never interleaved in output. Fallback to non-streaming preserves the original user message.
- **trace.downstream.code:** `src/JD.AI.Core/Agents/AgentLoop.cs`, `src/JD.AI.Core/Agents/AgentOutput.cs`, `src/JD.AI.Core/Agents/StreamingContentParser.cs`
- **trace.downstream.testing:** `specs/testing/agent-conversation.yaml`

### behavior.slash-command-routing
- **useCaseRef:** `usecase.execute-slash-command`
- **bddScenarios:**
  1. *Known command dispatches to handler:* Given the operator types `/model gpt-4` / When SlashCommandRouter.ExecuteAsync processes the input / Then the model-switch handler is invoked with argument `gpt-4`.
  2. *Alias resolves to canonical command:* Given the operator types `/sp` / When TryResolveDispatch normalizes the input / Then it resolves to the `/system-prompt` handler.
  3. *Unknown command returns error:* Given the operator types `/nonexistent` / When TryResolveDispatch fails to match / Then the router returns an error message listing available commands.
  4. *Quit command signals session exit:* Given the operator types `/quit` / When the handler executes / Then ExecuteAsync returns null, signaling InteractiveLoop to exit.
- **stateMachine:** `AwaitingInput → Parsing → Dispatching → Executed` (terminal). `Parsing → UnknownCommand` (terminal). `Dispatching → HandlerError` (terminal).
- **assertions:** Command matching is case-insensitive after normalization. The `/jdai-` prefix is stripped for compatibility. Static field init order ensures DispatchMap is built after Definitions. `/quit` and `/exit` are aliases.
- **trace.downstream.code:** `src/JD.AI/Commands/SlashCommandRouter.cs`, `src/JD.AI/Commands/SlashCommandCatalog.cs`
- **trace.downstream.testing:** `specs/testing/agent-conversation.yaml`

### behavior.context-transformation
- **useCaseRef:** `usecase.transform-context`
- **bddScenarios:**
  1. *Preserve mode keeps history unchanged:* Given an active conversation with 10 turns / When SwitchProviderAsync is called with SwitchMode.Preserve / Then ChatHistory retains all 10 turns unchanged.
  2. *Compact mode summarizes conversation:* Given an active conversation / When SwitchMode.Compact is used / Then the current LLM produces a summary and ChatHistory is replaced with a single system message containing it.
  3. *Transform mode creates handoff briefing:* Given an active conversation / When SwitchMode.Transform is used / Then a briefing covering key decisions, file paths, code changes, pending questions, and user style is generated and injected as both system and assistant messages.
  4. *Fresh mode clears history:* Given any conversation state / When SwitchMode.Fresh is used / Then ChatHistory is replaced with a new empty instance.
  5. *Cancel mode aborts the switch:* Given a switch in progress / When SwitchMode.Cancel is used / Then an OperationCanceledException is thrown and no changes are made.
- **stateMachine:** `Active → TransformRequested → Transforming → Transformed` (terminal). `TransformRequested → Cancelled` (terminal). `Transforming → TransformFailed` (terminal).
- **assertions:** A ForkPoint is recorded before every model switch capturing the turn index and message count. The kernel is rebuilt with the new provider's plugins and filters after transformation. The original system prompt is preserved or re-applied after context replacement.
- **trace.downstream.code:** `src/JD.AI.Core/Agents/ConversationTransformer.cs`, `src/JD.AI.Core/Agents/AgentSession.cs`
- **trace.downstream.testing:** `specs/testing/agent-conversation.yaml`

**Step 1: Create all 4 behavior YAML files.**

**Step 2: Update `specs/behavior/index.yaml`** — add 4 entries after the existing `behavior.provider-tool-calling-execution`:
```yaml
  - id: behavior.session-persistence
    title: Session Persistence
    path: specs/behavior/session-persistence.yaml
    status: draft
  - id: behavior.streaming-responses
    title: Streaming Responses
    path: specs/behavior/streaming-responses.yaml
    status: draft
  - id: behavior.slash-command-routing
    title: Slash Command Routing
    path: specs/behavior/slash-command-routing.yaml
    status: draft
  - id: behavior.context-transformation
    title: Context Transformation
    path: specs/behavior/context-transformation.yaml
    status: draft
```

**Step 3: Validate**
```bash
dotnet test tests/JD.AI.Tests/ --filter "FullyQualifiedName~BehaviorSpecification"
```

**Step 4: Commit**
```bash
git add specs/behavior/
git commit -m "feat(specs): add 4 session lifecycle behavior specs (#326)"
```

---

## Task 3: Create Architecture spec

**Files:**
- Create: `specs/architecture/session-lifecycle.yaml`
- Modify: `specs/architecture/index.yaml`

**Context:** The architecture spec uses C4 model concepts (systems, containers, components, dependency rules). The `architectureStyle` must be one of: `layered`, `microservices`, `event-driven`, `modular-monolith`, `hexagonal`. Downstream trace arrays (`deployment`, `security`, `operations`) can be empty.

**Spec details:**

### architecture.session-lifecycle
- **architectureStyle:** `modular-monolith`
- **systems:** JD.AI Agent Runtime — The interactive agent runtime managing sessions, conversations, and command dispatch.
- **containers:**
  1. Core Library (`JD.AI.Core`, .NET 10 / C#) — Domain logic for sessions, agents, and context transformation.
  2. CLI Host (`JD.AI`, .NET 10 / C#) — Terminal UI, interactive loop, slash command routing, and session configuration.
  3. Session Database (SQLite) — Persistent storage for session records, turns, tool calls, and file touches at `~/.jdai/sessions.db`.
- **components:**
  1. SessionStore (container: Core Library) — SQLite-backed CRUD for session records with transactional turn persistence.
  2. SessionExporter (container: Core Library) — JSON export/import for human-readable session snapshots.
  3. SessionIntegrity (container: Core Library) — Cross-check SQLite vs JSON exports with auto-repair.
  4. AgentSession (container: Core Library) — Central state container holding ChatHistory, Kernel, turn tracking, and model switch history.
  5. AgentLoop (container: Core Library) — Turn executor with streaming, tool calling, fallback, and error recovery.
  6. ConversationTransformer (container: Core Library) — Context adaptation for provider/model switches.
  7. StreamingContentParser (container: Core Library) — Chunk-boundary-safe parser for thinking tags in streamed responses.
  8. SlashCommandRouter (container: CLI Host) — Dispatches slash commands to handler methods.
  9. SlashCommandCatalog (container: CLI Host) — Static registry of all command definitions and aliases.
  10. InteractiveLoop (container: CLI Host) — Main REPL loop wiring user input to AgentLoop and SlashCommandRouter.
  11. SessionConfigurator (container: CLI Host) — Startup orchestrator creating AgentSession from CLI arguments.
  12. CheckpointStrategy (container: Core Library) — File-system checkpointing (commit, stash, or directory-copy).
- **dependencyRules:**
  1. CLI Host → Core Library: allowed (CLI depends on Core for all domain logic)
  2. Core Library → CLI Host: NOT allowed (Core has no knowledge of CLI/TUI concerns)
  3. Core Library → Session Database: allowed (SessionStore directly accesses SQLite)
  4. CLI Host → Session Database: NOT allowed (CLI accesses sessions only through Core Library)
- **trace.upstream:** `specs/capabilities/session-management.yaml`, `specs/capabilities/agent-conversation.yaml`, `specs/capabilities/context-transformation.yaml`
- **trace.downstream:** `deployment: []`, `security: []`, `operations: []`

**Step 1: Create `specs/architecture/session-lifecycle.yaml`.**

**Step 2: Update `specs/architecture/index.yaml`** — add entry:
```yaml
  - id: architecture.session-lifecycle
    title: Session Lifecycle Architecture
    path: specs/architecture/session-lifecycle.yaml
    status: draft
```

**Step 3: Validate**
```bash
dotnet test tests/JD.AI.Tests/ --filter "FullyQualifiedName~ArchitectureSpecification"
```

**Step 4: Commit**
```bash
git add specs/architecture/
git commit -m "feat(specs): add session lifecycle architecture spec (#326)"
```

---

## Task 4: Create 2 Quality specs

**Files:**
- Create: `specs/quality/session-reliability.yaml`
- Create: `specs/quality/streaming-latency.yaml`
- Modify: `specs/quality/index.yaml`

**Context:** Quality specs define non-functional requirements (NFRs) with SLOs, SLIs, error budgets, and scalability expectations. The `category` must be one of: `performance`, `availability`, `reliability`, `scalability`, `security`. Downstream trace arrays can be empty.

**Spec details:**

### quality.session-reliability
- **category:** `reliability`
- **slos:**
  1. Zero Turn Data Loss — Target: `0 turns lost on process crash after RecordUserTurnAsync completes`. No completed turn is ever lost due to process termination.
  2. Session Resume Success Rate — Target: `>=99.9% of resume attempts succeed for non-corrupted sessions`.
  3. Integrity Check Accuracy — Target: `100% of SQLite-to-JSON mismatches detected by SessionIntegrity`.
- **slis:**
  1. Turn Persistence Confirmation — metric: `session_turn_save_success_total`, unit: count.
  2. Resume Attempt Outcome — metric: `session_resume_result`, unit: boolean.
  3. Integrity Mismatch Rate — metric: `session_integrity_mismatch_total`, unit: count.
- **errorBudgets:**
  1. sloRef: Zero Turn Data Loss, budget: `0%`, window: `30d`.
- **scalabilityExpectations:**
  1. dimension: `session-turn-count`, current: `500`, target: `5000` (sessions with thousands of turns must remain performant).
  2. dimension: `concurrent-sessions`, current: `1`, target: `10` (multi-tenant scenarios).
- **trace.upstream:** `specs/capabilities/session-management.yaml`
- **trace.downstream:** `testing: []`, `observability: []`, `operations: []`

### quality.streaming-latency
- **category:** `performance`
- **slos:**
  1. Time to First Token — Target: `<=200ms from user input to first streaming chunk rendered` (excludes LLM provider latency; measures framework overhead only).
  2. Streaming Chunk Processing — Target: `<=5ms per chunk processing latency` in StreamingContentParser and output rendering.
  3. Fallback Recovery Time — Target: `<=2s to detect streaming failure and begin non-streaming retry`.
- **slis:**
  1. First Token Latency — metric: `streaming_first_token_ms`, unit: milliseconds.
  2. Chunk Processing Time — metric: `streaming_chunk_process_ms`, unit: milliseconds.
  3. Fallback Detection Latency — metric: `streaming_fallback_detect_ms`, unit: milliseconds.
- **errorBudgets:**
  1. sloRef: Time to First Token, budget: `1%`, window: `7d`.
- **scalabilityExpectations:**
  1. dimension: `response-length`, current: `4096 tokens`, target: `32768 tokens` (long streaming responses must not degrade chunk processing latency).
- **trace.upstream:** `specs/capabilities/agent-conversation.yaml`
- **trace.downstream:** `testing: []`, `observability: []`, `operations: []`

**Step 1: Create both quality YAML files.**

**Step 2: Update `specs/quality/index.yaml`** — add 2 entries:
```yaml
  - id: quality.session-reliability
    title: Session Reliability
    path: specs/quality/session-reliability.yaml
    status: draft
  - id: quality.streaming-latency
    title: Streaming Latency
    path: specs/quality/streaming-latency.yaml
    status: draft
```

**Step 3: Validate**
```bash
dotnet test tests/JD.AI.Tests/ --filter "FullyQualifiedName~QualitySpecification"
```

**Step 4: Commit**
```bash
git add specs/quality/
git commit -m "feat(specs): add session reliability and streaming latency quality specs (#326)"
```

---

## Task 5: Create 2 Testing specs

**Files:**
- Create: `specs/testing/session-lifecycle.yaml`
- Create: `specs/testing/agent-conversation.yaml`
- Modify: `specs/testing/index.yaml`

**Context:** Testing specs define verification levels, behavior references, coverage targets, and generation rules. `behaviorRefs` must match `behavior.<name>` pattern. `verificationLevels` must be from: `unit`, `integration`, `acceptance`, `performance`, `security`, `e2e`. `generationRules[].strategy` must be `generated`, `manual`, or `hybrid`.

**Spec details:**

### testing.session-lifecycle
- **verificationLevels:** `unit`, `integration`
- **behaviorRefs:** `behavior.session-persistence`
- **qualityRefs:** `quality.session-reliability`
- **coverageTargets:**
  1. scope: `JD.AI.Core.Sessions`, target: `85%`, metric: `line`
  2. scope: `JD.AI.Core.Agents.Checkpointing`, target: `80%`, metric: `line`
- **generationRules:**
  1. source: `specs/behavior/session-persistence.yaml`, strategy: `hybrid`
- **trace.upstream:** `specs/behavior/session-persistence.yaml`
- **trace.downstream.ci:** `.github/workflows/ci.yml`
- **trace.downstream.release:** `docs/operations/deployment.md`

### testing.agent-conversation
- **verificationLevels:** `unit`, `integration`, `e2e`
- **behaviorRefs:** `behavior.streaming-responses`, `behavior.slash-command-routing`, `behavior.context-transformation`
- **qualityRefs:** `quality.streaming-latency`
- **coverageTargets:**
  1. scope: `JD.AI.Core.Agents.AgentLoop`, target: `80%`, metric: `line`
  2. scope: `JD.AI.Core.Agents.ConversationTransformer`, target: `90%`, metric: `line`
  3. scope: `JD.AI.Commands.SlashCommandCatalog`, target: `95%`, metric: `line`
- **generationRules:**
  1. source: `specs/behavior/streaming-responses.yaml`, strategy: `hybrid`
  2. source: `specs/behavior/slash-command-routing.yaml`, strategy: `manual`
  3. source: `specs/behavior/context-transformation.yaml`, strategy: `hybrid`
- **trace.upstream:** `specs/behavior/streaming-responses.yaml`, `specs/behavior/slash-command-routing.yaml`, `specs/behavior/context-transformation.yaml`
- **trace.downstream.ci:** `.github/workflows/ci.yml`, `.github/workflows/pr-validation.yml`
- **trace.downstream.release:** `docs/operations/deployment.md`

**Step 1: Create both testing YAML files.**

**Step 2: Update `specs/testing/index.yaml`** — add 2 entries:
```yaml
  - id: testing.session-lifecycle
    title: Session Lifecycle Testing
    path: specs/testing/session-lifecycle.yaml
    status: draft
  - id: testing.agent-conversation
    title: Agent Conversation Testing
    path: specs/testing/agent-conversation.yaml
    status: draft
```

**Step 3: Validate**
```bash
dotnet test tests/JD.AI.Tests/ --filter "FullyQualifiedName~TestingSpecification"
```

**Step 4: Commit**
```bash
git add specs/testing/
git commit -m "feat(specs): add session lifecycle and agent conversation testing specs (#326)"
```

---

## Task 6: Update capability specs with downstream use case refs

**Files:**
- Modify: `specs/capabilities/agent-conversation.yaml`
- Modify: `specs/capabilities/session-management.yaml`
- Modify: `specs/capabilities/context-transformation.yaml`

**Context:** The capability specs created in #325 have empty `relatedUseCases: []` and `trace.downstream.useCases: []`. Now that use cases exist, update both fields to reference them.

**Changes:**

### agent-conversation.yaml
- `relatedUseCases:` → `[usecase.execute-slash-command]`
- `trace.downstream.useCases:` → `[specs/usecases/execute-slash-command.yaml]`

### session-management.yaml
- `relatedUseCases:` → `[usecase.start-session, usecase.resume-session, usecase.recover-interrupted-conversation]`
- `trace.downstream.useCases:` → `[specs/usecases/start-session.yaml, specs/usecases/resume-session.yaml, specs/usecases/recover-interrupted-conversation.yaml]`

### context-transformation.yaml
- `relatedUseCases:` → `[usecase.transform-context]`
- `trace.downstream.useCases:` → `[specs/usecases/transform-context.yaml]`

**Step 1: Edit the 3 capability YAML files.**

**Step 2: Validate**
```bash
dotnet test tests/JD.AI.Tests/ --filter "FullyQualifiedName~CapabilitySpecification"
```

**Step 3: Commit**
```bash
git add specs/capabilities/agent-conversation.yaml specs/capabilities/session-management.yaml specs/capabilities/context-transformation.yaml
git commit -m "feat(specs): link capability specs to session lifecycle use cases (#326)"
```

---

## Task 7: Final cross-layer validation

**Step 1: Run ALL spec validators together**
```bash
dotnet test tests/JD.AI.Tests/ --filter "FullyQualifiedName~Specification"
```

This runs capability, use case, behavior, architecture, testing, quality, vision, persona, and interface validators. ALL must pass.

**Step 2: Verify traceability chain manually**
Spot-check that:
- Each use case's `capabilityRef` resolves in `specs/capabilities/index.yaml`
- Each behavior's `useCaseRef` resolves in `specs/usecases/index.yaml`
- All `trace.upstream` and `trace.downstream` file paths exist on disk

**Step 3: No commit needed — this is a verification-only task.**
