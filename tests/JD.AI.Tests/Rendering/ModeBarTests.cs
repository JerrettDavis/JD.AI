using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Rendering;

namespace JD.AI.Tests.Rendering;

[Collection("Console")]
public sealed class ModeBarTests : IDisposable
{
    public ModeBarTests()
    {
        ChatRenderer.ApplyTheme(TuiTheme.DefaultDark);
    }

    public void Dispose()
    {
        ChatRenderer.ApplyTheme(TuiTheme.DefaultDark);
    }

    [Theory]
    [InlineData(PermissionMode.Normal, "Normal")]
    [InlineData(PermissionMode.Plan, "Plan (read-only)")]
    [InlineData(PermissionMode.AcceptEdits, "Auto-edit")]
    [InlineData(PermissionMode.BypassAll, "Autopilot")]
    public void GetModeBarLabel_ReturnsExpectedLabel(PermissionMode mode, string expectedLabel)
    {
        var (label, _) = ChatRenderer.GetModeBarLabel(mode);
        Assert.Equal(expectedLabel, label);
    }

    [Fact]
    public void GetModeBarLabel_PlanMode_IsYellow()
    {
        var (_, color) = ChatRenderer.GetModeBarLabel(PermissionMode.Plan);
        Assert.Equal("yellow", color);
    }

    [Fact]
    public void GetModeBarLabel_BypassAll_IsRed()
    {
        var (_, color) = ChatRenderer.GetModeBarLabel(PermissionMode.BypassAll);
        Assert.Equal("red", color);
    }
}
