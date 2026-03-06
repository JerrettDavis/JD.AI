---
title: "Workflows"
description: "Workflow development guide — YAML definitions, workflow engine internals, step types, commands, and testing workflows."
---

# Workflows

Workflows are composable, multi-step automation sequences that can be defined, saved, replayed, and refined. They live in the `JD.AI.Workflows` project and provide a structured alternative to ad-hoc agent conversations.

## What workflows are

A workflow is a named sequence of steps that JD.AI executes in order. Each step can invoke a skill, call a tool, loop conditionally, or branch based on results. Workflows are:

- **Replayable** — run the same workflow across different projects
- **Refinable** — adjust parameters and re-run with `/workflow refine`
- **Composable** — nest workflows within workflows
- **Cataloged** — saved to `~/.jdai/workflows/` for reuse

## Architecture

```
┌──────────────────────┐
│  /workflow commands   │  CLI interface
├──────────────────────┤
│  IWorkflowCatalog    │  Storage (save, list, get, delete)
├──────────────────────┤
│  IWorkflowMatcher    │  Match user requests to workflows
├──────────────────────┤
│  Workflow Engine      │  Step execution loop
├──────────────────────┤
│  AgentSteps           │  Step implementations
└──────────────────────┘
```

### Key interfaces

```csharp
public interface IWorkflowCatalog
{
    Task SaveAsync(AgentWorkflowDefinition workflow, CancellationToken ct = default);
    Task<AgentWorkflowDefinition?> GetAsync(string name, string? version = null, CancellationToken ct = default);
    Task<IReadOnlyList<AgentWorkflowDefinition>> ListAsync(CancellationToken ct = default);
    Task DeleteAsync(string name, CancellationToken ct = default);
}

public interface IWorkflowMatcher
{
    Task<WorkflowMatchResult?> MatchAsync(AgentRequest request, CancellationToken ct = default);
}

public interface IAgentWorkflowDetector
{
    bool IsWorkflowRequired(AgentRequest request);
}
```

## Workflow definition

An `AgentWorkflowDefinition` describes a complete workflow:

```csharp
public record AgentWorkflowDefinition
{
    public string Name { get; init; }
    public string? Version { get; init; }
    public string Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; }
    public IReadOnlyList<AgentStepDefinition> Steps { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}
```

### YAML format

Workflows can be defined in YAML and loaded by the catalog:

```yaml
name: code-review-pipeline
version: "1.0"
description: Full code review with security and test coverage checks
tags: [review, security, testing]
steps:
  - kind: skill
    name: Analyze Code
    target: code-review
    parameters:
      focus: security

  - kind: tool
    name: Run Tests
    target: run_command
    parameters:
      command: dotnet test --verbosity minimal

  - kind: conditional
    name: Check Coverage
    condition: "{{previous.exitCode}} == 0"
    subSteps:
      - kind: tool
        name: Coverage Report
        target: run_command
        parameters:
          command: dotnet test --collect:"XPlat Code Coverage"

  - kind: skill
    name: Synthesize Report
    target: summarize
    parameters:
      format: markdown
```

## Step types

Steps are defined by the `AgentStepKind` enum:

| Kind | Description | Builder method |
|------|-------------|---------------|
| `Skill` | Invoke a named skill | `AgentStepDefinition.RunSkill(name)` |
| `Tool` | Call a Semantic Kernel tool | `AgentStepDefinition.InvokeTool(name)` |
| `Nested` | Run a sub-workflow | `AgentStepDefinition.RunWorkflow(name)` |
| `Loop` | Repeat steps until a condition | `AgentStepDefinition.LoopUntil(condition, subSteps)` |
| `Conditional` | Branch based on a condition | `AgentStepDefinition.If(condition, subSteps)` |

### Step definition

```csharp
public record AgentStepDefinition
{
    public AgentStepKind Kind { get; init; }
    public string Name { get; init; }
    public string? Target { get; init; }
    public string? Condition { get; init; }
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }
    public IReadOnlyList<AgentStepDefinition>? SubSteps { get; init; }

    // Fluent builders
    public static AgentStepDefinition RunSkill(string name) => ...;
    public static AgentStepDefinition InvokeTool(string name) => ...;
    public static AgentStepDefinition LoopUntil(string condition, params AgentStepDefinition[] subSteps) => ...;
    public static AgentStepDefinition If(string condition, params AgentStepDefinition[] subSteps) => ...;
}
```

## Workflow engine

The engine executes steps sequentially through a `IWorkflowContext`:

```csharp
public interface IWorkflowContext<TData>
{
    TData Data { get; }
    IReadOnlyDictionary<string, object> StepResults { get; }
    void SetStepResult(string stepName, object result);
    CancellationToken CancellationToken { get; }
}
```

### Execution flow

```
WorkflowEngine.RunAsync(definition)
  → foreach step in definition.Steps
      → Resolve step executor (Skill, Tool, Loop, Conditional)
      → Execute step
      → Store result in context
      → Evaluate next step conditions
  → Return workflow result
```

### Step execution

Each step kind has an executor. For example, `RunSkillStep`:

```csharp
public class RunSkillStep : IWorkflowStep
{
    public async Task ExecuteAsync(IWorkflowContext<AgentWorkflowData> context)
    {
        var skillName = _stepDefinition.Target;
        // Load skill, execute via agent, store result
        var result = await _agentSession.RunSkillAsync(skillName, _stepDefinition.Parameters);
        context.SetStepResult(_stepDefinition.Name, result);
    }
}
```

## Workflow Enforcement

