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

        result.Should().BeFalse();
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

        result.Should().BeTrue();
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

        result.Should().BeTrue();
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

        result.Should().BeFalse();
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
