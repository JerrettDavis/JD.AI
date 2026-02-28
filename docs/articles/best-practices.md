# Best practices

Practical patterns for getting consistent, high-quality results from JD.AI. These tips apply regardless of which provider you use.

## Manage your context window

The context window is the single most important resource in a JD.AI session. Every message, tool result, and file read consumes tokens — and once the window fills up, the assistant loses sight of earlier instructions.

**Keep sessions focused.** Start a new session for each unrelated task rather than reusing a long-running one.

**Compact early and often.** Run `/compact` before the context window fills up — not after. Use `/cost` to monitor token usage so you can compact proactively.

**Offload work to subagents.** Subagents run in their own context and return only a summary to the parent conversation. Delegate research, code review, or command execution to a subagent whenever the result can be summarized in a few sentences.

> [!TIP]
> Think of subagents as function calls — they do work in isolation and return a clean result, keeping your main context free for decision-making.

## Give JD.AI a way to verify its work

Prompts that include a verification step produce dramatically better results because the assistant can self-correct before returning.

| ❌ Weak prompt | ✅ Stronger prompt |
|---|---|
| "Implement email validation" | "Implement email validation and write unit tests, then run them to verify they pass" |
| "Fix the build" | "The build fails with error CS1061 in `UserService.cs` — fix it and run `dotnet build` to confirm it succeeds" |

The pattern is simple: **describe the task, then tell JD.AI how to check its own work.** Let it run tests, execute builds, or inspect output so it can iterate without another round-trip to you.

## Explore first, then plan, then code

Jumping straight to code changes is the most common source of incorrect results. Instead, break work into three phases:

1. **Explore** — ask JD.AI to read relevant files, trace call paths, and summarize the current behavior. Use an `explore` subagent to avoid polluting your main context.
2. **Plan** — have the assistant outline the changes it intends to make: which files, what modifications, and in what order. A `plan` subagent works well here.
3. **Implement** — execute the plan step by step, verifying each step as you go.

```text
use an explore agent to find how authentication middleware is configured
```

```text
create a plan to add rate limiting to the auth endpoints
```

```text
implement the plan, running tests after each change
```

This three-phase approach catches misunderstandings early and produces smaller, more targeted diffs.

## Write effective JDAI.md files

A `JDAI.md` file in your repository root is loaded automatically at the start of every session. It gives the assistant project-specific context it cannot infer on its own.

**What to include:** build commands, test commands, code style conventions, and architectural constraints.

**What to omit:** anything the assistant can discover by reading your code. Long documentation belongs in linked files, not inline.

```markdown
# Build & Test
- Build: `dotnet build`
- Test: `dotnet test --filter "Category!=Integration"`
- Format: `dotnet format`

# Code Style
- Use file-scoped namespaces
- XML doc comments on all public APIs
- Prefer async/await throughout
```

> [!WARNING]
> Keep `JDAI.md` concise. Bloated instruction files push useful context out of the window and cause directives to be ignored.

## Provide specific context in prompts

Vague prompts force the assistant to guess. Specific prompts converge faster.

| ❌ Vague | ✅ Specific |
|---|---|
| "Fix the tests" | "Fix the failing test in `UserServiceTests.cs` — the `CreateAsync` test returns null instead of the new user" |
| "Refactor this code" | "Extract the retry logic in `HttpClientWrapper.cs` into a shared `RetryPolicy` class under `src/Infrastructure/`" |
| "Add logging" | "Add `ILogger<OrderService>` to `OrderService` and log at Information level on order creation, Warning on validation failure" |

When possible, reference specific files, classes, or error messages. Point to existing patterns the assistant should follow.

## Use subagents strategically

Each subagent type is optimized for a different kind of work:

| Subagent | Best for |
|---|---|
| `explore` | Codebase research and architecture questions — read-only, no context cost |
| `task` | Running commands (build, test, lint) and reporting results |
| `plan` | Creating structured implementation plans before coding |
| `review` | Code review with focused, actionable feedback |
| `general` | Complex multi-step work requiring the full toolset |

For multi-faceted problems, compose a **team** of subagents — one to explore, one to plan, one to implement — and coordinate them from your main session. This keeps each agent focused and your parent context clean.

## Manage sessions effectively

**Name your sessions** so you can find them later. A descriptive name is far easier to locate than a timestamp.

```text
/name feature/rate-limiting
/save
```

**Save before ending important sessions.** Use `/save` to persist conversation state so nothing is lost.

**Resume where you left off.** Use `/sessions` to browse saved sessions and `/resume` to continue a previous conversation with full context intact.

Well-managed sessions compound over time — each saved session becomes a reusable reference for similar future tasks.