The workflow enforcement layer intercepts every tool call and determines whether a structured workflow is required before the agent proceeds. It sits between the user request and the workflow engine, acting as a gatekeeper.

See [Workflow Enforcement](workflow-enforcement.md) for the full DSL reference, agent roles, and enforcement modes.

In relation to the content on this page: the **workflow engine** (described above) executes step sequences, while the **enforcement layer** decides *whether* a workflow must be run at all and blocks raw tool calls when a matching plan-first workflow is required. The two layers work together — enforcement selects or mandates a workflow, the engine executes it.

## Conversation-to-Workflow Capture

The agent can turn a completed conversation into a reusable workflow automatically. After walking through a multi-step task interactively, run:

```text
/workflow capture
```

JD.AI inspects the recent conversation history, extracts the sequence of tool calls and skill invocations, and writes a new workflow YAML to `~/.jdai/workflows/`. You are prompted to name the workflow and optionally edit the generated YAML before saving.

This is the fastest way to build a workflow — do it interactively once, capture it, then replay it across any project with `/workflow run <name>`.

## Workflow Versioning

Every `AgentWorkflowDefinition` carries an optional `Version` field. When you refine an existing workflow (add steps, change conditions, update parameters), JD.AI creates a new version and archives the previous one:

```
~/.jdai/workflows/
  code-review-pipeline/
    current.yaml          ← active version
    versions/
      1.0.yaml
      1.1.yaml
      2.0.yaml
```

**Version commands:**

| Command | Description |
|---------|-------------|
| `/workflow history <name>` | List all saved versions with timestamps |
| `/workflow rollback <name> <version>` | Make an older version the active version |

Example:

```text
> /workflow history code-review-pipeline
code-review-pipeline versions:
  2.0  (current)  2026-03-01 — added blackboard aggregation step
  1.1             2026-02-14 — added conditional coverage gate
  1.0             2026-01-30 — initial version

> /workflow rollback code-review-pipeline 1.1
Rolled back code-review-pipeline to version 1.1
```

Old versions are never deleted automatically; remove them manually from `~/.jdai/workflows/<name>/versions/` if needed.

## Workflow Commands Reference

| Command | Description |
|---------|-------------|
| `/workflow list` | Show all saved workflows |
| `/workflow show <name>` | Display workflow YAML |
| `/workflow run <name>` | Execute a saved workflow |
| `/workflow create` | Wizard to create a new workflow |
| `/workflow capture` | Capture recent conversation as a workflow |
| `/workflow edit <name>` | Open workflow in editor |
| `/workflow delete <name>` | Remove a workflow |
| `/workflow history <name>` | Show version history |
| `/workflow rollback <name> <version>` | Revert to an earlier version |
| `/workflow export <name> <file>` | Export workflow to a YAML file |
| `/workflow import <file>` | Import workflow from a YAML file |

### Example usage

```text
> /workflow run code-review-pipeline
Running workflow: code-review-pipeline (5 steps)
  ✓ Step 1/5: Analyze Code (12s)
  ✓ Step 2/5: Run Tests (45s)
  ✓ Step 3/5: Check Coverage (conditionally executed)
  ✓ Step 4/5: Coverage Report (8s)
  ✓ Step 5/5: Synthesize Report (6s)
Workflow complete (1m 11s)
```

## Writing custom workflow steps

### 1. Define the step class

```csharp
public class HttpRequestStep : IWorkflowStep
{
    private readonly AgentStepDefinition _step;
    private readonly HttpClient _httpClient;

    public HttpRequestStep(AgentStepDefinition step, HttpClient httpClient)
    {
        _step = step;
        _httpClient = httpClient;
    }

    public async Task ExecuteAsync(IWorkflowContext<AgentWorkflowData> context)
    {
        var url = _step.Parameters?["url"] ?? throw new ArgumentException("url is required");
        var method = _step.Parameters?.GetValueOrDefault("method", "GET");

        var response = method.ToUpperInvariant() switch
        {
            "GET" => await _httpClient.GetStringAsync(url, context.CancellationToken),
            "POST" => await PostAsync(url, _step.Parameters, context.CancellationToken),
            _ => throw new ArgumentException($"Unsupported method: {method}")
        };

        context.SetStepResult(_step.Name, response);
    }
}
```

### 2. Register the step executor

Register custom step kinds in the workflow engine's step resolver.

## Testing workflows

Test workflows by constructing definitions programmatically:

```csharp
[Fact]
public async Task Workflow_ExecutesStepsInOrder()
{
    var definition = new AgentWorkflowDefinition
    {
        Name = "test-workflow",
        Description = "Test",
        Tags = Array.Empty<string>(),
        Steps = new[]
        {
            AgentStepDefinition.RunSkill("analyze"),
            AgentStepDefinition.InvokeTool("run_command"),
        }
    };

    var engine = new WorkflowEngine(mockExecutor, mockCatalog);
    var result = await engine.RunAsync(definition);

    Assert.Equal(2, result.StepResults.Count);
}

[Fact]
public async Task ConditionalStep_SkipsWhenConditionFalse()
{
    var definition = new AgentWorkflowDefinition
    {
        Name = "conditional-test",
        Steps = new[]
        {
            AgentStepDefinition.If("false", AgentStepDefinition.RunSkill("should-not-run")),
        }
    };

    var result = await engine.RunAsync(definition);
    Assert.Empty(result.StepResults);
}
```

## See also

- [Architecture Overview](index.md) — where workflows fit in the system
- [Custom Tools](custom-tools.md) — tools used by workflow steps
- [Commands Reference](../reference/commands.md) — all slash commands
