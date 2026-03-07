using FluentAssertions;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Tools;

public sealed class ToolEnumTests
{
    // ── ToolCategory enum ─────────────────────────────────────────────────

    [Theory]
    [InlineData(ToolCategory.Filesystem, 0)]
    [InlineData(ToolCategory.Git, 1)]
    [InlineData(ToolCategory.GitHub, 2)]
    [InlineData(ToolCategory.Shell, 3)]
    [InlineData(ToolCategory.Web, 4)]
    [InlineData(ToolCategory.Search, 5)]
    [InlineData(ToolCategory.Network, 6)]
    [InlineData(ToolCategory.Memory, 7)]
    [InlineData(ToolCategory.Orchestration, 8)]
    [InlineData(ToolCategory.Analysis, 9)]
    [InlineData(ToolCategory.Scheduling, 10)]
    [InlineData(ToolCategory.Multimodal, 11)]
    [InlineData(ToolCategory.Security, 12)]
    public void ToolCategory_Values(ToolCategory category, int expected) =>
        ((int)category).Should().Be(expected);

    // ── SafetyTier enum ───────────────────────────────────────────────────

    [Theory]
    [InlineData(SafetyTier.AutoApprove, 0)]
    [InlineData(SafetyTier.ConfirmOnce, 1)]
    [InlineData(SafetyTier.AlwaysConfirm, 2)]
    public void SafetyTier_Values(SafetyTier tier, int expected) =>
        ((int)tier).Should().Be(expected);
}
