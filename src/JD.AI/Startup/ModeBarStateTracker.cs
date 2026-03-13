using JD.AI.Core.Agents;

namespace JD.AI.Startup;

/// <summary>
/// Tracks the last rendered permission mode and decides whether the mode bar
/// should be rendered again.
/// </summary>
internal sealed class ModeBarStateTracker
{
    private PermissionMode? _lastRenderedMode;

    public bool TryMarkForRender(PermissionMode modeToRender)
    {
        if (_lastRenderedMode == modeToRender)
            return false;

        _lastRenderedMode = modeToRender;
        return true;
    }
}
