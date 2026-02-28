---
description: "Build and distribute JD.AI plugins using the Plugin SDK — register Semantic Kernel functions, hooks, and event handlers."
---

# Plugin SDK

The JD.AI Plugin SDK (`JD.AI.Plugins.SDK`) provides the interfaces, attributes, and manifest format for building plugins that extend the gateway with custom tools, event handlers, and integrations. Plugins are loaded as .NET assemblies and participate in the gateway's Semantic Kernel pipeline.

> [!NOTE]
> The Plugin SDK is for compiled .NET plugins that run inside the gateway process. For file-based skills and hooks (SKILL.md, hooks.json), see [Skills, Plugins, and Hooks](skills-and-plugins.md).

## Quick start

Create a new plugin in under five minutes:

```bash
# Create a class library
dotnet new classlib -n MyPlugin
cd MyPlugin

# Add the SDK reference
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
    public string Description => "A simple example plugin.";

    public Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
    {
        // Register a Semantic Kernel function
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

## IJdAiPlugin interface

Every plugin implements `IJdAiPlugin`:

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

### Lifecycle

1. **Discovery.** The gateway scans plugin directories for assemblies containing types marked with `[JdAiPlugin]` or implementing `IJdAiPlugin`.
2. **Instantiation.** The plugin is created via its parameterless constructor.
3. **Initialization.** `InitializeAsync` is called with an `IPluginContext`. Register all SK functions, event handlers, and configuration here.
4. **Active.** The plugin is live — its registered functions are available to agents.
5. **Shutdown.** `ShutdownAsync` is called when the gateway is stopping. Clean up resources.
6. **Disposal.** `DisposeAsync` is called after shutdown for final cleanup.

```
Discovery → Instantiation → InitializeAsync → [Active] → ShutdownAsync → DisposeAsync
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | Unique identifier (e.g., `"jd.ai.plugin.github"`). Use reverse-DNS style. |
| `Name` | `string` | Human-readable display name |
| `Version` | `string` | SemVer version string (e.g., `"1.0.0"`) |
| `Description` | `string` | Short description of what the plugin does |

## IPluginContext

The context object provides access to gateway services during initialization:

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

### Kernel

The `Kernel` property gives direct access to the Semantic Kernel instance. Register functions, plugins, and filters here:

```csharp
public Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
{
    // Register tool functions
    context.Kernel.Plugins.AddFromObject(new DatabaseTools(), "Database");

    // Register prompt functions
    context.Kernel.Plugins.AddFromPromptDirectory("./prompts");

    return Task.CompletedTask;
}
```

### Configuration

Plugin-specific key-value configuration from the manifest or gateway settings:

```csharp
public Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
{
    var apiKey = context.Configuration.GetValueOrDefault("api_key", "");
    var baseUrl = context.Configuration.GetValueOrDefault("base_url", "https://api.example.com");

    // Use configuration values...
    return Task.CompletedTask;
}
```

### Event handlers

Subscribe to gateway events with `OnEvent`:

```csharp
context.OnEvent("agent.spawned", async data =>
{
    context.Log(PluginLogLevel.Info, $"New agent spawned: {data}");
    await Task.CompletedTask;
});

context.OnEvent("agent.turn_complete", async data =>
{
    // Log every agent turn for auditing
    await AuditLogger.LogTurnAsync(data);
});
```

### Service resolution

Access any service from the gateway's DI container:

```csharp
var channelRegistry = context.GetService<IChannelRegistry>();
var eventBus = context.GetService<IEventBus>();
var sessionStore = context.GetService<SessionStore>();
```

### Logging

Use the `Log` method with the appropriate level:

```csharp
context.Log(PluginLogLevel.Debug, "Processing data...");
context.Log(PluginLogLevel.Info, "Plugin ready");
context.Log(PluginLogLevel.Warning, "Configuration value missing, using default");
context.Log(PluginLogLevel.Error, "Failed to connect to external service");
```

| Level | When to use |
|-------|-------------|
| `Debug` | Detailed diagnostic information |
| `Info` | Normal operational messages |
| `Warning` | Unexpected situations that don't prevent operation |
| `Error` | Failures that affect plugin functionality |

## JdAiPluginAttribute

Mark plugin classes with this attribute for assembly scanning:

```csharp
[JdAiPlugin(Id = "my-plugin", Name = "My Plugin")]
public class MyPlugin : IJdAiPlugin
{
    // ...
}
```

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string?` | Plugin ID override (falls back to `IJdAiPlugin.Id`) |
| `Name` | `string?` | Plugin name override (falls back to `IJdAiPlugin.Name`) |

## Plugin manifest

Distribute plugins with a `plugin.json` manifest:

```json
{
  "id": "jd.ai.plugin.github",
  "name": "GitHub Integration",
  "version": "1.2.0",
  "description": "GitHub PR reviews, issue management, and CI status checks.",
  "author": "JD.AI Contributors",
  "license": "MIT",
  "entryAssembly": "JD.AI.Plugin.GitHub.dll",
  "permissions": [
    "network",
    "read-events"
  ],
  "configuration": {
    "github_token": "",
    "default_org": ""
  }
}
```

### Manifest fields

| Field | Required | Type | Description |
|-------|:--------:|------|-------------|
| `id` | Yes | `string` | Unique plugin identifier |
| `name` | Yes | `string` | Display name |
| `version` | Yes | `string` | SemVer version |
| `description` | No | `string` | Plugin description |
| `author` | No | `string` | Author or organization |
| `license` | No | `string` | SPDX license identifier |
| `entryAssembly` | No | `string` | DLL file name (resolved relative to plugin directory) |
| `permissions` | No | `string[]` | Declared permissions the plugin requires |
| `configuration` | No | `object` | Default configuration values (overridable by host) |

### Plugin directory structure

```text
plugins/
└── jd.ai.plugin.github/
    ├── plugin.json
    ├── JD.AI.Plugin.GitHub.dll
    └── JD.AI.Plugin.GitHub.deps.json
