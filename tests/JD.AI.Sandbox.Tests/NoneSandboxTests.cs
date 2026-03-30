using JD.AI.Sandbox.Abstractions;

namespace JD.AI.Sandbox.Tests;

public class NoneSandboxTests
{
    [Fact]
    public void NoneSandbox_Platform_IsNone()
    {
        var sandbox = new NoneSandbox(new SandboxPolicy { Name = "Test" });
        Assert.Equal(SandboxPlatform.None, sandbox.Platform);
    }

    [Fact]
    public void NoneSandbox_Policy_IsStored()
    {
        var policy = new SandboxPolicy { Name = "MyPolicy", MaxCpuTimeMs = 1000 };
        var sandbox = new NoneSandbox(policy);
        Assert.Same(policy, sandbox.Policy);
    }

    [Fact]
    public async Task NoneSandbox_RunAsync_EchoCommand_ReturnsOutput()
    {
        var sandbox = new NoneSandbox(new SandboxPolicy { Name = "Test" });

        // Use cmd /c echo on Windows since echo is a shell builtin
        string exe, args;
        if (OperatingSystem.IsWindows())
        {
            exe = "cmd";
            args = "/c echo hello";
        }
        else
        {
            exe = "echo";
            args = "hello";
        }

        var result = await sandbox.RunAsync(exe, args);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.StandardOutput);
    }

    [Fact]
    public async Task NoneSandbox_RunAsync_NonexistentCommand_ReturnsFailure()
    {
        var sandbox = new NoneSandbox(new SandboxPolicy { Name = "Test" });

        var result = await sandbox.RunAsync("nonexistent-command-xyz");

        Assert.False(result.Success);
        Assert.NotEqual(0, result.ExitCode);
    }
}
