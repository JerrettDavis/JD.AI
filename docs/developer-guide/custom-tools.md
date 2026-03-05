---
title: "Custom Tools"
description: "How to write custom Semantic Kernel tools for JD.AI — KernelFunction creation, tool safety, registration, parameter schemas, and testing."
---

# Custom Tools

Tools in JD.AI are Semantic Kernel plugins — plain C# classes with `[KernelFunction]` attributes. The AI agent discovers and invokes them automatically based on their descriptions and parameter schemas.

## How tools work

Every tool call flows through the Semantic Kernel function invocation pipeline:

```
LLM decides to call a tool
  → SK deserializes parameters from the tool_call JSON
  → IFunctionInvocationFilter chain runs (including ToolConfirmationFilter)
  → User confirms (or auto-approved via /autorun)
  → Your [KernelFunction] method executes
  → Return value is serialized and sent back to the LLM
```

## Creating a tool

Create a class in `src/JD.AI.Core/Tools/` with `[KernelFunction]` methods:

```csharp
using System.ComponentModel;
using Microsoft.SemanticKernel;

public class DatabaseTools
{
    private readonly string _connectionString;

    public DatabaseTools(string connectionString)
    {
        _connectionString = connectionString;
    }

    [KernelFunction("query_database")]
    [Description("Execute a read-only SQL query and return results as a table")]
    public async Task<string> QueryDatabaseAsync(
        [Description("SQL SELECT query to execute")] string query,
        [Description("Maximum rows to return")] int maxRows = 50,
        CancellationToken ct = default)
    {
        if (!query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return "Error: Only SELECT queries are allowed.";

        // Execute query and format results...
        return formattedResults;
    }

    [KernelFunction("list_tables")]
    [Description("List all tables in the database")]
    public async Task<string> ListTablesAsync(CancellationToken ct = default)
    {
        // Implementation...
        return tableList;
    }
}
```

### Key rules

1. **Every public method** that should be a tool needs `[KernelFunction("tool_name")]`
2. **Every method and parameter** needs `[Description("...")]` — the LLM uses these to decide when and how to call the tool
3. **Return `string`** — the LLM reads the return value as text. For complex data, format it as a table, JSON, or markdown
4. **Use `snake_case`** for function names — this matches the convention used by all built-in tools
5. **Accept `CancellationToken`** for async operations — SK passes it automatically

## Parameter types

Semantic Kernel handles parameter deserialization from the LLM's JSON tool call:

| C# Type | JSON Type | Notes |
|---------|-----------|-------|
| `string` | `string` | Most common |
| `int`, `long` | `number` | Integers |
| `double`, `float` | `number` | Floating point |
| `bool` | `boolean` | Flags |
| `int?`, `string?` | nullable | Optional parameters — use default values |
| `enum` | `string` | SK maps string values to enum members |

```csharp
[KernelFunction("search_logs")]
[Description("Search application logs")]
public string SearchLogs(
    [Description("Search pattern (regex)")] string pattern,
    [Description("Log level filter")] LogLevel? level = null,
    [Description("Maximum results")] int maxResults = 20,
    [Description("Include timestamps")] bool includeTimestamps = true)
{
    // Parameters with defaults are optional for the LLM
}

public enum LogLevel { Debug, Info, Warning, Error, Critical }
```

## Tool safety

JD.AI uses a `ToolConfirmationFilter` (`IFunctionInvocationFilter`) that prompts the user before tool execution. Safety is managed at the execution level:

- **Read-only tools** (e.g., `read_file`, `grep`, `glob`) — safe, can be auto-approved via `/autorun`
- **Write tools** (e.g., `write_file`, `edit_file`) — require confirmation by default
- **Destructive tools** (e.g., `run_command`) — always require confirmation unless explicitly overridden

When writing tools, design with safety in mind:

```csharp
// GOOD: Separate read and write operations
[KernelFunction("list_deployments")]
[Description("List all active deployments (read-only)")]
public async Task<string> ListDeploymentsAsync(CancellationToken ct = default)
{
    // Safe read-only operation
}

[KernelFunction("restart_deployment")]
[Description("Restart a deployment by name — this causes downtime")]
public async Task<string> RestartDeploymentAsync(
    [Description("Deployment name")] string name,
    CancellationToken ct = default)
{
    // Destructive — user will be prompted to confirm
}
```

