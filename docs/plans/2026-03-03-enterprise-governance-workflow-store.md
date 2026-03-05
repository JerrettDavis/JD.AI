# Enterprise Governance and Shared Workflow Store — Implementation Plan

> REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add enterprise governance (policy engine, audit logging, budget tracking, data redaction), a shared workflow store, and hierarchical org instructions to JD.AI.

**Architecture:** The feature is built across 4 parallel work streams in `JD.AI.Core` (policy engine, audit, org instructions) and `JD.AI.Workflows` (shared workflow store). The policy engine evaluates YAML-defined policies at tool invocation time via `ToolConfirmationFilter`. Audit events flow through an `IAuditSink` abstraction with file, Elasticsearch, and webhook implementations. The shared workflow store extends the existing `IWorkflowCatalog` pattern with a new `IWorkflowStore` interface supporting Git-backed storage. All streams integrate through the existing `SlashCommandRouter` for TUI commands.

**Tech Stack:** C# / .NET 10, xUnit + FluentAssertions + NSubstitute, `YamlDotNet` (new dependency for policy parsing), `System.Text.Json`, `LibGit2Sharp` (new dependency for Git-backed workflow store)

**Issue:** https://github.com/JerrettDavis/JD.AI/issues/26

---

## Stream A: Policy Engine (Tasks 1–5)

### Task 1: Policy Models and YAML Schema

**Files:**
- Create: `src/JD.AI.Core/Governance/PolicyModels.cs`
- Create: `src/JD.AI.Core/Governance/PolicyParser.cs`
- Test: `tests/JD.AI.Tests/Governance/PolicyParserTests.cs`
- Modify: `src/JD.AI.Core/JD.AI.Core.csproj` (add YamlDotNet)

**Context:** Define the C# models matching the YAML policy schema from the issue. The parser deserializes YAML into these models with validation.

**Step 1: Add YamlDotNet package reference**

Add to `JD.AI.Core.csproj`:
```xml
<PackageReference Include="YamlDotNet" />
```

**Step 2: Write PolicyModels.cs**

```csharp
namespace JD.AI.Core.Governance;

public sealed class PolicyDocument
{
    public string ApiVersion { get; set; } = "jdai/v1";
    public string Kind { get; set; } = "Policy";
    public PolicyMetadata Metadata { get; set; } = new();
    public PolicySpec Spec { get; set; } = new();
}

public sealed class PolicyMetadata
{
    public string Name { get; set; } = string.Empty;
    public PolicyScope Scope { get; set; } = PolicyScope.User;
    public int Priority { get; set; } = 0;
}

public enum PolicyScope { Global, Organization, Team, Project, User }

public sealed class PolicySpec
{
    public ToolPolicy? Tools { get; set; }
    public ProviderPolicy? Providers { get; set; }
    public ModelPolicy? Models { get; set; }
    public BudgetPolicy? Budget { get; set; }
    public DataPolicy? Data { get; set; }
    public SessionPolicy? Sessions { get; set; }
    public AuditPolicy? Audit { get; set; }
}

public sealed class ToolPolicy
{
    public IList<string> Allowed { get; set; } = [];
    public IList<string> Denied { get; set; } = [];
}

public sealed class ProviderPolicy
{
    public IList<string> Allowed { get; set; } = [];
    public IList<string> Denied { get; set; } = [];
}

public sealed class ModelPolicy
{
    public int? MaxContextWindow { get; set; }
    public IList<string> Denied { get; set; } = [];
}

public sealed class BudgetPolicy
{
    public decimal? MaxDailyUsd { get; set; }
    public decimal? MaxMonthlyUsd { get; set; }
    public int AlertThresholdPercent { get; set; } = 80;
}

public sealed class DataPolicy
{
    public IList<string> NoExternalProviders { get; set; } = [];
    public IList<string> RedactPatterns { get; set; } = [];
}

public sealed class SessionPolicy
{
    public int? RetentionDays { get; set; }
    public bool RequireProjectTag { get; set; }
}

public sealed class AuditPolicy
{
    public bool Enabled { get; set; }
    public string Sink { get; set; } = "file";
    public string? Endpoint { get; set; }
    public string? Index { get; set; }
    public string? Token { get; set; }
    public string? Url { get; set; }
    public string? ConnectionString { get; set; }
    public string? Server { get; set; }
}
```

