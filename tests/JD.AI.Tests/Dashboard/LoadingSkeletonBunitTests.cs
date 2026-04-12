using Bunit;
using JD.AI.Dashboard.Wasm.Components.Shared;
using Microsoft.AspNetCore.Components;

namespace JD.AI.Tests.Dashboard;

public sealed class LoadingSkeletonBunitTests : DashboardBunitTestContext
{
    [Fact]
    public void LoadingSkeleton_RendersRowSkeletons_WithCorrectCount()
    {
        var cut = RenderWithMudProviders<LoadingSkeleton>(
            p => p.Add(c => c.Rows, 3).Add(c => c.SkeletonType, "rows"));

        var rows = cut.FindAll("[data-testid='skeleton-row']");
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public void LoadingSkeleton_RendersCardSkeletons_WithCorrectCount()
    {
        var cut = RenderWithMudProviders<LoadingSkeleton>(
            p => p.Add(c => c.Rows, 2).Add(c => c.SkeletonType, "cards"));

        var cards = cut.FindAll("[data-testid='skeleton-card']");
        Assert.Equal(2, cards.Count);
    }

    [Fact]
    public void LoadingSkeleton_RendersPanelSkeleton()
    {
        var cut = RenderWithMudProviders<LoadingSkeleton>(
            p => p.Add(c => c.SkeletonType, "panel"));

        Assert.NotNull(cut.Find("[data-testid='skeleton-panel']"));
    }
}
