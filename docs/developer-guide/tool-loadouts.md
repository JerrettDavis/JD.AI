---
title: "Tool Loadouts"
description: "Create curated, context-aware tool bundles for agents — ToolLoadout, ToolCategory, ToolLoadoutBuilder, IToolLoadoutRegistry, inheritance, wildcard patterns, and integration with AgentSession and SubagentRunner."
---

# Tool Loadouts

Tool Loadouts (also called *Toolbelts*) let you expose a **curated subset** of tools to an agent instead of the full registry. As the JD.AI tool catalog grows, providing every tool to every agent becomes increasingly expensive (more tokens), noisy (worse tool selection), and model-incompatible (small context windows). Loadouts solve all three problems.

## Why loadouts matter

| Problem | Without loadouts | With loadouts |
|---------|-----------------|---------------|
| Context size | Every tool sent to every model | Only relevant tools sent |
| Tool selection accuracy | Models hallucinate or choose the wrong tool | Smaller set → higher precision |
| Model compatibility | Small/local models overwhelmed | Tight budgets respected |
| Agent specialization | All agents see all tools | Each agent type gets what it needs |
| Scalability | Degradation with 150+ tools | Clean at 1000+ tools |

---

## Core concepts

### Tool categories

Tools are grouped into 13 logical **categories** defined in `ToolCategory`:

| Category | Description | Example plugins |
|----------|-------------|----------------|
| `Filesystem` | File read/write, directory, diff, notebooks | `file`, `batchEdit`, `diff`, `notebook` |
| `Git` | Version control | `git` |
| `GitHub` | GitHub API integration | `github` |
| `Shell` | Shell execution and process management | `shell`, `environment`, `clipboard`, `runtime` |
| `Web` | Web fetching and browser automation | `web`, `browser` |
| `Search` | Content search and file patterns | `search`, `websearch` |
| `Network` | Network connectivity tools | `tailscale` |
| `Memory` | Semantic memory and knowledge stores | `memory` |
| `Orchestration` | Agents, sessions, MCP, channels | `tasks`, `sessions`, `mcp`, `capabilities`, `channels` |
| `Analysis` | Reasoning, benchmarks, introspection | `think`, `benchmark`, `usage`, `parityDocs` |
| `Scheduling` | Cron and scheduled execution | `scheduler` |
| `Multimodal` | Image and PDF processing | `multimodal` |
| `Security` | Policy, encoding, cryptography | `policy`, `encoding` |

### Tool loadout

A `ToolLoadout` is an immutable configuration object describing:

| Property | Type | Purpose |
|----------|------|---------|
| `Name` | `string` | Unique identifier (e.g. `"developer"`) |
| `ParentLoadoutName` | `string?` | Optional parent for inheritance |
| `DefaultPlugins` | `IReadOnlySet<string>` | Explicitly named plugins to always load |
| `IncludedCategories` | `IReadOnlySet<ToolCategory>` | All plugins in these categories are loaded |
| `DiscoverablePatterns` | `IReadOnlyList<string>` | Plugins available on request (not eager); supports `*` wildcard suffix |
| `DisabledPlugins` | `IReadOnlySet<string>` | Explicitly blocked plugins; overrides any inclusion |

### Discoverable tools

Discoverable tools are **available on request but not eagerly loaded**. This lets agents ask "do I have docker tools?" and receive a list of available-but-unloaded capabilities, then request them explicitly. Agents using the `capability_list` tool can always enumerate their active plugins.

Patterns support:

- **Exact match:** `"docker"` — only the plugin named `docker`
- **Prefix wildcard:** `"docker*"` — any plugin whose name starts with `docker`
- **Catch-all:** `"*"` — all unloaded, non-disabled plugins are discoverable

---

## Built-in loadouts

The `ToolLoadoutRegistry` ships five loadouts defined in `WellKnownLoadouts`:

