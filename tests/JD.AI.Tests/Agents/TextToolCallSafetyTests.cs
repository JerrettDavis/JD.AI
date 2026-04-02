using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Agents;

public sealed class TextToolCallSafetyTests
{
    private static readonly IReadOnlyDictionary<string, SafetyTier> TierMap =
        new Dictionary<string, SafetyTier>(StringComparer.OrdinalIgnoreCase)
        {
            ["read_file"] = SafetyTier.AutoApprove,
            ["write_file"] = SafetyTier.ConfirmOnce,
            ["list_directory"] = SafetyTier.ConfirmOnce,
            ["run_command"] = SafetyTier.AlwaysConfirm,
        };

    [Fact]
    public void ExplicitDeny_AlwaysBlocks()
    {
        var output = new SpyAgentOutput();
        var profile = new ToolPermissionProfile();
        profile.AddDenied("run_command", projectScope: false);

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "run_command",
            "command=ls",
            PermissionMode.BypassAll,
            skipPermissions: true,
            autoRunEnabled: true,
            TierMap,
            profile,
            new HashSet<string>(StringComparer.Ordinal),
            output);

        result.Allowed.Should().BeFalse();
        result.Message.Should().Be("Tool blocked: blocked by explicit deny rule.");
        output.ConfirmCalled.Should().BeFalse();
    }

    [Fact]
    public void ExplicitAllow_SkipsPrompt()
    {
        var output = new SpyAgentOutput();
        var profile = new ToolPermissionProfile();
        profile.AddAllowed("run_command", projectScope: false);

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "run_command",
            "command=ls",
            PermissionMode.Normal,
            skipPermissions: false,
            autoRunEnabled: false,
            TierMap,
            profile,
            new HashSet<string>(StringComparer.Ordinal),
            output);

        result.Allowed.Should().BeTrue();
        output.ConfirmCalled.Should().BeFalse();
    }

    [Fact]
    public void SkipPermissions_BypassesPromptEvenWhenPermissionModeIsNormal()
    {
        var output = new SpyAgentOutput();

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "run_command",
            "command=ls",
            PermissionMode.Normal,
            skipPermissions: true,
            autoRunEnabled: false,
            TierMap,
            permissionProfile: null,
            new HashSet<string>(StringComparer.Ordinal),
            output);

        result.Allowed.Should().BeTrue();
        result.Message.Should().Be("Allowed without prompt.");
        output.ConfirmCalled.Should().BeFalse();
    }

    [Fact]
    public void NoExplicitAllow_RequiresPrompt()
    {
        var output = new SpyAgentOutput { ConfirmResult = true };

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "run_command",
            "command=ls",
            PermissionMode.Normal,
            skipPermissions: false,
            autoRunEnabled: false,
            TierMap,
            new ToolPermissionProfile(),
            new HashSet<string>(StringComparer.Ordinal),
            output);

        result.Allowed.Should().BeTrue();
        output.ConfirmCalled.Should().BeTrue();
    }

    [Fact]
    public void PlanMode_BlocksMutatingTools_EvenWhenNotDenied()
    {
        var output = new SpyAgentOutput();

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "run_command",
            "command=ls",
            PermissionMode.Plan,
            skipPermissions: false,
            autoRunEnabled: false,
            TierMap,
            new ToolPermissionProfile(),
            new HashSet<string>(StringComparer.Ordinal),
            output);

        result.Allowed.Should().BeFalse();
        result.Message.Should().Be("Tool blocked: plan mode — read-only.");
    }

    [Fact]
    public void ConfirmOncePrompt_AfterApproval_ReusesCachedDecision()
    {
        var output = new SpyAgentOutput { ConfirmResult = true };
        var confirmedOnce = new HashSet<string>(StringComparer.Ordinal);

        var first = AgentLoop.EvaluateTextToolCallSafety(
            "write_file",
            "path=a.txt",
            PermissionMode.Normal,
            skipPermissions: false,
            autoRunEnabled: false,
            TierMap,
            new ToolPermissionProfile(),
            confirmedOnce,
            output);

        output.ConfirmCalled.Should().BeTrue();

        output.ConfirmCalled = false;
        var second = AgentLoop.EvaluateTextToolCallSafety(
            "write_file",
            "path=b.txt",
            PermissionMode.Normal,
            skipPermissions: false,
            autoRunEnabled: false,
            TierMap,
            new ToolPermissionProfile(),
            confirmedOnce,
            output);

        first.Allowed.Should().BeTrue();
        second.Allowed.Should().BeTrue();
        output.ConfirmCalled.Should().BeFalse();
    }

    [Fact]
    public void AcceptEdits_NonEditConfirmOnce_StillPrompts()
    {
        var output = new SpyAgentOutput { ConfirmResult = true };

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "list_directory",
            "path=.",
            PermissionMode.AcceptEdits,
            skipPermissions: false,
            autoRunEnabled: false,
            TierMap,
            new ToolPermissionProfile(),
            new HashSet<string>(StringComparer.Ordinal),
            output);

        result.Allowed.Should().BeTrue();
        output.ConfirmCalled.Should().BeTrue();
    }

    [Fact]
    public void UserDeniedPrompt_ReturnsExplicitDenialReason()
    {
        var output = new SpyAgentOutput { ConfirmResult = false };

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "run_command",
            "command=ls",
            PermissionMode.Normal,
            skipPermissions: false,
            autoRunEnabled: false,
            TierMap,
            new ToolPermissionProfile(),
            new HashSet<string>(StringComparer.Ordinal),
            output);

        result.Allowed.Should().BeFalse();
        result.Message.Should().Be("User denied tool execution.");
    }

    private sealed class SpyAgentOutput : IAgentOutput
    {
        public bool ConfirmResult { get; set; } = true;
        public bool ConfirmCalled { get; set; }

        public void RenderInfo(string message) { }
        public void RenderWarning(string message) { }
        public void RenderError(string message) { }
        public void BeginThinking() { }
        public void WriteThinkingChunk(string text) { }
        public void EndThinking() { }
        public void BeginStreaming() { }
        public void WriteStreamingChunk(string text) { }
        public void EndStreaming() { }

        public bool ConfirmToolCall(string toolName, string? args)
        {
            ConfirmCalled = true;
            return ConfirmResult;
        }
    }
}
