using Bunit;
using JD.AI.Dashboard.Wasm.Models;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using SkillsPageComponent = JD.AI.Dashboard.Wasm.Pages.Skills;

namespace JD.AI.Tests.Dashboard;

public sealed class SkillsPageBunitTests : DashboardBunitTestContext
{
    private const string TwoSkillsJson = """
        [
          {"id":"github","name":"github","emoji":"\uD83D\uDC19",
           "description":"GitHub ops","category":"Development",
           "status":"Ready","enabled":true},
          {"id":"1password","name":"1password","emoji":"\uD83D\uDD10",
           "description":"1Password CLI","category":"Security",
           "status":"NeedsSetup","enabled":false}
        ]
        """;

    [Fact]
    public void Skills_ShowsStatusFilterTabs()
    {
        var api = CreateApiClient(_ => JsonResponse(TwoSkillsJson));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<SkillsPageComponent>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='filter-all']"));
            Assert.NotNull(cut.Find("[data-testid='filter-ready']"));
            Assert.NotNull(cut.Find("[data-testid='filter-needs-setup']"));
            Assert.NotNull(cut.Find("[data-testid='filter-disabled']"));
        });
    }

    [Fact]
    public void Skills_RendersSkillCards()
    {
        var api = CreateApiClient(_ => JsonResponse(TwoSkillsJson));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<SkillsPageComponent>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("github", cut.Markup);
            Assert.Contains("1password", cut.Markup);
        });
    }

    [Fact]
    public void Skills_FilterByReady_ShowsOnlyReadySkills()
    {
        var api = CreateApiClient(_ => JsonResponse(TwoSkillsJson));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<SkillsPageComponent>();

        cut.WaitForAssertion(() => cut.Find("[data-testid='filter-ready']").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("github", cut.Markup);
            Assert.DoesNotContain("1password", cut.Markup);
        });
    }

    [Fact]
    public void Skills_SearchFilter_NarrowsResults()
    {
        var api = CreateApiClient(_ => JsonResponse(TwoSkillsJson));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<SkillsPageComponent>();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='skill-search'] input").Change("github"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("github", cut.Markup);
            Assert.DoesNotContain("1password", cut.Markup);
        });
    }

    [Fact]
    public void Skills_EmptyState_ShownWhenNoSkillsMatchFilter()
    {
        var api = CreateApiClient(_ => JsonResponse(TwoSkillsJson));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<SkillsPageComponent>();

        cut.WaitForAssertion(() => cut.Find("[data-testid='filter-disabled']").Click());

        cut.WaitForAssertion(() =>
            Assert.NotNull(cut.Find("[data-testid='skills-empty']")));
    }

    [Fact]
    public void Skills_ToggleSwitch_ExistsPerCard()
    {
        var api = CreateApiClient(_ => JsonResponse(TwoSkillsJson));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<SkillsPageComponent>();

        cut.WaitForAssertion(() =>
            Assert.Equal(2, cut.FindAll("[data-testid='skill-toggle']").Count));
    }

    [Fact]
    public void Skills_ConfigureButton_ExistsPerCard()
    {
        var api = CreateApiClient(_ => JsonResponse(TwoSkillsJson));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<SkillsPageComponent>();

        cut.WaitForAssertion(() =>
            Assert.Equal(2, cut.FindAll("[data-testid='skill-configure']").Count));
    }
}
