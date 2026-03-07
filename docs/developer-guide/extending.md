---
title: "Extending JD.AI"
description: "Getting started guide for extending JD.AI — project layout, development setup, coding standards, and testing conventions."
---

# Extending JD.AI

JD.AI is built on Microsoft Semantic Kernel and designed for extensibility. This guide covers how to set up a development environment, navigate the project layout, and follow the project's conventions.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- Git
- At least one AI provider configured (see [Providers](../reference/providers.md))

## Fork and clone

```bash
# Fork on GitHub, then clone your fork
git clone https://github.com/<you>/JD.AI.git
cd JD.AI

# Restore dependencies
dotnet restore

# Build the full solution
dotnet build

# Run the test suite
dotnet test
```

## Project layout

```
JD.AI/
├── src/
│   ├── JD.AI/                          # CLI entry point, commands, TUI
│   │   ├── Program.cs                  # Bootstrap and DI setup
│   │   ├── Commands/                   # Slash command handlers
│   │   └── Tools/                      # SubagentTools, TeamTools SK plugins
│   ├── JD.AI.Core/                     # Shared foundation
│   │   ├── Providers/                  # IProviderDetector implementations
│   │   ├── Tools/                      # Built-in tool plugins (17 categories)
│   │   ├── Agents/                     # SubagentRunner, orchestration
│   │   │   └── Orchestration/          # Strategy implementations
│   │   ├── Config/                     # AtomicConfigStore, data directories
│   │   ├── Mcp/                        # MCP server integration
│   │   └── Sessions/                   # SessionStore (SQLite)
│   ├── JD.AI.Workflows/               # Workflow engine
│   ├── JD.AI.Telemetry/               # OpenTelemetry integration
│   ├── JD.AI.Plugins.SDK/             # Plugin development SDK
│   ├── JD.AI.Gateway/                 # HTTP gateway + SignalR
│   ├── JD.AI.Daemon/                  # Background service host
│   ├── JD.AI.Dashboard.Wasm/          # Blazor WebAssembly dashboard
│   └── JD.AI.Channels.*/              # Channel adapters (6 projects)
├── tests/
│   ├── JD.AI.Tests/
│   ├── JD.AI.Core.Tests/
│   └── JD.AI.Workflows.Tests/
├── docs/                               # docfx documentation
├── samples/                            # Sample projects
├── Directory.Build.props               # Shared MSBuild properties
└── Directory.Packages.props            # Central package management
```

## Where to add things

| You want to... | Add it in... |
|-----------------|-------------|
| Add a new tool | `src/JD.AI.Core/Tools/` — create a class with `[KernelFunction]` methods |
| Add a new provider | `src/JD.AI.Core/Providers/` — implement `IProviderDetector` |
| Add a slash command | `src/JD.AI/Commands/` — implement the command handler |
| Add a channel adapter | `src/JD.AI.Channels.<Name>/` — implement `IChannel` |
| Add a workflow step | `src/JD.AI.Workflows/Steps/` — implement the step class |
| Add an orchestration strategy | `src/JD.AI.Core/Agents/Orchestration/` — implement `IOrchestrationStrategy` |
| Add a gateway endpoint | `src/JD.AI.Gateway/` — add a Minimal API endpoint |

## Coding standards

### Analyzers and formatting

The solution uses centralized build configuration via `Directory.Build.props`:

- **Nullable reference types** are enabled globally
- **Implicit usings** are enabled
- Code style is enforced by Roslyn analyzers

Run the formatter before committing:

```bash
dotnet format
```

### Naming conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Tool classes | `{Category}Tools` | `FileTools`, `GitTools` |
| Provider detectors | `{Provider}Detector` | `OllamaDetector`, `OpenAIDetector` |
| Channel adapters | `{Platform}Channel` | `DiscordChannel`, `SlackChannel` |
| Orchestration strategies | `{Name}Strategy` | `FanOutStrategy`, `DebateStrategy` |
| Interfaces | `I{Name}` prefix | `IProviderDetector`, `IChannel` |

### Common patterns

**Semantic Kernel tools** — All tools use `[KernelFunction]` and `[Description]` attributes:

```csharp
public class MyTools
{
    [KernelFunction("my_operation")]
    [Description("What this operation does")]
    public async Task<string> MyOperationAsync(
        [Description("Parameter description")] string input,
        CancellationToken ct = default)
    {
        // Implementation
        return result;
    }
}
```

**DI registration** — Services follow the standard `IServiceCollection` pattern:

```csharp
services.AddSingleton<IProviderDetector, MyProviderDetector>();
```

## Testing conventions

The solution includes **772+ tests** across three test projects:

```bash
# Run all tests
dotnet test

# Run a specific test project
dotnet test tests/JD.AI.Core.Tests

# Run tests matching a filter
dotnet test --filter "FullyQualifiedName~FileTools"
```

### Test organization

- Tests mirror the `src/` structure — `JD.AI.Core.Tests/Tools/FileToolsTests.cs` tests `JD.AI.Core/Tools/FileTools.cs`
- Use xUnit as the test framework
- Use `Moq` or similar for mocking interfaces

### Writing tests for tools

```csharp
public class MyToolsTests
{
    [Fact]
    public async Task MyOperation_ReturnsExpectedResult()
    {
        var tools = new MyTools();
        var result = await tools.MyOperationAsync("input");
        Assert.Contains("expected", result);
    }
}
```

## Building and running

```bash
# Build the CLI
dotnet build src/JD.AI

# Run the CLI
dotnet run --project src/JD.AI

# Build and run the gateway
dotnet run --project src/JD.AI.Gateway

# Pack the CLI as a global tool
dotnet pack src/JD.AI -c Release
```

## Next steps

- [Custom Tools](custom-tools.md) — write Semantic Kernel tool plugins
- [Custom Providers](custom-providers.md) — integrate new AI providers
- [Plugin SDK](plugins.md) — build distributable gateway plugins
- [Architecture Overview](index.md) — understand the full system

## Contributing

See [CONTRIBUTING.md](https://github.com/JerrettDavis/JD.AI/blob/main/CONTRIBUTING.md) for development setup, pull request guidelines, and the code of conduct.
