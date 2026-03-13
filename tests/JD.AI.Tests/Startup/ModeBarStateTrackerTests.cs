using JD.AI.Core.Agents;
using JD.AI.Startup;

namespace JD.AI.Tests.Startup;

public sealed class ModeBarStateTrackerTests
{
    [Fact]
    public void TryMarkForRender_FirstMode_ReturnsTrue()
    {
        var tracker = new ModeBarStateTracker();

        var shouldRender = tracker.TryMarkForRender(PermissionMode.Normal);

        Assert.True(shouldRender);
    }

    [Fact]
    public void TryMarkForRender_SameModeTwice_ReturnsFalseSecondTime()
    {
        var tracker = new ModeBarStateTracker();

        _ = tracker.TryMarkForRender(PermissionMode.Normal);
        var shouldRender = tracker.TryMarkForRender(PermissionMode.Normal);

        Assert.False(shouldRender);
    }

    [Fact]
    public void TryMarkForRender_ModeTransitionSequence_RendersOnlyOnChanges()
    {
        var tracker = new ModeBarStateTracker();

        var sequence = new[]
        {
            PermissionMode.Normal,
            PermissionMode.Normal,
            PermissionMode.Plan,
            PermissionMode.Plan,
            PermissionMode.AcceptEdits,
            PermissionMode.BypassAll,
            PermissionMode.BypassAll,
            PermissionMode.Normal,
        };

        var renderedFlags = sequence.Select(tracker.TryMarkForRender).ToArray();

        Assert.Equal([true, false, true, false, true, true, false, true], renderedFlags);
    }
}
