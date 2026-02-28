# Extending JD.AI

JD.AI is built on Microsoft Semantic Kernel, making it extensible through standard SK patterns.

## Writing custom tools

Tools are Semantic Kernel plugins with `[KernelFunction]` attributes:

```csharp
public class MyCustomTools
{
    [KernelFunction("my_tool")]
    [Description("Description of what my tool does")]
    public string MyTool(
        [Description("Parameter description")] string input)
    {
        return $"Processed: {input}";
    }
}
```

Register in the kernel:
```csharp
kernel.Plugins.AddFromObject(new MyCustomTools(), "MyTools");
```

## Adding providers

Implement `IProviderDetector`:
```csharp
public class MyProviderDetector : IProviderDetector
{
    public string Name => "MyProvider";
    
    public async Task<ProviderInfo> DetectAsync(CancellationToken ct)
    {
        // Detection logic
        return new ProviderInfo(Name, isAvailable: true, ...);
    }
    
    public IKernelBuilder ConfigureKernel(
        IKernelBuilder builder, 
        ProviderModelInfo model)
    {
        // Configure the kernel with your provider
        return builder;
    }
}
```

## Using Claude Code skills
Create SKILL.md files in `~/.claude/skills/` or `.claude/skills/`:
```markdown
---
name: my-skill
description: What this skill does
---

Instructions for the AI agent when this skill is loaded.
```

## Architecture overview
```
Program.cs
├── ProviderRegistry (detect + build kernels)
├── AgentSession (kernel, chat history, tools)
├── AgentLoop (streaming + tool calling loop)
├── SlashCommandRouter (20 commands)
├── ChatRenderer (Spectre.Console TUI)
├── InteractiveInput (readline + completions)
├── SessionStore (SQLite persistence)
└── Tools/ (8 plugin categories)
```

## Related packages
| Package | NuGet | Description |
|---------|-------|-------------|
| `JD.SemanticKernel.Extensions` | [![NuGet](https://img.shields.io/nuget/v/JD.SemanticKernel.Extensions)](https://nuget.org/packages/JD.SemanticKernel.Extensions) | Skills, hooks, plugins bridge |
| `JD.SemanticKernel.Connectors.ClaudeCode` | [![NuGet](https://img.shields.io/nuget/v/JD.SemanticKernel.Connectors.ClaudeCode)](https://nuget.org/packages/JD.SemanticKernel.Connectors.ClaudeCode) | Claude Code auth provider |
| `JD.SemanticKernel.Connectors.GitHubCopilot` | [![NuGet](https://img.shields.io/nuget/v/JD.SemanticKernel.Connectors.GitHubCopilot)](https://nuget.org/packages/JD.SemanticKernel.Connectors.GitHubCopilot) | Copilot auth provider |

## Contributing
See [CONTRIBUTING.md](https://github.com/JerrettDavis/JD.AI/blob/main/CONTRIBUTING.md) for development setup and guidelines.
