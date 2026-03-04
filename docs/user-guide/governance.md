---
description: "Policy engine and permissions for JD.AI: policy file format, scope hierarchy, tool allow/deny lists, provider and model restrictions, budget limits, and data redaction."
---

# Policy engine and permissions

JD.AI's governance system lets administrators and teams define what the agent is permitted to do before it acts. Policies control which tools the agent can invoke, which AI providers and models it may use, how much money it may spend, and what content must be redacted before reaching an external provider. Policies are plain YAML files that live alongside your project or user configuration and compose automatically using conservative merge rules.

The goal is predictable, auditable behavior: every tool invocation passes through the policy evaluator and the safety tier check before execution. The result — allowed, denied, or user-confirmed — is recorded in the audit log.

## Quick start

The fastest way to restrict JD.AI to read-only operations is to place a policy file in your project:

1. Create the directory:

   ```text
   mkdir -p .jdai/policies
   ```

2. Write a minimal policy file at `.jdai/policies/security.yaml`:

   ```yaml
   apiVersion: jdai/v1
   kind: Policy
   metadata:
     name: read-only
     scope: Project
   spec:
     tools:
       allowed:
         - read_file
         - list_directory
         - grep
         - glob
         - git_status
         - git_diff
         - git_log
         - git_branch
         - think
         - get_environment
     audit:
       enabled: true
       sink: file
   ```

3. Start JD.AI from the project directory. Any tool not in the `allowed` list is blocked:

   ```text
   Policy blocked: write_file — Tool 'write_file' is not in the allowed list.
   ```

> [!NOTE]
> Policy files in `.jdai/policies/` are automatically assigned `Project` scope if no `scope` is specified in their metadata. Files in `~/.jdai/policies/` default to `User` scope.

## Policy file format

Policy files are YAML documents with a fixed top-level structure. The parser uses camelCase key names and silently ignores unrecognised fields.

### Complete annotated example

```yaml
# Required: identifies this as a JD.AI policy document.
apiVersion: jdai/v1
kind: Policy

metadata:
  # Human-readable name used in audit events and diagnostic output.
  name: engineering-baseline

  # Scope controls merge order: Global < Organization < Team < Project < User.
  # Files in ~/.jdai/policies/ default to User.
  # Files in {project}/.jdai/policies/ default to Project.
  scope: Organization

  # Lower priority values are applied first within the same scope.
  # Default is 0.
  priority: 10

spec:
  tools:
    # If non-empty: only these tools may run. Evaluated as an exact,
    # case-insensitive match. An empty list means "no restriction".
    allowed:
      - read_file
      - list_directory
      - grep
      - glob
      - git_status
      - git_diff
      - git_log
      - git_branch
      - write_file
      - edit_file
      - git_commit
      - think
      - get_environment
      - ask_questions

    # These tools are always blocked, regardless of the allowed list.
    denied:
      - run_command
      - execute_code
      - web_search

  providers:
    # If non-empty: only these provider names may be used.
    allowed:
      - claude
      - openai

    # These providers are always blocked.
    denied:
      - ollama

  models:
    # Maximum context window size in tokens. Requests to models with a
    # larger context window are blocked.
    maxContextWindow: 128000

    # Glob patterns (*, ?) matched against model IDs. Case-insensitive.
    denied:
      - gpt-4-turbo*
      - o1-*

  budget:
    # Hard daily spend limit in USD. Blocks further requests when exceeded.
    maxDailyUsd: 10.00

    # Hard monthly spend limit in USD.
    maxMonthlyUsd: 100.00

    # Emit a warning in the UI when spend reaches this percentage of the limit.
    # Default is 80.
    alertThresholdPercent: 75

  data:
    # Glob patterns matching file paths. Files matching these patterns may
    # not be sent to providers outside this list.
    noExternalProviders:
      - "src/proprietary/**"
      - "**/*.key"

    # .NET regex patterns applied to all outbound content. Matches are
    # replaced with [REDACTED] before the content reaches any provider.
    # Each pattern has a 1-second evaluation timeout (ReDoS protection).
    redactPatterns:
      - '(?i)api[_-]?key\s*[:=]\s*\S+'
      - '(?i)password\s*[:=]\s*\S+'
      - '\b\d{4}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}\b'

  sessions:
    # Delete sessions older than this many days. Null means keep forever.
    retentionDays: 90

    # Require every session to carry a project tag before saving.
    requireProjectTag: false

  audit:
    # Enable audit logging. When false the audit section is ignored.
    enabled: true

    # Sink type: "file" (default), "elasticsearch", or "webhook".
    sink: file

    # File sink: no additional configuration required.
    # See "Audit Logging" for elasticsearch and webhook options.
```

