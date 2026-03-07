using FluentAssertions;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Tools;

/// <summary>
/// Tests for <see cref="PolicyTools"/> — static validation, evaluation,
/// listing, RBAC, export, and audit query.
/// </summary>
public sealed class PolicyToolsTests
{
    // ── ValidatePolicy (static, pure logic) ────────────────────────────

    [Fact]
    public void ValidatePolicy_EmptyContent_ReportsError()
    {
        var result = PolicyTools.ValidatePolicy("");
        result.Should().Contain("empty");
        result.Should().Contain("1 error(s)");
    }

    [Fact]
    public void ValidatePolicy_WhitespaceOnly_ReportsError()
    {
        var result = PolicyTools.ValidatePolicy("   \n  ");
        result.Should().Contain("empty");
    }

    [Fact]
    public void ValidatePolicy_MissingAllRequiredFields_ReportsAllErrors()
    {
        var result = PolicyTools.ValidatePolicy("some: value");
        result.Should().Contain("apiVersion");
        result.Should().Contain("kind");
        result.Should().Contain("metadata");
        result.Should().Contain("spec");
        result.Should().Contain("4 error(s)");
    }

    [Fact]
    public void ValidatePolicy_HasAllRequiredFields_NoErrors()
    {
        const string yaml = """
            apiVersion: jdai/v1
            kind: JdaiPolicy
            metadata:
              name: test
            spec:
              tools:
                allowed: [git]
            """;

        var result = PolicyTools.ValidatePolicy(yaml);
        result.Should().Contain("0 error(s)");
    }

    [Fact]
    public void ValidatePolicy_NonStandardKind_WarnsAboutKind()
    {
        const string yaml = """
            apiVersion: jdai/v1
            kind: CustomPolicy
            metadata:
              name: test
            spec:
              tools:
                allowed: [git]
            """;

        var result = PolicyTools.ValidatePolicy(yaml);
        result.Should().Contain("kind: JdaiPolicy");
        result.Should().Contain("1 warning(s)");
    }

    [Fact]
    public void ValidatePolicy_StandardKind_NoKindWarning()
    {
        const string yaml = """
            apiVersion: jdai/v1
            kind: JdaiPolicy
            metadata:
              name: test
            spec:
              tools:
                allowed: [git]
            """;

        var result = PolicyTools.ValidatePolicy(yaml);
        result.Should().NotContain("non-standard kind");
    }

    [Fact]
    public void ValidatePolicy_ValidScope_NoScopeWarning()
    {
        const string yaml = """
            apiVersion: jdai/v1
            kind: JdaiPolicy
            metadata:
              name: test
              scope: project
            spec:
              tools:
                allowed: [git]
            """;

        var result = PolicyTools.ValidatePolicy(yaml);
        result.Should().NotContain("Scope should be");
    }

    [Fact]
    public void ValidatePolicy_InvalidScope_WarnsAboutScope()
    {
        const string yaml = """
            apiVersion: jdai/v1
            kind: JdaiPolicy
            metadata:
              name: test
              scope: universe
            spec:
              tools:
                allowed: [git]
            """;

        var result = PolicyTools.ValidatePolicy(yaml);
        result.Should().Contain("Scope should be one of");
    }

    [Theory]
    [InlineData("global")]
    [InlineData("org")]
    [InlineData("team")]
    [InlineData("project")]
    [InlineData("user")]
    public void ValidatePolicy_AllValidScopes_Accepted(string scope)
    {
        var yaml = $"""
            apiVersion: jdai/v1
            kind: JdaiPolicy
            metadata:
              name: test
              scope: {scope}
            spec:
              tools:
                allowed: [git]
            """;

        var result = PolicyTools.ValidatePolicy(yaml);
        result.Should().NotContain("Scope should be");
    }

    [Fact]
    public void ValidatePolicy_NoPolicySections_WarnsAboutNoSections()
    {
        // Note: "metadata:" contains substring "data:" so we must avoid that field
        // to test zero-section detection.
        const string yaml = """
            apiVersion: jdai/v1
            kind: JdaiPolicy
            meta:
              name: test
            spec:
              nothing: here
            """;

        var result = PolicyTools.ValidatePolicy(yaml);
        result.Should().Contain("No policy sections found");
    }

