using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Agents;

public sealed class ToolPermissionChannelParityTests
{
    private static readonly IReadOnlyDictionary<string, SafetyTier> TierMap =
        new Dictionary<string, SafetyTier>(StringComparer.OrdinalIgnoreCase)
        {
            ["read_file"] = SafetyTier.AutoApprove,
            ["run_command"] = SafetyTier.AlwaysConfirm,
        };

    [Fact]
    public void ExplicitDeny_BlocksBothStructuredAndTextChannels()
    {
        var profile = new ToolPermissionProfile();
        profile.AddDenied("run_command", projectScope: false);
        var output = new SpyAgentOutput();

        var structuredGate = ToolExecutionPermissionEvaluator.Evaluate(
            "run_command",
            PermissionMode.Normal,
            SafetyTier.AlwaysConfirm,
            profile);

        var textAllowed = AgentLoop.EvaluateTextToolCallSafety(
            "run_command",
            "command=ls",
            PermissionMode.Normal,
            skipPermissions: false,
            autoRunEnabled: false,
            TierMap,
            profile,
            new HashSet<string>(StringComparer.Ordinal),
            output);

        structuredGate.Decision.Should().Be(ToolExecutionGateDecision.Blocked);
        textAllowed.Should().BeFalse();
    }

    [Fact]
    public void ExplicitAllow_AllowWithoutPromptInBothChannels()
    {
        var profile = new ToolPermissionProfile();
        profile.AddAllowed("run_command", projectScope: false);
        var output = new SpyAgentOutput();

        var structuredGate = ToolExecutionPermissionEvaluator.Evaluate(
            "run_command",
            PermissionMode.Normal,
            SafetyTier.AlwaysConfirm,
            profile);

        var textAllowed = AgentLoop.EvaluateTextToolCallSafety(
            "run_command",
            "command=ls",
            PermissionMode.Normal,
            skipPermissions: false,
            autoRunEnabled: false,
            TierMap,
            profile,
            new HashSet<string>(StringComparer.Ordinal),
            output);

        structuredGate.Decision.Should().Be(ToolExecutionGateDecision.AllowWithoutPrompt);
        textAllowed.Should().BeTrue();
        output.ConfirmCalled.Should().BeFalse();
    }

    [Fact]
    public void MissingExplicitRule_RequiresPromptInBothChannels()
    {
        var profile = new ToolPermissionProfile();
        var output = new SpyAgentOutput { ConfirmResult = true };

        var structuredGate = ToolExecutionPermissionEvaluator.Evaluate(
            "run_command",
            PermissionMode.Normal,
            SafetyTier.AlwaysConfirm,
            profile);

        var textAllowed = AgentLoop.EvaluateTextToolCallSafety(
            "run_command",
            "command=ls",
            PermissionMode.Normal,
            skipPermissions: false,
            autoRunEnabled: false,
            TierMap,
            profile,
            new HashSet<string>(StringComparer.Ordinal),
            output);

        structuredGate.Decision.Should().Be(ToolExecutionGateDecision.RequirePrompt);
        textAllowed.Should().BeTrue();
        output.ConfirmCalled.Should().BeTrue();
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