| Constant | Name | Included categories |
|----------|------|---------------------|
| `WellKnownLoadouts.Minimal` | `"minimal"` | Filesystem, Shell + `think` plugin; `"*"` discoverable |
| `WellKnownLoadouts.Developer` | `"developer"` | Minimal + Git, GitHub, Search, Analysis, Memory; `docker*`, `mcp*` discoverable |
| `WellKnownLoadouts.Research` | `"research"` | Minimal + Search, Web, Memory, Multimodal |
| `WellKnownLoadouts.DevOps` | `"devops"` | Minimal + Git, Network, Scheduling; `docker*`, `kube*`, `terraform*` discoverable |
| `WellKnownLoadouts.Full` | `"full"` | All 13 categories |

Inheritance: `developer`, `research`, and `devops` all extend `minimal`.

---

## Fluent builder API

Use `ToolLoadoutBuilder` to create custom loadouts:

```csharp
using JD.AI.Core.Tools;

var myLoadout = ToolLoadoutBuilder
    .Create("security-reviewer")
    .Extends(WellKnownLoadouts.Developer)   // inherit developer defaults
    .IncludeCategory(ToolCategory.Security) // add security tools
    .AddPlugin("think")                     // explicit named plugin
    .AddDiscoverable("docker*")             // docker tools available on request
    .Disable("websearch")                   // never expose web search
    .Build();
```

### Builder methods

| Method | Effect |
|--------|--------|
| `Create(name)` | Start a new builder |
| `Extends(parentName)` | Inherit from a named parent |
| `IncludeCategory(cat)` | Load all plugins in this category |
| `AddPlugin(name)` | Load a specific named plugin |
| `AddDiscoverable(pattern)` | Mark a pattern as discoverable (lazy) |
| `Disable(name)` | Explicitly block a plugin (overrides all inclusions) |
| `Build()` | Return the immutable `ToolLoadout` |

---

## Registry

`IToolLoadoutRegistry` is the central service for managing and resolving loadouts.

```csharp
public interface IToolLoadoutRegistry
{
    void Register(ToolLoadout loadout);
    ToolLoadout? GetLoadout(string name);
    IReadOnlyList<ToolLoadout> GetAll();

    IReadOnlySet<string> ResolveActivePlugins(
        string loadoutName,
        IEnumerable<KernelPlugin> availablePlugins);

    IReadOnlySet<string> ResolveDiscoverablePlugins(
        string loadoutName,
        IEnumerable<KernelPlugin> availablePlugins);
}
```

### Registering a custom loadout

```csharp
var registry = new ToolLoadoutRegistry(); // starts with 5 built-ins

var custom = ToolLoadoutBuilder
    .Create("data-analyst")
    .IncludeCategory(ToolCategory.Filesystem)
    .IncludeCategory(ToolCategory.Search)
    .IncludeCategory(ToolCategory.Analysis)
    .AddDiscoverable("*")
    .Build();

registry.Register(custom);
```

> [!NOTE]
> Registering a loadout with the same name as an existing one **overwrites** the previous definition.

### Resolving active plugins

Given a set of plugins registered on the kernel, the registry returns which ones should be loaded:

```csharp
var activePlugins = registry.ResolveActivePlugins(
    "developer",
    kernel.Plugins);    // IEnumerable<KernelPlugin>

// Returns: {"file", "shell", "think", "git", "github", "search", ...}
```

### Resolving discoverable plugins

Returns plugins that are available but not currently active:

```csharp
var discoverable = registry.ResolveDiscoverablePlugins(
    "developer",
    kernel.Plugins);

// Returns: {"mcp", "docker", ...} — available on request
```

---

## Inheritance and resolution

Loadouts form an **inheritance chain**. When resolving, settings from the full chain are merged root-first:

```
developer → minimal → (no parent)
```

Resolution order:

1. Walk the parent chain from root to leaf, building lists of `DefaultPlugins`, `IncludedCategories`, and `DiscoverablePatterns`
2. A plugin is **active** if it appears in `DefaultPlugins` OR its category is in `IncludedCategories`
3. A plugin is **disabled** if it appears in `DisabledPlugins` at **any** level (disabled takes precedence over inclusions)
4. A non-active, non-disabled plugin is **discoverable** if it matches any pattern in `DiscoverablePatterns`

**Cycle detection:** if a parent chain forms a cycle (A → B → A), the chain is silently broken after the first revisit — no infinite loop, no exception.