## Registering tools

Register your tools with the Semantic Kernel instance:

```csharp
// In the kernel setup (e.g., Program.cs or a DI registration)
kernel.Plugins.AddFromObject(new DatabaseTools(connectionString), "DatabaseTools");
```

The plugin name (`"DatabaseTools"`) is used for namespacing — the LLM sees functions as `DatabaseTools-query_database`.

### Registering with constructor dependencies

If your tool needs services from DI, resolve them during registration:

```csharp
var dbTools = new DatabaseTools(
    configuration.GetConnectionString("Default")!);
kernel.Plugins.AddFromObject(dbTools, "Database");
```

## Patterns from built-in tools

### FileTools pattern — working directory scoping

```csharp
public class FileTools
{
    private readonly string _workingDirectory;

    public FileTools(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }

    [KernelFunction("read_file")]
    [Description("Read file contents with an optional line range")]
    public string ReadFile(
        [Description("File path (relative to working directory)")] string path,
        [Description("Start line (1-based, optional)")] int? startLine = null,
        [Description("End line (-1 for end of file, optional)")] int? endLine = null)
    {
        var fullPath = Path.GetFullPath(path, _workingDirectory);
        // Validate path is within working directory
        // Read and return contents
    }
}
```

### GitTools pattern — async shell execution

```csharp
public class GitTools
{
    private readonly string _workingDirectory;

    public GitTools(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }

    [KernelFunction("git_status")]
    [Description("Show the working tree status")]
    public async Task<string> GitStatusAsync(
        [Description("Repository path")] string? path = null,
        CancellationToken ct = default)
    {
        var repoPath = path ?? _workingDirectory;
        // Execute git command and return output
    }

    [KernelFunction("git_diff")]
    [Description("Show changes between commits, working tree, etc.")]
    public async Task<string> GitDiffAsync(
        [Description("Diff target (branch, commit, or 'staged')")] string? target = null,
        [Description("File path filter")] string? path = null,
        CancellationToken ct = default)
    {
        // Execute git diff and return output
    }
}
```

### SubagentTools pattern — complex orchestration

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
        // Build scoped kernel, run agent, return result
    }
}
```

## Error handling

Return error messages as strings rather than throwing exceptions. The LLM reads the return value and can recover:

```csharp
[KernelFunction("read_config")]
[Description("Read a configuration value")]
public string ReadConfig(
    [Description("Configuration key")] string key)
{
    try
    {
        var value = _configStore.Get(key);
        return value ?? $"Configuration key '{key}' not found.";
    }
    catch (Exception ex)
    {
        return $"Error reading configuration: {ex.Message}";
    }
}
```

## Testing tools

Test tools as plain C# classes — no Semantic Kernel infrastructure needed:

```csharp
public class DatabaseToolsTests
{
    [Fact]
    public async Task QueryDatabase_RejectsNonSelectQueries()
    {
        var tools = new DatabaseTools("Data Source=:memory:");
        var result = await tools.QueryDatabaseAsync("DROP TABLE users");
        Assert.Contains("Only SELECT queries", result);
    }

    [Fact]
    public async Task ListTables_ReturnsFormattedList()
    {
        var tools = new DatabaseTools(TestDb.ConnectionString);
        var result = await tools.ListTablesAsync();
        Assert.Contains("users", result);
    }
}
```

For integration tests that verify the LLM can discover and call your tool, register it with a test kernel:

```csharp
[Fact]
public void Tool_IsDiscoverableByKernel()
{
    var kernel = Kernel.CreateBuilder().Build();
    kernel.Plugins.AddFromObject(new DatabaseTools(":memory:"), "Database");

    var functions = kernel.Plugins.GetFunctionsMetadata();
    Assert.Contains(functions, f => f.Name == "query_database");
    Assert.Contains(functions, f => f.Name == "list_tables");
}
```

## See also

- [Architecture Overview](index.md) — tool pipeline and agent lifecycle
- [Extending JD.AI](extending.md) — project layout and coding standards
- [Tool Loadouts](tool-loadouts.md) — expose only relevant tools to each agent
- [Tools Reference](../reference/tools.md) — all built-in tools
- [Plugin SDK](plugins.md) — distributable plugins with tool registration