**Step 3: Write PolicyParser.cs**

```csharp
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Governance;

public static class PolicyParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static PolicyDocument Parse(string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);
        return Deserializer.Deserialize<PolicyDocument>(yaml);
    }

    public static PolicyDocument ParseFile(string path)
    {
        var yaml = File.ReadAllText(path);
        return Parse(yaml);
    }

    public static IReadOnlyList<PolicyDocument> ParseDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return [];

        var files = Directory.GetFiles(directoryPath, "*.yaml")
            .Concat(Directory.GetFiles(directoryPath, "*.yml"));

        return files.Select(ParseFile).ToList();
    }
}
```

**Step 4: Write PolicyParserTests.cs**

Tests should cover: valid YAML round-trips, missing fields get defaults, invalid YAML throws, ParseDirectory reads multiple files, empty directory returns empty list.

**Step 5: Run tests, commit**

```bash
dotnet test tests/JD.AI.Tests --filter "FullyQualifiedName~PolicyParser" -v minimal
git add src/JD.AI.Core/Governance/ tests/JD.AI.Tests/Governance/ src/JD.AI.Core/JD.AI.Core.csproj
git commit -m "feat(governance): add policy YAML models and parser"
```

---

### Task 2: Policy Resolver (Scope Merging)

**Files:**
- Create: `src/JD.AI.Core/Governance/PolicyResolver.cs`
- Test: `tests/JD.AI.Tests/Governance/PolicyResolverTests.cs`

**Context:** Merges policies across scopes. For `allowed` lists → intersection (more restrictive wins). For `denied` lists → union (any deny applies). For numeric limits → minimum wins. Higher-scoped policies have lower priority; more specific scopes override.

**Step 1: Write PolicyResolver.cs**

```csharp
namespace JD.AI.Core.Governance;

public sealed class PolicyResolver
{
    /// <summary>
    /// Merges multiple policies in scope order (global first, user last).
    /// Most-specific-wins for scalar values; intersection for allows, union for denies.
    /// </summary>
    public PolicySpec Resolve(IEnumerable<PolicyDocument> policies)
    {
        var ordered = policies
            .OrderBy(p => p.Metadata.Scope)
            .ThenBy(p => p.Metadata.Priority)
            .ToList();

        if (ordered.Count == 0)
            return new PolicySpec();

        var result = new PolicySpec();

        // Tools: intersection of allowed, union of denied
        var toolAllowed = MergeAllowed(ordered, p => p.Spec.Tools?.Allowed);
        var toolDenied = MergeUnion(ordered, p => p.Spec.Tools?.Denied);
        if (toolAllowed.Count > 0 || toolDenied.Count > 0)
            result.Tools = new ToolPolicy { Allowed = toolAllowed, Denied = toolDenied };

        // Providers: intersection of allowed, union of denied
        var provAllowed = MergeAllowed(ordered, p => p.Spec.Providers?.Allowed);
        var provDenied = MergeUnion(ordered, p => p.Spec.Providers?.Denied);
        if (provAllowed.Count > 0 || provDenied.Count > 0)
            result.Providers = new ProviderPolicy { Allowed = provAllowed, Denied = provDenied };

        // Models: union of denied, minimum context window
        var modelDenied = MergeUnion(ordered, p => p.Spec.Models?.Denied);
        var maxCtx = ordered
            .Select(p => p.Spec.Models?.MaxContextWindow)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .DefaultIfEmpty(int.MaxValue)
            .Min();
        if (modelDenied.Count > 0 || maxCtx < int.MaxValue)
        {
            result.Models = new ModelPolicy { Denied = modelDenied };
            if (maxCtx < int.MaxValue) result.Models.MaxContextWindow = maxCtx;
        }

        // Budget: minimum of all limits
        var budgets = ordered.Select(p => p.Spec.Budget).Where(b => b is not null).ToList();
        if (budgets.Count > 0)
        {
            result.Budget = new BudgetPolicy
            {
                MaxDailyUsd = budgets.Select(b => b!.MaxDailyUsd).Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(decimal.MaxValue).Min(),
                MaxMonthlyUsd = budgets.Select(b => b!.MaxMonthlyUsd).Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(decimal.MaxValue).Min(),
                AlertThresholdPercent = budgets.Select(b => b!.AlertThresholdPercent).Min(),
            };
            if (result.Budget.MaxDailyUsd == decimal.MaxValue) result.Budget.MaxDailyUsd = null;
            if (result.Budget.MaxMonthlyUsd == decimal.MaxValue) result.Budget.MaxMonthlyUsd = null;
        }

        // Data: union of all patterns
        var noExternal = MergeUnion(ordered, p => p.Spec.Data?.NoExternalProviders);
        var redact = MergeUnion(ordered, p => p.Spec.Data?.RedactPatterns);
        if (noExternal.Count > 0 || redact.Count > 0)
            result.Data = new DataPolicy { NoExternalProviders = noExternal, RedactPatterns = redact };

        // Sessions: most-specific non-null wins (last in ordered list)
        var sessions = ordered.Select(p => p.Spec.Sessions).Where(s => s is not null).ToList();
        if (sessions.Count > 0)
        {
            var last = sessions[^1];
            var minRetention = sessions.Select(s => s!.RetentionDays).Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(int.MaxValue).Min();
            result.Sessions = new SessionPolicy
            {
                RetentionDays = minRetention < int.MaxValue ? minRetention : null,
                RequireProjectTag = sessions.Any(s => s!.RequireProjectTag),
            };
        }

        // Audit: most-specific non-null wins
        var audits = ordered.Select(p => p.Spec.Audit).Where(a => a is not null).ToList();
        if (audits.Count > 0)
            result.Audit = audits[^1];

        return result;
    }

    private static List<string> MergeAllowed(
        IReadOnlyList<PolicyDocument> policies,
        Func<PolicyDocument, IList<string>?> selector)
    {
        HashSet<string>? result = null;
        foreach (var policy in policies)
        {
            var list = selector(policy);
            if (list is null || list.Count == 0) continue;
            var set = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
            result = result is null ? set : [.. result.Intersect(set, StringComparer.OrdinalIgnoreCase)];
        }
        return result is null ? [] : [.. result];
    }

    private static List<string> MergeUnion(
        IReadOnlyList<PolicyDocument> policies,
        Func<PolicyDocument, IList<string>?> selector)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var policy in policies)
        {
            var list = selector(policy);
            if (list is not null)
                result.UnionWith(list);
        }
        return [.. result];
    }
}
```

