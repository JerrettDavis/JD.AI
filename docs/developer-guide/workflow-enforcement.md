---
title: "Workflow Enforcement"
description: "How the Workflow Enforcement Layer intercepts agent tool-call sequences and coordinates them through a structured plan-first, execute-second workflow."
---

# Workflow Enforcement

Without coordination, agents may call tools in an arbitrary order, interleaving side effects in ways that are difficult to audit, reproduce, or refine. The Workflow Enforcement Layer sits between the agent loop and the Semantic Kernel tool pipeline. It intercepts any request that would trigger multiple tool usages and routes it through the `WorkflowFramework` before any tool executes.

The result is a **plan-first, execute-second** pattern: the agent generates or matches a workflow plan, the user confirms it, and only then does execution begin.

## Overview

The enforcement layer answers three questions every time the agent receives a request:

1. **Does this request require multiple tool calls?** — `AgentWorkflowDetector` decides.
2. **Is there a known workflow for this intent?** — `TagWorkflowMatcher` searches the catalog.
3. **How should the workflow be obtained?** — Either matched from the catalog or generated fresh by `WorkflowGenerator`.

Once a workflow definition is available it is presented to the user for confirmation. After confirmation the workflow engine executes each step, and `WorkflowExecutionCapture` records the run for audit and replay.

## Architecture

```
User Request
    │
    ▼
┌──────────────────────────┐
│  AgentWorkflowDetector   │  Is multi-tool coordination required?
└──────────────────────────┘
         │ yes
         ▼
┌──────────────────────────┐
│  TagWorkflowMatcher      │  Does a saved workflow match this intent?
└──────────────────────────┘
         │                  │
      match               no match
         │                  │
         ▼                  ▼
┌──────────────┐  ┌──────────────────────┐
│ Workflow     │  │  WorkflowGenerator   │  LLM-assisted YAML generation
│ Catalog      │  │  (WorkflowEmitter)   │
└──────────────┘  └──────────────────────┘
         │                  │
         └────────┬─────────┘
                  │
                  ▼
        ┌─────────────────┐
        │  User Confirms  │  Present plan; wait for approval or edit
        └─────────────────┘
                  │
                  ▼
        ┌─────────────────┐
        │ Workflow Engine │  Step-by-step execution
        └─────────────────┘
                  │
                  ▼
        ┌──────────────────────────┐
        │ WorkflowExecutionCapture │  Record for replay / audit
        └──────────────────────────┘
```

## Detection flow

### AgentWorkflowDetector

`AgentWorkflowDetector` examines every incoming `AgentRequest` before the agent loop runs. It uses lightweight heuristics — estimated tool-call count, presence of multi-phase keywords, subagent spawning patterns — to decide whether to hand control to the enforcement layer.

```csharp
public interface IAgentWorkflowDetector
{
    bool IsWorkflowRequired(AgentRequest request);
}
```

When `IsWorkflowRequired` returns `true`, the agent loop suspends and defers to the workflow coordinator.

### TagWorkflowMatcher

`TagWorkflowMatcher` searches the workflow catalog for existing definitions whose tags and description semantically match the incoming intent. It uses a ranked scoring approach: exact tag overlap scores highest, followed by embedding similarity on the description.

```csharp
public interface IWorkflowMatcher
{
    Task<WorkflowMatchResult?> MatchAsync(AgentRequest request, CancellationToken ct = default);
}
```

A `WorkflowMatchResult` includes the matched definition and a confidence score. Matches below the configured threshold are discarded and treated as no-match, triggering generation.

### WorkflowEmitter and WorkflowGenerator

When no suitable match exists, `WorkflowEmitter` invokes `WorkflowGenerator`. The generator calls the LLM with:

- The full conversation history (user intent in context)
- The list of available tool names and descriptions
- A system prompt instructing it to produce a valid YAML workflow definition

The resulting YAML is parsed, validated against the workflow schema, and returned as an `AgentWorkflowDefinition`. If the LLM produces invalid YAML, `WorkflowGenerator` retries up to three times with the validation error appended to the prompt.

## Workflow DSL reference

Workflows are defined in YAML. A complete definition has the following top-level fields:

```yaml
name: code-review-pipeline
version: "1.0"
description: Full code review with security and test coverage checks
tags: [review, security, testing]
steps:
  - ...
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | ✓ | Unique identifier in the catalog |
| `version` | string | | Semantic version; incremented on refinement |
| `description` | string | ✓ | Human-readable intent |
| `tags` | string[] | | Used by `TagWorkflowMatcher` for retrieval |
| `steps` | step[] | ✓ | Ordered list of step definitions |

### Step kinds

Each step must have a `kind` field. The supported kinds are:

#### `skill`

Invokes a named skill from the skills system.

```yaml
- kind: skill
  name: Analyze Code
  target: code-review
  parameters:
    focus: security
    format: markdown
