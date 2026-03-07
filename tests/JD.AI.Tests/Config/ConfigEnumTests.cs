using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;

namespace JD.AI.Tests.Config;

public sealed class ConfigEnumTests
{
    // ── OutputStyle enum ──────────────────────────────────────────────────

    [Theory]
    [InlineData(OutputStyle.Rich, 0)]
    [InlineData(OutputStyle.Plain, 1)]
    [InlineData(OutputStyle.Compact, 2)]
    [InlineData(OutputStyle.Json, 3)]
    public void OutputStyle_Values(OutputStyle style, int expected) =>
        ((int)style).Should().Be(expected);

    // ── TuiTheme enum ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(TuiTheme.DefaultDark, 0)]
    [InlineData(TuiTheme.Monokai, 1)]
    [InlineData(TuiTheme.SolarizedDark, 2)]
    [InlineData(TuiTheme.SolarizedLight, 3)]
    [InlineData(TuiTheme.Nord, 4)]
    [InlineData(TuiTheme.Dracula, 5)]
    [InlineData(TuiTheme.OneDark, 6)]
    [InlineData(TuiTheme.CatppuccinMocha, 7)]
    [InlineData(TuiTheme.Gruvbox, 8)]
    [InlineData(TuiTheme.HighContrast, 9)]
    public void TuiTheme_Values(TuiTheme theme, int expected) =>
        ((int)theme).Should().Be(expected);

    // ── SystemPromptCompaction enum ───────────────────────────────────────

    [Theory]
    [InlineData(SystemPromptCompaction.Off, 0)]
    [InlineData(SystemPromptCompaction.Auto, 1)]
    [InlineData(SystemPromptCompaction.Always, 2)]
    public void SystemPromptCompaction_Values(SystemPromptCompaction mode, int expected) =>
        ((int)mode).Should().Be(expected);

    // ── PermissionMode enum ───────────────────────────────────────────────

    [Theory]
    [InlineData(PermissionMode.Normal, 0)]
    [InlineData(PermissionMode.Plan, 1)]
    [InlineData(PermissionMode.AcceptEdits, 2)]
    [InlineData(PermissionMode.BypassAll, 3)]
    public void PermissionMode_Values(PermissionMode mode, int expected) =>
        ((int)mode).Should().Be(expected);
}
