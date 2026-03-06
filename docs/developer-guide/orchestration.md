---
title: "Team Orchestration"
description: "IOrchestrationStrategy interface, implementing custom strategies, TeamContext shared state, synthesis patterns, and progress events."
---

# Team Orchestration

Team orchestration coordinates multiple [subagents](subagents.md) working together on complex tasks. This guide covers the internals for developers who want to understand, customize, or extend the orchestration system.

![Team orchestration progress panel](../images/demo-orchestration.png)

## IOrchestrationStrategy interface

All strategies implement this interface from `src/JD.AI.Core/Agents/Orchestration/`:

```csharp
public interface IOrchestrationStrategy
{
    string Name { get; }
    Task<TeamResult> ExecuteAsync(
        IReadOnlyList<SubagentConfig> agents,
        TeamContext context,
        ISubagentExecutor executor,
        AgentSession parentSession,
        Action<SubagentProgress>? onProgress = null,
        CancellationToken ct = default);
}
```

| Parameter | Purpose |
|-----------|---------|
| `agents` | List of agent configurations (type, name, prompt, tools) |
| `context` | Shared state — scratchpad, event stream, results |
| `executor` | Abstraction for single/multi-turn execution |
| `parentSession` | Parent's session for provider/model access |
| `onProgress` | Callback for live progress updates (UI rendering) |

## Built-in strategies

### SequentialStrategy

Agents execute in pipeline order. Each receives the previous agent's output plus scratchpad access.

```csharp
public class SequentialStrategy : IOrchestrationStrategy
{
    public string Name => "sequential";

    public async Task<TeamResult> ExecuteAsync(...)
    {
        string previousOutput = "";
        foreach (var agent in agents)
        {
            var enrichedPrompt = $"{agent.Prompt}\n\nPrevious agent output:\n{previousOutput}";
            var result = await executor.ExecuteAsync(agent, enrichedPrompt, ct);
            context.AddResult(agent.Name, result);
            previousOutput = result.Output;
            onProgress?.Invoke(new SubagentProgress(agent.Name, "completed"));
        }
        return context.BuildTeamResult(Name);
    }
}
```

### FanOutStrategy

All agents run concurrently. A synthesizer merges results.

```csharp
public class FanOutStrategy : IOrchestrationStrategy
{
    public string Name => "fan-out";

    public async Task<TeamResult> ExecuteAsync(...)
    {
        // Run all agents in parallel
        var tasks = agents.Select(agent =>
            executor.ExecuteAsync(agent, agent.Prompt, ct));
        var results = await Task.WhenAll(tasks);

        // Store results in context
        for (int i = 0; i < agents.Count; i++)
            context.AddResult(agents[i].Name, results[i]);

        // Synthesize with a dedicated agent
        var synthesis = await SynthesizeResultsAsync(context, executor, ct);
        return context.BuildTeamResult(Name, synthesis);
    }
}
```

### SupervisorStrategy

A coordinator agent dispatches tasks dynamically, reviews results, and can redirect or retry.

### DebateStrategy

Multiple agents provide independent perspectives. A moderator synthesizes the best answer.

## TeamContext

All agents in a team share a `TeamContext` — a thread-safe shared state:

```csharp
public class TeamContext
{
    // Key-value scratchpad for sharing data between agents
    public ConcurrentDictionary<string, string> Scratchpad { get; }

    // Chronological event stream
    public IReadOnlyList<TeamEvent> Events { get; }

    // Per-agent results
    public IReadOnlyDictionary<string, AgentResult> Results { get; }

    public void AddEvent(TeamEvent evt);
    public void AddResult(string agentName, AgentResult result);
    public TeamResult BuildTeamResult(string strategyName, string? synthesis = null);
}
```

### TeamContext tools

Agents query shared context via the `query_team_context` SK function:

```csharp
[KernelFunction("query_team_context")]
[Description("Query shared team context")]
public string QueryTeamContext(
    [Description("Key: events, results, or a scratchpad key")] string key)
{
    return key switch
    {
        "events" => FormatEvents(_context.Events),
        "results" => FormatResults(_context.Results),
        _ => _context.Scratchpad.GetValueOrDefault(key, $"Key '{key}' not found")
    };
}
```

