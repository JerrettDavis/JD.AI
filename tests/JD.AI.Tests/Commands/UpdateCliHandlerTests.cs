using JD.AI.Commands;
using JD.AI.Core.Installation;

namespace JD.AI.Tests.Commands;

public sealed class UpdateCliHandlerTests : IDisposable
{
    private readonly Func<CancellationToken, Task<InstallationInfo>> _originalDetect = UpdateCliHandler.DetectInstallationAsync;
    private readonly Func<InstallationInfo, IInstallStrategy> _originalUpdateFactory = UpdateCliHandler.UpdateStrategyFactory;
    private readonly Func<InstallationInfo, IInstallStrategy> _originalInstallFactory = UpdateCliHandler.InstallStrategyFactory;

    [Fact]
    public async Task RunAsync_WithUnknownSubcommand_ReturnsZero()
    {
        var code = await UpdateCliHandler.RunAsync("unknown", []);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task RunAsync_UpdateHelp_ReturnsZero()
    {
        var code = await UpdateCliHandler.RunAsync("update", ["--help"]);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task RunAsync_InstallHelp_ReturnsZero()
    {
        var code = await UpdateCliHandler.RunAsync("install", ["--help"]);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Update_CheckOnly_WithNewerVersion_ReturnsZeroWithoutApplying()
    {
        var strategy = CreateStrategy(latest: "1.1.0", applySuccess: true);
        ConfigureHandlers(currentVersion: "1.0.0", updateStrategy: strategy);

        var code = await UpdateCliHandler.RunAsync("update", ["--check"]);

        Assert.Equal(0, code);
        Assert.Equal(0, strategy.ApplyCalls);
    }

    [Fact]
    public async Task Update_WhenLatestVersionUnknown_ReturnsOneUnlessForced()
    {
        ConfigureHandlers(
            currentVersion: "1.0.0",
            updateStrategy: CreateStrategy(latest: null, applySuccess: true));

        var code = await UpdateCliHandler.RunAsync("update", []);

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Update_WhenAlreadyOnLatest_ReturnsZero()
    {
        ConfigureHandlers(
            currentVersion: "1.0.0",
            updateStrategy: CreateStrategy(latest: "1.0.0", applySuccess: true));

        var code = await UpdateCliHandler.RunAsync("update", []);

        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Update_ApplyPath_ReturnsZeroOnSuccess_AndOneOnFailure()
    {
        var ok = CreateStrategy(latest: "1.2.0", applySuccess: true);
        ConfigureHandlers(currentVersion: "1.0.0", updateStrategy: ok);
        var successCode = await UpdateCliHandler.RunAsync("update", []);

        var fail = CreateStrategy(latest: "1.3.0", applySuccess: false);
        ConfigureHandlers(currentVersion: "1.0.0", updateStrategy: fail);
        var failureCode = await UpdateCliHandler.RunAsync("update", []);

        Assert.Equal(0, successCode);
        Assert.Equal(1, failureCode);
        Assert.Equal(1, ok.ApplyCalls);
        Assert.Equal(1, fail.ApplyCalls);
    }

    [Fact]
    public async Task Install_WhenLatestCannotBeResolved_ReturnsOne()
    {
        ConfigureHandlers(
            currentVersion: "1.0.0",
            installStrategy: CreateStrategy(latest: null, applySuccess: true));

        var code = await UpdateCliHandler.RunAsync("install", []);

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Install_WhenAlreadyLatest_ReturnsZeroWithoutApplying()
    {
        var strategy = CreateStrategy(latest: "1.0.0", applySuccess: true);
        ConfigureHandlers(currentVersion: "1.0.0", installStrategy: strategy);

        var code = await UpdateCliHandler.RunAsync("install", []);

        Assert.Equal(0, code);
        Assert.Equal(0, strategy.ApplyCalls);
    }

    [Fact]
    public async Task Install_WithExplicitVersion_UsesApplyResult()
    {
        var success = CreateStrategy(latest: "9.9.9", applySuccess: true);
        ConfigureHandlers(currentVersion: "1.0.0", installStrategy: success);
        var successCode = await UpdateCliHandler.RunAsync("install", ["2.0.0", "--force"]);

        var failure = CreateStrategy(latest: "9.9.9", applySuccess: false);
        ConfigureHandlers(currentVersion: "1.0.0", installStrategy: failure);
        var failureCode = await UpdateCliHandler.RunAsync("install", ["2.0.0", "--force"]);

        Assert.Equal(0, successCode);
        Assert.Equal(1, failureCode);
        Assert.Equal("2.0.0", success.LastTargetVersion);
        Assert.Equal("2.0.0", failure.LastTargetVersion);
    }

    public void Dispose()
    {
        UpdateCliHandler.DetectInstallationAsync = _originalDetect;
        UpdateCliHandler.UpdateStrategyFactory = _originalUpdateFactory;
        UpdateCliHandler.InstallStrategyFactory = _originalInstallFactory;
    }

    private static FakeInstallStrategy CreateStrategy(string? latest, bool applySuccess) =>
        new("fake", latest, new InstallResult(
            Success: applySuccess,
            Output: applySuccess ? "ok" : "failed",
            RequiresRestart: applySuccess));

    private static void ConfigureHandlers(
        string currentVersion,
        FakeInstallStrategy? updateStrategy = null,
        FakeInstallStrategy? installStrategy = null)
    {
        var info = new InstallationInfo(
            InstallKind.Unknown,
            ExecutablePath: "/tmp/jdai",
            CurrentVersion: currentVersion,
            RuntimeId: "linux-x64");

        UpdateCliHandler.DetectInstallationAsync = _ => Task.FromResult(info);
        UpdateCliHandler.UpdateStrategyFactory = _ => updateStrategy ?? CreateStrategy("1.0.0", true);
        UpdateCliHandler.InstallStrategyFactory = _ => installStrategy ?? CreateStrategy("1.0.0", true);
    }

    private sealed class FakeInstallStrategy(
        string name,
        string? latestVersion,
        InstallResult applyResult) : IInstallStrategy
    {
        private readonly string? _latestVersion = latestVersion;
        private readonly InstallResult _applyResult = applyResult;

        public string Name { get; } = name;

        public int ApplyCalls { get; private set; }

        public string? LastTargetVersion { get; private set; }

        public Task<string?> GetLatestVersionAsync(CancellationToken ct = default) =>
            Task.FromResult(_latestVersion);

        public Task<InstallResult> ApplyAsync(string? targetVersion = null, CancellationToken ct = default)
        {
            ApplyCalls++;
            LastTargetVersion = targetVersion;
            return Task.FromResult(_applyResult);
        }
    }
}
