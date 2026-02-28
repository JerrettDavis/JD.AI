# Skills, Plugins, and Hooks

JD.AI integrates with the Claude Code extension ecosystem. Skills, plugins, and hooks are loaded at startup and registered as Semantic Kernel functions and filters through the `JD.SemanticKernel.Extensions` library.

## Skills

Skills are markdown files (`SKILL.md`) that provide instructions and context to the AI agent. Each skill declares a name, description, and optional tool constraints in YAML frontmatter.

### Where skills live

| Location | Path | Scope |
|----------|------|-------|
| Personal | `~/.claude/skills/<name>/SKILL.md` | All projects |
| Project | `.claude/skills/<name>/SKILL.md` | This project only |

Project skills take precedence when names collide with personal skills.

### How JD.AI loads skills

At startup, JD.AI:

1. Scans `~/.claude/skills/` for personal skills
2. Scans `.claude/skills/` in the project directory
3. Parses SKILL.md frontmatter (`name`, `description`, `allowed-tools`)
4. Registers each skill as available context for the agent

### Example skill

```text
~/.claude/skills/code-review/SKILL.md
```

```markdown
---
name: code-review
description: Review code for quality and best practices
---

When reviewing code:
1. Check for error handling
2. Verify input validation
3. Look for security vulnerabilities
4. Ensure test coverage
```

### Startup output

When skills load successfully, JD.AI logs each one:

```text
Loaded skills from C:\Users\you\.claude\skills
  • code-review: Review code for quality and best practices
  • commit-message: Generate conventional commit messages
```

## Plugins

Plugins are directories with a `plugin.json` manifest that bundle skills, hooks, and configuration into a single distributable unit.

### Plugin structure

```text
.claude/plugins/my-plugin/
├── plugin.json              # Manifest
├── skills/
│   └── my-skill/SKILL.md   # Plugin skills
└── hooks/
    └── hooks.json           # Plugin hooks
```

### Loading

| Location | Path |
|----------|------|
| Personal | `~/.claude/plugins/<name>/` |
| Project | `.claude/plugins/<name>/` |

JD.AI reads each `plugin.json`, resolves internal dependencies, and registers the plugin's skills and hooks in a single pass.

## Hooks

Hooks are event-driven filters that run before or after tool execution.

### Hook events

| Event | When |
|-------|------|
| `PreToolUse` | Before a tool is invoked |
| `PostToolUse` | After a tool completes |

### How hooks integrate

Hooks are parsed from `hooks.json` and registered as Semantic Kernel `IFunctionInvocationFilter` instances. They can:

- **Modify arguments** before execution
- **Block execution** based on rules
- **Post-process results** after a tool completes
- **Log or audit** tool usage

## How it connects to Semantic Kernel

JD.AI uses `JD.SemanticKernel.Extensions` to bridge Claude Code's file-based extension format into Semantic Kernel primitives:

| Claude Code concept | Semantic Kernel equivalent |
|---------------------|---------------------------|
| `SKILL.md` | `KernelFunction` |
| `hooks.json` | `IFunctionInvocationFilter` / `IPromptRenderFilter` |
| `plugin.json` | Plugin with dependency resolution |

This mapping lets the same skill and hook files work in both Claude Code and JD.AI without modification.

## Related packages

| Package | Description |
|---------|-------------|
| `JD.SemanticKernel.Extensions` | Core parsing and registration of skills, plugins, and hooks |
| `JD.SemanticKernel.Connectors.ClaudeCode` | Claude Code authentication provider |
| `JD.SemanticKernel.Connectors.GitHubCopilot` | GitHub Copilot authentication provider |
