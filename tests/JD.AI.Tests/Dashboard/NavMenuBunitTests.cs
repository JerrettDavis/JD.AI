using Bunit;
using JD.AI.Dashboard.Wasm.Layout;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace JD.AI.Tests.Dashboard;

public sealed class NavMenuBunitTests : BunitContext
{
    public NavMenuBunitTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);
        JSInterop.SetupVoid("localStorage.setItem", _ => true).SetVoidResult();
        Services.AddScoped<NavState>();
        Services.AddMudServices();
    }

    [Fact]
    public void NavMenu_RendersAllFourSections()
    {
        var cut = Render<NavMenu>();

        Assert.NotNull(cut.Find("[data-testid='nav-section-chat']"));
        Assert.NotNull(cut.Find("[data-testid='nav-section-control']"));
        Assert.NotNull(cut.Find("[data-testid='nav-section-agents']"));
        Assert.NotNull(cut.Find("[data-testid='nav-section-settings']"));
    }

    [Fact]
    public void NavMenu_ChatLinkNavigatesToChatRoute()
    {
        var cut = Render<NavMenu>();
        var chatLink = cut.Find("[data-testid='nav-chat'] a, a[data-testid='nav-chat']");
        Assert.Equal("/chat", chatLink.GetAttribute("href"));
    }

    [Fact]
    public void NavMenu_ControlGroupContainsOverviewLink()
    {
        var cut = Render<NavMenu>();
        Assert.NotNull(cut.Find("[data-testid='nav-control-overview']"));
    }

    [Fact]
    public void NavMenu_AgentsGroupContainsAgentsAndSkillsLinks()
    {
        var cut = Render<NavMenu>();
        Assert.NotNull(cut.Find("[data-testid='nav-agents']"));
        Assert.NotNull(cut.Find("[data-testid='nav-skills']"));
    }

    [Fact]
    public void NavMenu_SettingsGroupContainsFourLinks()
    {
        var cut = Render<NavMenu>();
        Assert.NotNull(cut.Find("[data-testid='nav-settings-ai']"));
        Assert.NotNull(cut.Find("[data-testid='nav-settings-comms']"));
        Assert.NotNull(cut.Find("[data-testid='nav-settings-config']"));
        Assert.NotNull(cut.Find("[data-testid='nav-settings-logs']"));
    }
}
