using JD.AI.Commands;
using JD.AI.Core.Installation;

namespace JD.AI.Tests.Commands;

[Collection("Console")]
public sealed class UpdateCliHandlerTests : IDisposable
{
    private readonly Func<CancellationToken, Task<InstallationInfo>> _originalDetect =
        UpdateCliHandler.DetectInstallationAsync;

    private readonly Func<InstallationInfo, IInstallStrategy> _originalInstallFactory =
        UpdateCliHandler.InstallStrategyFactory;

    private readonly Func<InstallationInfo, IInstallStrategy> _originalUpdateFactory =
        UpdateCliHandler.UpdateStrategyFactory;

    public void Dispose()
    {
        UpdateCliHandler.DetectInstallationAsync = _originalDetect;
        UpdateCliHandler.UpdateStrategyFactory = _originalUpdateFactory;
        UpdateCliHandler.InstallStrategyFactory = _originalInstallFactory;
    }

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
        var strategy = CreateStrategy("1.1.0", true);
        ConfigureHandlers("1.0.0", strategy);

        var code = await UpdateCliHandler.RunAsync("update", ["--check"]);

        Assert.Equal(0, code);
        Assert.Equal(0, strategy.ApplyCalls);
    }

    [Fact]
    public async Task Update_WhenLatestVersionUnknown_ReturnsOneUnlessForced()
    {
        ConfigureHandlers(
            "1.0.0",
            CreateStrategy(null, true));

        var code = await UpdateCliHandler.RunAsync("update", []);

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Update_WhenAlreadyOnLatest_ReturnsZero()
    {
        ConfigureHandlers(
            "1.0.0",
            CreateStrategy("1.0.0", true));

        var code = await UpdateCliHandler.RunAsync("update", []);

        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Update_ApplyPath_ReturnsZeroOnSuccess_AndOneOnFailure()
    {
        var ok = CreateStrategy("1.2.0", true);
        ConfigureHandlers("1.0.0", ok);
        var successCode = await UpdateCliHandler.RunAsync("update", []);

        var fail = CreateStrategy("1.3.0", false);
        ConfigureHandlers("1.0.0", fail);
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
            "1.0.0",
            installStrategy: CreateStrategy(null, true));

        var code = await UpdateCliHandler.RunAsync("install", []);

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Install_WhenAlreadyLatest_ReturnsZeroWithoutApplying()
    {
        var strategy = CreateStrategy("1.0.0", true);
        ConfigureHandlers("1.0.0", installStrategy: strategy);

        var code = await UpdateCliHandler.RunAsync("install", []);

        Assert.Equal(0, code);
        Assert.Equal(0, strategy.ApplyCalls);
    }

    [Fact]
    public async Task Install_WithExplicitVersion_UsesApplyResult()
    {
        var success = CreateStrategy("9.9.9", true);
        ConfigureHandlers("1.0.0", installStrategy: success);
        var successCode = await UpdateCliHandler.RunAsync("install", ["2.0.0", "--force"]);

        var failure = CreateStrategy("9.9.9", false);
        ConfigureHandlers("1.0.0", installStrategy: failure);
        var failureCode = await UpdateCliHandler.RunAsync("install", ["2.0.0", "--force"]);

        Assert.Equal(0, successCode);
        Assert.Equal(1, failureCode);
        Assert.Equal("2.0.0", success.LastTargetVersion);
        Assert.Equal("2.0.0", failure.LastTargetVersion);
    }

    private static FakeInstallStrategy CreateStrategy(string? latest, bool applySuccess) =>
        new("fake",
            latest,
            new InstallResult(
                applySuccess,
                applySuccess ? "ok" : "failed",
                applySuccess));

    private static void ConfigureHandlers(
        string currentVersion,
        FakeInstallStrategy? updateStrategy = null,
        FakeInstallStrategy? installStrategy = null)
    {
        var info = new InstallationInfo(
            InstallKind.Unknown,
            "/tmp/jdai",
            currentVersion,
            "linux-x64");

        UpdateCliHandler.DetectInstallationAsync = _ => Task.FromResult(info);
        UpdateCliHandler.UpdateStrategyFactory = _ => updateStrategy ?? CreateStrategy("1.0.0", true);
        UpdateCliHandler.InstallStrategyFactory = _ => installStrategy ?? CreateStrategy("1.0.0", true);
    }

    private sealed class FakeInstallStrategy(
        string name,
        string? latestVersion,
        InstallResult applyResult) : IInstallStrategy
    {
        private readonly InstallResult _applyResult = applyResult;
        private readonly string? _latestVersion = latestVersion;

        public int ApplyCalls { get; private set; }

        public string? LastTargetVersion { get; private set; }

        public string Name { get; } = name;

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