    [Fact]
    public void ValidatePolicy_WithToolsSection_NoSectionsWarning()
    {
        const string yaml = """
            apiVersion: jdai/v1
            kind: JdaiPolicy
            metadata:
              name: test
            spec:
              tools:
                allowed: [git]
            """;

        var result = PolicyTools.ValidatePolicy(yaml);
        result.Should().NotContain("No policy sections found");
    }

    [Fact]
    public void ValidatePolicy_HighBudget_WarnsAboutHighValue()
    {
        const string yaml = """
            apiVersion: jdai/v1
            kind: JdaiPolicy
            metadata:
              name: test
            spec:
              budget:
                maxDailyUsd: 5000
            """;

        var result = PolicyTools.ValidatePolicy(yaml);
        result.Should().Contain("unusually high");
    }

    [Fact]
    public void ValidatePolicy_NormalBudget_NoHighValueWarning()
    {
        const string yaml = """
            apiVersion: jdai/v1
            kind: JdaiPolicy
            metadata:
              name: test
            spec:
              budget:
                maxDailyUsd: 50
            """;

        var result = PolicyTools.ValidatePolicy(yaml);
        result.Should().NotContain("unusually high");
    }

    [Fact]
    public void ValidatePolicy_FullyValid_ShowsValidMessage()
    {
        const string yaml = """
            apiVersion: jdai/v1
            kind: JdaiPolicy
            metadata:
              name: test
              scope: project
            spec:
              tools:
                allowed: [git, policy]
              budget:
                maxDailyUsd: 100
            """;

        var result = PolicyTools.ValidatePolicy(yaml);
        result.Should().Contain("Valid");
        result.Should().Contain("0 error(s), 0 warning(s)");
    }

    [Fact]
    public void ValidatePolicy_CaseInsensitiveFieldCheck()
    {
        const string yaml = """
            APIVERSION: jdai/v1
            KIND: JdaiPolicy
            METADATA:
              name: test
            SPEC:
              tools:
                allowed: [git]
            """;

        var result = PolicyTools.ValidatePolicy(yaml);
        result.Should().Contain("0 error(s)");
    }

    // ── EvaluatePolicy (with mock IPolicyEvaluator) ──────────────────

    [Fact]
    public void EvaluatePolicy_NullEvaluator_ReturnsNotConfigured()
    {
        var tools = new PolicyTools();
        var result = tools.EvaluatePolicy("tool", "git");
        result.Should().Contain("No policy evaluator configured");
    }

    [Fact]
    public void EvaluatePolicy_Tool_Allow_ReturnsAllowIcon()
    {
        var evaluator = new FakePolicyEvaluator(PolicyDecision.Allow, "Allowed by default");
        var tools = new PolicyTools(evaluator);
        var result = tools.EvaluatePolicy("tool", "git");
        result.Should().Contain("Allow");
        result.Should().Contain("Allowed by default");
    }

    [Fact]
    public void EvaluatePolicy_Tool_Deny_ReturnsDenyIcon()
    {
        var evaluator = new FakePolicyEvaluator(PolicyDecision.Deny, "Blocked by policy", "security-policy");
        var tools = new PolicyTools(evaluator);
        var result = tools.EvaluatePolicy("tool", "rm");
        result.Should().Contain("Deny");
        result.Should().Contain("security-policy");
    }

    [Fact]
    public void EvaluatePolicy_Provider_DelegatesToEvaluator()
    {
        var evaluator = new FakePolicyEvaluator(PolicyDecision.RequireApproval, "Needs approval");
        var tools = new PolicyTools(evaluator);
        var result = tools.EvaluatePolicy("provider", "openai");
        result.Should().Contain("RequireApproval");
        evaluator.LastEvaluatedType.Should().Be("provider");
    }

    [Fact]
    public void EvaluatePolicy_Model_DelegatesToEvaluator()
    {
        var evaluator = new FakePolicyEvaluator(PolicyDecision.Audit, "Audit trail");
        var tools = new PolicyTools(evaluator);
        var result = tools.EvaluatePolicy("model", "gpt-4");
        result.Should().Contain("Audit");
        evaluator.LastEvaluatedType.Should().Be("model");
    }

