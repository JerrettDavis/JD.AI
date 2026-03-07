using JD.AI.Rendering;
using Xunit;

namespace JD.AI.Tests.Rendering;

/// <summary>
/// Tests for InteractiveInput event wiring (Shift+Tab, Alt+T, Alt+P).
/// Actual key handling requires a console, so these verify event registration.
/// </summary>
public sealed class InteractiveInputEventTests
{
    [Fact]
    public void OnTogglePlanMode_EventCanBeSubscribed()
    {
        var completions = new CompletionProvider();
        var input = new InteractiveInput(completions);
        var fired = false;

        input.OnTogglePlanMode += (_, _) => fired = true;

        // Event doesn't fire without key press, but subscription should not throw
        Assert.False(fired);
    }

    [Fact]
    public void OnToggleExtendedThinking_EventCanBeSubscribed()
    {
        var completions = new CompletionProvider();
        var input = new InteractiveInput(completions);
        var fired = false;

        input.OnToggleExtendedThinking += (_, _) => fired = true;

        Assert.False(fired);
    }

    [Fact]
    public void OnCycleModel_EventCanBeSubscribed()
    {
        var completions = new CompletionProvider();
        var input = new InteractiveInput(completions);
        var fired = false;

        input.OnCycleModel += (_, _) => fired = true;

        Assert.False(fired);
    }

    [Fact]
    public void VimModeEnabled_DefaultsFalse()
    {
        var completions = new CompletionProvider();
        var input = new InteractiveInput(completions);

        Assert.False(input.VimModeEnabled);
    }
}
