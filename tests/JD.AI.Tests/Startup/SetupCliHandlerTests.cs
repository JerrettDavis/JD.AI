using JD.AI.Core.Infrastructure;
using JD.AI.Startup;

namespace JD.AI.Tests.Startup;

[Collection("Console")]
public sealed class SetupCliHandlerTests : IDisposable
{
    private readonly Func<string[], Task<int>> _originalOnboarding = SetupCliHandler.RunOnboardingAsync;
    private readonly Func<string, CancellationToken, Task<bool>> _originalIsToolAvailable = SetupCliHandler.IsToolAvailableAsync;
    private readonly Func<string, string, TimeSpan, CancellationToken, Task<ProcessResult>> _originalRunProcess = SetupCliHandler.RunProcessAsync;

    public void Dispose()
    {
        SetupCliHandler.RunOnboardingAsync = _originalOnboarding;
        SetupCliHandler.IsToolAvailableAsync = _originalIsToolAvailable;
        SetupCliHandler.RunProcessAsync = _originalRunProcess;
    }

    [Fact]
    public async Task RunAsync_Help_ReturnsZero()
    {
        var code = await SetupCliHandler.RunAsync(["--help"]);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task RunAsync_WhenBothMajorStepsSkipped_ReturnsOne()
    {
        var code = await SetupCliHandler.RunAsync(["--skip-daemon", "--skip-onboard"]);
        Assert.Equal(1, code);
    }

    [Fact]
    public async Task RunAsync_InvalidBridgeAction_ReturnsOne()
    {
        var code = await SetupCliHandler.RunAsync(["--bridge", "invalid-mode"]);
        Assert.Equal(1, code);
    }

    [Fact]
    public async Task RunAsync_DaemonOnly_SkipsOnboarding_AndRunsDaemonSteps()
    {
        var onboardingCalls = 0;
        var commands = new List<(string File, string Args)>();

        SetupCliHandler.RunOnboardingAsync = _ =>
        {
            onboardingCalls++;
            return Task.FromResult(0);
        };
        SetupCliHandler.IsToolAvailableAsync = (tool, _) =>
            Task.FromResult(
                string.Equals(tool, "dotnet", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tool, "jdai-daemon", StringComparison.OrdinalIgnoreCase));
        SetupCliHandler.RunProcessAsync = (file, args, _, _) =>
        {
            commands.Add((file, args));
            return Task.FromResult(new ProcessResult(0, "ok", ""));
        };

        var code = await SetupCliHandler.RunAsync(["--daemon-only"]);

        Assert.Equal(0, code);
        Assert.Equal(0, onboardingCalls);
        Assert.Contains(commands, c => c is { File: "dotnet", Args: "tool update -g JD.AI.Daemon" });
        Assert.Contains(commands, c => c is { File: "jdai-daemon", Args: "install" });
        Assert.Contains(commands, c => c is { File: "jdai-daemon", Args: "start" });
        Assert.Contains(commands, c => c is { File: "jdai-daemon", Args: "status" });
        Assert.Contains(commands, c => c is { File: "jdai-daemon", Args: "bridge status" });
    }

    [Fact]
    public async Task RunAsync_OnboardingOnly_ForwardsOnboardingArgs()
    {
        string[]? receivedArgs = null;
        SetupCliHandler.RunOnboardingAsync = args =>
        {
            receivedArgs = args;
            return Task.FromResult(0);
        };
        SetupCliHandler.IsToolAvailableAsync = (_, _) => Task.FromResult(true);
        SetupCliHandler.RunProcessAsync = (_, _, _, _) => Task.FromResult(new ProcessResult(0, "ok", ""));

        var code = await SetupCliHandler.RunAsync(
            ["--skip-daemon", "--provider", "OpenAI Codex", "--model", "gpt-5.3-codex", "--skip-mcp"]);

        Assert.Equal(0, code);
        Assert.NotNull(receivedArgs);
        Assert.Contains("--provider", receivedArgs!);
        Assert.Contains("OpenAI Codex", receivedArgs!);
        Assert.Contains("--model", receivedArgs!);
        Assert.Contains("gpt-5.3-codex", receivedArgs!);
        Assert.Contains("--skip-mcp", receivedArgs!);
    }
}