**Step 2: Write PolicyResolverTests.cs**

Tests: single policy pass-through, intersection of tool allows across scopes, union of tool denies, minimum budget wins, scope ordering respected, empty input returns empty spec.

**Step 3: Run tests, commit**

```bash
dotnet test tests/JD.AI.Tests --filter "FullyQualifiedName~PolicyResolver" -v minimal
git add src/JD.AI.Core/Governance/PolicyResolver.cs tests/JD.AI.Tests/Governance/PolicyResolverTests.cs
git commit -m "feat(governance): add policy resolver with scope merging"
```

---

### Task 3: Policy Evaluator

**Files:**
- Create: `src/JD.AI.Core/Governance/IPolicyEvaluator.cs`
- Create: `src/JD.AI.Core/Governance/PolicyEvaluator.cs`
- Create: `src/JD.AI.Core/Governance/PolicyLoader.cs`
- Test: `tests/JD.AI.Tests/Governance/PolicyEvaluatorTests.cs`

**Context:** The evaluator checks a tool invocation against the resolved policy and returns Allow/Deny/Audit. PolicyLoader discovers and loads policies from the filesystem hierarchy.

**Key interfaces:**

```csharp
namespace JD.AI.Core.Governance;

public enum PolicyDecision { Allow, Deny, RequireApproval, Audit }

public sealed record PolicyEvaluationResult(
    PolicyDecision Decision,
    string? Reason = null,
    string? PolicyName = null);

public sealed record PolicyContext(
    string? UserId = null,
    string? ProjectPath = null,
    string? ProviderName = null,
    string? ModelId = null);

public interface IPolicyEvaluator
{
    PolicyEvaluationResult EvaluateTool(string toolName, PolicyContext context);
    PolicyEvaluationResult EvaluateProvider(string providerName, PolicyContext context);
    PolicyEvaluationResult EvaluateModel(string modelId, int? contextWindow, PolicyContext context);
    PolicySpec GetResolvedPolicy();
}
```

