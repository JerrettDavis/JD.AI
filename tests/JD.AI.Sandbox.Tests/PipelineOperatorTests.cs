using JD.AI.Sandbox.Abstractions;
using JD.AI.Sandbox.Pipeline;

namespace JD.AI.Sandbox.Tests;

public class PipelineOperatorTests
{
    [Fact]
    public void Plan_CreatesTwoStages()
    {
        var stages = SandboxPipeline.Plan(
            plannerPolicy: new SandboxPolicy { Name = "Planner" },
            executorPolicy: new SandboxPolicy { Name = "Executor" },
            plannerExe: "/bin/echo",
            executorExe: "/bin/cat");

        Assert.Equal(2, stages.Length);
        Assert.Equal("planner", stages[0].Name);
        Assert.Equal("executor", stages[1].Name);
        Assert.Equal("/bin/echo", stages[0].ExecutablePath);
        Assert.Equal("/bin/cat", stages[1].ExecutablePath);
    }

    [Fact]
    public void PlannerPolicy_HasCorrectDefaults()
    {
        var policy = SandboxPipeline.PlannerPolicy();

        Assert.Equal("PlannerPolicy", policy.Name);
        Assert.True(policy.AllowNetwork);
        Assert.False(policy.AllowRead);
        Assert.False(policy.AllowWrite);
        Assert.False(policy.AllowProcessSpawn);
    }

    [Fact]
    public void ExecutorPolicy_HasCorrectDefaults()
    {
        var policy = SandboxPipeline.ExecutorPolicy("/data");

        Assert.Equal("ExecutorPolicy", policy.Name);
        Assert.False(policy.AllowNetwork);
        Assert.True(policy.AllowRead);
        Assert.True(policy.AllowWrite);
        Assert.False(policy.AllowProcessSpawn);
        Assert.Single(policy.AllowedPaths);
        Assert.Equal("/data", policy.AllowedPaths[0]);
    }

    [Fact]
    public void ExecutorPolicy_WithNoPaths_AllowsNothing()
    {
        var policy = SandboxPipeline.ExecutorPolicy();

        Assert.Empty(policy.AllowedPaths);
        Assert.False(policy.AllowNetwork);
        Assert.True(policy.AllowRead);
        Assert.True(policy.AllowWrite);
    }
}
