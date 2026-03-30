using JD.AI.Sandbox.Abstractions;

namespace JD.AI.Sandbox.Tests;

public class SandboxPolicyTests
{
    [Fact]
    public void DefaultPolicy_HasCorrectDefaults()
    {
        var policy = new SandboxPolicy { Name = "Test" };

        Assert.True(policy.AllowNetwork);
        Assert.True(policy.AllowRead);
        Assert.True(policy.AllowWrite);
        Assert.False(policy.AllowProcessSpawn);
        Assert.Null(policy.MaxCpuTimeMs);
        Assert.Null(policy.MaxMemoryBytes);
        Assert.Empty(policy.AllowedPaths);
        Assert.Empty(policy.DeniedPaths);
    }

    [Fact]
    public void Policy_WithLimits_SetsResourceLimits()
    {
        var policy = new SandboxPolicy
        {
            Name = "Limited",
            MaxCpuTimeMs = 5000,
            MaxMemoryBytes = 1_073_741_824,
        };

        Assert.Equal(5000, policy.MaxCpuTimeMs);
        Assert.Equal(1_073_741_824, policy.MaxMemoryBytes);
    }

    [Fact]
    public void Policy_AllowedPaths_AreStored()
    {
        var paths = new[] { "/tmp/sandbox", "/var/data" };
        var policy = new SandboxPolicy
        {
            Name = "PathRestricted",
            AllowedPaths = paths,
        };

        Assert.Equal(2, policy.AllowedPaths.Count);
        Assert.Contains("/tmp/sandbox", policy.AllowedPaths);
        Assert.Contains("/var/data", policy.AllowedPaths);
    }

    [Fact]
    public void Policy_DeniedPaths_AreStored()
    {
        var paths = new[] { "/etc/passwd", "/root" };
        var policy = new SandboxPolicy
        {
            Name = "Denied",
            DeniedPaths = paths,
        };

        Assert.Equal(2, policy.DeniedPaths.Count);
        Assert.Contains("/etc/passwd", policy.DeniedPaths, StringComparer.Ordinal);
    }

    [Fact]
    public void Policy_EnvironmentVariables_AreStored()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal) { ["PATH"] = "/bin", ["HOME"] = "/tmp" };
        var policy = new SandboxPolicy
        {
            Name = "EnvTest",
            EnvironmentVariables = env,
        };

        Assert.Equal(2, policy.EnvironmentVariables.Count);
        Assert.Equal("/bin", policy.EnvironmentVariables["PATH"]);
        Assert.Equal("/tmp", policy.EnvironmentVariables["HOME"]);
    }
}
