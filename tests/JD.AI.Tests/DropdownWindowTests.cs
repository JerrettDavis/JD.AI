using JD.AI.Rendering;

namespace JD.AI.Tests;

public sealed class DropdownWindowTests
{
    [Fact]
    public void FewItems_WindowShowsAll()
    {
        var window = DropdownWindow.Compute(totalItems: 3, maxVisible: 8, selectedIndex: 0);
        Assert.Equal(0, window.StartIndex);
        Assert.Equal(3, window.VisibleCount);
    }

    [Fact]
    public void ExactlyMaxItems_WindowShowsAll()
    {
        var window = DropdownWindow.Compute(totalItems: 8, maxVisible: 8, selectedIndex: 0);
        Assert.Equal(0, window.StartIndex);
        Assert.Equal(8, window.VisibleCount);
    }

    [Fact]
    public void SelectionWithinFirstPage_WindowStartsAtZero()
    {
        var window = DropdownWindow.Compute(totalItems: 15, maxVisible: 8, selectedIndex: 5);
        Assert.Equal(0, window.StartIndex);
        Assert.Equal(8, window.VisibleCount);
    }

    [Fact]
    public void SelectionAtMaxVisible_WindowScrollsDown()
    {
        // selected=8 means the 9th item — should scroll so it's the last visible
        var window = DropdownWindow.Compute(totalItems: 15, maxVisible: 8, selectedIndex: 8);
        Assert.Equal(1, window.StartIndex);
        Assert.Equal(8, window.VisibleCount);
    }

    [Fact]
    public void SelectionAtEnd_WindowScrollsToShowLast()
    {
        var window = DropdownWindow.Compute(totalItems: 15, maxVisible: 8, selectedIndex: 14);
        Assert.Equal(7, window.StartIndex);
        Assert.Equal(8, window.VisibleCount);
    }

    [Fact]
    public void SelectionAtLastItem_WindowEndsAtLastItem()
    {
        var window = DropdownWindow.Compute(totalItems: 20, maxVisible: 8, selectedIndex: 19);
        Assert.Equal(12, window.StartIndex);
        Assert.Equal(8, window.VisibleCount);
    }

    [Fact]
    public void SelectionWrapsBackToZero_WindowResetsToTop()
    {
        // After wrapping, selected goes back to 0
        var window = DropdownWindow.Compute(totalItems: 15, maxVisible: 8, selectedIndex: 0);
        Assert.Equal(0, window.StartIndex);
    }

    [Fact]
    public void SelectedItemIsAlwaysWithinVisibleRange()
    {
        // Exhaustive test for a 20-item list with 8 visible
        for (var sel = 0; sel < 20; sel++)
        {
            var window = DropdownWindow.Compute(totalItems: 20, maxVisible: 8, selectedIndex: sel);
            Assert.True(sel >= window.StartIndex, $"selected={sel} below StartIndex={window.StartIndex}");
            Assert.True(sel < window.StartIndex + window.VisibleCount,
                $"selected={sel} beyond visible range [{window.StartIndex}..{window.StartIndex + window.VisibleCount - 1}]");
        }
    }

    [Fact]
    public void SingleItem_WindowShowsOne()
    {
        var window = DropdownWindow.Compute(totalItems: 1, maxVisible: 8, selectedIndex: 0);
        Assert.Equal(0, window.StartIndex);
        Assert.Equal(1, window.VisibleCount);
    }

    [Fact]
    public void ZeroItems_WindowShowsNone()
    {
        var window = DropdownWindow.Compute(totalItems: 0, maxVisible: 8, selectedIndex: 0);
        Assert.Equal(0, window.StartIndex);
        Assert.Equal(0, window.VisibleCount);
    }
}
