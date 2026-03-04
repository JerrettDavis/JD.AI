using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

public sealed class PolicyEvaluatorTests
{
    private static PolicyContext EmptyContext() => new();

    [Fact]
    public void EvaluateTool_NoToolPolicy_Allows()
    {
        var evaluator = new PolicyEvaluator(new PolicySpec());

        var result = evaluator.EvaluateTool("read_file", EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void EvaluateTool_ToolInDeniedList_Denies()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy { Denied = ["shell_exec"] },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateTool("shell_exec", EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Reason.Should().Contain("shell_exec");
    }

    [Fact]
    public void EvaluateTool_ToolInAllowedList_Allows()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy { Allowed = ["read_file", "write_file"] },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateTool("read_file", EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void EvaluateTool_ToolNotInAllowedList_Denies()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy { Allowed = ["read_file"] },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateTool("shell_exec", EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Reason.Should().Contain("not in the allowed list");
    }

    [Fact]
    public void EvaluateTool_DeniedTakesPrecedenceOverAllowed_Denies()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy
            {
                Allowed = ["read_file", "shell_exec"],
                Denied = ["shell_exec"],
            },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateTool("shell_exec", EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Deny);
    }

    [Fact]
    public void EvaluateTool_EmptyAllowedList_AllowsAll()
    {
        // Empty allowed list means "no restriction on allowed"
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy { Allowed = [] },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateTool("any_tool", EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void EvaluateTool_IsCaseInsensitive()
    {
        var spec = new PolicySpec
        {
            Tools = new ToolPolicy { Denied = ["Shell_Exec"] },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateTool("shell_exec", EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Deny);
    }

    [Fact]
    public void EvaluateProvider_NoProviderPolicy_Allows()
    {
        var evaluator = new PolicyEvaluator(new PolicySpec());

        var result = evaluator.EvaluateProvider("openai", EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void EvaluateProvider_ProviderInDeniedList_Denies()
    {
        var spec = new PolicySpec
        {
            Providers = new ProviderPolicy { Denied = ["ollama"] },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateProvider("ollama", EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Reason.Should().Contain("ollama");
    }

    [Fact]
    public void EvaluateProvider_ProviderInAllowedList_Allows()
    {
        var spec = new PolicySpec
        {
            Providers = new ProviderPolicy { Allowed = ["openai", "anthropic"] },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateProvider("openai", EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void EvaluateProvider_ProviderNotInAllowedList_Denies()
    {
        var spec = new PolicySpec
        {
            Providers = new ProviderPolicy { Allowed = ["openai"] },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateProvider("ollama", EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Deny);
    }

    [Fact]
    public void EvaluateModel_NoModelPolicy_Allows()
    {
        var evaluator = new PolicyEvaluator(new PolicySpec());

        var result = evaluator.EvaluateModel("gpt-4o", null, EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void EvaluateModel_ExactMatchInDeniedList_Denies()
    {
        var spec = new PolicySpec
        {
            Models = new ModelPolicy { Denied = ["gpt-4-turbo"] },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateModel("gpt-4-turbo", null, EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Deny);
    }

    [Fact]
    public void EvaluateModel_GlobPatternMatchesDeniedList_Denies()
    {
        var spec = new PolicySpec
        {
            Models = new ModelPolicy { Denied = ["gpt-*"] },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateModel("gpt-4o", null, EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Reason.Should().Contain("denied pattern");
    }

    [Fact]
    public void EvaluateModel_GlobPatternDoesNotMatch_Allows()
    {
        var spec = new PolicySpec
        {
            Models = new ModelPolicy { Denied = ["gpt-*"] },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateModel("claude-3-opus", null, EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void EvaluateModel_ContextWindowExceedsLimit_Denies()
    {
        var spec = new PolicySpec
        {
            Models = new ModelPolicy { MaxContextWindow = 32000 },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateModel("gpt-4o", 128000, EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Reason.Should().Contain("context window");
    }

    [Fact]
    public void EvaluateModel_ContextWindowWithinLimit_Allows()
    {
        var spec = new PolicySpec
        {
            Models = new ModelPolicy { MaxContextWindow = 128000 },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateModel("gpt-4o", 32000, EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void EvaluateModel_NullContextWindow_IgnoresLimit()
    {
        var spec = new PolicySpec
        {
            Models = new ModelPolicy { MaxContextWindow = 32000 },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateModel("gpt-4o", null, EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void GetResolvedPolicy_ReturnsConstructorSpec()
    {
        var spec = new PolicySpec
        {
            Budget = new BudgetPolicy { MaxDailyUsd = 5m },
        };
        var evaluator = new PolicyEvaluator(spec);

        evaluator.GetResolvedPolicy().Should().BeSameAs(spec);
    }

    [Fact]
    public void EvaluateModel_QuestionMarkGlobPattern_Matches()
    {
        var spec = new PolicySpec
        {
            Models = new ModelPolicy { Denied = ["gpt-?"] },
        };
        var evaluator = new PolicyEvaluator(spec);

        var result = evaluator.EvaluateModel("gpt-4", null, EmptyContext());

        result.Decision.Should().Be(PolicyDecision.Deny);
    }
}
