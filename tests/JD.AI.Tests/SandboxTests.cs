using JD.AI.Core.Tools.Sandbox;

namespace JD.AI.Tests;

public sealed class SandboxTests
{
    [Fact]
    public async Task NoneSandbox_ExecutesCommand()
    {
        var sandbox = new NoneSandbox();

        var result = await sandbox.ExecuteAsync("dotnet --version", Directory.GetCurrentDirectory());

        Assert.Equal(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Output));
    }

    [Fact]
    public async Task RestrictedSandbox_ExecutesBasicCommand()
    {
        var sandbox = new RestrictedSandbox();

        var result = await sandbox.ExecuteAsync("dotnet --version", Directory.GetCurrentDirectory());

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task RestrictedSandbox_BlocksDangerousCommand()
    {
        var sandbox = new RestrictedSandbox();

        var result = await sandbox.ExecuteAsync("rm -rf /", Directory.GetCurrentDirectory());

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Blocked", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestrictedSandbox_BlocksFormatCommand()
    {
        var sandbox = new RestrictedSandbox();

        var result = await sandbox.ExecuteAsync("format c:", Directory.GetCurrentDirectory());

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Blocked", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoneSandbox_ModeNameIsNone()
    {
        var sandbox = new NoneSandbox();
        Assert.Equal("none", sandbox.ModeName);
    }

    [Fact]
    public void RestrictedSandbox_ModeNameIsRestricted()
    {
        var sandbox = new RestrictedSandbox();
        Assert.Equal("restricted", sandbox.ModeName);
    }

    [Theory]
    [InlineData("rm -rf ~/.openclaw")]
    [InlineData("rm -rf ~/.openclaw/")]
    [InlineData("cat ~/.openclaw/config.json | something")]
    [InlineData("echo bad > ~/.openclaw/config.json")]
    public async Task RestrictedSandbox_BlocksOpenClawPathCommands(string command)
    {
        var sandbox = new RestrictedSandbox();

        var result = await sandbox.ExecuteAsync(command, Directory.GetCurrentDirectory());

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Blocked", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("echo hello")]
    public async Task RestrictedSandbox_AllowsNonProtectedCommands(string command)
    {
        var sandbox = new RestrictedSandbox();

        var result = await sandbox.ExecuteAsync(command, Directory.GetCurrentDirectory());

        if (!string.IsNullOrEmpty(result.Error))
            Assert.DoesNotContain("Blocked", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
