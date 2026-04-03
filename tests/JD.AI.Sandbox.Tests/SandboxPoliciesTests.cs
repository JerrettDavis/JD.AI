using JD.AI.Sandbox.Abstractions;
using JD.AI.Sandbox.Policies;

namespace JD.AI.Sandbox.Tests;

public sealed class SandboxPoliciesTests
{
    [Fact]
    public void Planner_ReturnsExpectedLockedDownFilesystemShape()
    {
        var policy = SandboxPolicies.Planner();

        Assert.Equal("Planner", policy.Name);
        Assert.True(policy.AllowNetwork);
        Assert.False(policy.AllowRead);
        Assert.False(policy.AllowWrite);
        Assert.False(policy.AllowProcessSpawn);
        Assert.Empty(policy.AllowedPaths);
    }

    [Fact]
    public void Executor_PreservesAllowedPathsAndDisablesNetwork()
    {
        var policy = SandboxPolicies.Executor("/safe/a", "/safe/b");

        Assert.Equal("Executor", policy.Name);
        Assert.False(policy.AllowNetwork);
        Assert.True(policy.AllowRead);
        Assert.True(policy.AllowWrite);
        Assert.Equal(["/safe/a", "/safe/b"], policy.AllowedPaths);
    }

    [Fact]
    public void ReadOnly_DisablesWritesButKeepsReadAccess()
    {
        var policy = SandboxPolicies.ReadOnly("/repo");

        Assert.Equal("ReadOnly", policy.Name);
        Assert.False(policy.AllowNetwork);
        Assert.True(policy.AllowRead);
        Assert.False(policy.AllowWrite);
        Assert.Equal(["/repo"], policy.AllowedPaths);
    }

    [Fact]
    public void WithLimits_ClonesPolicyAndOverridesOnlySpecifiedLimits()
    {
        var original = new SandboxPolicy
        {
            Name = "Base",
            AllowNetwork = false,
            AllowRead = true,
            AllowWrite = false,
            AllowedPaths = ["/allowed"],
            DeniedPaths = ["/denied"],
            AllowProcessSpawn = false,
            MaxCpuTimeMs = 10,
            MaxMemoryBytes = 20,
            EnvironmentVariables = new Dictionary<string, string?>(StringComparer.Ordinal) { ["MODE"] = "test" },
            WorkingDirectory = "/workspace"
        };

        var updated = original.WithLimits(maxMemoryBytes: 1234);

        Assert.NotSame(original, updated);
        Assert.Equal("Base", updated.Name);
        Assert.Equal(10, updated.MaxCpuTimeMs);
        Assert.Equal(1234, updated.MaxMemoryBytes);
        Assert.Equal(original.AllowedPaths, updated.AllowedPaths);
        Assert.Equal(original.DeniedPaths, updated.DeniedPaths);
        Assert.Equal("test", updated.EnvironmentVariables["MODE"]);
        Assert.Equal("/workspace", updated.WorkingDirectory);
    }
}
