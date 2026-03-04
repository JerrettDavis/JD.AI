---
title: "Plugin SDK"
description: "Build and distribute JD.AI plugins — the Plugin SDK, file-based skills and hooks, plugin manifest, and the complete plugin lifecycle."
---

# Plugin SDK

JD.AI supports two extension mechanisms: the **Plugin SDK** for compiled .NET plugins that run inside the gateway, and **file-based skills and hooks** for markdown-driven agent instructions and tool filters. This guide covers both.

## Plugin SDK (compiled .NET plugins)

The `JD.AI.Plugins.SDK` NuGet package provides interfaces, attributes, and the manifest format for building gateway plugins.

### Quick start

```bash
dotnet new classlib -n MyPlugin
cd MyPlugin
dotnet add package JD.AI.Plugins.SDK
```

Implement `IJdAiPlugin`:

```csharp
using JD.AI.Plugins.SDK;
using Microsoft.SemanticKernel;
using System.ComponentModel;

[JdAiPlugin(Id = "my-plugin", Name = "My Plugin")]
public class MyPlugin : IJdAiPlugin
{
    public string Id => "my-plugin";
    public string Name => "My Plugin";
    public string Version => "1.0.0";
    public string Description => "A custom JD.AI plugin.";

    public Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
    {
        context.Kernel.Plugins.AddFromObject(new MyTools(), "MyTools");
        context.Log(PluginLogLevel.Info, "My Plugin initialized");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class MyTools
{
    [KernelFunction("greet")]
    [Description("Greets a user by name")]
    public string Greet([Description("The user's name")] string name)
        => $"Hello, {name}! Welcome to JD.AI.";
}
```

### IJdAiPlugin interface

```csharp
public interface IJdAiPlugin : IAsyncDisposable
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Description { get; }

    Task InitializeAsync(IPluginContext context, CancellationToken ct = default);
    Task ShutdownAsync(CancellationToken ct = default);
}
```

### Plugin lifecycle

```
Discovery → Instantiation → InitializeAsync → [Active] → ShutdownAsync → DisposeAsync
```

1. **Discovery** — The gateway scans plugin directories for assemblies with `[JdAiPlugin]` types
2. **Instantiation** — Plugin created via parameterless constructor
3. **Initialization** — `InitializeAsync` receives `IPluginContext` for registering SK functions, events, and configuration
4. **Active** — Registered functions are available to agents
5. **Shutdown** — `ShutdownAsync` called when gateway stops
6. **Disposal** — `DisposeAsync` for final cleanup

### IPluginContext

```csharp
public interface IPluginContext
{
    Kernel Kernel { get; }
    IReadOnlyDictionary<string, string> Configuration { get; }

    void OnEvent(string eventType, Func<object?, Task> handler);
    T? GetService<T>() where T : class;
    void Log(PluginLogLevel level, string message);
}
```

| Member | Purpose |
|--------|---------|
| `Kernel` | Register SK functions and plugins |
| `Configuration` | Plugin-specific key-value settings from the manifest |
| `OnEvent` | Subscribe to gateway events (`agent.spawned`, `agent.turn_complete`, etc.) |
| `GetService<T>` | Resolve services from the gateway DI container |
| `Log` | Structured logging at Debug, Info, Warning, or Error level |

### Event handling

```csharp
context.OnEvent("agent.spawned", async data =>
{
    context.Log(PluginLogLevel.Info, $"Agent spawned: {data}");
    await Task.CompletedTask;
});

context.OnEvent("agent.turn_complete", async data =>
{
    await AuditLogger.LogTurnAsync(data);
});
```

### Service resolution

Access gateway services via DI:

```csharp
var channelRegistry = context.GetService<IChannelRegistry>();
var eventBus = context.GetService<IEventBus>();
var sessionStore = context.GetService<SessionStore>();
```

### Plugin manifest

Distribute plugins with a `plugin.json`:

```json
{
  "id": "jd.ai.plugin.github",
  "name": "GitHub Integration",
  "version": "1.2.0",
  "description": "GitHub PR reviews, issue management, and CI status.",
  "author": "JD.AI Contributors",
  "license": "MIT",
  "entryAssembly": "JD.AI.Plugin.GitHub.dll",
  "permissions": ["network", "read-events"],
  "configuration": {
    "github_token": "",
    "default_org": ""
  }
}
```

