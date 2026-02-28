# Common Workflows

Practical, step-by-step guides for the tasks developers tackle most often with JD.AI. Each workflow shows numbered steps with example prompts you can adapt to your own projects.

## Understand new codebases

Ramp up on an unfamiliar project without reading every file by hand.

1. **Get a high-level overview** — start broad:

   ```text
   what does this project do?
   ```

2. **Explore folder structure** — understand how code is organized:

   ```text
   explain the folder structure and key files
   ```

3. **Find specific logic** — drill into a feature area:

   ```text
   where is the authentication logic?
   ```

4. **Delegate deep exploration** — spawn a focused subagent for larger investigations:

   ```text
   spawn an explore agent to map out the API endpoints
   ```

## Fix bugs efficiently

Move from symptom to fix with a tight feedback loop.

1. **Describe the symptom** — include error messages and reproduction steps:

   ```text
   the login endpoint returns 401 after session timeout — here is the stack trace: ...
   ```

2. **Let JD.AI locate the root cause** — point it at the right area:

   ```text
   the login fails after session timeout, check src/auth/
   ```

3. **Write a failing test first** — prove the bug exists before touching production code:

   ```text
   write a test that reproduces the session-timeout 401
   ```

4. **Implement the fix and verify** — apply the change, then run the full suite:

   ```text
   fix the bug and run the tests to make sure nothing else broke
   ```

## Refactor code

Improve structure incrementally while keeping existing behavior intact.

1. **Analyze before changing** — understand what needs work:

   ```text
   analyze the payment module for code smells
   ```

2. **Plan the refactor** — get a concrete action list:

   ```text
   create a plan to refactor payments to use async/await
   ```

3. **Execute step by step** — make one change at a time and verify after each:

   ```text
   apply step 1 of the plan and run the tests
   ```

4. **Run tests after every change** — catch regressions immediately:

   ```text
   run the full test suite and report any failures
   ```

## Write tests

Build confidence in your code with thorough test coverage.

1. **Check current coverage** — find the gaps:

   ```text
   what code paths in UserService aren't tested?
   ```

2. **Generate tests** — target a specific method and its edge cases:

   ```text
   write unit tests for the Calculator.Divide method, including edge cases
   ```

3. **Follow existing patterns** — match the conventions already in the project:

   ```text
   look at existing tests to match the style
   ```

4. **Verify** — run the new tests and confirm they pass:

   ```text
   run the tests and report results
   ```

## Create pull requests

Go from local changes to a reviewable PR in a single conversation.

1. **Review changes** — see what you have staged and unstaged:

   ```text
   what files have I changed?
   ```

2. **Create a commit** — let JD.AI draft a message following conventional commits:

   ```text
   commit my changes with a descriptive conventional commit message
   ```

3. **Push and open a PR** — target the right branch:

   ```text
   push to origin and create a PR targeting main
   ```

## Handle documentation

Keep docs accurate and complete without switching tools.

1. **Generate from code** — sync documentation with the actual implementation:

   ```text
   update the README to reflect current installation steps
   ```

2. **Add XML docs** — ensure public APIs are documented:

   ```text
   add XML documentation to all public methods in UserService
   ```

3. **Create guides** — produce onboarding or contributor documentation:

   ```text
   write a getting-started guide for new contributors
   ```

## Use subagents for specialized tasks

Delegate scoped work to purpose-built subagents that run in their own context.

1. **Explore** — deep-dive into a feature without polluting your main conversation:

   ```text
   use an explore agent to find how caching works
   ```

2. **Task** — offload a long-running command and get a summary:

   ```text
   use a task agent to run the full test suite and report failures
   ```

3. **Review** — get a second opinion on your changes:

   ```text
   use a review agent to check my changes against the coding standards
   ```

## Orchestrate teams for complex work

Coordinate multiple subagents when a single agent is not enough.

1. **Sequential pipeline** — chain stages end to end:

   ```text
   analyze the codebase, plan the migration, implement it, then review the result
   ```

2. **Fan-out** — run independent analyses in parallel:

   ```text
   analyze the frontend, backend, and database layers in parallel and summarize
   ```

3. **Debate** — solicit competing perspectives before committing to a direction:

   ```text
   get two different architectural proposals for the new notification service
   ```

## Manage sessions

Preserve and resume your work across terminal sessions.

| Command | Purpose |
|---------|---------|
| `/name feature-auth` | Name the current session |
| `/save` | Persist the session to disk |
| `/sessions` | List all saved sessions |
| `/resume <id>` | Continue a previous session |
| `/export` | Export the session as JSON |

> [!TIP]
> Name your sessions descriptively — `/name feature-auth` is easier to find later than an auto-generated ID.

## Next steps

- [Best Practices](best-practices.md) — tips for writing effective prompts
- [Subagents](subagents.md) — full reference for subagent types and capabilities
- [Orchestration](orchestration.md) — strategies for multi-agent coordination
- [Commands Reference](commands-reference.md) — complete list of slash commands