```

Place plugin directories in:

| Location | Path | Scope |
|----------|------|-------|
| Personal | `~/.jdai/plugins/` | All projects |
| Project | `.jdai/plugins/` | Current project only |

## Example: audit logging plugin

A complete plugin that logs all agent interactions to a file:

```csharp
using JD.AI.Plugins.SDK;
using Microsoft.SemanticKernel;
using System.ComponentModel;

[JdAiPlugin(Id = "jd.ai.plugin.audit", Name = "Audit Logger")]
public sealed class AuditPlugin : IJdAiPlugin
{
    private StreamWriter? _writer;

    public string Id => "jd.ai.plugin.audit";
    public string Name => "Audit Logger";
    public string Version => "1.0.0";
    public string Description => "Logs all agent events to an audit file.";

    public async Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
    {
        var logPath = context.Configuration.GetValueOrDefault(
            "log_path", Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile), ".jdai", "audit.log"));

        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        _writer = new StreamWriter(logPath, append: true) { AutoFlush = true };

        // Subscribe to agent events
        context.OnEvent("agent.spawned", async data =>
        {
            await WriteLogAsync("SPAWN", data?.ToString() ?? "");
        });

        context.OnEvent("agent.turn_complete", async data =>
        {
            await WriteLogAsync("TURN", data?.ToString() ?? "");
        });

        context.OnEvent("agent.stopped", async data =>
        {
            await WriteLogAsync("STOP", data?.ToString() ?? "");
        });

        // Register a tool for querying audit logs
        context.Kernel.Plugins.AddFromObject(
            new AuditTools(logPath), "AuditTools");

        context.Log(PluginLogLevel.Info, $"Audit logging to {logPath}");
        await Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _writer?.Dispose();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _writer?.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task WriteLogAsync(string eventType, string data)
    {
        if (_writer is not null)
        {
            await _writer.WriteLineAsync(
                $"[{DateTimeOffset.UtcNow:O}] [{eventType}] {data}");
        }
    }
}

public sealed class AuditTools(string logPath)
{
    [KernelFunction("search_audit_log")]
    [Description("Search the audit log for entries matching a keyword")]
    public async Task<string> SearchAuditLog(
        [Description("Keyword to search for")] string keyword,
        [Description("Maximum number of results")] int maxResults = 10)
    {
        if (!File.Exists(logPath))
            return "No audit log found.";

        var matches = (await File.ReadAllLinesAsync(logPath))
            .Where(line => line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .TakeLast(maxResults)
            .ToList();

        return matches.Count == 0
            ? $"No entries matching '{keyword}'."
            : string.Join("\n", matches);
    }
}
```

### Manifest for the audit plugin

```json
{
  "id": "jd.ai.plugin.audit",
  "name": "Audit Logger",
  "version": "1.0.0",
  "description": "Logs all agent events to an audit file.",
  "author": "JD.AI",
  "license": "MIT",
  "entryAssembly": "JD.AI.Plugin.Audit.dll",
  "permissions": ["read-events", "filesystem"],
  "configuration": {
    "log_path": ""
  }
}
```

## Best practices

- **Keep plugins focused.** Each plugin should do one thing well. Prefer multiple small plugins over one large one.
- **Use configuration, not hardcoded values.** Put API keys, file paths, and feature flags in the manifest's `configuration` section.
- **Handle errors gracefully.** Plugins run inside the gateway process — unhandled exceptions affect the entire gateway. Catch exceptions in event handlers and log them.
- **Respect cancellation.** Pass `CancellationToken` through to async operations and check for cancellation in long-running work.
- **Log at appropriate levels.** Use `Debug` for development, `Info` for normal operation. Avoid logging sensitive data.
- **Declare permissions.** List the permissions your plugin needs in the manifest so administrators can review them before deployment.

## Plugin SDK vs file-based extensions

| Feature | Plugin SDK (this page) | File-based (SKILL.md / hooks.json) |
|---------|:---------------------:|:----------------------------------:|
| **Language** | C# / .NET | Markdown / JSON |
| **Capabilities** | Full SK API, DI, events | Instructions, tool filters |
| **Distribution** | NuGet / assembly | File copy |
| **Isolation** | In-process | In-process |
| **Use case** | Complex integrations | Agent instructions, simple filters |

## See also

- [Skills, Plugins, and Hooks](skills-and-plugins.md) — file-based extension system
- [Extending JD.AI](extending.md) — custom tools and providers
- [Gateway API Reference](gateway-api.md) — REST endpoints and SignalR hubs