    [Fact]
    public void EvaluatePolicy_UnknownType_ReturnsAllow()
    {
        var evaluator = new FakePolicyEvaluator(PolicyDecision.Allow, "default");
        var tools = new PolicyTools(evaluator);
        var result = tools.EvaluatePolicy("unknown", "thing");
        result.Should().Contain("Allow");
    }

    [Fact]
    public void EvaluatePolicy_CaseInsensitiveType()
    {
        var evaluator = new FakePolicyEvaluator(PolicyDecision.Allow, "ok");
        var tools = new PolicyTools(evaluator);
        var result = tools.EvaluatePolicy("TOOL", "git");
        result.Should().Contain("Allow");
        evaluator.LastEvaluatedType.Should().Be("tool");
    }

    [Fact]
    public void EvaluatePolicy_EvaluatorThrows_ReturnsError()
    {
        var evaluator = new ThrowingPolicyEvaluator();
        var tools = new PolicyTools(evaluator);
        var result = tools.EvaluatePolicy("tool", "git");
        result.Should().Contain("Error");
    }

    // ── ListPolicies ──────────────────────────────────────────────────

    [Fact]
    public void ListPolicies_NullEvaluator_ReturnsNotConfigured()
    {
        var tools = new PolicyTools();
        var result = tools.ListPolicies();
        result.Should().Contain("No policy evaluator configured");
    }

