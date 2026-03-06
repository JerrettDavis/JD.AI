---
title: "Skills"
description: "Build, configure, and manage JD.AI skills — reusable prompt-based behaviors loaded from plugin directories with lifecycle management, gating, evaluation, and hot-reload."
---

# Skills

Skills are reusable, prompt-based behaviors that extend what the agent can do without writing compiled code. Each skill is a Markdown file containing a YAML frontmatter header and a prompt body. JD.AI's `SkillLifecycleManager` loads, validates, gates, and hot-reloads skills from plugin directories at runtime.

## Overview — skills vs tools vs agents

| Concept | Definition | Implementation |
|---------|------------|----------------|
| **Skill** | A named prompt-based behavior with optional tool access restrictions and eligibility gates | `.md` file with YAML frontmatter |
| **Tool** | A compiled C# function exposed to the LLM as a Semantic Kernel `KernelFunction` | `[KernelFunction]`-attributed method |
| **Agent** | An isolated AI session with its own kernel, tool scope, and conversation history | `SubagentRunner` + `AgentSession` |

Skills sit between tools and agents. They provide reusable instructions to the LLM (like agents) but are lightweight — no separate kernel, no separate session. A skill executes within the current agent's conversation turn, with access limited to the tools declared in its frontmatter.

## Skill file structure

A skill is a single `SKILL.md` file. The filename is always `SKILL.md`; the skill's identity comes from its frontmatter `name` field, and its directory within the plugin structure.

```
~/.jdai/plugins/my-plugin/skills/code-review/SKILL.md
```

### Annotated example

```markdown
---
# Required — identifies the skill in the runtime catalog
name: code-review

# Required — shown in /skills status and used by TagWorkflowMatcher
description: Review code for quality, security, and test coverage

# Optional — conditions that must be true for this skill to be eligible
when:
  os: any                        # 'windows', 'linux', 'macos', or 'any'
  requires:
    bins: [git]                  # All of these must be on PATH
    anyBins: [rg, grep]          # At least one of these must be on PATH
    env: [GITHUB_TOKEN]          # Environment variable must be set
    config:
      feature.codeReview: true   # Config key must equal this value

# Optional — limit which SK tools the skill may invoke during its turn
allowed-tools:
  - read_file
  - grep
  - git_diff
  - list_directory

# Optional — JD.AI-specific metadata (placed under metadata.jdai)
metadata:
  jdai:
    skillKey: code-review        # Stable key for cross-version reference
    always: false                # If true, always include this skill context
    primaryEnv: GITHUB_TOKEN     # Primary credential for status display
---

When reviewing code, you must:

1. Read the changed files using `read_file` and `git_diff`.
2. Check for missing error handling — every public method that can fail should
   handle exceptions explicitly.
3. Identify input validation gaps — untrusted inputs must be validated before use.
4. Look for security vulnerabilities: SQL injection, path traversal, hardcoded secrets.
5. Assess test coverage — changes without tests are flagged unless they are
   configuration or documentation files.
6. Produce a structured report in Markdown with sections for each concern.
```