```

| Field | Description |
|-------|-------------|
| `target` | Skill name (must exist in the skill catalog) |
| `parameters` | Key-value map passed to the skill as prompt variables |

#### `tool`

Calls a Semantic Kernel tool function directly.

```yaml
- kind: tool
  name: Run Tests
  target: run_command
  parameters:
    command: dotnet test --verbosity minimal
```

| Field | Description |
|-------|-------------|
| `target` | Tool function name (e.g., `run_command`, `read_file`) |
| `parameters` | Arguments forwarded to the tool function |

#### `nested`

Executes another saved workflow as a sub-step.

```yaml
- kind: nested
  name: Security Scan
  target: security-audit-workflow
```

| Field | Description |
|-------|-------------|
| `target` | Name of the workflow in the catalog |

#### `loop`

Repeats a set of sub-steps until a condition evaluates to `true`.

```yaml
- kind: loop
  name: Retry Until Green
  condition: "{{previous.exitCode}} == 0"
  subSteps:
    - kind: tool
      name: Run Tests
      target: run_command
      parameters:
        command: dotnet test
```

| Field | Description |
|-------|-------------|
| `condition` | Expression evaluated after each iteration; loop stops when `true` |
| `subSteps` | Steps to execute each iteration |

#### `conditional`

Executes sub-steps only when the condition is met.

```yaml
- kind: conditional
  name: Coverage Report
  condition: "{{previous.exitCode}} == 0"
  subSteps:
    - kind: tool
      name: Collect Coverage
      target: run_command
      parameters:
        command: dotnet test --collect:"XPlat Code Coverage"
```

### Condition expressions

Conditions are simple expression strings evaluated against the workflow execution context. The following template variables are available:

| Variable | Description |
|----------|-------------|
| `{{previous.exitCode}}` | Exit code of the preceding tool step |
| `{{previous.output}}` | Text output of the preceding step |
| `{{steps.<name>.exitCode}}` | Exit code of a named step |
| `{{steps.<name>.output}}` | Output of a named step |
| `{{env.<VAR>}}` | Environment variable value |

Supported operators: `==`, `!=`, `<`, `>`, `<=`, `>=`, `contains`, `startsWith`.

## Agent roles in workflows

### Agents as workflow steps (not orchestrators)

In the enforcement model, agents are **first-class workflow steps** — they appear in workflow YAML like any other step and are responsible only for their scoped task. They do not call tools directly during workflow execution; instead, they define or refine the workflow plan at generation time and then participate as named steps.

This separation prevents agents from accumulating side effects outside the workflow plan and ensures every tool call is attributable to a specific workflow step.

```yaml
steps:
  # The agent is a step, not a free-roaming orchestrator
  - kind: skill
    name: Plan Architecture
    target: architecture-planning-agent
    parameters:
      context: "{{userRequest}}"

  - kind: tool
    name: Scaffold Project
    target: run_command
    parameters:
      command: dotnet new webapi -n {{steps.Plan Architecture.projectName}}
```

### Agents as workflow generators

Before execution begins, the LLM (acting as a planning agent) is invoked by `WorkflowGenerator` to produce the YAML plan. At this stage, the LLM may call a lightweight set of read-only introspection tools (e.g., list available skills, describe tool signatures) to inform the plan. These calls happen inside the generator and are not subject to workflow confirmation themselves.

## Enforcement modes

Configure the enforcement behavior in `~/.jdai/config.json` or `.jdai/config.json`:

```json
{
  "workflows": {
    "enforcement": {
      "mode": "Strict",
      "minimumToolCallThreshold": 2,
      "matchConfidenceThreshold": 0.75
    }
  }
}
```

| Mode | Behavior |
|------|----------|
| `Strict` | Always intercept multi-tool requests; enforce plan-first execution. No direct tool calls permitted when a workflow is warranted. |
| `Advisory` | Intercept and suggest a workflow, but allow the user to dismiss the suggestion and proceed with direct tool calls. |
| `Disabled` | Bypass enforcement entirely; agents call tools directly. Intended for development and debugging only. |

`minimumToolCallThreshold` controls how many estimated tool calls trigger the detector. Default is `2`. `matchConfidenceThreshold` controls how confident `TagWorkflowMatcher` must be before presenting an existing workflow instead of generating a new one.

## Workflow lifecycle

```
1. Generate    →  WorkflowGenerator produces YAML from intent
2. Present     →  User sees the plan (steps, tools, parameters)
3. Confirm     →  User approves, edits, or cancels
4. Execute     →  WorkflowEngine runs steps sequentially
5. Capture     →  WorkflowExecutionCapture records the run
6. Refine      →  User adjusts parameters; WorkflowVersioning saves a new version
```

### WorkflowVersioning

Each refinement with `/workflow refine` creates a new version entry in the catalog. Versions are stored alongside the base definition and are addressable by version string:

```bash
/workflow run code-review-pipeline --version 1.2
```

The catalog retains all versions, enabling diff and rollback:

```text
/workflow versions code-review-pipeline
  1.0  (original)
  1.1  focus: security → focus: security,performance
  1.2  added coverage step
