using JD.AI.Core.Governance;
using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public class PolicyToolsTests
{
    // ── policy_evaluate ──────────────────────────────────────

    [Fact]
    public void Evaluate_NoPolicyEvaluator_ReturnsNotConfigured()
    {
        var tools = new PolicyTools();
        var result = tools.EvaluatePolicy("tool", "read_file");
        Assert.Contains("No policy evaluator configured", result);
    }

    [Fact]
    public void Evaluate_WithEvaluator_ReturnsTool()
    {
        var spec = CreateDefaultSpec();
        var evaluator = new PolicyEvaluator(spec);
        var tools = new PolicyTools(evaluator);

        var result = tools.EvaluatePolicy("tool", "read_file");

        Assert.Contains("## Policy Evaluation: tool/read_file", result);
        Assert.Contains("**Decision**:", result);
    }

    [Fact]
    public void Evaluate_DeniedTool_ReturnsDeny()
    {
        var spec = CreateDefaultSpec();
        spec.Tools = new ToolPolicy { Denied = ["run_command"] };
        var evaluator = new PolicyEvaluator(spec);
        var tools = new PolicyTools(evaluator);

        var result = tools.EvaluatePolicy("tool", "run_command");

        Assert.Contains("Deny", result);
    }

    [Fact]
    public void Evaluate_AllowedTool_ReturnsAllow()
    {
        var spec = CreateDefaultSpec();
        var evaluator = new PolicyEvaluator(spec);
        var tools = new PolicyTools(evaluator);

        var result = tools.EvaluatePolicy("tool", "read_file");

        Assert.Contains("Allow", result);
    }

    [Fact]
    public void Evaluate_Provider_ReturnsResult()
    {
        var spec = CreateDefaultSpec();
        var evaluator = new PolicyEvaluator(spec);
        var tools = new PolicyTools(evaluator);

        var result = tools.EvaluatePolicy("provider", "ollama");

        Assert.Contains("## Policy Evaluation: provider/ollama", result);
    }

    [Fact]
    public void Evaluate_Model_ReturnsResult()
    {
        var spec = CreateDefaultSpec();
        var evaluator = new PolicyEvaluator(spec);
        var tools = new PolicyTools(evaluator);

        var result = tools.EvaluatePolicy("model", "gpt-4o");

        Assert.Contains("## Policy Evaluation: model/gpt-4o", result);
    }

    // ── policy_list ──────────────────────────────────────────

    [Fact]
    public void List_NoPolicyEvaluator_ReturnsNotConfigured()
    {
        var tools = new PolicyTools();
        var result = tools.ListPolicies();
        Assert.Contains("No policy evaluator configured", result);
    }

    [Fact]
    public void List_WithPolicies_ShowsToolPolicy()
    {
        var spec = CreateDefaultSpec();
        spec.Tools = new ToolPolicy
        {
            Allowed = ["read_file", "write_file"],
            Denied = ["run_command"]
        };
        var evaluator = new PolicyEvaluator(spec);
        var tools = new PolicyTools(evaluator);

        var result = tools.ListPolicies();

        Assert.Contains("### Tool Policy", result);
        Assert.Contains("`read_file`", result);
        Assert.Contains("`run_command`", result);
    }

    [Fact]
    public void List_WithBudget_ShowsBudgetPolicy()
    {
        var spec = CreateDefaultSpec();
        spec.Budget = new BudgetPolicy { MaxDailyUsd = 10.0m, MaxSessionUsd = 2.0m };
        var evaluator = new PolicyEvaluator(spec);
        var tools = new PolicyTools(evaluator);

        var result = tools.ListPolicies();

        Assert.Contains("### Budget Policy", result);
        Assert.Contains("$10.00", result);
        Assert.Contains("$2.00", result);
    }

    // ── rbac_check ───────────────────────────────────────────

    [Fact]
    public void Rbac_NoPolicyEvaluator_ReturnsAllAvailable()
    {
        var tools = new PolicyTools();
        var result = tools.CheckRbac();

        Assert.Contains("all capabilities are available", result);
    }

    [Fact]
    public void Rbac_WithWhitelist_ShowsRestricted()
    {
        var spec = CreateDefaultSpec();
        spec.Tools = new ToolPolicy { Allowed = ["read_file", "grep"] };
        var evaluator = new PolicyEvaluator(spec);
        var tools = new PolicyTools(evaluator);

        var result = tools.CheckRbac();

        Assert.Contains("Whitelist mode", result);
        Assert.Contains("2 tools allowed", result);
    }

    [Fact]
    public void Rbac_WithBlacklist_ShowsBlocked()
    {
        var spec = CreateDefaultSpec();
        spec.Tools = new ToolPolicy { Denied = ["run_command", "web_search"] };
        var evaluator = new PolicyEvaluator(spec);
        var tools = new PolicyTools(evaluator);

        var result = tools.CheckRbac();

        Assert.Contains("Blacklist mode", result);
        Assert.Contains("2 tools blocked", result);
    }

    // ── policy_validate ──────────────────────────────────────

    [Fact]
    public void Validate_EmptyContent_ReturnsError()
    {
        var result = PolicyTools.ValidatePolicy("");
        Assert.Contains("Policy content is empty", result);
    }

    [Fact]
    public void Validate_ValidPolicy_ReturnsValid()
    {
        var yaml = """
            apiVersion: jdai/v1
            kind: JdaiPolicy
            metadata:
              name: test-policy
              scope: project
            spec:
              tools:
                denied:
                  - run_command
            """;

        var result = PolicyTools.ValidatePolicy(yaml);

        Assert.Contains("Valid", result);
        Assert.Contains("0 error(s)", result);
    }

    [Fact]
    public void Validate_MissingFields_ReturnsErrors()
    {
        var yaml = "spec:\n  tools:\n    denied: [run_command]";

        var result = PolicyTools.ValidatePolicy(yaml);

        Assert.Contains("Missing required field: `apiVersion`", result);
        Assert.Contains("Missing required field: `kind`", result);
    }

    [Fact]
    public void Validate_HighBudget_ReturnsWarning()
    {
        var yaml = """
            apiVersion: jdai/v1
            kind: JdaiPolicy
            metadata:
              name: test
              scope: project
            spec:
              budget:
                maxDailyUsd: 5000
            """;

        var result = PolicyTools.ValidatePolicy(yaml);

        Assert.Contains("unusually high", result);
    }

    // ── policy_export ────────────────────────────────────────

    [Fact]
    public void Export_NoPolicyEvaluator_ReturnsNotConfigured()
    {
        var tools = new PolicyTools();
        var result = tools.ExportPolicy();
        Assert.Contains("No policy evaluator configured", result);
    }

    [Fact]
    public void Export_WithEvaluator_ReturnsJson()
    {
        var spec = CreateDefaultSpec();
        spec.Tools = new ToolPolicy { Denied = ["run_command"] };
        var evaluator = new PolicyEvaluator(spec);
        var tools = new PolicyTools(evaluator);

        var result = tools.ExportPolicy();

        // Should be valid JSON
        Assert.Contains("run_command", result);
    }

    // ── audit_query ──────────────────────────────────────────

    [Fact]
    public async Task AuditQuery_NoService_ReturnsNotConfigured()
    {
        var tools = new PolicyTools();
        var result = await tools.QueryAudit();
        Assert.Contains("No audit service configured", result);
    }

    // ── Helpers ──────────────────────────────────────────────

    private static PolicySpec CreateDefaultSpec()
    {
        return new PolicySpec();
    }
}