**Unknown parent:** if a parent name is not found in the registry, the chain simply stops at that point.

### Example — disable overriding inheritance

```csharp
// Parent includes Shell
var parent = ToolLoadoutBuilder
    .Create("base")
    .IncludeCategory(ToolCategory.Shell)
    .Build();

// Child extends parent, but disables shell
var child = ToolLoadoutBuilder
    .Create("restricted")
    .Extends("base")
    .Disable("shell")
    .Build();

// Result: shell is NOT active even though parent included the Shell category
```

---

## Integration with AgentSession

`AgentSession` exposes the active loadout via:

```csharp
public string? ActiveLoadoutName { get; set; }
```

When `null` (the default), all registered plugins are available. Setting it signals which loadout should be used to filter tools for this session:

```csharp
session.ActiveLoadoutName = WellKnownLoadouts.Minimal;
```

> [!NOTE]
> `ActiveLoadoutName` is informational by default — setting it on a session does not automatically filter the kernel's plugins. Call `IToolLoadoutRegistry.ResolveActivePlugins` to get the plugin set to load, then build a scoped kernel with only those plugins (see SubagentRunner for a reference implementation).

---

## Integration with SubagentRunner

`SubagentRunner` accepts an optional `IToolLoadoutRegistry`. When provided, each subagent type is mapped to a loadout name via `GetLoadoutName`:

```csharp
var runner = new SubagentRunner(parentSession, loadoutRegistry);
```

| SubagentType | Mapped loadout |
|-------------|----------------|
| `Explore` | `research` |
| `Task` | `minimal` |
| `Plan` | `developer` |
| `Review` | `developer` |
| `General` | `full` |

When a registry is **not** provided, the runner uses a hardcoded fallback plugin set that matches the actual plugin names registered in `Program.cs`.

---

## Plugin name reference

The `ToolLoadoutRegistry.PluginCategoryMap` dictionary maps every known plugin name to its `ToolCategory`. Plugin names are the strings passed as the second argument to `AddFromType<T>(name)` or `AddFromObject(obj, name)`.

| Plugin name | Category | Registered as |
|-------------|----------|---------------|
| `file` | Filesystem | `AddFromType<FileTools>("file")` |
| `batchEdit` | Filesystem | `AddFromType<BatchEditTools>("batchEdit")` |
| `diff` | Filesystem | `AddFromType<DiffTools>("diff")` |
| `notebook` | Filesystem | `AddFromType<NotebookTools>("notebook")` |
| `migration` | Filesystem | `AddFromType<MigrationTools>("migration")` |
| `git` | Git | `AddFromType<GitTools>("git")` |
| `github` | GitHub | `AddFromType<GitHubTools>("github")` |
| `shell` | Shell | `AddFromType<ShellTools>("shell")` |
| `environment` | Shell | `AddFromType<EnvironmentTools>("environment")` |
| `clipboard` | Shell | `AddFromType<ClipboardTools>("clipboard")` |
| `runtime` | Shell | `AddFromObject(new ExecProcessTools(...), "runtime")` |
| `web` | Web | `AddFromType<WebTools>("web")` |
| `browser` | Web | `AddFromType<BrowserTools>("browser")` |
| `search` | Search | `AddFromType<SearchTools>("search")` |
| `websearch` | Search | `AddFromType<WebSearchTools>("websearch")` |
| `tailscale` | Network | `AddFromType<TailscaleTools>("tailscale")` |
| `memory` | Memory | `AddFromObject(new MemoryTools(), "memory")` |
| `tasks` | Orchestration | `AddFromObject(taskTools, "tasks")` |
| `sessions` | Orchestration | `AddFromObject(new SessionOrchestrationTools(...), "sessions")` |
| `mcp` | Orchestration | `AddFromType<McpTransportTools>("mcp")` |
| `mcpEcosystem` | Orchestration | `AddFromType<McpEcosystemTools>("mcpEcosystem")` |
| `capabilities` | Orchestration | `AddFromObject(new CapabilityTools(kernel), "capabilities")` |
| `channels` | Orchestration | `AddFromObject(new ChannelOpsTools(...), "channels")` |
| `think` | Analysis | `AddFromType<ThinkTools>("think")` |
| `parityDocs` | Analysis | `AddFromType<ParityDocsTools>("parityDocs")` |
| `skillParity` | Analysis | `AddFromType<SkillParityTools>("skillParity")` |
| `benchmark` | Analysis | `AddFromObject(new BenchmarkTools(kernel), "benchmark")` |
| `usage` | Analysis | `AddFromObject(usageTools, "usage")` |
| `scheduler` | Scheduling | `AddFromObject(new SchedulerTools(), "scheduler")` |
| `multimodal` | Multimodal | `AddFromType<MultimodalTools>("multimodal")` |
| `policy` | Security | `AddFromObject(new PolicyTools(...), "policy")` |
| `encoding` | Security | `AddFromType<EncodingCryptoTools>("encoding")` |