```

### WorkflowExecutionCapture

Every run is recorded with:

- Start and end timestamps
- Per-step input parameters, output, and duration
- Exit codes for tool steps
- The complete `AgentWorkflowDefinition` snapshot at execution time (so version changes do not retroactively alter history)

Captured runs are stored in `~/.jdai/workflow-runs/` as JSON and can be replayed with `/workflow replay <run-id>`.

## Writing workflow-aware tools

Tools do not need special code to participate in workflows. However, tools that are commonly used in automated contexts should:

**Support cancellation tokens.** The workflow engine cancels all pending steps when the user presses `Ctrl+C`.

```csharp
[KernelFunction("run_command")]
public async Task<CommandResult> RunCommandAsync(
    [Description("Shell command to run")] string command,
    CancellationToken cancellationToken = default)
{
    // Pass cancellationToken to the underlying process
}
```

**Return structured output.** Steps downstream in the workflow can reference `{{steps.<name>.output}}`. Tools that return structured text (e.g., JSON or key=value pairs) are easier to reference in conditions.

**Declare `exitCode` when applicable.** For shell-execution tools, include an `exitCode` field in the result so that `conditional` and `loop` steps can branch correctly.

**Avoid user-interactive prompts.** Tools invoked from workflow steps run non-interactively. Confirmation prompts that would block the terminal should be suppressed when a workflow context is active. Inspect `IWorkflowExecutionContext` (available via DI) to detect this:

```csharp
public class ShellTools
{
    private readonly IWorkflowExecutionContext? _workflowContext;

    public ShellTools(IWorkflowExecutionContext? workflowContext = null)
    {
        _workflowContext = workflowContext;
    }

    [KernelFunction("run_command")]
    public async Task<CommandResult> RunCommandAsync(string command, CancellationToken ct = default)
    {
        bool interactive = _workflowContext is null;
        if (interactive)
        {
            // prompt for confirmation
        }
        // execute
    }
}
```

## Testing workflows with WorkflowExecutionCapture

Use `WorkflowExecutionCapture` directly in unit tests to assert on step-level behavior without running a full agent session:

```csharp
[Fact]
public async Task CodeReviewWorkflow_ExecutesAllSteps()
{
    // Arrange
    var definition = new AgentWorkflowDefinition
    {
        Name = "code-review-pipeline",
        Description = "Full review",
        Tags = ["review"],
        Steps =
        [
            AgentStepDefinition.RunSkill("code-review"),
            AgentStepDefinition.InvokeTool("run_command")
                .WithParameter("command", "dotnet test"),
        ]
    };

    var capture = new WorkflowExecutionCapture();
    var engine = new WorkflowEngine(mockExecutor, mockCatalog, capture);

    // Act
    var result = await engine.RunAsync(definition, CancellationToken.None);

    // Assert — two steps executed
    Assert.Equal(2, result.StepResults.Count);

    // Assert — capture recorded both steps
    Assert.Equal(2, capture.StepRecords.Count);
    Assert.All(capture.StepRecords, r => Assert.True(r.Duration > TimeSpan.Zero));
}

[Fact]
public async Task ConditionalStep_SkipsWhenConditionFalse()
{
    var definition = new AgentWorkflowDefinition
    {
        Name = "conditional-test",
        Steps =
        [
            AgentStepDefinition.If(
                "{{previous.exitCode}} == 1",
                AgentStepDefinition.RunSkill("should-not-run"))
        ]
    };

    var result = await engine.RunAsync(definition, CancellationToken.None);
    Assert.Empty(result.StepResults);
}

[Fact]
public async Task WorkflowCapture_CanReplayRun()
{
    // Run a workflow and capture it
    var capture = new WorkflowExecutionCapture();
    await engine.RunAsync(definition, CancellationToken.None);

    // Serialize and deserialize the capture
    var json = capture.Serialize();
    var loaded = WorkflowExecutionCapture.Deserialize(json);

    // Replay produces the same step sequence
    var replayResult = await engine.ReplayAsync(loaded, CancellationToken.None);
    Assert.Equal(capture.StepRecords.Count, replayResult.StepResults.Count);
}
```

> [!TIP]
> When testing conditional and loop steps, seed the mock executor to return specific `exitCode` values so you can exercise both branches of a condition.

## See also

- [Workflows](workflows.md) — YAML DSL reference, workflow commands, and step types
- [Skills](skills.md) — skills invoked from `skill` steps
- [Custom Tools](custom-tools.md) — writing Semantic Kernel tools used in `tool` steps
- [Architecture Overview](index.md) — where enforcement fits in the agent lifecycle