## SubagentConfig

Configure each agent in a team:

```csharp
public record SubagentConfig
{
    public string Name { get; init; }
    public SubagentType Type { get; init; }
    public string Prompt { get; init; }
    public string? SystemPrompt { get; init; }
    public int MaxTurns { get; init; } = 10;
    public string? ModelId { get; init; }
    public IReadOnlyList<string>? AdditionalTools { get; init; }
    public string? Perspective { get; init; }  // Used by DebateStrategy
}
```

## Result types

```csharp
public record AgentResult(
    string Output,
    int TokensUsed,
    TimeSpan ExecutionTime);

public record TeamResult(
    string FinalOutput,
    string StrategyName,
    IReadOnlyDictionary<string, AgentResult> AgentResults,
    int TotalTokens,
    TimeSpan TotalDuration);
```

## Implementing a custom strategy

### 1. Implement IOrchestrationStrategy

```csharp
public class RoundRobinStrategy : IOrchestrationStrategy
{
    public string Name => "round-robin";

    public async Task<TeamResult> ExecuteAsync(
        IReadOnlyList<SubagentConfig> agents,
        TeamContext context,
        ISubagentExecutor executor,
        AgentSession parentSession,
        Action<SubagentProgress>? onProgress = null,
        CancellationToken ct = default)
    {
        const int maxRounds = 3;
        for (int round = 0; round < maxRounds; round++)
        {
            foreach (var agent in agents)
            {
                var prompt = BuildRoundPrompt(agent, context, round);
                var result = await executor.ExecuteAsync(agent, prompt, ct);
                context.AddResult($"{agent.Name}-round-{round}", result);
                context.AddEvent(new TeamEvent(
                    $"{agent.Name} completed round {round}",
                    DateTimeOffset.UtcNow));

                onProgress?.Invoke(new SubagentProgress(
                    agent.Name, $"Round {round + 1}/{maxRounds} complete"));
            }
        }

        // Final synthesis
        var synthesis = await SynthesizeAsync(context, executor, ct);
        return context.BuildTeamResult(Name, synthesis);
    }
}
```

### 2. Register in DI

```csharp
services.AddSingleton<IOrchestrationStrategy, RoundRobinStrategy>();
```

The orchestration engine collects all `IOrchestrationStrategy` instances and matches by name.

## Progress events

The `onProgress` callback enables real-time UI rendering. JD.AI's Spectre.Console panel uses this to display agent status:

```csharp
onProgress?.Invoke(new SubagentProgress(
    AgentName: "security-reviewer",
    Status: "Analyzing authentication module...",
    IsComplete: false));
```

The progress panel shows:

- Current status per agent (running, completed, failed)
- Active task description
- Elapsed time

## Nesting guards

Orchestration includes depth guards to prevent infinite recursion:

- **Default max depth:** 2 levels of nesting
- **Configurable** via `AgentSession.MaxOrchestrationDepth`
- Teams that exceed the limit receive an error result

## Synthesis patterns

The synthesis step runs after all agents complete. Strategies use a dedicated synthesis prompt:

```csharp
private async Task<string> SynthesizeResultsAsync(
    TeamContext context, ISubagentExecutor executor, CancellationToken ct)
{
    var resultsBlock = string.Join("\n\n",
        context.Results.Select(r => $"## {r.Key}\n{r.Value.Output}"));

    var synthesisAgent = new SubagentConfig
    {
        Name = "synthesizer",
        Type = SubagentType.General,
        Prompt = $"Synthesize these results into a unified answer:\n\n{resultsBlock}"
    };

    var result = await executor.ExecuteAsync(synthesisAgent, synthesisAgent.Prompt, ct);
    return result.Output;
}
```

## See also

- [Subagents](subagents.md) — individual agent types and capabilities
- [Architecture Overview](index.md) — system architecture
- [Orchestration user guide](orchestration.md) — end-user documentation
