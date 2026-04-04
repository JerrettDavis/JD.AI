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

    private readonly Func<CancellationToken, Task<IReadOnlyList<InstalledTool>>> _originalGetInstalledToolsAsync =
        UpdateCliHandler.GetInstalledToolsAsync;

    private readonly Func<IReadOnlyList<InstalledTool>?, CancellationToken, Task<UpdatePlan>> _originalCheckAllAsync =
        UpdateCliHandler.CheckAllAsync;

    private readonly Func<string, CancellationToken, Task<string?>> _originalGetLatestVersionAsync =
        UpdateCliHandler.GetLatestVersionAsync;

    private readonly Func<string, CancellationToken, Task<string?>> _originalGetInstalledToolVersionAsync =
        UpdateCliHandler.GetInstalledToolVersionAsync;

    private readonly Func<string, string?, CancellationToken, Task<InstallResult>> _originalApplyToolUpdateAsync =
        UpdateCliHandler.ApplyToolUpdateAsync;

    private readonly Func<UpdatePlan, bool, Action<InstalledTool, InstallResult>?, CancellationToken, Task>
        _originalApplyAllToolUpdatesAsync = UpdateCliHandler.ApplyAllToolUpdatesAsync;

    public void Dispose()
    {
        UpdateCliHandler.DetectInstallationAsync = _originalDetect;
        UpdateCliHandler.UpdateStrategyFactory = _originalUpdateFactory;
        UpdateCliHandler.InstallStrategyFactory = _originalInstallFactory;
        UpdateCliHandler.GetInstalledToolsAsync = _originalGetInstalledToolsAsync;
        UpdateCliHandler.CheckAllAsync = _originalCheckAllAsync;
        UpdateCliHandler.GetLatestVersionAsync = _originalGetLatestVersionAsync;
        UpdateCliHandler.GetInstalledToolVersionAsync = _originalGetInstalledToolVersionAsync;
        UpdateCliHandler.ApplyToolUpdateAsync = _originalApplyToolUpdateAsync;
        UpdateCliHandler.ApplyAllToolUpdatesAsync = _originalApplyAllToolUpdatesAsync;
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
        var tool = new InstalledTool("JD.AI", "jdai", "1.0.0", InstallKind.Unknown);
        var toolUpdate = new ToolUpdate(tool, "1.1.0", IsNewer: true);
        var plan = new UpdatePlan([tool], [toolUpdate], HasUpdates: true);

        ConfigureHandlers("1.0.0", fakeTools: [tool], fakeUpdatePlan: plan);

        var code = await UpdateCliHandler.RunAsync("update", ["--check"]);

        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Update_CheckOnly_WhenNoToolsInstalled_FallsBackToSelfCheck()
    {
        var strategy = CreateStrategy("1.2.0", true);
        ConfigureHandlers("1.0.0", strategy, fakeTools: []);

        var code = await UpdateCliHandler.RunAsync("update", ["--check"]);

        Assert.Equal(0, code);
        Assert.Equal(0, strategy.ApplyCalls);
    }

    [Fact]
    public async Task Update_SelfCheck_UsesSelfUpdatePath()
    {
        var strategy = CreateStrategy("1.2.0", true);
        ConfigureHandlers("1.0.0", strategy, fakeTools: []);

        var code = await UpdateCliHandler.RunAsync("update", ["--self", "--check"]);

        Assert.Equal(0, code);
        Assert.Equal(0, strategy.ApplyCalls);
    }

    [Fact]
    public async Task Update_WhenTargetsConflict_ReturnsOne()
    {
        ConfigureHandlers("1.0.0");

        var code = await UpdateCliHandler.RunAsync("update", ["--self", "--all"]);

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Update_WhenCheckAndForceAreCombined_ReturnsOne()
    {
        ConfigureHandlers("1.0.0");

        var code = await UpdateCliHandler.RunAsync("update", ["jdai-daemon", "--check", "--force"]);

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Update_WhenUnknownFlagIsProvided_ReturnsOne()
    {
        ConfigureHandlers("1.0.0");

        var code = await UpdateCliHandler.RunAsync("update", ["--bogus"]);

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Update_WhenMultipleToolNamesAreProvided_ReturnsOne()
    {
        ConfigureHandlers("1.0.0");

        var code = await UpdateCliHandler.RunAsync("update", ["jdai", "jdai-daemon"]);

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Update_NamedToolCheck_WhenToolIsNotInstalled_ReturnsOne()
    {
        ConfigureHandlers("1.0.0");
        UpdateCliHandler.GetInstalledToolVersionAsync = (_, _) => Task.FromResult<string?>(null);

        var code = await UpdateCliHandler.RunAsync("update", ["jdai-daemon", "--check"]);

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Update_NamedToolApply_WhenToolIsNotInstalled_ReturnsOne()
    {
        ConfigureHandlers("1.0.0");
        UpdateCliHandler.GetInstalledToolVersionAsync = (_, _) => Task.FromResult<string?>(null);

        var code = await UpdateCliHandler.RunAsync("update", ["jdai-daemon"]);

        Assert.Equal(1, code);
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
    public async Task Update_WhenNoOtherToolsInstalled_FallsBackToSelfUpdate()
    {
        var strategy = CreateStrategy("1.2.0", true);
        ConfigureHandlers("1.0.0", strategy, fakeTools: []);

        var code = await UpdateCliHandler.RunAsync("update", []);

        Assert.Equal(0, code);
        Assert.Equal(1, strategy.ApplyCalls);
        Assert.Equal("1.2.0", strategy.LastTargetVersion);
    }

    [Fact]
    public async Task Update_BulkForce_ReturnsOne()
    {
        ConfigureHandlers("1.0.0", fakeTools:
        [
            new InstalledTool("JD.AI.Daemon", "jdai-daemon", "1.0.0", InstallKind.Unknown),
        ]);

        var code = await UpdateCliHandler.RunAsync("update", ["--all", "--force"]);

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Update_All_WhenNoToolsInstalled_ReturnsOne()
    {
        ConfigureHandlers("1.0.0", fakeTools: []);

        var code = await UpdateCliHandler.RunAsync("update", ["--all"]);

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Update_All_WhenNoUpdatesAreAvailable_ReturnsZero()
    {
        var tool = new InstalledTool("JD.AI", "jdai", "1.0.0", InstallKind.Unknown);
        var plan = new UpdatePlan([tool], [], HasUpdates: false)
        {
            Results = [new ToolUpdate(tool, "1.0.0", IsNewer: false)],
        };

        ConfigureHandlers("1.0.0", fakeTools: [tool], fakeUpdatePlan: plan);

        var code = await UpdateCliHandler.RunAsync("update", ["--all"]);

        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Update_All_WhenApplyAllSucceeds_ReturnsZero()
    {
        var tool = new InstalledTool("JD.AI.Daemon", "jdai-daemon", "1.0.0", InstallKind.Unknown);
        var plan = new UpdatePlan([tool], [new ToolUpdate(tool, "1.1.0", IsNewer: true)], HasUpdates: true);
        var updatedTool = string.Empty;

        ConfigureHandlers("1.0.0", fakeTools: [tool], fakeUpdatePlan: plan);
        UpdateCliHandler.ApplyAllToolUpdatesAsync = (updatePlan, continueOnError, onToolUpdated, _) =>
        {
            Assert.True(continueOnError);
            Assert.Same(plan, updatePlan);
            var result = new InstallResult(true, "updated", RequiresRestart: true);
            onToolUpdated?.Invoke(tool, result);
            updatedTool = tool.PackageId;
            return Task.CompletedTask;
        };

        var code = await UpdateCliHandler.RunAsync("update", ["--all"]);

        Assert.Equal(0, code);
        Assert.Equal("JD.AI.Daemon", updatedTool);
    }

    [Fact]
    public async Task Update_All_WhenApplyAllReportsFailure_ReturnsOne()
    {
        var tool = new InstalledTool("JD.AI.Daemon", "jdai-daemon", "1.0.0", InstallKind.Unknown);
        var plan = new UpdatePlan([tool], [new ToolUpdate(tool, "1.1.0", IsNewer: true)], HasUpdates: true);

        ConfigureHandlers("1.0.0", fakeTools: [tool], fakeUpdatePlan: plan);
        UpdateCliHandler.ApplyAllToolUpdatesAsync = (_, _, onToolUpdated, _) =>
        {
            onToolUpdated?.Invoke(tool, new InstallResult(false, "boom"));
            return Task.CompletedTask;
        };

        var code = await UpdateCliHandler.RunAsync("update", ["--all"]);

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Update_CheckOnly_WhenToolVersionLookupFails_ReturnsOne()
    {
        var tool = new InstalledTool("JD.AI.Daemon", "jdai-daemon", "1.0.0", InstallKind.Unknown);
        var plan = new UpdatePlan([tool], [], HasUpdates: false)
        {
            Results =
            [
                new ToolUpdate(tool, null, IsNewer: false),
            ],
        };

        ConfigureHandlers("1.0.0", fakeTools: [tool], fakeUpdatePlan: plan);

        var code = await UpdateCliHandler.RunAsync("update", ["--check"]);

        Assert.Equal(1, code);
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
    public async Task RunAsync_WhenOperationIsCancelled_Returns130()
    {
        UpdateCliHandler.DetectInstallationAsync = _ => Task.FromException<InstallationInfo>(new OperationCanceledException());

        var code = await UpdateCliHandler.RunAsync("update", ["--self"]);

        Assert.Equal(130, code);
    }

    [Fact]
    public async Task Update_SelfForce_WhenLatestVersionUnknown_AppliesWithoutTargetVersion()
    {
        var strategy = CreateStrategy(null, true);
        ConfigureHandlers("1.0.0", strategy);

        var code = await UpdateCliHandler.RunAsync("update", ["--self", "--force"]);

        Assert.Equal(0, code);
        Assert.Equal(1, strategy.ApplyCalls);
        Assert.Null(strategy.LastTargetVersion);
    }

    [Fact]
    public async Task Update_SelfApply_WhenDetachedUpdaterLaunches_ReturnsZero()
    {
        var strategy = new FakeInstallStrategy(
            "fake",
            "1.2.0",
            new InstallResult(true, "launched", RequiresRestart: true, LaunchedDetached: true));
        ConfigureHandlers("1.0.0", strategy);

        var code = await UpdateCliHandler.RunAsync("update", ["--self"]);

        Assert.Equal(0, code);
        Assert.Equal(1, strategy.ApplyCalls);
        Assert.Equal("1.2.0", strategy.LastTargetVersion);
    }

    [Fact]
    public async Task Update_NamedToolCheck_WhenUpdateExists_ReturnsZeroWithoutApplying()
    {
        var tool = new InstalledTool("JD.AI.Gateway", "jdai-gateway", "1.0.0", InstallKind.Unknown);
        ConfigureHandlers("1.0.0", fakeTools: [tool]);
        UpdateCliHandler.GetLatestVersionAsync = (packageId, _) =>
        {
            Assert.Equal("JD.AI.Gateway", packageId);
            return Task.FromResult<string?>("1.1.0");
        };

        var code = await UpdateCliHandler.RunAsync("update", ["jdai-gateway", "--check"]);

        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Update_NamedTool_WhenLatestVersionUnknown_ReturnsOne()
    {
        var tool = new InstalledTool("Custom.Package", "custom-tool", "1.0.0", InstallKind.Unknown);
        ConfigureHandlers("1.0.0", fakeTools: [tool]);
        UpdateCliHandler.GetLatestVersionAsync = (packageId, _) =>
        {
            Assert.Equal("Custom.Package", packageId);
            return Task.FromResult<string?>(null);
        };

        var code = await UpdateCliHandler.RunAsync("update", ["Custom.Package"]);

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Update_NamedTool_WhenAlreadyLatestAndNotForced_ReturnsZero()
    {
        var tool = new InstalledTool("JD.AI.TUI", "jdai-tui", "1.0.0", InstallKind.Unknown);
        ConfigureHandlers("1.0.0", fakeTools: [tool]);
        UpdateCliHandler.GetLatestVersionAsync = (packageId, _) =>
        {
            Assert.Equal("JD.AI.TUI", packageId);
            return Task.FromResult<string?>("1.0.0");
        };

        var code = await UpdateCliHandler.RunAsync("update", ["jdai-tui"]);

        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Update_NamedTool_WhenApplySucceeds_ReturnsZero()
    {
        var tool = new InstalledTool("JD.AI.Daemon", "jdai-daemon", "1.0.0", InstallKind.Unknown);
        string? appliedPackage = null;
        string? appliedVersion = null;

        ConfigureHandlers("1.0.0", fakeTools: [tool]);
        UpdateCliHandler.GetLatestVersionAsync = (_, _) => Task.FromResult<string?>("1.1.0");
        UpdateCliHandler.ApplyToolUpdateAsync = (packageId, targetVersion, _) =>
        {
            appliedPackage = packageId;
            appliedVersion = targetVersion;
            return Task.FromResult(new InstallResult(true, "ok", RequiresRestart: true));
        };

        var code = await UpdateCliHandler.RunAsync("update", ["jdai-daemon"]);

        Assert.Equal(0, code);
        Assert.Equal("JD.AI.Daemon", appliedPackage);
        Assert.Equal("1.1.0", appliedVersion);
    }

    [Fact]
    public async Task Update_NamedTool_WhenApplyFails_ReturnsOne()
    {
        var tool = new InstalledTool("JD.AI.Daemon", "jdai-daemon", "1.0.0", InstallKind.Unknown);

        ConfigureHandlers("1.0.0", fakeTools: [tool]);
        UpdateCliHandler.GetLatestVersionAsync = (_, _) => Task.FromResult<string?>("1.1.0");
        UpdateCliHandler.ApplyToolUpdateAsync = (_, _, _) =>
            Task.FromResult(new InstallResult(false, "fail"));

        var code = await UpdateCliHandler.RunAsync("update", ["jdai-daemon"]);

        Assert.Equal(1, code);
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

    [Fact]
    public async Task Install_ForceWithoutVersion_AppliesLatestTargetPlaceholder()
    {
        var strategy = CreateStrategy("9.9.9", true);
        ConfigureHandlers("1.0.0", installStrategy: strategy);

        var code = await UpdateCliHandler.RunAsync("install", ["--force"]);

        Assert.Equal(0, code);
        Assert.Null(strategy.LastTargetVersion);
        Assert.Equal(1, strategy.ApplyCalls);
    }

    [Fact]
    public async Task Install_WhenUnknownFlagIsProvided_ReturnsOne()
    {
        ConfigureHandlers("1.0.0");

        var code = await UpdateCliHandler.RunAsync("install", ["--bogus"]);

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Install_WhenMultipleVersionsAreProvided_ReturnsOne()
    {
        ConfigureHandlers("1.0.0");

        var code = await UpdateCliHandler.RunAsync("install", ["1.2.3", "extra"]);

        Assert.Equal(1, code);
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
        FakeInstallStrategy? installStrategy = null,
        IReadOnlyList<InstalledTool>? fakeTools = null,
        UpdatePlan? fakeUpdatePlan = null)
    {
        var info = new InstallationInfo(
            InstallKind.Unknown,
            "/tmp/jdai",
            currentVersion,
            "linux-x64");

        UpdateCliHandler.DetectInstallationAsync = _ => Task.FromResult(info);
        UpdateCliHandler.UpdateStrategyFactory = _ => updateStrategy ?? CreateStrategy("1.0.0", true);
        UpdateCliHandler.InstallStrategyFactory = _ => installStrategy ?? CreateStrategy("1.0.0", true);
        UpdateCliHandler.GetInstalledToolsAsync = _ => Task.FromResult<IReadOnlyList<InstalledTool>>(
            fakeTools ?? []);
        UpdateCliHandler.CheckAllAsync = (_, _) => Task.FromResult(
            fakeUpdatePlan ?? new UpdatePlan([], [], HasUpdates: false));
        UpdateCliHandler.GetInstalledToolVersionAsync = (packageId, _) =>
            Task.FromResult(
                fakeTools?.FirstOrDefault(t => string.Equals(t.PackageId, packageId, StringComparison.Ordinal))?.CurrentVersion);
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