**PolicyLoader** discovers policy files from: `~/.jdai/policies/`, project `.jdai/policies/`, org config repo path (configurable). Uses `PolicyParser.ParseDirectory` for each scope, feeds them into `PolicyResolver`.

**Tests:** Tool allowed/denied evaluation, provider restriction, model glob pattern matching (`gpt-*`), context-aware evaluation, no policies = allow all.

**Step: Run tests, commit**

```bash
git commit -m "feat(governance): add policy evaluator and filesystem policy loader"
```

---

### Task 4: Integrate Policy Evaluator with ToolConfirmationFilter

**Files:**
- Modify: `src/JD.AI/Agent/ToolConfirmationFilter.cs`
- Modify: `tests/JD.AI.Tests/Governance/PolicyEvaluatorTests.cs` (integration tests)

**Context:** Inject `IPolicyEvaluator?` into `ToolConfirmationFilter`. Before the existing safety-tier check, evaluate the tool against policy. If Deny → block immediately with reason. If Audit → allow but flag for audit. The existing tier logic remains as fallback when no policy is configured.

**Key change in ToolConfirmationFilter:**

```csharp
// Add to constructor:
private readonly IPolicyEvaluator? _policyEvaluator;

public ToolConfirmationFilter(AgentSession session, IPolicyEvaluator? policyEvaluator = null)
{
    _session = session;
    _policyEvaluator = policyEvaluator;
}

// In OnAutoFunctionInvocationAsync, before safety tier check:
if (_policyEvaluator is not null)
{
    var policyResult = _policyEvaluator.EvaluateTool(functionName, new PolicyContext(
        ProjectPath: _session.SessionInfo?.ProjectPath));

    if (policyResult.Decision == PolicyDecision.Deny)
    {
        ChatRenderer.RenderWarning($"Policy blocked: {functionName} — {policyResult.Reason}");
        context.Result = new FunctionResult(context.Function, $"Blocked by policy: {policyResult.Reason}");
        return;
    }
}
```

**Tests:** Policy deny overrides AutoApprove tier, policy allow with existing tier still works, null evaluator = no change to behavior.

**Step: Run tests, commit**

```bash
git commit -m "feat(governance): integrate policy evaluator with ToolConfirmationFilter"
```

---

### Task 5: Budget Tracking

**Files:**
- Create: `src/JD.AI.Core/Governance/BudgetTracker.cs`
- Create: `src/JD.AI.Core/Governance/IBudgetTracker.cs`
- Test: `tests/JD.AI.Tests/Governance/BudgetTrackerTests.cs`

**Context:** Tracks spending against policy budget limits. Persists daily/monthly totals to `~/.jdai/budget.json`. Alerts when threshold percentage reached. Blocks when limit exceeded.

**Key interface:**

```csharp
public interface IBudgetTracker
{
    Task RecordSpendAsync(decimal amountUsd, string providerName, CancellationToken ct = default);
    Task<BudgetStatus> GetStatusAsync(CancellationToken ct = default);
    bool IsWithinBudget(BudgetPolicy? policy);
}

public sealed record BudgetStatus(
    decimal TodayUsd,
    decimal MonthUsd,
    bool DailyLimitExceeded,
    bool MonthlyLimitExceeded,
    bool AlertTriggered);
```

**Tests:** Record spend updates totals, daily limit exceeded, monthly limit exceeded, alert threshold triggered, no policy = always within budget.

**Step: Commit**

```bash
git commit -m "feat(governance): add budget tracker with daily/monthly limits"
```

---

## Stream B: Audit Logging (Tasks 6–8)

### Task 6: Audit Event Model and IAuditSink

**Files:**
- Create: `src/JD.AI.Core/Governance/Audit/AuditEvent.cs`
- Create: `src/JD.AI.Core/Governance/Audit/IAuditSink.cs`
- Create: `src/JD.AI.Core/Governance/Audit/AuditService.cs`
- Test: `tests/JD.AI.Tests/Governance/Audit/AuditServiceTests.cs`

**Context:** Core audit event model and sink abstraction. The `AuditService` dispatches events to all registered sinks.

