using FluentAssertions;
using JD.AI.Core.Sessions;

namespace JD.AI.Tests.Sessions;

public sealed class SessionRecordTests
{
    // ── SessionInfo ─────────────────────────────────────────────────────

    [Fact]
    public void SessionInfo_DefaultValues()
    {
        var info = new SessionInfo();
        info.Id.Should().NotBeNullOrWhiteSpace().And.HaveLength(16);
        info.Name.Should().BeNull();
        info.ProjectPath.Should().BeEmpty();
        info.ProjectHash.Should().BeEmpty();
        info.ModelId.Should().BeNull();
        info.ProviderName.Should().BeNull();
        info.TotalTokens.Should().Be(0);
        info.MessageCount.Should().Be(0);
        info.IsActive.Should().BeTrue();
        info.ModelSwitchHistory.Should().BeEmpty();
        info.ForkPoints.Should().BeEmpty();
        info.Turns.Should().BeEmpty();
    }

    [Fact]
    public void SessionInfo_MutableProperties()
    {
        var info = new SessionInfo();
        info.Name = "test session";
        info.TotalTokens = 5000;
        info.MessageCount = 10;
        info.IsActive = false;

        info.Name.Should().Be("test session");
        info.TotalTokens.Should().Be(5000);
        info.MessageCount.Should().Be(10);
        info.IsActive.Should().BeFalse();
    }

    // ── TurnRecord ──────────────────────────────────────────────────────

    [Fact]
    public void TurnRecord_DefaultValues()
    {
        var turn = new TurnRecord();
        turn.Id.Should().NotBeNullOrWhiteSpace().And.HaveLength(16);
        turn.SessionId.Should().BeEmpty();
        turn.Role.Should().BeEmpty();
        turn.Content.Should().BeNull();
        turn.ThinkingText.Should().BeNull();
        turn.TokensIn.Should().Be(0);
        turn.TokensOut.Should().Be(0);
        turn.DurationMs.Should().Be(0);
        turn.CumulativeContextTokens.Should().Be(0);
        turn.ContextWindowTokens.Should().Be(0);
        turn.ToolCalls.Should().BeEmpty();
        turn.FilesTouched.Should().BeEmpty();
    }

    [Fact]
    public void TurnRecord_ContextFieldsRoundTrip()
    {
        var turn = new TurnRecord
        {
            CumulativeContextTokens = 12_000,
            ContextWindowTokens = 128_000,
        };
        turn.CumulativeContextTokens.Should().Be(12_000);
        turn.ContextWindowTokens.Should().Be(128_000);
    }

    // ── ToolCallRecord ──────────────────────────────────────────────────

    [Fact]
    public void ToolCallRecord_DefaultValues()
    {
        var record = new ToolCallRecord();
        record.Id.Should().NotBeNullOrWhiteSpace();
        record.TurnId.Should().BeEmpty();
        record.ToolName.Should().BeEmpty();
        record.Arguments.Should().BeNull();
        record.Result.Should().BeNull();
        record.Status.Should().Be("ok");
        record.DurationMs.Should().Be(0);
    }

    // ── FileTouchRecord ─────────────────────────────────────────────────

    [Fact]
    public void FileTouchRecord_DefaultValues()
    {
        var record = new FileTouchRecord();
        record.Id.Should().NotBeNullOrWhiteSpace();
        record.TurnId.Should().BeEmpty();
        record.FilePath.Should().BeEmpty();
        record.Operation.Should().BeEmpty();
    }

    // ── ProjectHasher ───────────────────────────────────────────────────

    [Fact]
    public void ProjectHasher_ProducesDeterministicHash()
    {
        var hash1 = ProjectHasher.Hash("/home/user/project");
        var hash2 = ProjectHasher.Hash("/home/user/project");
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ProjectHasher_DifferentPaths_DifferentHashes()
    {
        var hash1 = ProjectHasher.Hash("/path/a");
        var hash2 = ProjectHasher.Hash("/path/b");
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ProjectHasher_Returns8CharLowercaseHex()
    {
        var hash = ProjectHasher.Hash("/some/path");
        hash.Should().HaveLength(8);
        hash.Should().MatchRegex("^[0-9a-f]{8}$");
    }
}