---

## Adding a new plugin to the map

When you add a new tool plugin, register it in `ToolLoadoutRegistry.PluginCategoryMap`:

```csharp
// In ToolLoadoutRegistry.cs
public static readonly IReadOnlyDictionary<string, ToolCategory> PluginCategoryMap =
    new Dictionary<string, ToolCategory>(StringComparer.OrdinalIgnoreCase)
    {
        // ... existing entries ...

        // Your new plugin
        ["myPlugin"] = ToolCategory.Filesystem,
    };
```

Then register the plugin as usual in the kernel setup:

```csharp
kernel.Plugins.AddFromType<MyPluginTools>("myPlugin");
```

---

## Implementing a loadout-aware tool host

The following pattern shows how to build a kernel that only loads the plugins permitted by a loadout:

```csharp
public Kernel BuildScopedKernel(
    Kernel fullKernel,
    string loadoutName,
    IToolLoadoutRegistry registry)
{
    var builder = Kernel.CreateBuilder();

    // Copy the chat completion service
    builder.Services.AddSingleton(
        fullKernel.GetRequiredService<IChatCompletionService>());

    var scoped = builder.Build();

    // Resolve which plugins should be active
    var allowedNames = registry.ResolveActivePlugins(
        loadoutName, fullKernel.Plugins);

    foreach (var plugin in fullKernel.Plugins)
    {
        if (allowedNames.Contains(plugin.Name))
            scoped.Plugins.Add(plugin);
    }

    return scoped;
}
```

---

## Testing loadouts

Loadouts are plain value objects — test them without a running LLM:

```csharp
[Fact]
public void DeveloperLoadout_IncludesGitPlugin()
{
    var registry = new ToolLoadoutRegistry();
    var plugins = new[]
    {
        KernelPluginFactory.CreateFromObject(new FakeTool(), "git"),
        KernelPluginFactory.CreateFromObject(new FakeTool(), "shell"),
        KernelPluginFactory.CreateFromObject(new FakeTool(), "docker"),
    };

    var active = registry.ResolveActivePlugins(
        WellKnownLoadouts.Developer, plugins);

    Assert.Contains("git", active);
    Assert.Contains("shell", active);
    Assert.DoesNotContain("docker", active); // discoverable, not active
}

[Fact]
public void CustomLoadout_DisableOverridesCategory()
{
    var registry = new ToolLoadoutRegistry();

    var restricted = ToolLoadoutBuilder
        .Create("restricted")
        .IncludeCategory(ToolCategory.Shell)
        .Disable("shell")
        .Build();
    registry.Register(restricted);

    var plugins = new[] { KernelPluginFactory.CreateFromObject(new FakeTool(), "shell") };
    var active = registry.ResolveActivePlugins("restricted", plugins);

    Assert.DoesNotContain("shell", active);
}
```

---

## See also

- [Custom Tools](custom-tools.md) — writing Semantic Kernel tool plugins
- [Subagents](subagents.md) — how subagent types map to loadouts
- [Architecture Overview](index.md) — tool pipeline and agent lifecycle
- [Tools (user guide)](../user-guide/tools.md) — what tools are available
- [Tools Reference](../reference/tools.md) — full parameter documentation
