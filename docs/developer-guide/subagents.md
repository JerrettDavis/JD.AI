---
title: "Subagents"
description: "SubagentRunner internals, creating custom subagent types, tool scoping, model selection, execution modes, and the SubagentTools SK plugin."
---

# Subagents

Subagents are isolated AI instances that run scoped tasks with their own `Kernel`, chat history, and tool access. This guide covers the internals for developers who want to understand, customize, or extend the subagent system.

## SubagentRunner internals

The `SubagentRunner` in `src/JD.AI.Core/Agents/SubagentRunner.cs` is responsible for building and executing subagents:

```csharp
public sealed class SubagentRunner
{
    public async Task<string> RunAsync(
        SubagentType type,
        string prompt,
        CancellationToken ct);
}
```

Execution flow:

1. **Build a scoped Kernel** — the runner creates a new `Kernel` from the current provider, registering only the tools allowed for the agent type
2. **Set model preference** — each type has a model preference (fast/cheap or smart/capable)
3. **Execute** — single-turn returns one response; multi-turn runs the full agentic tool-calling loop
4. **Return result** — the subagent's final output is returned as a string to the caller

```
SubagentRunner.RunAsync(type, prompt)
  → BuildScopedKernel(type)     // Only allowed tools
  → SelectModel(type)            // Fast/cheap or smart/capable
  → ExecuteTurn(kernel, prompt)  // Single or multi-turn
  → Return result string
```

## Subagent types and tool scoping

Each type gets a specific tool subset:

| Type | Tools | Model | Use case |
|------|-------|-------|----------|
| `explore` | `read_file`, `grep`, `glob`, `git_log`, `list_directory` | Fast/cheap | Read-only codebase analysis |
| `task` | `run_command`, `read_file`, `list_directory` | Fast/cheap | Running builds, tests, scripts |
| `plan` | `read_file`, `grep`, `glob`, `memory_store`, `memory_search` | Smart/capable | Implementation planning |
| `review` | `read_file`, `grep`, `git_diff`, `git_log`, `git_status` | Smart/capable | Code review and analysis |
| `general` | All tools | Same as parent | Full-capability fallback |

### Tool scoping implementation

The runner builds tool sets per type:

```csharp
private IEnumerable<KernelPlugin> GetToolsForType(SubagentType type)
{
    return type switch
    {
        SubagentType.Explore => new[]
        {
            KernelPluginFactory.CreateFromObject(new FileTools(_cwd), "FileTools"),
            KernelPluginFactory.CreateFromObject(new SearchTools(_cwd), "SearchTools"),
            // git_log only — no write operations
        },
        SubagentType.General => _allTools, // Full access
        // ...
    };
}
```

## Execution modes

### Single-turn (default)

One prompt → one response. No tool calls. Fast and cheap.

```csharp
// Internal: single-turn execution
var result = await kernel.InvokePromptAsync(prompt, ct: ct);
return result.ToString();
```

### Multi-turn

Full agentic loop with iterative tool calling. The subagent can invoke tools, inspect results, and continue reasoning.

```csharp
// Internal: multi-turn execution loop
var history = new ChatHistory();
history.AddUserMessage(prompt);

while (!done)
{
    var response = await chatCompletion.GetChatMessageContentAsync(history, settings, kernel, ct);
    // Process tool calls, append results, continue loop
}
```

## SubagentTools SK plugin

The `SubagentTools` class in `src/JD.AI/Tools/SubagentTools.cs` exposes subagent capabilities to the LLM:

```csharp
public class SubagentTools
{
    [KernelFunction("spawn_agent")]
    [Description("Spawn a specialized subagent for a scoped task")]
    public async Task<string> SpawnAgentAsync(
        [Description("Agent type: explore, task, plan, review, general")] string type,
        [Description("Task prompt for the subagent")] string prompt,
        [Description("Execution mode: single or multi")] string mode = "single")
    {
        var agentType = Enum.Parse<SubagentType>(type, ignoreCase: true);
        var execMode = mode == "multi" ? ExecutionMode.MultiTurn : ExecutionMode.SingleTurn;
        return await _runner.RunAsync(agentType, prompt, execMode, _ct);
    }

    [KernelFunction("spawn_team")]
    [Description("Spawn a coordinated team of agents with a strategy")]
    public async Task<string> SpawnTeamAsync(
        [Description("Strategy: sequential, fan-out, supervisor, debate")] string strategy,
        [Description("Comma-separated agent configs (type:name)")] string agents,
        [Description("The team's goal")] string goal,
        [Description("Use multi-turn execution")] bool multiTurn = false)
    {
        // Parse agent configs, create team, execute strategy
    }

    [KernelFunction("query_team_context")]
    [Description("Query shared team context")]
    public string QueryTeamContext(
        [Description("Key: events, results, or a scratchpad key")] string key)
    {
        // Return team scratchpad, events, or results
    }
}
```

## Creating custom subagent types

To add a new subagent type:

### 1. Add the type to the enum

```csharp
// In JD.AI.Core/Agents/SubagentType.cs
public enum SubagentType
{
    Explore,
    Task,
    Plan,
    Review,
    General,
    Security  // Your new type
}
```

### 2. Define the tool set

```csharp
SubagentType.Security => new[]
{
    KernelPluginFactory.CreateFromObject(new FileTools(_cwd), "FileTools"),
    KernelPluginFactory.CreateFromObject(new SearchTools(_cwd), "SearchTools"),
    KernelPluginFactory.CreateFromObject(new GitTools(_cwd), "GitTools"),
    KernelPluginFactory.CreateFromObject(new SecurityScanTools(), "SecurityTools"),
},
```

### 3. Set model preference

```csharp
SubagentType.Security => ModelPreference.SmartCapable,
```

### 4. Add a system prompt

```csharp
SubagentType.Security => """
    You are a security analyst. Focus on identifying vulnerabilities,
    insecure patterns, and potential attack vectors. Reference OWASP
    Top 10 and CWE identifiers where applicable.
    """,
```

## Nesting

Subagents can spawn their own subagents for hierarchical task decomposition. Maximum nesting depth defaults to **2** and is configurable.

```text
Main Agent
  └── general subagent (implements feature)
        ├── explore subagent (understands existing code)
        └── task subagent (runs tests after changes)
```

## Model selection

Each subagent type has a model preference that maps to the available models:

| Preference | Behavior |
|-----------|----------|
| `Fast/Cheap` | Selects the fastest available model (e.g., Haiku, GPT-4o-mini) |
| `Smart/Capable` | Selects the most capable model (e.g., Sonnet, GPT-4o) |
| `Same as parent` | Uses the same model as the parent session |

The `SubagentRunner` resolves model preference against the active provider's model list.

## See also

- [Team Orchestration](orchestration.md) — coordinating multiple subagents
- [Architecture Overview](index.md) — agent lifecycle
- [Custom Tools](custom-tools.md) — writing tools for subagent use
- [Subagents user guide](subagents.md) — end-user subagent documentation
