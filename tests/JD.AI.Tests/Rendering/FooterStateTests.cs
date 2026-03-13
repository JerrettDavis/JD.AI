using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Rendering;

namespace JD.AI.Tests.Rendering;

public sealed class FooterStateTests
{
    private static FooterState CreateDefault(
        string workingDirectory = "/home/user/projects/myapp",
        string? gitBranch = "main",
        string? prLink = null,
        long contextTokensUsed = 12_000,
        long contextWindowSize = 200_000,
        string provider = "Anthropic",
        string model = "claude-sonnet-4-6",
        int turnCount = 5,
        PermissionMode mode = PermissionMode.Normal,
        double warnThresholdPercent = 15.0,
        IReadOnlyList<PluginSegment>? pluginSegments = null) =>
        new(
            workingDirectory,
            gitBranch,
            prLink,
            contextTokensUsed,
            contextWindowSize,
            provider,
            model,
            turnCount,
            mode,
            warnThresholdPercent,
            pluginSegments ?? []);

    [Fact]
    public void ToSegments_AlwaysIncludesRequiredKeys()
    {
        var state = CreateDefault();
        var segments = state.ToSegments();

        segments.Should().ContainKey("folder");
        segments.Should().ContainKey("context");
        segments.Should().ContainKey("provider");
        segments.Should().ContainKey("model");
        segments.Should().ContainKey("turns");
    }

    [Fact]
    public void ToSegments_BranchIsNull_WhenNotInGitRepo()
    {
        var state = CreateDefault(gitBranch: null);
        var segments = state.ToSegments();

        segments.Should().ContainKey("branch");
        segments["branch"].Should().BeNull();
    }

    [Fact]
    public void ToSegments_ContextFormat_IsHumanReadable()
    {
        // 12k used out of 200k
        var state = CreateDefault(contextTokensUsed: 12_000, contextWindowSize: 200_000);
        var segments = state.ToSegments();

        segments["context"].Should().Be("12.0k/200.0k");
    }

    [Fact]
    public void ToSegments_CompactWarning_AppearsWhenBelowThreshold()
    {
        // 180k/200k = 90% used = 10% remaining; warn threshold = 15% → warning should appear
        var state = CreateDefault(
            contextTokensUsed: 180_000,
            contextWindowSize: 200_000,
            warnThresholdPercent: 15.0);
        var segments = state.ToSegments();

        segments.Should().ContainKey("compact");
        segments["compact"].Should().NotBeNull();
    }

    [Fact]
    public void ToSegments_NoCompactWarning_WhenAboveThreshold()
    {
        // 100k/200k = 50% used = 50% remaining; warn threshold = 15% → no warning
        var state = CreateDefault(
            contextTokensUsed: 100_000,
            contextWindowSize: 200_000,
            warnThresholdPercent: 15.0);
        var segments = state.ToSegments();

        segments.Should().ContainKey("compact");
        segments["compact"].Should().BeNull();
    }

    [Fact]
    public void ToSegments_FolderPath_IsShortenedWithTilde()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var workingDir = Path.Combine(homeDir, "projects", "myapp");

        var state = CreateDefault(workingDirectory: workingDir);
        var segments = state.ToSegments();

        segments["folder"].Should().StartWith("~");
        segments["folder"].Should().NotContain(homeDir);
    }
}