```csharp
namespace JD.AI.Core.Governance.Audit;

public sealed class AuditEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? UserId { get; init; }
    public string? SessionId { get; init; }
    public string? TraceId { get; init; }
    public string Action { get; init; } = string.Empty;     // tool.invoke, session.create, policy.deny, etc.
    public string? Resource { get; init; }                   // tool name, file path, etc.
    public string? Detail { get; init; }                     // JSON payload
    public AuditSeverity Severity { get; init; } = AuditSeverity.Info;
    public PolicyDecision? PolicyResult { get; init; }
}

public enum AuditSeverity { Debug, Info, Warning, Error, Critical }

public interface IAuditSink
{
    string Name { get; }
    Task WriteAsync(AuditEvent evt, CancellationToken ct = default);
    Task FlushAsync(CancellationToken ct = default);
}

public sealed class AuditService
{
    private readonly IReadOnlyList<IAuditSink> _sinks;

    public AuditService(IEnumerable<IAuditSink> sinks) => _sinks = sinks.ToList();

    public async Task EmitAsync(AuditEvent evt, CancellationToken ct = default)
    {
        foreach (var sink in _sinks)
        {
            try { await sink.WriteAsync(evt, ct).ConfigureAwait(false); }
            catch { /* Don't let audit failure break the main flow */ }
        }
    }
}
```

**Tests:** AuditService dispatches to all sinks, sink failure doesn't propagate, empty sinks list works.

**Step: Commit**

```bash
git commit -m "feat(audit): add audit event model, IAuditSink, and AuditService"
```

---

### Task 7: File Audit Sink

**Files:**
- Create: `src/JD.AI.Core/Governance/Audit/FileAuditSink.cs`
- Test: `tests/JD.AI.Tests/Governance/Audit/FileAuditSinkTests.cs`

**Context:** Default sink. Writes JSON-line format to `~/.jdai/audit/audit-{yyyy-MM-dd}.jsonl` with daily rotation.

**Tests:** Writes event to correct dated file, daily rotation creates new file, flush ensures write, concurrent writes safe.

**Step: Commit**

```bash
git commit -m "feat(audit): add file-based audit sink with daily rotation"
```

---

### Task 8: Elasticsearch and Webhook Audit Sinks

**Files:**
- Create: `src/JD.AI.Core/Governance/Audit/ElasticsearchAuditSink.cs`
- Create: `src/JD.AI.Core/Governance/Audit/WebhookAuditSink.cs`
- Test: `tests/JD.AI.Tests/Governance/Audit/ElasticsearchAuditSinkTests.cs`
- Test: `tests/JD.AI.Tests/Governance/Audit/WebhookAuditSinkTests.cs`

**Context:** Elasticsearch sink POSTs to `{endpoint}/{index}/_doc`. Webhook sink POSTs JSON to configured URL. Both use `HttpClient`.

**Tests:** Correct HTTP request formed (mock HttpMessageHandler), connection failure handled gracefully, index template with date substitution.

**Step: Commit**

```bash
git commit -m "feat(audit): add Elasticsearch and webhook audit sinks"
```

---

## Stream C: Shared Workflow Store (Tasks 9–11)

### Task 9: SharedWorkflow Model and IWorkflowStore

**Files:**
- Create: `src/JD.AI.Workflows/Store/SharedWorkflow.cs`
- Create: `src/JD.AI.Workflows/Store/IWorkflowStore.cs`
- Create: `src/JD.AI.Workflows/Store/WorkflowVisibility.cs`
- Test: `tests/JD.AI.Tests/Governance/WorkflowStore/SharedWorkflowTests.cs`

**Context:** Extends the existing workflow model with enterprise sharing metadata (author, visibility, required tools, semantic versioning, published timestamp).

```csharp
namespace JD.AI.Workflows.Store;

public enum WorkflowVisibility { Private, Team, Organization, Public }

public sealed class SharedWorkflow
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..16];
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0.0";
    public string Description { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public IList<string> Tags { get; init; } = [];
    public IList<string> RequiredTools { get; init; } = [];
    public WorkflowVisibility Visibility { get; init; } = WorkflowVisibility.Team;
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;
    public string DefinitionJson { get; init; } = string.Empty;
}

public interface IWorkflowStore
{
    Task PublishAsync(SharedWorkflow workflow, CancellationToken ct = default);
    Task<SharedWorkflow?> GetAsync(string id, string? version = null, CancellationToken ct = default);
    Task<IReadOnlyList<SharedWorkflow>> CatalogAsync(string? tag = null, string? author = null, CancellationToken ct = default);
    Task<IReadOnlyList<SharedWorkflow>> SearchAsync(string query, CancellationToken ct = default);
    Task<IReadOnlyList<SharedWorkflow>> VersionsAsync(string name, CancellationToken ct = default);
    Task<bool> InstallAsync(string id, string? version, string localDirectory, CancellationToken ct = default);
}
```