### Top-level fields

| Field | Type | Default | Description |
|---|---|---|---|
| `apiVersion` | string | `jdai/v1` | Must be `jdai/v1`. |
| `kind` | string | `Policy` | Must be `Policy`. |
| `metadata.name` | string | — | Unique name for this policy document. |
| `metadata.scope` | enum | `User` | Processing order scope. See [Policy scoping](#policy-scoping). |
| `metadata.priority` | int | `0` | Tie-breaker within the same scope. Lower values first. |
| `spec.tools` | object | null | Tool allow/deny rules. |
| `spec.providers` | object | null | Provider allow/deny rules. |
| `spec.models` | object | null | Model glob deny rules and context window cap. |
| `spec.budget` | object | null | USD spend limits. |
| `spec.data` | object | null | Data residency and redaction rules. |
| `spec.sessions` | object | null | Session retention and tagging rules. |
| `spec.audit` | object | null | Audit sink configuration. |

## Policy scoping

JD.AI loads policies from two locations and merges them in scope order from least to most specific.

### File locations

| Scope | Location | Notes |
|---|---|---|
| `Global` | Built-in defaults (no file) | Implicit permissive baseline. |
| `Organization` | `{JDAI_ORG_CONFIG}/policies/` or `~/.jdai/org-config-path` | Set via env var or pointer file. |
| `Team` | `~/.jdai/policies/` with `scope: Team` | Place in user data dir, set scope explicitly. |
| `Project` | `{projectDir}/.jdai/policies/` | Auto-assigned if `scope` is unset. |
| `User` | `~/.jdai/policies/` | Default for files in the user data dir. |

> [!NOTE]
> The `PolicyLoader` currently scans two directories: `~/.jdai/policies/` (for Global/User/Team scope files) and `{projectPath}/.jdai/policies/` (for Project scope files). The `JDAI_ORG_CONFIG` environment variable points to an organization config directory; policy files placed inside its `policies/` subdirectory are loaded with `Organization` scope.

### Merge rules

When multiple policies apply, `PolicyResolver` combines them in scope order (Global → Organization → Team → Project → User), then by ascending `priority` within the same scope. The merge rules are conservative:

1. **`allowed` lists — intersection**: A tool/provider is permitted only if it appears in every `allowed` list that is non-empty. An empty `allowed` list means "no restriction from this policy".
2. **`denied` lists — union**: A tool/provider is blocked if any policy denies it.
3. **Numeric limits — minimum wins**: `maxDailyUsd`, `maxMonthlyUsd`, `maxContextWindow`, `alertThresholdPercent`, and `retentionDays` all take the smallest value across all policies.
4. **`requireProjectTag` — any true wins**: If any policy requires a project tag, sessions must have one.
5. **Audit — enabled if any policy enables it**: The sink configuration is taken from the most specific (last) policy.
6. **Data lists — union**: `noExternalProviders` and `redactPatterns` accumulate across all policies.

### Worked example: two policies merging

**Organization policy** (`scope: Organization`):

```yaml
spec:
  tools:
    allowed:
      - read_file
      - write_file
      - run_command
  budget:
    maxDailyUsd: 20.00
```

**Project policy** (`scope: Project`):

```yaml
spec:
  tools:
    allowed:
      - read_file
      - write_file
      - git_commit
    denied:
      - run_command
  budget:
    maxDailyUsd: 5.00
```

**Resolved effective policy**:

| Aspect | Result | Rule applied |
|---|---|---|
| `tools.allowed` | `{read_file, write_file}` | Intersection of both allowed lists |
| `tools.denied` | `{run_command}` | Union of denied lists |
| `budget.maxDailyUsd` | `5.00` | Minimum wins |

`git_commit` is absent from the organization allowed list, so it is excluded by intersection. `run_command` passes the organization allowed list but is blocked by the project denied list — deny always wins.

## Tool allow/deny lists

Every tool invocation passes through `PolicyEvaluator.EvaluateTool` before the safety tier check. The evaluation logic is:

1. If the tool name is in `spec.tools.denied`, the call is blocked (decision: `Deny`).
2. If `spec.tools.allowed` is non-empty and the tool name is not in it, the call is blocked.
3. Otherwise the call proceeds to the safety tier check.

Tool name matching is exact and case-insensitive.

### All tools and their default safety tiers

| Tool | Safety tier | Category |
|---|---|---|
| `read_file` | AutoApprove | File |
| `list_directory` | AutoApprove | File |
| `write_file` | ConfirmOnce | File |
| `edit_file` | ConfirmOnce | File |
| `grep` | AutoApprove | Search |
| `glob` | AutoApprove | Search |
| `run_command` | AlwaysConfirm | Shell |
| `execute_code` | AlwaysConfirm | Shell |
| `git_status` | AutoApprove | Git |
| `git_diff` | AutoApprove | Git |
| `git_log` | AutoApprove | Git |
| `git_branch` | AutoApprove | Git |
| `git_commit` | ConfirmOnce | Git |
| `git_push` | ConfirmOnce | Git |
| `git_pull` | ConfirmOnce | Git |
| `git_checkout` | ConfirmOnce | Git |
| `git_stash` | ConfirmOnce | Git |
| `web_fetch` | AutoApprove | Web |
| `web_search` | AlwaysConfirm | Web |
| `memory_search` | AutoApprove | Memory |
| `memory_store` | ConfirmOnce | Memory |
| `memory_forget` | ConfirmOnce | Memory |
| `spawn_agent` | ConfirmOnce | Subagent |
| `spawn_team` | ConfirmOnce | Subagent |
| `think` | AutoApprove | Reasoning |
| `ask_questions` | AutoApprove | Questions |
| `get_environment` | AutoApprove | Environment |
| `list_tasks` | AutoApprove | Tasks |
| `export_tasks` | AutoApprove | Tasks |
| `create_task` | ConfirmOnce | Tasks |
| `update_task` | ConfirmOnce | Tasks |
| `complete_task` | ConfirmOnce | Tasks |
| `read_clipboard` | AutoApprove | Clipboard |
| `write_clipboard` | ConfirmOnce | Clipboard |
| `get_usage` | AutoApprove | Usage |
| `reset_usage` | ConfirmOnce | Usage |
| `create_patch` | AutoApprove | Diff |
| `apply_patch` | ConfirmOnce | Diff |
| `batch_edit_files` | ConfirmOnce | Edit |

### Safety tier behavior

| Tier | Behavior |
|---|---|
| `AutoApprove` | Runs without confirmation. |
| `ConfirmOnce` | Prompts once per session; subsequent invocations of the same tool run without asking. |
| `AlwaysConfirm` | Prompts on every invocation. |

A policy `denied` entry overrides `AutoApprove`. If `web_search` is in `spec.tools.denied`, the policy blocks it before the safety tier check runs — the user is never prompted.

### Example: read-only project policy

```yaml
apiVersion: jdai/v1
kind: Policy
metadata:
  name: ci-readonly
  scope: Project
spec:
  tools:
    allowed:
      - read_file
      - list_directory
      - grep
      - glob
      - git_status
      - git_diff
      - git_log
      - git_branch
      - think
      - get_environment
      - memory_search
      - web_fetch
      - ask_questions
      - create_patch
      - get_usage
      - export_tasks
      - list_tasks
```

This policy permits every `AutoApprove` read tool while blocking all write and shell tools by omission from the `allowed` list.

## Provider restrictions

`PolicyEvaluator.EvaluateProvider` applies the same allow/deny logic to provider names. Provider names are the lowercase identifiers used by the provider registry: `claude`, `openai`, `ollama`, `foundry`, `copilot`.

### Example: require local inference for proprietary repositories

```yaml
apiVersion: jdai/v1
kind: Policy
metadata:
  name: local-only
  scope: Project
spec:
  providers:
    allowed:
      - ollama
      - foundry
```

With this policy active, any attempt to route a request to `claude` or `openai` is blocked with a `Deny` result before the request is sent.

> [!WARNING]
> Provider restriction applies to the provider selected at session start. If a user passes `--provider openai` on the command line and the policy denies `openai`, the session will fail to start. Communicate policy constraints to your team via the organization instructions file.

## Model restrictions

`PolicyEvaluator.EvaluateModel` supports two independent checks:

1. **Glob pattern deny list**: Model IDs are matched against each pattern using `*` (any characters) and `?` (single character). The match is case-insensitive.
2. **Context window cap**: If the resolved policy has `maxContextWindow` set and the model's advertised context window exceeds it, the model is denied.

### Example: block OpenAI preview models

```yaml
spec:
  models:
    denied:
      - gpt-4-turbo*
      - o1-*
      - o3-*
    maxContextWindow: 200000
```

The pattern `gpt-4-turbo*` matches `gpt-4-turbo`, `gpt-4-turbo-2024-04-09`, and any future variant. `o1-*` matches `o1-preview`, `o1-mini`, etc.

> [!NOTE]
> Glob patterns are converted to regular expressions at evaluation time. The `*` wildcard is translated to `.*` and `?` to `.`. Pattern matching uses `RegexOptions.IgnoreCase`.

## Budget limits

The `BudgetTracker` records provider spend in `~/.jdai/budget.json` as a JSON structure keyed by UTC date (`yyyy-MM-dd`). Each entry stores the total spend and a per-provider breakdown.

### Budget evaluation

Before each provider call, the agent checks `IBudgetTracker.IsWithinBudgetAsync`. If the resolved policy has limits set and either limit is exceeded, the call is blocked.

| Policy field | Type | Default | Description |
|---|---|---|---|
| `maxDailyUsd` | decimal? | null | Maximum USD spend for the current UTC day. |
| `maxMonthlyUsd` | decimal? | null | Maximum USD spend for the current UTC calendar month. |
| `alertThresholdPercent` | int | 80 | Percentage of a limit at which a warning is shown in the UI. |

### Alert threshold behavior

When spend reaches `alertThresholdPercent` of either the daily or monthly limit, `BudgetStatus.AlertTriggered` is set to `true`. The agent loop surfaces this as a warning in the TUI. The agent is not blocked until the limit is actually exceeded.

### Budget data format

`~/.jdai/budget.json` is written as indented JSON:

```json
{
  "dailyEntries": {
    "2026-03-03": {
      "date": "2026-03-03",
      "totalUsd": 1.42,
      "byProvider": {
        "claude": 0.95,
        "openai": 0.47
      }
    }
  }
}
```

> [!WARNING]
> `budget.json` is not encrypted. It contains cost metadata only — no prompts or responses. Restrict file system access to the `~/.jdai/` directory if the spend data is considered sensitive.

## Data redaction

The `DataRedactor` applies .NET regular expressions to all outbound content before it is sent to an AI provider. Matches are replaced with the literal string `[REDACTED]`.

### Configuring redaction patterns

Patterns are standard .NET regex syntax placed in `spec.data.redactPatterns`. Each pattern is compiled with `RegexOptions.IgnoreCase` and evaluated with a 1-second timeout to prevent ReDoS attacks. If a pattern exceeds the timeout on a given input, it is silently skipped — the remaining patterns still run.

```yaml
spec:
  data:
    redactPatterns:
      # API keys and tokens
      - '(?i)api[_-]?key\s*[:=]\s*\S+'
      # Passwords
      - '(?i)password\s*[:=]\s*\S+'
      # Credit card numbers (16-digit groups)
      - '\b\d{4}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}\b'
      # AWS access key IDs
      - '\bAKIA[0-9A-Z]{16}\b'
```

There are no built-in patterns. All redaction is opt-in and must be declared explicitly in a policy.

### Data residency: noExternalProviders

`spec.data.noExternalProviders` contains glob patterns matched against file paths. Files matching these patterns may not have their content sent to external providers. This is enforced as a policy-level block; the tool invocation is denied with a `Deny` decision.

```yaml
spec:
  data:
    noExternalProviders:
      - "src/billing/**"
      - "**/*.pem"
      - "**/secrets/**"
```

When multiple policies define `noExternalProviders`, the patterns are merged by union: all patterns from all policies are applied.

> [!WARNING]
> `noExternalProviders` protects against the agent reading a matching file and forwarding it to a provider. It does not prevent a user from manually copy-pasting content into the prompt. Layer this with `redactPatterns` for defence in depth.

## Policy evaluation order

For every tool invocation, the `ToolConfirmationFilter` applies checks in this sequence:

1. **Policy check**: `IPolicyEvaluator.EvaluateTool` evaluates the resolved policy. If the decision is `Deny`, execution stops and an audit event is emitted with `status=denied`.
2. **Safety tier check**: The tool's `SafetyTier` is consulted. If confirmation is required and the user is prompted:
   - User approves: proceed.
   - User denies: execution stops and an audit event is emitted with `status=user_denied`.
3. **Execute**: `next(context)` is called — the tool runs.
4. **Audit**: An audit event is emitted with `status=ok`.

The flow diagram:

```text
Tool invocation requested
        │
        ▼
  Policy.EvaluateTool
   ┌─── Deny? ──────────────────► Block + audit(denied)
   │
   ▼
  Safety tier check
   ┌─── AlwaysConfirm / ConfirmOnce (first call)?
   │       │
   │       ▼
   │   Prompt user
   │   ┌── Denied? ─────────────► Block + audit(user_denied)
   │   │
   │   ▼
   │   (ConfirmOnce: add to session allow set)
   │
   ▼
  Execute tool
        │
        ▼
  audit(ok)
```

## Environment variables

| Variable | Description |
|---|---|
| `JDAI_ORG_CONFIG` | Path to an organization configuration directory. Policy files in `{JDAI_ORG_CONFIG}/policies/` are loaded with `Organization` scope. Overrides the path stored in `~/.jdai/org-config-path`. |
| `JDAI_DATA_DIR` | Override the `~/.jdai/` root data directory. When set, all policy, budget, and audit files are read from and written to this path. |

### Distributing organization policies

Set `JDAI_ORG_CONFIG` to a shared network path or a cloned Git repository containing your organization's policy files:

```bash
# In a shell profile or CI environment
export JDAI_ORG_CONFIG=/mnt/corp/jdai-config
```

JD.AI reads `$JDAI_ORG_CONFIG/policies/*.yaml` at startup and merges them with project and user policies. The environment variable takes precedence over the `~/.jdai/org-config-path` pointer file.

## See also

- [Audit Logging](audit-logging.md) — Configuring and querying the audit trail.
- [Configuration](configuration.md) — Project instructions, data directories, and environment variables.
- [Tools Reference](tools-reference.md) — Complete tool documentation including safety tiers.