### Plugin directories

| Location | Path | Scope |
|----------|------|-------|
| Personal | `~/.jdai/plugins/` | All projects |
| Project | `.jdai/plugins/` | Current project only |

```text
plugins/
└── jd.ai.plugin.github/
    ├── plugin.json
    ├── JD.AI.Plugin.GitHub.dll
    └── JD.AI.Plugin.GitHub.deps.json
```

## File-based skills

Skills are markdown files (`SKILL.md`) that provide instructions and context to the AI agent. JD.AI loads skills through the `JD.SemanticKernel.Extensions` bridge.

### Skill locations

| Location | Path | Scope |
|----------|------|-------|
| Personal | `~/.claude/skills/<name>/SKILL.md` | All projects |
| Project | `.claude/skills/<name>/SKILL.md` | This project only |

Project skills take precedence when names collide.

### SKILL.md format

```markdown
---
name: code-review
description: Review code for quality and best practices
allowed-tools:
  - read_file
  - grep
  - git_diff
---

When reviewing code:
1. Check for error handling
2. Verify input validation
3. Look for security vulnerabilities
4. Ensure test coverage
```

### How skills load

At startup, JD.AI:

1. Scans `~/.claude/skills/` for personal skills
2. Scans `.claude/skills/` in the project directory
3. Parses YAML frontmatter (`name`, `description`, `allowed-tools`)
4. Registers each skill as available context

### Semantic Kernel mapping

| Claude Code concept | Semantic Kernel equivalent |
|---------------------|---------------------------|
| `SKILL.md` | `KernelFunction` (prompt-based) |
| `hooks.json` | `IFunctionInvocationFilter` / `IPromptRenderFilter` |
| `plugin.json` | Plugin with dependency resolution |

## File-based hooks

Hooks are event-driven filters that run before or after tool execution.

### hooks.json format

```json
{
  "hooks": [
    {
      "event": "PreToolUse",
      "tool": "run_command",
      "action": "confirm",
      "message": "This will execute a shell command. Continue?"
    },
    {
      "event": "PostToolUse",
      "tool": "write_file",
      "action": "log",
      "message": "File written: {{result}}"
    }
  ]
}
```

### Hook events

| Event | When | Capabilities |
|-------|------|-------------|
| `PreToolUse` | Before a tool is invoked | Modify arguments, block execution |
| `PostToolUse` | After a tool completes | Post-process results, log/audit |

### How hooks integrate

Hooks from `hooks.json` are registered as `IFunctionInvocationFilter` instances in the Semantic Kernel pipeline:

```
LLM tool_call → PreToolUse hooks → Tool execution → PostToolUse hooks → Result to LLM
```

### Plugin directory with hooks

```text
.claude/plugins/my-plugin/
├── plugin.json              # Manifest
├── skills/
│   └── my-skill/SKILL.md   # Plugin skills
└── hooks/
    └── hooks.json           # Plugin hooks
```

## Plugin SDK vs file-based extensions

| Feature | Plugin SDK | File-based (SKILL.md / hooks.json) |
|---------|:---------:|:----------------------------------:|
| **Language** | C# / .NET | Markdown / JSON |
| **Capabilities** | Full SK API, DI, events | Instructions, tool filters |
| **Distribution** | NuGet / assembly | File copy |
| **Isolation** | In-process | In-process |
| **Use case** | Complex integrations | Agent instructions, simple filters |

## Best practices

- **Keep plugins focused** — each plugin should do one thing well
- **Use configuration** — put API keys and feature flags in the manifest `configuration` section
- **Handle errors gracefully** — plugins run in-process; unhandled exceptions affect the gateway
- **Respect cancellation** — pass `CancellationToken` through to async operations
- **Log at appropriate levels** — use `Debug` during development, `Info` for normal operation
- **Declare permissions** — list required permissions in the manifest for admin review

## See also

- [Architecture Overview](index.md) — how plugins fit into the system
- [Custom Tools](custom-tools.md) — writing Semantic Kernel tool functions
- [Gateway API](gateway-api.md) — REST endpoints and SignalR hubs
- [Extending JD.AI](extending.md) — development setup and conventions