**Tests:** SharedWorkflow defaults, model serialization round-trip.

**Step: Commit**

```bash
git commit -m "feat(workflows): add SharedWorkflow model and IWorkflowStore interface"
```

---

### Task 10: Git-Backed Workflow Store

**Files:**
- Create: `src/JD.AI.Workflows/Store/GitWorkflowStore.cs`
- Modify: `src/JD.AI.Workflows/JD.AI.Workflows.csproj` (add LibGit2Sharp)
- Test: `tests/JD.AI.Tests/Governance/WorkflowStore/GitWorkflowStoreTests.cs`

**Context:** Default implementation using a bare Git repo as the backing store. Each workflow is stored as a JSON file at `{name}/{version}.json`. Operations use LibGit2Sharp for clone, commit, push, and pull.

**Key implementation notes:**
- Constructor takes repo URL and local cache path
- `PublishAsync` clones/pulls, writes JSON, commits and pushes
- `CatalogAsync` scans the repo tree for all `*/latest.json` manifests
- `SearchAsync` does simple substring matching on name, description, tags
- `InstallAsync` copies the workflow definition to local `~/.jdai/workflows/`
- Falls back to `FileWorkflowCatalog`-style flat file if no Git repo configured

**Tests:** Publish creates file in repo, catalog returns published workflows, search filters by query, install copies to local dir. Use temp directories with `git init --bare` for test repos.

**Step: Commit**

```bash
git commit -m "feat(workflows): add Git-backed workflow store implementation"
```

---

### Task 11: TUI Workflow Store Commands

**Files:**
- Modify: `src/JD.AI/Commands/SlashCommandRouter.cs` — extend `/workflow` with `catalog`, `publish`, `install`, `search`, `versions` subcommands
- Test: `tests/JD.AI.Tests/Governance/WorkflowStore/WorkflowStoreCommandTests.cs`

**Context:** Add new subcommands to the existing `/workflow` handler. The router gets an optional `IWorkflowStore` injected alongside the existing `IWorkflowCatalog`.

**New subcommands:**
- `/workflow catalog [--tag <tag>] [--author <author>]` — list shared workflows
- `/workflow publish <name> [--visibility team|org|public]` — publish current workflow
- `/workflow install <id> [version]` — download from store
- `/workflow search <query>` — full-text search
- `/workflow versions <name>` — version history

**Step: Commit**

```bash
git commit -m "feat(workflows): add TUI commands for shared workflow store"
```

---

## Stream D: Organization Instructions & Data Redaction (Tasks 12–14)

### Task 12: Hierarchical Organization Instructions

**Files:**
- Modify: `src/JD.AI.Core/Agents/InstructionsLoader.cs`
- Modify: `src/JD.AI.Core/Config/DataDirectories.cs` (add OrgConfigPath)
- Test: `tests/JD.AI.Tests/Governance/OrgInstructionsTests.cs`

**Context:** Extend `InstructionsLoader` to support org-level instructions. Add a configurable org config repo path. Loading order: org → project → user (existing behavior handles project/user; add org prefix).

**Key changes:**
- Add `OrgConfigPath` property to `DataDirectories` (reads from `JDAI_ORG_CONFIG` env var or `~/.jdai/org-config-path` file)
- In `InstructionsLoader.Load()`, prepend org-level search if `OrgConfigPath` is set
- Org instructions from `{OrgConfigPath}/.jdai/instructions.md` loaded first

**Tests:** Org instructions prepended to system prompt, project overrides org, missing org path = no org instructions.

**Step: Commit**

```bash
git commit -m "feat(governance): add hierarchical org-level instructions loading"
```

---

### Task 13: Data Redaction Engine

