using FluentAssertions;
using JD.AI.Core.Governance;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class PolicyEvaluationSteps
{
    private readonly ScenarioContext _context;

    public PolicyEvaluationSteps(ScenarioContext context) => _context = context;

    [Given(@"a policy with no tool restrictions")]
    public void GivenAPolicyWithNoToolRestrictions()
    {
        var policy = new PolicySpec();
        _context.Set(new PolicyEvaluator(policy), "Evaluator");
    }

    [Given(@"a policy denying tools:")]
    public void GivenAPolicyDenyingTools(Table table)
    {
        var denied = table.Rows.Select(r => r["tool"]).ToList();
        var policy = new PolicySpec
        {
            Tools = new ToolPolicy { Denied = denied }
        };
        _context.Set(new PolicyEvaluator(policy), "Evaluator");
    }

    [Given(@"a policy allowing only tools:")]
    public void GivenAPolicyAllowingOnlyTools(Table table)
    {
        var allowed = table.Rows.Select(r => r["tool"]).ToList();
        var policy = new PolicySpec
        {
            Tools = new ToolPolicy { Allowed = allowed }
        };
        _context.Set(new PolicyEvaluator(policy), "Evaluator");
    }

    [Given(@"a policy denying providers:")]
    public void GivenAPolicyDenyingProviders(Table table)
    {
        var denied = table.Rows.Select(r => r["provider"]).ToList();
        var policy = new PolicySpec
        {
            Providers = new ProviderPolicy { Denied = denied }
        };
        _context.Set(new PolicyEvaluator(policy), "Evaluator");
    }

    [Given(@"a policy with no provider restrictions")]
    public void GivenAPolicyWithNoProviderRestrictions()
    {
        var policy = new PolicySpec();
        _context.Set(new PolicyEvaluator(policy), "Evaluator");
    }

    [Given(@"a policy denying models matching ""(.*)""")]
    public void GivenAPolicyDenyingModelsMatching(string pattern)
    {
        var policy = new PolicySpec
        {
            Models = new ModelPolicy { Denied = [pattern] }
        };
        _context.Set(new PolicyEvaluator(policy), "Evaluator");
    }

    [Given(@"a policy with max context window (\d+)")]
    public void GivenAPolicyWithMaxContextWindow(int maxContextWindow)
    {
        var policy = new PolicySpec
        {
            Models = new ModelPolicy { MaxContextWindow = maxContextWindow }
        };
        _context.Set(new PolicyEvaluator(policy), "Evaluator");
    }

    [When(@"I evaluate tool ""(.*)""")]
    public void WhenIEvaluateTool(string toolName)
    {
        var evaluator = _context.Get<PolicyEvaluator>("Evaluator");
        var ctx = new PolicyContext();
        var result = evaluator.EvaluateTool(toolName, ctx);
        _context.Set(result, "PolicyResult");
    }

    [When(@"I evaluate provider ""(.*)""")]
    public void WhenIEvaluateProvider(string providerName)
    {
        var evaluator = _context.Get<PolicyEvaluator>("Evaluator");
        var ctx = new PolicyContext();
        var result = evaluator.EvaluateProvider(providerName, ctx);
        _context.Set(result, "PolicyResult");
    }

    [When(@"I evaluate model ""(.*)""")]
    public void WhenIEvaluateModel(string modelId)
    {
        var evaluator = _context.Get<PolicyEvaluator>("Evaluator");
        var ctx = new PolicyContext();
        var result = evaluator.EvaluateModel(modelId, contextWindow: null, ctx);
        _context.Set(result, "PolicyResult");
    }

    [When(@"I evaluate model ""(.*)"" with context window (\d+)")]
    public void WhenIEvaluateModelWithContextWindow(string modelId, int contextWindow)
    {
        var evaluator = _context.Get<PolicyEvaluator>("Evaluator");
        var ctx = new PolicyContext();
        var result = evaluator.EvaluateModel(modelId, contextWindow, ctx);
        _context.Set(result, "PolicyResult");
    }

    [Then(@"the policy decision should be ""(.*)""")]
    public void ThenThePolicyDecisionShouldBe(string expected)
    {
        var result = _context.Get<PolicyEvaluationResult>("PolicyResult");
        result.Decision.ToString().Should().Be(expected);
    }

    [Then(@"the policy reason should contain ""(.*)""")]
    public void ThenThePolicyReasonShouldContain(string expected)
    {
        var result = _context.Get<PolicyEvaluationResult>("PolicyResult");
        result.Reason.Should().Contain(expected);
    }
}
