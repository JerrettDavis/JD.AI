using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

/// <summary>
/// Extended tests for <see cref="PolicyEvaluator"/> covering the fail-closed
/// identity guard in <see cref="PolicyEvaluator.EvaluateWorkflowPublish"/>
/// and role-based overrides.
/// </summary>
public sealed class PolicyEvaluatorExtendedTests
{
    // ── Workflow publish fail-closed identity guard ────────────────────────

    [Fact]
    public void EvaluateWorkflowPublish_NullUserId_WithPublishAllowed_Denies()
    {
        var spec = new PolicySpec
        {
            Workflows = new WorkflowPolicy { PublishAllowed = ["alice"] },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateWorkflowPublish(new PolicyContext(UserId: null));

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Reason.Should().Contain("No authenticated user identity");
    }

    [Fact]
    public void EvaluateWorkflowPublish_WhitespaceUserId_WithPublishAllowed_Denies()
    {
        var spec = new PolicySpec
        {
            Workflows = new WorkflowPolicy { PublishAllowed = ["alice"] },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateWorkflowPublish(new PolicyContext(UserId: "   "));

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Reason.Should().Contain("No authenticated user identity");
    }

    [Fact]
    public void EvaluateWorkflowPublish_EmptyUserId_WithPublishDenied_Denies()
    {
        var spec = new PolicySpec
        {
            Workflows = new WorkflowPolicy { PublishDenied = ["mallory"] },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateWorkflowPublish(new PolicyContext(UserId: ""));

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Reason.Should().Contain("No authenticated user identity");
    }

    // ── Role-based tool overrides ─────────────────────────────────────────

    [Fact]
    public void EvaluateTool_RoleDenyTakesPrecedence()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy { Allowed = ["shell_exec"] },
            Roles = new RolePolicy
            {
                Definitions = new Dictionary<string, RoleDefinition>(StringComparer.Ordinal)
                {
                    ["restricted"] = new RoleDefinition { DenyTools = ["shell_exec"] },
                },
            },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateTool("shell_exec", new PolicyContext(RoleName: "restricted"));

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Reason.Should().Contain("role");
    }

    [Fact]
    public void EvaluateTool_RoleAllowOverridesBaseDeny()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy { Denied = ["shell_exec"] },
            Roles = new RolePolicy
            {
                Definitions = new Dictionary<string, RoleDefinition>(StringComparer.Ordinal)
                {
                    ["admin"] = new RoleDefinition { AllowTools = ["shell_exec"] },
                },
            },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateTool("shell_exec", new PolicyContext(RoleName: "admin"));

        result.Decision.Should().Be(PolicyDecision.Allow);
    }

    // ── Role-based provider overrides ─────────────────────────────────────

    [Fact]
    public void EvaluateProvider_RoleDenyOverrides()
    {
        var spec = new PolicySpec
        {
            Roles = new RolePolicy
            {
                Definitions = new Dictionary<string, RoleDefinition>(StringComparer.Ordinal)
                {
                    ["restricted"] = new RoleDefinition { DenyProviders = ["ollama"] },
                },
            },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateProvider("ollama", new PolicyContext(RoleName: "restricted"));

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Reason.Should().Contain("role");
    }

    // ── Role-based model overrides ────────────────────────────────────────

    [Fact]
    public void EvaluateModel_RoleDenyOverrides()
    {
        var spec = new PolicySpec
        {
            Roles = new RolePolicy
            {
                Definitions = new Dictionary<string, RoleDefinition>(StringComparer.Ordinal)
                {
                    ["basic"] = new RoleDefinition { DenyModels = ["gpt-4*"] },
                },
            },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateModel("gpt-4o", null, new PolicyContext(RoleName: "basic"));

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Reason.Should().Contain("role");
    }

    [Fact]
    public void EvaluateModel_RoleAllowOverridesBaseDeny()
    {
        var spec = new PolicySpec
        {
            Models = new ModelPolicy { Denied = ["gpt-4o"] },
            Roles = new RolePolicy
            {
                Definitions = new Dictionary<string, RoleDefinition>(StringComparer.Ordinal)
                {
                    ["admin"] = new RoleDefinition { AllowModels = ["gpt-4o"] },
                },
            },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateModel("gpt-4o", null, new PolicyContext(RoleName: "admin"));

        result.Decision.Should().Be(PolicyDecision.Allow);
    }

    // ── Role inheritance ──────────────────────────────────────────────────

    [Fact]
    public void EvaluateTool_RoleInheritance_MergesParentGrants()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy { Denied = ["shell_exec", "read_file"] },
            Roles = new RolePolicy
            {
                Definitions = new Dictionary<string, RoleDefinition>(StringComparer.Ordinal)
                {
                    ["base"] = new RoleDefinition { AllowTools = ["read_file"] },
                    ["elevated"] = new RoleDefinition
                    {
                        Inherits = ["base"],
                        AllowTools = ["shell_exec"],
                    },
                },
            },
        };
        var evaluator = new PolicyEvaluator(spec);

        // "elevated" inherits "base" → gets both AllowTools
        var shellResult = evaluator.EvaluateTool("shell_exec", new PolicyContext(RoleName: "elevated"));
        var readResult = evaluator.EvaluateTool("read_file", new PolicyContext(RoleName: "elevated"));

        shellResult.Decision.Should().Be(PolicyDecision.Allow);
        readResult.Decision.Should().Be(PolicyDecision.Allow);
    }

    // ── Null arguments ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullSpec_Throws()
    {
        var act = () => new PolicyEvaluator(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EvaluateTool_NullToolName_Throws()
    {
        var evaluator = new PolicyEvaluator(new PolicySpec());
        var act = () => evaluator.EvaluateTool(null!, new PolicyContext());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EvaluateProvider_NullProviderName_Throws()
    {
        var evaluator = new PolicyEvaluator(new PolicySpec());
        var act = () => evaluator.EvaluateProvider(null!, new PolicyContext());
        act.Should().Throw<ArgumentNullException>();
    }
}