**Files:**
- Create: `src/JD.AI.Core/Governance/DataRedactor.cs`
- Test: `tests/JD.AI.Tests/Governance/DataRedactorTests.cs`

**Context:** Applies regex-based redaction patterns from the resolved policy before content is sent to providers. Replaces matched patterns with `[REDACTED]`.

```csharp
namespace JD.AI.Core.Governance;

public sealed class DataRedactor
{
    private readonly IReadOnlyList<Regex> _patterns;

    public DataRedactor(IEnumerable<string> patterns)
    {
        _patterns = patterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
            .ToList();
    }

    public string Redact(string input)
    {
        if (_patterns.Count == 0) return input;
        var result = input;
        foreach (var pattern in _patterns)
            result = pattern.Replace(result, "[REDACTED]");
        return result;
    }

    public bool HasSensitiveContent(string input) =>
        _patterns.Any(p => p.IsMatch(input));
}
```

**Tests:** API key patterns redacted, password patterns redacted, no patterns = pass-through, regex timeout doesn't crash (malicious pattern).

**Step: Commit**

```bash
git commit -m "feat(governance): add data redaction engine with regex patterns"
```

---

### Task 14: Audit Integration with ToolConfirmationFilter and Session Lifecycle

**Files:**
- Modify: `src/JD.AI/Agent/ToolConfirmationFilter.cs` — emit audit events on tool invocations
- Modify: `src/JD.AI.Core/Agents/AgentSession.cs` — emit audit events on session create/close
- Test: `tests/JD.AI.Tests/Governance/Audit/AuditIntegrationTests.cs`

**Context:** Wire `AuditService` into the existing tool and session infrastructure. Every tool invocation (allowed or denied) emits an audit event. Session lifecycle events (create, resume, close) also emit.

**Key change in ToolConfirmationFilter:**

```csharp
// After tool execution or denial:
if (_auditService is not null)
{
    await _auditService.EmitAsync(new AuditEvent
    {
        Action = "tool.invoke",
        Resource = functionName,
        SessionId = _session.SessionInfo?.Id,
        Detail = args,
        PolicyResult = policyResult?.Decision,
        Severity = policyResult?.Decision == PolicyDecision.Deny
            ? AuditSeverity.Warning
            : AuditSeverity.Info,
    }).ConfigureAwait(false);
}
```

**Tests:** Tool invocation emits audit event, denied tool emits warning-level event, session create emits event.

**Step: Commit**

```bash
git commit -m "feat(audit): integrate audit events with tool invocations and session lifecycle"
```

---

## Task 15: Wiring and Build Verification

**Files:**
- Modify: `src/JD.AI/Program.cs` — wire up PolicyLoader, PolicyEvaluator, AuditService, BudgetTracker, IWorkflowStore, DataRedactor
- Modify: `src/JD.AI/Commands/SlashCommandRouter.cs` — accept new dependencies

**Context:** Connect all the new services in the TUI entry point. Load policies from filesystem, create evaluator, create audit service with configured sinks, inject into ToolConfirmationFilter and SlashCommandRouter.

**Step: Full build and test**

```bash
dotnet build JD.AI.slnx
dotnet test JD.AI.slnx -v minimal
```

**Step: Commit**

```bash
git commit -m "feat(governance): wire all governance services into TUI entry point"
```

---

## Parallelization Strategy

These streams can be worked on independently:

| Stream | Tasks | Dependencies |
|--------|-------|-------------|
| A: Policy Engine | 1–5 | Sequential within stream |
| B: Audit Logging | 6–8 | Sequential within stream; uses `PolicyDecision` enum from Task 3 |
| C: Workflow Store | 9–11 | Sequential within stream; fully independent |
| D: Org Instructions + Redaction | 12–13 | Independent of each other |
| Integration | 14–15 | Depends on A + B completion |

**Recommended agent assignment:**
- Agent 1 (sonnet): Stream A (Tasks 1–5) — policy engine is foundational
- Agent 2 (sonnet): Stream B (Tasks 6–8) — audit logging
- Agent 3 (sonnet): Stream C (Tasks 9–11) — workflow store
- Agent 4 (haiku): Stream D (Tasks 12–13) — smaller scope, simpler code
- Final integration (Tasks 14–15): done by lead after streams merge
