using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="AgentLoop.EvaluateTextToolCallSafety"/> —
/// the pure-logic safety gate for text-based tool calls that bypass SK
/// auto-function-invocation filters.
/// </summary>
public sealed class TextToolCallSafetyTests
{
    private static readonly IReadOnlyDictionary<string, SafetyTier> TierMap =
        new Dictionary<string, SafetyTier>(StringComparer.OrdinalIgnoreCase)
        {
            ["read_file"] = SafetyTier.AutoApprove,
            ["edit_file"] = SafetyTier.ConfirmOnce,
            ["run_command"] = SafetyTier.AlwaysConfirm,
        };

    // ── Plan mode ────────────────────────────────────────────────────────

    [Fact]
    public void PlanMode_BlocksNonAutoApprove_ConfirmOnce()
    {
        var output = new SpyAgentOutput();
        var confirmed = new HashSet<string>(StringComparer.Ordinal);

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "edit_file", "path=foo.cs",
            PermissionMode.Plan, skipPermissions: false, autoRunEnabled: false,
            TierMap, confirmed, output);

        result.Should().BeFalse();
        output.ConfirmCalled.Should().BeFalse();
    }

    [Fact]
    public void PlanMode_BlocksNonAutoApprove_AlwaysConfirm()
    {
        var output = new SpyAgentOutput();
        var confirmed = new HashSet<string>(StringComparer.Ordinal);

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "run_command", "command=ls",
            PermissionMode.Plan, skipPermissions: false, autoRunEnabled: false,
            TierMap, confirmed, output);

        result.Should().BeFalse();
    }

    [Fact]
    public void PlanMode_AllowsAutoApprove()
    {
        var output = new SpyAgentOutput();
        var confirmed = new HashSet<string>(StringComparer.Ordinal);

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "read_file", "path=foo.cs",
            PermissionMode.Plan, skipPermissions: false, autoRunEnabled: false,
            TierMap, confirmed, output);

        result.Should().BeTrue();
    }

    // ── BypassAll mode ───────────────────────────────────────────────────

    [Fact]
    public void BypassAll_SkipsAllChecks()
    {
        var output = new SpyAgentOutput();
        var confirmed = new HashSet<string>(StringComparer.Ordinal);

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "run_command", "command=rm -rf /",
            PermissionMode.BypassAll, skipPermissions: false, autoRunEnabled: false,
            TierMap, confirmed, output);

        result.Should().BeTrue();
        output.ConfirmCalled.Should().BeFalse();
    }

    // ── Normal mode ──────────────────────────────────────────────────────

    [Fact]
    public void NormalMode_AlwaysConfirm_PromptsUser()
    {
        var output = new SpyAgentOutput { ConfirmResult = true };
        var confirmed = new HashSet<string>(StringComparer.Ordinal);

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "run_command", "command=pwd",
            PermissionMode.Normal, skipPermissions: false, autoRunEnabled: false,
            TierMap, confirmed, output);

        result.Should().BeTrue();
        output.ConfirmCalled.Should().BeTrue();
    }

    [Fact]
    public void NormalMode_UserDenies_ReturnsFalse()
    {
        var output = new SpyAgentOutput { ConfirmResult = false };
        var confirmed = new HashSet<string>(StringComparer.Ordinal);

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "run_command", "command=pwd",
            PermissionMode.Normal, skipPermissions: false, autoRunEnabled: false,
            TierMap, confirmed, output);

        result.Should().BeFalse();
        output.ConfirmCalled.Should().BeTrue();
    }

    [Fact]
    public void NormalMode_ConfirmOnce_RemembersApproval()
    {
        var output = new SpyAgentOutput { ConfirmResult = true };
        var confirmed = new HashSet<string>(StringComparer.Ordinal);

        // First call — should prompt
        var result1 = AgentLoop.EvaluateTextToolCallSafety(
            "edit_file", "path=foo.cs",
            PermissionMode.Normal, skipPermissions: false, autoRunEnabled: false,
            TierMap, confirmed, output);

        result1.Should().BeTrue();
        output.ConfirmCalled.Should().BeTrue();
        confirmed.Should().Contain("edit_file");

        // Reset spy
        output.ConfirmCalled = false;

        // Second call — should NOT prompt
        var result2 = AgentLoop.EvaluateTextToolCallSafety(
            "edit_file", "path=bar.cs",
            PermissionMode.Normal, skipPermissions: false, autoRunEnabled: false,
            TierMap, confirmed, output);

        result2.Should().BeTrue();
        output.ConfirmCalled.Should().BeFalse();
    }

    [Fact]
    public void NormalMode_AutoApprove_NoPrompt()
    {
        var output = new SpyAgentOutput();
        var confirmed = new HashSet<string>(StringComparer.Ordinal);

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "read_file", "path=foo.cs",
            PermissionMode.Normal, skipPermissions: false, autoRunEnabled: false,
            TierMap, confirmed, output);

        result.Should().BeTrue();
        output.ConfirmCalled.Should().BeFalse();
    }

    // ── SkipPermissions / AutoRunEnabled ─────────────────────────────────

    [Fact]
    public void SkipPermissions_BypassesAll()
    {
        var output = new SpyAgentOutput();
        var confirmed = new HashSet<string>(StringComparer.Ordinal);

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "run_command", "command=pwd",
            PermissionMode.Normal, skipPermissions: true, autoRunEnabled: false,
            TierMap, confirmed, output);

        result.Should().BeTrue();
        output.ConfirmCalled.Should().BeFalse();
    }

    [Fact]
    public void AutoRunEnabled_BypassesAll()
    {
        var output = new SpyAgentOutput();
        var confirmed = new HashSet<string>(StringComparer.Ordinal);

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "run_command", "command=pwd",
            PermissionMode.Normal, skipPermissions: false, autoRunEnabled: true,
            TierMap, confirmed, output);

        result.Should().BeTrue();
        output.ConfirmCalled.Should().BeFalse();
    }

    // ── AcceptEdits mode ─────────────────────────────────────────────────

    [Fact]
    public void AcceptEdits_AutoApprovesConfirmOnce()
    {
        var output = new SpyAgentOutput();
        var confirmed = new HashSet<string>(StringComparer.Ordinal);

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "edit_file", "path=foo.cs",
            PermissionMode.AcceptEdits, skipPermissions: false, autoRunEnabled: false,
            TierMap, confirmed, output);

        result.Should().BeTrue();
        output.ConfirmCalled.Should().BeFalse();
    }

    [Fact]
    public void AcceptEdits_PromptsForAlwaysConfirm()
    {
        var output = new SpyAgentOutput { ConfirmResult = true };
        var confirmed = new HashSet<string>(StringComparer.Ordinal);

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "run_command", "command=pwd",
            PermissionMode.AcceptEdits, skipPermissions: false, autoRunEnabled: false,
            TierMap, confirmed, output);

        result.Should().BeTrue();
        output.ConfirmCalled.Should().BeTrue();
    }

    // ── Null tier map ────────────────────────────────────────────────────

    [Fact]
    public void NullTierMap_DefaultsToAlwaysConfirm()
    {
        var output = new SpyAgentOutput { ConfirmResult = true };
        var confirmed = new HashSet<string>(StringComparer.Ordinal);

        var result = AgentLoop.EvaluateTextToolCallSafety(
            "some_unknown_tool", "arg=val",
            PermissionMode.Normal, skipPermissions: false, autoRunEnabled: false,
            tierMap: null, confirmed, output);

        result.Should().BeTrue();
        output.ConfirmCalled.Should().BeTrue();
    }

    // ── Test spy ─────────────────────────────────────────────────────────

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
