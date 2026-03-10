namespace JD.AI.Rendering;

/// <summary>
/// Computes the visible slice of a dropdown list that keeps the selected item in view.
/// Pure value type with no side effects — all rendering state is derived from inputs.
/// </summary>
public readonly record struct DropdownWindow(int StartIndex, int VisibleCount)
{
    /// <summary>
    /// Computes a window of <paramref name="maxVisible"/> items that keeps
    /// <paramref name="selectedIndex"/> in view.
    /// </summary>
    public static DropdownWindow Compute(int totalItems, int maxVisible, int selectedIndex)
    {
        var visibleCount = Math.Min(totalItems, maxVisible);
        var startIndex = selectedIndex < maxVisible
            ? 0
            : selectedIndex - maxVisible + 1;
        return new DropdownWindow(startIndex, visibleCount);
    }
}
