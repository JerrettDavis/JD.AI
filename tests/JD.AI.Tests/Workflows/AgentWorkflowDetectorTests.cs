using FluentAssertions;
using JD.AI.Workflows;

namespace JD.AI.Tests.Workflows;

public sealed class AgentWorkflowDetectorTests
{
    private readonly AgentWorkflowDetector _detector = new();

    // ── Null / empty / whitespace guards ──────────────────────────────────

    [Fact]
    public void NullMessage_ReturnsFalse() =>
        _detector.IsWorkflowRequired(new AgentRequest(null!)).Should().BeFalse();

    [Fact]
    public void EmptyMessage_ReturnsFalse() =>
        _detector.IsWorkflowRequired(new AgentRequest("")).Should().BeFalse();

    [Fact]
    public void WhitespaceMessage_ReturnsFalse() =>
        _detector.IsWorkflowRequired(new AgentRequest("   \t  \n  ")).Should().BeFalse();

    // ── Minimum length boundary ───────────────────────────────────────────

    [Fact]
    public void MessageWith29Chars_ContainingKeyword_ReturnsFalse()
    {
        // 29 chars: "implement this very short msg" (28) + pad
        var msg = "implement " + new string('x', 19); // 10 + 19 = 29
        msg.Length.Should().Be(29);
        _detector.IsWorkflowRequired(new AgentRequest(msg)).Should().BeFalse();
    }

    [Fact]
    public void MessageWith30Chars_ContainingKeyword_ReturnsTrue()
    {
        var msg = "implement " + new string('x', 20); // 10 + 20 = 30
        msg.Length.Should().Be(30);
        _detector.IsWorkflowRequired(new AgentRequest(msg)).Should().BeTrue();
    }

    [Fact]
    public void MessageWith31Chars_ContainingKeyword_ReturnsTrue()
    {
        var msg = "implement " + new string('x', 21); // 10 + 21 = 31
        msg.Length.Should().Be(31);
        _detector.IsWorkflowRequired(new AgentRequest(msg)).Should().BeTrue();
    }

    // ── All 18 keywords individually ──────────────────────────────────────

    [Theory]
    [InlineData("implement")]
    [InlineData("create")]
    [InlineData("scaffold")]
    [InlineData("build")]
    [InlineData("review")]
    [InlineData("test")]
    [InlineData("deploy")]
    [InlineData("generate")]
    [InlineData("design")]
    [InlineData("architect")]
    [InlineData("plan")]
    [InlineData("develop")]
    [InlineData("refactor")]
    [InlineData("migrate")]
    [InlineData("integrate")]
    [InlineData("setup")]
    [InlineData("initialize")]
    [InlineData("bootstrap")]
    public void EachKeyword_IsRecognized(string keyword)
    {
        var msg = $"Please {keyword} this feature for the application system";
        msg.Length.Should().BeGreaterThanOrEqualTo(30);
        _detector.IsWorkflowRequired(new AgentRequest(msg)).Should().BeTrue();
    }

    // ── Case insensitivity ────────────────────────────────────────────────

    [Theory]
    [InlineData("IMPLEMENT a user login feature with JWT tokens")]
    [InlineData("Create a REST API for the inventory system")]
    [InlineData("SCAFFOLD a new microservice for notifications")]
    [InlineData("Deploy the application to staging environment")]
    public void Keywords_AreCaseInsensitive(string message) =>
        _detector.IsWorkflowRequired(new AgentRequest(message)).Should().BeTrue();

    // ── No keyword in long message ────────────────────────────────────────

    [Fact]
    public void LongMessage_WithoutKeyword_ReturnsFalse()
    {
        var msg = "What is the weather like today in the city center and surrounding areas";
        msg.Length.Should().BeGreaterThan(30);
        _detector.IsWorkflowRequired(new AgentRequest(msg)).Should().BeFalse();
    }

    // ── Keyword at different positions ────────────────────────────────────

    [Fact]
    public void KeywordAtStart_IsDetected()
    {
        var msg = "implement the authentication module for the app";
        _detector.IsWorkflowRequired(new AgentRequest(msg)).Should().BeTrue();
    }

    [Fact]
    public void KeywordAtEnd_IsDetected()
    {
        var msg = "the authentication module needs you to implement";
        _detector.IsWorkflowRequired(new AgentRequest(msg)).Should().BeTrue();
    }

    [Fact]
    public void KeywordInMiddle_IsDetected()
    {
        var msg = "please implement the module for the system";
        _detector.IsWorkflowRequired(new AgentRequest(msg)).Should().BeTrue();
    }

    // ── AgentRequest record ───────────────────────────────────────────────

    [Fact]
    public void AgentRequest_MessageProperty()
    {
        var req = new AgentRequest("hello world");
        req.Message.Should().Be("hello world");
    }

    [Fact]
    public void AgentRequest_SessionIdDefault()
    {
        var req = new AgentRequest("hello world");
        req.SessionId.Should().BeNull();
    }

    [Fact]
    public void AgentRequest_SessionIdSet()
    {
        var req = new AgentRequest("hello world", "sess-123");
        req.SessionId.Should().Be("sess-123");
    }

    [Fact]
    public void AgentRequest_RecordEquality()
    {
        var a = new AgentRequest("hello", "s1");
        var b = new AgentRequest("hello", "s1");
        a.Should().Be(b);
    }
}