    [Fact]
    public void ListPolicies_WithToolPolicy_ListsAllowedAndDenied()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy
            {
                Allowed = ["git", "policy"],
                Denied = ["rm"],
            },
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.ListPolicies();
        result.Should().Contain("git");
        result.Should().Contain("policy");
        result.Should().Contain("rm");
        result.Should().Contain("Tool Policy");
    }

    [Fact]
    public void ListPolicies_WithBudget_ShowsLimits()
    {
        var spec = new PolicySpec
        {
            Budget = new BudgetPolicy
            {
                MaxDailyUsd = 50,
                MaxMonthlyUsd = 500,
                MaxSessionUsd = 10,
            },
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.ListPolicies();
        result.Should().Contain("50.00");
        result.Should().Contain("500.00");
        result.Should().Contain("10.00");
        result.Should().Contain("Budget Policy");
    }

    [Fact]
    public void ListPolicies_WithProviderPolicy_ListsProviders()
    {
        var spec = new PolicySpec
        {
            Providers = new ProviderPolicy
            {
                Allowed = ["anthropic"],
                Denied = ["openai"],
            },
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.ListPolicies();
        result.Should().Contain("anthropic");
        result.Should().Contain("openai");
        result.Should().Contain("Provider Policy");
    }

    [Fact]
    public void ListPolicies_WithModelPolicy_ShowsContextWindow()
    {
        var spec = new PolicySpec
        {
            Models = new ModelPolicy
            {
                MaxContextWindow = 128000,
                Denied = ["gpt-3.5"],
            },
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.ListPolicies();
        result.Should().Contain("128,000");
        result.Should().Contain("gpt-3.5");
        result.Should().Contain("Model Policy");
    }

    [Fact]
    public void ListPolicies_WithDataPolicy_ShowsRedactPatterns()
    {
        var spec = new PolicySpec
        {
            Data = new DataPolicy
            {
                NoExternalProviders = ["openai"],
                RedactPatterns = [@"\b\d{3}-\d{2}-\d{4}\b"],
            },
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.ListPolicies();
        result.Should().Contain("Data Policy");
        result.Should().Contain("1 active");
    }

    [Fact]
    public void ListPolicies_WithSessionPolicy_ShowsRetention()
    {
        var spec = new PolicySpec
        {
            Sessions = new SessionPolicy
            {
                RetentionDays = 30,
                RequireProjectTag = true,
            },
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.ListPolicies();
        result.Should().Contain("30 days");
        result.Should().Contain("True");
        result.Should().Contain("Session Policy");
    }

    [Fact]
    public void ListPolicies_WithCircuitBreaker_ShowsThresholds()
    {
        var spec = new PolicySpec
        {
            CircuitBreaker = new CircuitBreakerPolicy
            {
                RepetitionWarningThreshold = 3,
                RepetitionHardStopThreshold = 5,
                CooldownSeconds = 60,
            },
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.ListPolicies();
        result.Should().Contain("Circuit Breaker");
        result.Should().Contain("60s");
    }

    [Fact]
    public void ListPolicies_EmptyToolPolicy_ShowsNoRestrictions()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy(),
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.ListPolicies();
        result.Should().Contain("No restrictions");
    }

    [Fact]
    public void ListPolicies_EvaluatorThrows_ReturnsError()
    {
        var evaluator = new ThrowingPolicyEvaluator();
        var tools = new PolicyTools(evaluator);
        var result = tools.ListPolicies();
        result.Should().Contain("Error");
    }

    // ── CheckRbac ─────────────────────────────────────────────────────

    [Fact]
    public void CheckRbac_NullEvaluator_ReturnsAllAvailable()
    {
        var tools = new PolicyTools();
        var result = tools.CheckRbac("testuser");
        result.Should().Contain("all capabilities are available");
    }

    [Fact]
    public void CheckRbac_WithToolWhitelist_ShowsWhitelistMode()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy { Allowed = ["git", "policy"] },
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.CheckRbac("testuser");
        result.Should().Contain("Whitelist mode");
        result.Should().Contain("2 tools allowed");
    }

    [Fact]
    public void CheckRbac_WithToolBlacklist_ShowsBlacklistMode()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy { Denied = ["rm", "kill"] },
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.CheckRbac("testuser");
        result.Should().Contain("Blacklist mode");
        result.Should().Contain("2 tools blocked");
    }

    [Fact]
    public void CheckRbac_NoToolRestrictions_ShowsOpen()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy(),
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.CheckRbac("testuser");
        result.Should().Contain("All tools available");
    }

    [Fact]
    public void CheckRbac_WithBudget_ShowsBudgetLimits()
    {
        var spec = new PolicySpec
        {
            Budget = new BudgetPolicy
            {
                MaxDailyUsd = 25,
                MaxSessionUsd = 5,
            },
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.CheckRbac("testuser");
        result.Should().Contain("25.00");
        result.Should().Contain("5.00");
    }

    [Fact]
    public void CheckRbac_NoBudget_ShowsNoBudgetLimits()
    {
        var spec = new PolicySpec();
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.CheckRbac("testuser");
        result.Should().Contain("No budget limits");
    }

    [Fact]
    public void CheckRbac_DefaultUserId_UsesCurrentUser()
    {
        var spec = new PolicySpec();
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.CheckRbac();
        result.Should().Contain("RBAC Check");
    }

    [Fact]
    public void CheckRbac_WithProviderRestrictions_ShowsProviderAccess()
    {
        var spec = new PolicySpec
        {
            Providers = new ProviderPolicy { Allowed = ["anthropic"] },
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.CheckRbac("testuser");
        result.Should().Contain("Restricted to");
        result.Should().Contain("anthropic");
    }

    [Fact]
    public void CheckRbac_WithProviderDenied_ShowsBlocked()
    {
        var spec = new PolicySpec
        {
            Providers = new ProviderPolicy { Denied = ["openai"] },
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.CheckRbac("testuser");
        result.Should().Contain("Blocked");
        result.Should().Contain("openai");
    }

    [Fact]
    public void CheckRbac_NoProviderRestrictions_ShowsAllOpen()
    {
        var spec = new PolicySpec();
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.CheckRbac("testuser");
        result.Should().Contain("All providers available");
    }

    [Fact]
    public void CheckRbac_WithDataProtection_ShowsCounts()
    {
        var spec = new PolicySpec
        {
            Data = new DataPolicy
            {
                NoExternalProviders = ["openai", "cohere"],
                RedactPatterns = [@"\bSSN\b", @"\bCCV\b"],
            },
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.CheckRbac("testuser");
        result.Should().Contain("2"); // providers blocked count
        result.Should().Contain("Data Protection");
    }

    // ── ExportPolicy ──────────────────────────────────────────────────

    [Fact]
    public void ExportPolicy_NullEvaluator_ReturnsNotConfigured()
    {
        var tools = new PolicyTools();
        var result = tools.ExportPolicy();
        result.Should().Contain("No policy evaluator configured");
    }

    [Fact]
    public void ExportPolicy_WithSpec_ReturnsJson()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy { Allowed = ["git"] },
        };
        var evaluator = new FakePolicyEvaluator(spec: spec);
        var tools = new PolicyTools(evaluator);
        var result = tools.ExportPolicy();
        result.Should().Contain("\"git\"");
    }

    [Fact]
    public void ExportPolicy_EvaluatorThrows_ReturnsError()
    {
        var evaluator = new ThrowingPolicyEvaluator();
        var tools = new PolicyTools(evaluator);
        var result = tools.ExportPolicy();
        result.Should().Contain("Error");
    }

    // ── QueryAudit ────────────────────────────────────────────────────

    [Fact]
    public async Task QueryAudit_NullAuditService_ReturnsNotConfigured()
    {
        var tools = new PolicyTools();
        var result = await tools.QueryAudit();
        result.Should().Contain("No audit service configured");
    }

    [Fact]
    public async Task QueryAudit_WithAuditService_ReturnsActiveMessage()
    {
        var auditService = new AuditService([]);
        var tools = new PolicyTools(auditService: auditService);
        var result = await tools.QueryAudit();
        result.Should().Contain("Audit service is active");
        result.Should().Contain("10 events"); // default count
    }

    [Fact]
    public async Task QueryAudit_CustomCount_ClampsToRange()
    {
        var auditService = new AuditService([]);
        var tools = new PolicyTools(auditService: auditService);
        var result = await tools.QueryAudit(100); // clamped to 50
        result.Should().Contain("50 events");
    }

    [Fact]
    public async Task QueryAudit_ZeroCount_ClampsToOne()
    {
        var auditService = new AuditService([]);
        var tools = new PolicyTools(auditService: auditService);
        var result = await tools.QueryAudit(0); // clamped to 1
        result.Should().Contain("1 events");
    }

    // ── Test Doubles ──────────────────────────────────────────────────

    private sealed class FakePolicyEvaluator : IPolicyEvaluator
    {
        private readonly PolicyEvaluationResult _result;
        private readonly PolicySpec _spec;

        public string? LastEvaluatedType { get; private set; }

        public FakePolicyEvaluator(
            PolicyDecision decision = PolicyDecision.Allow,
            string? reason = null,
            string? policyName = null,
            PolicySpec? spec = null)
        {
            _result = new PolicyEvaluationResult(decision, reason, policyName);
            _spec = spec ?? new PolicySpec();
        }

        public PolicyEvaluationResult EvaluateTool(string toolName, PolicyContext context)
        {
            LastEvaluatedType = "tool";
            return _result;
        }

        public PolicyEvaluationResult EvaluateProvider(string providerName, PolicyContext context)
        {
            LastEvaluatedType = "provider";
            return _result;
        }

        public PolicyEvaluationResult EvaluateModel(string modelId, int? contextWindow, PolicyContext context)
        {
            LastEvaluatedType = "model";
            return _result;
        }

        public PolicyEvaluationResult EvaluateWorkflowPublish(PolicyContext context) => _result;

        public PolicySpec GetResolvedPolicy() => _spec;
    }

    private sealed class ThrowingPolicyEvaluator : IPolicyEvaluator
    {
        public PolicyEvaluationResult EvaluateTool(string toolName, PolicyContext context)
            => throw new InvalidOperationException("Test exception");

        public PolicyEvaluationResult EvaluateProvider(string providerName, PolicyContext context)
            => throw new InvalidOperationException("Test exception");

        public PolicyEvaluationResult EvaluateModel(string modelId, int? contextWindow, PolicyContext context)
            => throw new InvalidOperationException("Test exception");

        public PolicyEvaluationResult EvaluateWorkflowPublish(PolicyContext context)
            => throw new InvalidOperationException("Test exception");

        public PolicySpec GetResolvedPolicy()
            => throw new InvalidOperationException("Test exception");
    }
}
