# Quickstart

Get productive with JD.AI in under five minutes. This guide walks through a typical workflow — from launching the assistant to committing changes — so you can see how every major feature fits together.

## Before you begin

Make sure you have:

- **JD.AI installed** — `dotnet tool install -g JD.AI`
- **At least one provider configured** — Claude Code, GitHub Copilot, or Ollama

See [Getting Started](getting-started.md) for full installation and provider setup instructions.

## Step 1: Start JD.AI in your project

```bash
cd /path/to/your/project
jdai
```

On startup JD.AI detects every available provider, selects the best model, and displays a welcome banner with the active provider and model name. If multiple providers are available you can switch at any time with `/provider`.

## Step 2: Explore your codebase

Ask plain-language questions to orient yourself:

```text
what does this project do?
explain the folder structure
where is the main entry point?
```

JD.AI reads files, follows references, and synthesizes an answer — no manual searching required.

## Step 3: Make a code change

Describe the change you want:

```text
add input validation to the user registration form
```

JD.AI locates the relevant files, proposes edits, and asks you to confirm before applying them. Each tool invocation (file read, file write, shell command) requires your approval unless you have opted into auto-run.

## Step 4: Use tools

JD.AI has built-in developer tools — file I/O, grep, shell, git, and web search. Ask naturally and the right tool is selected automatically:

```text
search for all TODO comments in the codebase
```

The assistant invokes the grep tool, streams results back, and summarizes the findings.

## Step 5: Work with git

```text
what files have I changed?
commit my changes with a descriptive message
```

JD.AI runs `git status` and `git diff`, drafts a commit message, and executes the commit after your approval.

## Step 6: Spawn a subagent

For larger tasks you can delegate to a scoped subagent:

```text
use an explore agent to find how authentication works in this codebase
```

Subagents run in their own context with scoped tool access and report results back to the main conversation.

## Step 7: Use slash commands

Key commands to know:

- `/help` — list all commands
- `/model <name>` — switch model
- `/compact` — compress conversation history to free up context
- `/save` — persist the current session
- `/sessions` — list saved sessions

## Step 8: Save and resume sessions

Name and save your session so you can pick up later:

```text
/name my-feature-session
/save
```

Resume it in a future run:

```text
/sessions
/resume <id>
```

## Essential commands

| Command | What it does |
|---------|-------------|
| `jdai` | Start interactive mode |
| `/help` | Show available commands |
| `/models` | List available models |
| `/compact` | Compress conversation |
| `/save` | Save current session |
| `/quit` | Exit JD.AI |

## Pro tips

- Run `/compact` before your context window fills up — it summarizes the conversation and reclaims token space.
- Spawn subagents for specialized tasks like code review, exploration, or multi-file refactoring.
- Create a `JDAI.md` file in your repository root with project-specific instructions that JD.AI reads on startup.
- Use `/autorun` to skip tool-confirmation prompts during repetitive workflows.

## What's next

- [Best Practices](best-practices.md) — patterns for getting the most out of JD.AI
- [Common Workflows](common-workflows.md) — real-world task walkthroughs
- [Tools Reference](tools-reference.md) — full list of built-in tools and their options
