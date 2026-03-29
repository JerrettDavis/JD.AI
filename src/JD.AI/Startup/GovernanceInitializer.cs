using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Checkpointing;
using JD.AI.Core.Agents.Orchestration;
using JD.AI.Core.Config;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using JD.AI.Core.Safety;
using JD.AI.Core.Tools;
using JD.AI.Core.Usage;
using JD.AI.Rendering;
using JD.AI.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Startup;

/// <summary>
/// Result of governance, audit, budget, and safety initialization.
/// </summary>
internal sealed record GovernanceSetup(
    IPolicyEvaluator? PolicyEvaluator,
    AuditService AuditService,
    FileAuditSink FileAuditSink,
    BudgetTracker BudgetTracker,
    BudgetPolicy? BudgetPolicy,
    CircuitBreaker CircuitBreaker,
    ICheckpointStrategy CheckpointStrategy,
    InstructionsResult Instructions);

/// <summary>
/// Initializes governance policies, audit sinks, budget tracking,
/// circuit breaker, tool confirmation filter, and project instructions.
/// Extracted from Program.cs lines 314-413.
/// </summary>
internal static class GovernanceInitializer
{
    public static GovernanceSetup Initialize(
        string projectPath,
        AgentSession session,
        Kernel kernel,
        CliOptions opts,
        decimal? maxBudgetUsd)
    {
        // Load governance policies
        var policies = PolicyLoader.Load(projectPath);
        IPolicyEvaluator? policyEvaluator = null;
        if (policies.Count > 0)
        {
            var resolvedSpec = PolicyResolver.Resolve(policies);
            policyEvaluator = new PolicyEvaluator(resolvedSpec);
            if (!opts.PrintMode) ChatRenderer.RenderInfo($"  Loaded {policies.Count} governance policy file(s)");
        }

        // Audit sinks
        var auditSinks = new List<IAuditSink>();
        var auditDir = Path.Combine(DataDirectories.Root, "audit");
        var fileAuditSink = new FileAuditSink(auditDir);
        auditSinks.Add(fileAuditSink);

        var auditPolicy = policies
            .SelectMany(p => p.Spec.Audit is { } a ? [a] : Array.Empty<AuditPolicy>())
            .FirstOrDefault();
        if (auditPolicy is not null)
        {
            if (!string.IsNullOrWhiteSpace(auditPolicy.Endpoint) && !string.IsNullOrWhiteSpace(auditPolicy.Index))
                auditSinks.Add(new ElasticsearchAuditSink(
                    new HttpClient(), auditPolicy.Endpoint, auditPolicy.Index, auditPolicy.Token));
            if (!string.IsNullOrWhiteSpace(auditPolicy.Url))
                auditSinks.Add(new WebhookAuditSink(new HttpClient(), auditPolicy.Url));
        }

        var auditService = new AuditService(auditSinks);
        session.AuditService = auditService;

        // Budget tracking
        var budgetTracker = new BudgetTracker();
        BudgetPolicy? budgetPolicy = null;
        if (maxBudgetUsd.HasValue)
        {
            budgetPolicy = new BudgetPolicy { MaxSessionUsd = maxBudgetUsd };
        }

        var governanceBudget = policies
            .SelectMany(p => p.Spec.Budget is { } b ? [b] : Array.Empty<BudgetPolicy>())
            .FirstOrDefault();
        if (governanceBudget is not null)
        {
            budgetPolicy ??= new BudgetPolicy();
            budgetPolicy.MaxDailyUsd ??= governanceBudget.MaxDailyUsd;
            budgetPolicy.MaxMonthlyUsd ??= governanceBudget.MaxMonthlyUsd;
            budgetPolicy.MaxSessionUsd ??= governanceBudget.MaxSessionUsd;
        }

        // Circuit breaker for tool loop detection
        var cbPolicy = policies
            .SelectMany(p => p.Spec.CircuitBreaker is { } cb ? [cb] : Array.Empty<CircuitBreakerPolicy>())
            .FirstOrDefault();

        var detector = new ToolLoopDetector(
            windowSize: cbPolicy?.WindowSize ?? 50,
            repetitionWarningThreshold: cbPolicy?.RepetitionWarningThreshold ?? 3,
            repetitionHardStopThreshold: cbPolicy?.RepetitionHardStopThreshold ?? 5,
            pingPongThreshold: cbPolicy?.PingPongThreshold ?? 4);

        var circuitBreaker = new CircuitBreaker(
            detector,
            cooldownPeriod: TimeSpan.FromSeconds(cbPolicy?.CooldownSeconds ?? 30),
            hardenedMode: cbPolicy?.Hardened ?? false);

        // Tool confirmation filter
        kernel.AutoFunctionInvocationFilters.Add(
            new ToolConfirmationFilter(session, policyEvaluator, auditService, circuitBreaker));

        // Wire safety tier map for text-based tool call validation
        session.ToolSafetyTiers = ToolConfirmationFilter.ToolTierMap;

        // Policy tools
        kernel.Plugins.AddFromObject(new PolicyTools(policyEvaluator, auditService), "policy");

        // Tool loadout system — built-ins wrapped by file-based user loadouts
        var builtInRegistry = new ToolLoadoutRegistry();
        var fileRegistry = new FileToolLoadoutRegistry([
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdai", "loadouts"),
            Path.Combine(Directory.GetCurrentDirectory(), "loadouts"),
        ]);
        var loadoutRegistry = new CompositeToolLoadoutRegistry(fileRegistry, builtInRegistry);
        var allPlugins = kernel.Plugins.ToList().AsReadOnly();
        session.LoadoutRegistry = loadoutRegistry;
        session.AllPlugins = allPlugins;
        kernel.Plugins.AddFromObject(
            new ToolDiscoveryTools(kernel, loadoutRegistry, allPlugins), "toolDiscovery");

        // Agent definition registry — file-backed versioned registry + memory cache
        var agentRootPath = Path.Combine(DataDirectories.Root, "agents");
        var agentRegistry = new FileAgentDefinitionRegistry(agentRootPath);
        var agentLoader = new FileAgentDefinitionLoader(
            agentRegistry,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FileAgentDefinitionLoader>.Instance);
        var agentSearchPaths = new[]
        {
            Path.Combine(agentRootPath, AgentEnvironments.Dev),
            Path.Combine(projectPath, "agents"),
            Path.Combine(projectPath, ".agents"),
        };
        agentLoader.LoadAll(agentSearchPaths);
        session.AgentDefinitionRegistry = agentRegistry;
        if (!opts.PrintMode && agentRegistry.GetAll().Count > 0)
            ChatRenderer.RenderInfo($"  Loaded {agentRegistry.GetAll().Count} agent definition(s)");

        // Project instructions
        var instructions = InstructionsLoader.Load();
        if (instructions.HasInstructions)
        {
            if (!opts.PrintMode) ChatRenderer.RenderInfo($"  Loaded {instructions.Files.Count} instruction file(s)");
        }

        // Subagent runner
        var orchestrator = new TeamOrchestrator(session);
        kernel.ImportPluginFromObject(new SubagentTools(orchestrator), "SubagentTools");

        // Approval service — policy-based or auto-approve in print/non-interactive mode
        var approvalInner = opts.PrintMode
            ? (JD.AI.Core.Governance.IApprovalService)new JD.AI.Core.Governance.AutoApproveService()
            : new JD.AI.Core.Governance.AutoApproveService(); // default: auto-approve; replace for human-in-loop

        session.ApprovalService = policies.Count > 0
            ? new JD.AI.Core.Governance.PolicyBasedApprovalService(
                PolicyResolver.Resolve(policies),
                approvalInner)
            : approvalInner;

        // Checkpoint strategy
        ICheckpointStrategy checkpointStrategy = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), ".git"))
            ? new StashCheckpointStrategy()
            : new DirectoryCheckpointStrategy();

        // Tool filtering
        ApplyToolFiltering(kernel, opts);

        return new GovernanceSetup(
            policyEvaluator, auditService, fileAuditSink, budgetTracker,
            budgetPolicy, circuitBreaker, checkpointStrategy, instructions);
    }

    private static void ApplyToolFiltering(Kernel kernel, CliOptions opts)
    {
        if (opts.AllowedTools is { Length: > 0 })
        {
            var allowed = new HashSet<string>(opts.AllowedTools, StringComparer.OrdinalIgnoreCase);
            var toRemove = kernel.Plugins
                .SelectMany(p => p.Select(f => (Plugin: p, Function: f)))
                .Where(pf => !allowed.Contains(pf.Function.Name) &&
                             !allowed.Contains($"{pf.Plugin.Name}-{pf.Function.Name}"))
                .Select(pf => pf.Plugin.Name)
                .Distinct()
                .ToList();
            foreach (var name in toRemove)
            {
                if (!allowed.Contains(name))
                    kernel.Plugins.Remove(kernel.Plugins[name]);
            }
        }

        if (opts.DisallowedTools is { Length: > 0 })
        {
            var disallowed = new HashSet<string>(opts.DisallowedTools, StringComparer.OrdinalIgnoreCase);
            var toRemove = kernel.Plugins.Where(p => disallowed.Contains(p.Name))
                .Select(p => p.Name).ToList();
            foreach (var name in toRemove)
            {
                kernel.Plugins.Remove(kernel.Plugins[name]);
            }
        }
    }
}