### Frontmatter fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | ✓ | Unique skill identifier |
| `description` | string | ✓ | Human-readable summary |
| `when` | object | | Eligibility gate (see [Gating](#gating)) |
| `allowed-tools` | string[] | | Tools the skill may call. Omit to inherit all current agent tools. |
| `metadata.jdai.skillKey` | string | | Stable identifier for catalog lookup across versions |
| `metadata.jdai.always` | bool | | Include skill context on every agent turn when `true` |
| `metadata.jdai.primaryEnv` | string | | Env var name displayed in `/skills status` |

### Prompt body

Everything below the closing `---` of the frontmatter is the skill's prompt. It is injected into the conversation as a system-level instruction before the current user message is processed. Write it as direct imperative instructions to the LLM.

> [!IMPORTANT]
> The prompt body is plain text. Markdown formatting (headers, lists) is valid and renders in model context, but the model treats it as instructions, not as output to echo back.

## SkillLifecycleManager internals

`SkillLifecycleManager` owns the complete lifecycle of all loaded skills. It is a singleton service registered in the gateway DI container.

```
Discovery → Parse → Schema Validate → Conflict Resolution → Gate Evaluation → Registration
```

### Discovery

At startup, the manager scans skill source directories in the following precedence order (highest to lowest):

| Source | Path | Scope |
|--------|------|-------|
| Workspace | `.jdai/skills/<name>/SKILL.md` | Current project |
| Managed | `~/.jdai/skills/<name>/SKILL.md` | User-global |
| Bundled | `<install>/skills/<name>/SKILL.md` | Product-shipped |
| Legacy | `~/.claude/skills/<name>/SKILL.md` | Imported (lower precedence) |
| Legacy | `.claude/skills/<name>/SKILL.md` | Imported (lower precedence) |

Plugin-packaged skills (installed via `jdai plugin install`) are discovered through the plugin directory:

```text
~/.jdai/plugins/<plugin-name>/skills/<skill-name>/SKILL.md
```

### Parse and schema validation

Each `SKILL.md` is parsed with a YAML frontmatter parser. Unknown top-level keys are rejected and the skill is marked `invalid` with a reason code. Validation errors appear in `/skills status`.

### Conflict resolution

When two skills share the same `name`, the higher-precedence source wins. Lower-precedence duplicates appear as `shadowed` in `/skills status` and are not loaded.

### Gate evaluation

After conflict resolution, each skill's `when` block is evaluated against the current environment. Skills that fail any gate condition are excluded from the runtime catalog with an explicit reason (`os_mismatch`, `missing_bin`, `missing_env`, `config_disabled`).

### Registration

Eligible skills are registered as `KernelFunction` prompt functions in the active `Kernel`. The skill's `allowed-tools` list is enforced by a per-invocation `IFunctionInvocationFilter` that rejects tool calls not on the allowlist.

## Writing a skill

### Step 1 — Create the directory

```bash
mkdir -p ~/.jdai/skills/my-skill
```

### Step 2 — Create `SKILL.md`

```markdown
---
name: my-skill
description: Does something useful
allowed-tools:
  - read_file
  - run_command
---

Your instructions to the model go here.
Be specific about inputs, outputs, and constraints.
```

### Step 3 — Verify it loads

```text
/skills status
```

The output lists every discovered skill with its state (`active`, `excluded`, `shadowed`, `invalid`) and, for excluded skills, the reason.

### Step 4 — Invoke the skill

Skills are invokable directly from the chat:

```text
> /skill my-skill
```

Or from a workflow step:

```yaml
- kind: skill
  name: Run My Skill
  target: my-skill
  parameters:
    someVar: someValue
```

### Step 5 — Use parameters

Parameters passed from a workflow step or `/skill` invocation are available in the prompt body as `{{paramName}}` template variables:

```markdown
---
name: summarize
description: Summarize a file
allowed-tools: [read_file]
---

Read the file at `{{filePath}}` and produce a {{format}} summary.
Focus on: {{focus}}.
```

## Gating

The `when` block controls whether a skill is eligible to load. All specified conditions must be satisfied.

### `os`

```yaml
when:
  os: windows   # 'windows', 'linux', 'macos', or 'any'
```

### `requires.bins`

All listed executables must be present on `PATH`.

```yaml
when:
  requires:
    bins: [git, dotnet]
```

### `requires.anyBins`

At least one of the listed executables must be present on `PATH`.

```yaml
when:
  requires:
    anyBins: [rg, grep, findstr]
```

### `requires.env`

All listed environment variables must be set and non-empty.

```yaml
when:
  requires:
    env: [GITHUB_TOKEN, GITHUB_ORG]
```

### `requires.config`

Config keys from `~/.jdai/config.json` or `.jdai/config.json` must equal the specified values.

```yaml
when:
  requires:
    config:
      features.codeReview.enabled: true
      provider: claude-code
```

### Combining conditions

All conditions are combined with AND. A skill that requires both a binary and an environment variable is only active when both are present:

```yaml
when:
  os: linux
  requires:
    bins: [docker]
    env: [DOCKER_HOST]
```

## Evaluation subsystem

Skills can self-evaluate their output quality before the result is returned to the user. Evaluation runs as a post-processing step within the same agent turn.

### Enabling evaluation

Add an `evaluation` block to the frontmatter:

```yaml
---
name: code-review
description: Review code for quality
evaluation:
  enabled: true
  minScore: 0.7          # 0.0–1.0; retry if below threshold
  maxRetries: 2          # How many times to retry before returning best attempt
  criteria:
    - "Report contains at least one finding per changed file"
    - "Each finding includes a severity level"
    - "Report is in Markdown format"
---
```

### How evaluation works

After the skill produces its output, `SkillEvaluator` invokes the LLM a second time with:

- The original skill prompt
- The skill's output
- The `criteria` list

The LLM assigns a score from 0.0 to 1.0. If the score is below `minScore`, the skill is re-invoked (up to `maxRetries` times). The highest-scoring attempt is returned.

### Evaluation in practice

Evaluation is most useful for skills that produce structured output (reports, summaries, JSON) where quality can be assessed against explicit criteria. Avoid enabling evaluation on skills that have side effects (e.g., skills that run commands), as the skill body will be re-executed on retry.

## Hot-reload behavior

`SkillLifecycleManager` uses **fingerprint watchers** to detect changes to `SKILL.md` files on disk. When a file changes:

1. The watcher fires (debounced by `watchDebounceMs`, default 250 ms).
2. The affected skill is re-parsed, re-validated, and re-gated.
3. If eligible, it replaces the previous registration in the `Kernel`.
4. If the skill is now invalid or excluded, it is unregistered.

No restart is required. Hot-reload is controlled in `skills.json`:

```json
{
  "skills": {
    "load": {
      "watch": true,
      "watchDebounceMs": 250
    }
  }
}
```

Set `watch: false` to disable hot-reload (useful in CI environments where filesystem stability is required).

> [!NOTE]
> Hot-reload replaces the skill registration for new turns. Any turn already in flight with the old skill version completes with the old definition.

You can also force a reload manually:

```text
/skills reload
```

## Invoking skills from workflows

Skills are invoked from workflow YAML using `kind: skill` steps. See the [Workflow DSL reference](workflows.md#step-types) for the full step format.

```yaml
steps:
  - kind: skill
    name: Review Security
    target: code-review
    parameters:
      focus: security
      format: markdown

  - kind: conditional
    name: Escalate If Critical
    condition: "{{steps.Review Security.output}} contains 'CRITICAL'"
    subSteps:
      - kind: skill
        name: Escalation Report
        target: summarize
        parameters:
          filePath: "{{steps.Review Security.output}}"
          format: executive-summary
```

The `target` field must match the `name` in a skill's frontmatter exactly. The step's `parameters` are injected as template variables into the skill's prompt body.

### Skills in the RunSkillStep executor

The `RunSkillStep` executor resolves the skill by name from the runtime catalog, builds a prompt from the skill body with parameter substitution, invokes the LLM with the skill's `allowed-tools` restriction in effect, and stores the result in the workflow context.

```csharp
public class RunSkillStep : IWorkflowStep
{
    public async Task ExecuteAsync(IWorkflowContext<AgentWorkflowData> context)
    {
        var skillName = _stepDefinition.Target;
        var parameters = _stepDefinition.Parameters ?? ImmutableDictionary<string, string>.Empty;

        var result = await _agentSession.RunSkillAsync(
            skillName,
            parameters,
            context.CancellationToken);

        context.SetStepResult(_stepDefinition.Name, result);
    }
}
```

## Testing skills

### Unit testing the prompt body

Parse a `SKILL.md` file directly and verify frontmatter is valid:

```csharp
[Fact]
public void SkillFile_HasRequiredFrontmatter()
{
    var content = File.ReadAllText("skills/code-review/SKILL.md");
    var skill = SkillParser.Parse(content);

    Assert.Equal("code-review", skill.Name);
    Assert.NotEmpty(skill.Description);
    Assert.NotEmpty(skill.Body);
}
```

### Testing gate conditions

```csharp
[Fact]
public void Skill_IsExcluded_WhenBinaryMissing()
{
    var skill = new SkillDefinition
    {
        Name = "docker-helper",
        When = new GateCondition
        {
            Requires = new RequiresCondition
            {
                Bins = ["docker-that-does-not-exist"]
            }
        }
    };

    var evaluator = new SkillGateEvaluator(mockEnvironment);
    var result = evaluator.Evaluate(skill);

    Assert.Equal(GateResult.Excluded, result.Status);
    Assert.Equal("missing_bin", result.ReasonCode);
}
```

### Integration testing with WorkflowExecutionCapture

Run a workflow containing a skill step and assert the skill was invoked and produced output:

```csharp
[Fact]
public async Task SkillStep_ProducesOutput_InWorkflow()
{
    var definition = new AgentWorkflowDefinition
    {
        Name = "skill-test",
        Steps = [AgentStepDefinition.RunSkill("code-review")]
    };

    var capture = new WorkflowExecutionCapture();
    var engine = new WorkflowEngine(mockExecutor, mockCatalog, capture);
    var result = await engine.RunAsync(definition, CancellationToken.None);

    Assert.Single(result.StepResults);
    Assert.NotNull(capture.StepRecords[0].Output);
}
```

### Testing evaluation

```csharp
[Fact]
public async Task SkillEvaluator_Retries_WhenScoreTooLow()
{
    var skill = new SkillDefinition
    {
        Name = "summarize",
        Evaluation = new EvaluationConfig
        {
            Enabled = true,
            MinScore = 0.8,
            MaxRetries = 2,
            Criteria = ["Output is in Markdown format"]
        }
    };

    // Mock evaluator to return 0.5 on first attempt, 0.9 on second
    var mockEvaluator = new SequentialScoreEvaluator([0.5, 0.9]);
    var executor = new SkillExecutor(skill, mockAgent, mockEvaluator);

    var result = await executor.RunAsync("summarize this", CancellationToken.None);

    Assert.Equal(2, mockEvaluator.InvocationCount);
    Assert.Equal(0.9, result.EvaluationScore);
}
```

## Best practices

**Keep skills focused.** A skill that does one thing well is easier to gate, evaluate, and compose in workflows than a skill that tries to do several things.

**Restrict `allowed-tools` explicitly.** Omitting `allowed-tools` gives the skill access to the agent's full tool set. For skills invoked from automated workflows, enumerate only the tools the skill actually needs. This prevents unintended side effects and makes the skill easier to audit.

**Write criteria for evaluatable outputs.** If your skill produces a report or structured document, add an `evaluation` block with measurable criteria. Vague criteria ("output is good") do not produce useful scores; specific criteria ("output contains a Markdown table") do.

**Use `when` gates instead of guards in the prompt body.** Prompt-body guards ("if git is not available, skip this step") are less reliable than `requires.bins` gates, which prevent the skill from loading at all when the dependency is absent.

**Version your skills via the plugin.** File-based skills under `~/.jdai/skills/` are personal. For distributable skills, package them in a plugin so recipients get them through `jdai plugin install` and updates are managed via `jdai plugin update`.

**Test gate conditions in CI.** Gate conditions interact with the CI environment in non-obvious ways. Assert that skills expected to be `active` in CI are actually `active` — use `SkillGateEvaluator` in an integration test that runs against a known environment.

## See also

- [Plugin SDK](plugins.md) — distributing skills inside a compiled plugin
- [Workflow Enforcement](workflow-enforcement.md) — how skills are invoked from enforced workflows
- [Workflows](workflows.md) — full DSL reference for `skill` step kind
- [Custom Tools](custom-tools.md) — tools that skills can invoke
- [Architecture Overview](index.md) — where skills fit in the JD.AI stack
