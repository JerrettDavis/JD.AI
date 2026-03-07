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

### Installed lifecycle management

JD.AI persists Plugin SDK installs in a registry and exposes explicit lifecycle operations:

```
install → enable → disable → update → uninstall
```

`jdai` CLI:

```bash
jdai plugin list
jdai plugin install ./artifacts/My.Plugin.1.0.0.nupkg
jdai plugin enable my-plugin
jdai plugin disable my-plugin
jdai plugin update my-plugin
jdai plugin update            # update all installed plugins
jdai plugin uninstall my-plugin
```

Interactive slash commands:

```text
/plugins
/plugins install <path-or-url>
/plugins enable <id>
/plugins disable <id>
/plugins update [id]
/plugins uninstall <id>
/plugins info <id>
```

Gateway REST API:

- `GET /api/plugins`
- `GET /api/plugins/{id}`
- `POST /api/plugins/install`
- `POST /api/plugins/{id}/enable`
- `POST /api/plugins/{id}/disable`
- `POST /api/plugins/{id}/update`
- `POST /api/plugins/update`
- `DELETE /api/plugins/{id}`

Install sources currently supported:

- Local unpacked plugin directory
- Local `.zip` / `.nupkg` package file
- Direct `http(s)` package URL

Each installed plugin is isolated in its own collectible `AssemblyLoadContext`, and load failures are recorded without crashing the host process.

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
| `GetService<T>` | Resolve services from the gateway DI container (permission-gated) |
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

Access gateway services via DI. Plugins must declare matching service permissions in `plugin.json`:

```csharp
var channelRegistry = context.GetService<IChannelRegistry>();
var eventBus = context.GetService<IEventBus>();
var sessionStore = context.GetService<SessionStore>();
```

For `GetService<T>()`, declare either:

- `service:*` (all DI service access)
- `service:Namespace.TypeName` (full CLR type name)
- `service:TypeName` (short type name)

### Plugin manifest

Distribute plugins with a `plugin.json`:

```json
{
  "id": "jd.ai.plugin.github",
  "name": "GitHub Integration",
  "version": "1.2.0",
  "description": "GitHub PR reviews, issue management, and CI status.",
  "author": "JD.AI Contributors",
  "publisher": "JD.AI Contributors",
  "license": "MIT",
  "entryAssembly": "JD.AI.Plugin.GitHub.dll",
  "entryAssemblySha256": "C7E332D87854B9D4FEFD93A0D0414F6E594D256973A4689DCE8A273A7A6CE7A1",
  "permissions": [
    "event:*",
    "service:JD.AI.Core.Events.IEventBus",
    "service:ILoggerFactory"
  ],
  "configuration": {
    "github_token": "",
    "default_org": ""
  }
}
```

### Plugin security policy

- `permissions` are enforced at runtime with deny-by-default behavior.
- `GetService<T>()` and `OnEvent(...)` requests are audited in host logs.
- Optional trusted publisher enforcement is controlled via `JDAI_PLUGIN_TRUSTED_PUBLISHERS`:

```bash
export JDAI_PLUGIN_TRUSTED_PUBLISHERS="JD.AI Contributors,Contoso Security Team"
```

When set, plugin install/load fails unless `publisher` (or `author`) matches one of the trusted values.

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

Skills are markdown files (`SKILL.md`) that provide instructions and context to the AI agent. JD.AI loads skills through a native lifecycle manager with deterministic precedence, schema validation, eligibility gating, and hot reload.

### Skill source precedence

| Location | Path | Scope |
|----------|------|-------|
| Bundled | `<install>/skills/<name>/SKILL.md` | Product-shipped baseline |
| Managed | `~/.jdai/skills/<name>/SKILL.md` | User-global skills |
| Workspace | `.jdai/skills/<name>/SKILL.md` | Repository-local skills |
| Legacy (compat) | `~/.claude/skills/<name>/SKILL.md` | Imported with lower precedence |
| Legacy (compat) | `.claude/skills/<name>/SKILL.md` | Imported with lower precedence |

When two skills share the same `name`, precedence is `workspace > managed > bundled`, then source order as a tie-breaker. Lower-precedence duplicates are retained in status output as `shadowed`.

### Runtime config (`skills.json`)

| Location | Path | Scope |
|----------|------|-------|
| User | `~/.jdai/skills.json` | User-wide skill policy and entries |
| Workspace | `.jdai/skills.json` | Repo-local overrides |

```json
{
  "skills": {
    "load": {
      "watch": true,
      "watchDebounceMs": 250
    },
    "allowBundled": ["code-review"],
    "entries": {
      "my-skill": {
        "enabled": true,
        "apiKey": "env-or-secret-source",
        "env": {
          "MY_TOKEN": "value"
        },
        "config": {
          "feature": {
            "enabled": true
          }
        }
      }
    }
  }
}
```

`allowBundled` is optional and only applies to bundled skills. When present, bundled skills not listed are excluded.

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

1. Discovers `SKILL.md` files from bundled, managed, workspace, and legacy compatibility sources
2. Parses and schema-validates frontmatter
3. Resolves conflicts by precedence
4. Applies eligibility gates (`os`, `requires.bins`, `requires.anyBins`, `requires.env`, `requires.config`, and config-based disable)
5. Registers only eligible skills into the runtime plugin set

### Frontmatter metadata gates

JD.AI supports provider metadata under `metadata.jdai` (or `metadata.openclaw` for compatibility):

- `skillKey`
- `always`
- `primaryEnv`
- `os`
- `requires.bins`
- `requires.anyBins`
- `requires.env`
- `requires.config`

Unknown keys are rejected as invalid schema and surfaced in status output.

### Status and reload commands

Use interactive commands to inspect and refresh skills:

```text
/skills          # same as /skills status
/skills status   # deterministic eligibility report
/skills reload   # force refresh from filesystem/config
```

The status report includes explicit states (`active`, `excluded`, `shadowed`, `invalid`) with reason codes.

### Run-scoped environment injection

Environment variables configured in `skills.json` are injected only for the current turn execution scope and restored immediately after the run completes. Values are not persisted to transcript history.

### Semantic Kernel mapping

| External skill-system concept | Semantic Kernel equivalent |
|-------------------------------|---------------------------|
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
.jdai/plugins/my-plugin/
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
