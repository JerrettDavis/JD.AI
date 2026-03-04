using JD.AI.Workflows;

namespace JD.AI.Tests.Workflows;

public sealed class WorkflowGeneratorTests
{
    private readonly WorkflowGenerator _generator = new();

    [Fact]
    public void Generate_SimpleDescription_CreatesWorkflow()
    {
        var wf = _generator.Generate("Read the file, edit the contents, run the tests");

        Assert.NotNull(wf);
        Assert.Equal("1.0", wf.Version);
        Assert.True(wf.Steps.Count >= 2, $"Expected at least 2 steps, got {wf.Steps.Count}");
    }

    [Fact]
    public void Generate_WithName_UsesGivenName()
    {
        var wf = _generator.Generate("Build the project", "my-build");

        Assert.Equal("my-build", wf.Name);
    }

    [Fact]
    public void Generate_WithoutName_DerivesName()
    {
        var wf = _generator.Generate("Run tests and deploy");

        Assert.False(string.IsNullOrWhiteSpace(wf.Name));
    }

    [Fact]
    public void Generate_DetectsTestTag()
    {
        var wf = _generator.Generate("Run the test suite");

        Assert.Contains("testing", wf.Tags);
    }

    [Fact]
    public void Generate_DetectsCodeReviewTag()
    {
        var wf = _generator.Generate("Review the PR changes");

        Assert.Contains("code-review", wf.Tags);
    }

    [Fact]
    public void Generate_LoopPattern_CreatesLoopStep()
    {
        var wf = _generator.Generate("For each file in the directory, read the contents");

        var hasLoop = wf.Steps.Any(s => s.Kind == AgentStepKind.Loop);
        Assert.True(hasLoop, "Expected a loop step for 'for each' pattern");
    }

    [Fact]
    public void Generate_ConditionalPattern_CreatesConditionalStep()
    {
        var wf = _generator.Generate("Check if the build succeeded, then deploy");

        var hasConditional = wf.Steps.Any(s => s.Kind == AgentStepKind.Conditional);
        Assert.True(hasConditional, "Expected a conditional step for 'check if' pattern");
    }

    [Fact]
    public void Generate_MapsGitDiff_ToGitTool()
    {
        var wf = _generator.Generate("Compare the branches and diff the changes");

        var hasDiff = wf.Steps.Any(s => string.Equals(s.Target, "git-git_diff", StringComparison.Ordinal));
        Assert.True(hasDiff, "Expected git-git_diff tool for 'diff' description");
    }

    [Fact]
    public void Generate_TruncatesLongDescription()
    {
        var longDesc = new string('x', 300);
        var wf = _generator.Generate(longDesc);

        Assert.True(wf.Description.Length <= 204, "Description should be truncated"); // 200 + "..."
    }

    [Fact]
    public void DryRun_ValidWorkflow_ReturnsValid()
    {
        var wf = _generator.Generate("Read the file, search for patterns");
        var result = _generator.DryRun(wf);

        Assert.True(result.IsValid);
        Assert.Equal(wf.Name, result.WorkflowName);
        Assert.True(result.TotalSteps > 0);
        Assert.Empty(result.MissingTools);
    }

    [Fact]
    public void DryRun_WithMissingTool_ReportsInvalid()
    {
        var wf = new AgentWorkflowDefinition
        {
            Name = "test-wf",
            Steps =
            [
                AgentStepDefinition.InvokeTool("step1", "custom-nonexistent_tool"),
            ],
        };

        var availableTools = new HashSet<string>(StringComparer.Ordinal) { "file-read_file", "search-grep" };
        var result = _generator.DryRun(wf, availableTools);

        Assert.False(result.IsValid);
        Assert.Contains("custom-nonexistent_tool", result.MissingTools, StringComparer.Ordinal);
    }

    [Fact]
    public void DryRun_LoopWithoutCondition_WarnsUser()
    {
        var wf = new AgentWorkflowDefinition
        {
            Name = "loop-test",
            Steps =
            [
                AgentStepDefinition.LoopUntil("loop1", "", [
                    AgentStepDefinition.InvokeTool("body", "shell-run_command"),
                ]),
            ],
        };

        var result = _generator.DryRun(wf);

        Assert.True(result.Warnings.Count > 0, "Expected warnings about missing loop condition");
    }

    [Fact]
    public void DryRun_CountsNestedSteps()
    {
        var wf = new AgentWorkflowDefinition
        {
            Name = "nested-test",
            Steps =
            [
                AgentStepDefinition.If("check", "has_errors", [
                    AgentStepDefinition.InvokeTool("fix", "file-edit_file"),
                    AgentStepDefinition.InvokeTool("rerun", "shell-run_command"),
                ]),
                AgentStepDefinition.InvokeTool("final", "git-git_commit"),
            ],
        };

        var result = _generator.DryRun(wf);

        // 1 conditional + 2 sub-steps + 1 final = 4
        Assert.Equal(4, result.TotalSteps);
    }

    [Fact]
    public void FormatDryRun_ProducesReadableOutput()
    {
        var wf = _generator.Generate("Read the file, edit the code, run tests");
        var result = _generator.DryRun(wf);
        var formatted = WorkflowGenerator.FormatDryRun(result);

        Assert.Contains("📋 Dry Run:", formatted);
        Assert.Contains("Valid:", formatted);
        Assert.Contains("1.", formatted);
    }

    [Fact]
    public void FormatDryRun_ShowsMissingTools()
    {
        var result = new WorkflowDryRunResult
        {
            WorkflowName = "test",
            TotalSteps = 1,
            Steps = [new DryRunStep { Name = "s1", ToolOrTarget = "missing-tool", Description = "test" }],
            MissingTools = ["missing-tool"],
            IsValid = false,
        };

        var formatted = WorkflowGenerator.FormatDryRun(result);
        Assert.Contains("Missing tools", formatted);
        Assert.Contains("missing-tool", formatted);
    }

    [Fact]
    public void Compose_CombinesWorkflows()
    {
        var wf1 = _generator.Generate("Read the file", "read-step");
        var wf2 = _generator.Generate("Run the tests", "test-step");

        var composite = _generator.Compose("full-pipeline", [wf1, wf2]);

        Assert.Equal("full-pipeline", composite.Name);
        Assert.Equal(2, composite.Steps.Count);
        Assert.True(composite.Steps.All(s => s.Kind == AgentStepKind.Nested));
    }

    [Fact]
    public void Compose_MergesTags()
    {
        var wf1 = _generator.Generate("Run the tests", "test-wf");
        var wf2 = _generator.Generate("Deploy the release", "deploy-wf");

        var composite = _generator.Compose("ci-cd", [wf1, wf2]);

        Assert.Contains("testing", composite.Tags);
        Assert.Contains("deployment", composite.Tags);
    }

    [Fact]
    public void Compose_NestedStepsPreserveChildren()
    {
        var wf1 = new AgentWorkflowDefinition
        {
            Name = "inner",
            Steps =
            [
                AgentStepDefinition.InvokeTool("step-a", "file-read_file"),
                AgentStepDefinition.InvokeTool("step-b", "search-grep"),
            ],
        };

        var composite = _generator.Compose("outer", [wf1]);

        Assert.Single(composite.Steps);
        Assert.Equal(2, composite.Steps[0].SubSteps.Count);
    }
}
