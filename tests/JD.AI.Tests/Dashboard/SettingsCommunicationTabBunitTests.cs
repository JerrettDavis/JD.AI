using Bunit;
using JD.AI.Dashboard.Wasm.Components;
using JD.AI.Dashboard.Wasm.Models;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Tests.Dashboard;

public sealed class SettingsCommunicationTabBunitTests : DashboardBunitTestContext
{
    private static IList<ChannelConfigModel> SampleChannels =>
    [
        new() { Type = "discord", Name = "Discord", Enabled = true,
                AutoConnect = true, Settings = new Dictionary<string, string>
                {
                    ["botToken"] = "tok-secret",
                    ["guildId"] = "123456789"
                }},
        new() { Type = "slack", Name = "Slack", Enabled = false,
                AutoConnect = false, Settings = new Dictionary<string, string>() }
    ];

    [Fact]
    public void CommunicationTab_RendersProviderList()
    {
        var api = CreateApiClient(_ => JsonResponse("[]"));
        Services.AddSingleton(api);
        var cut = RenderWithMudProviders<SettingsCommunicationTab>(p =>
            p.Add(c => c.Channels, SampleChannels).Add(c => c.Api, api));
        var list = cut.Find("[data-testid='provider-list']");
        Assert.Contains("Discord", list.TextContent);
        Assert.Contains("Slack", list.TextContent);
    }

    [Fact]
    public void CommunicationTab_SelectProvider_ShowsRightPanel()
    {
        var api = CreateApiClient(_ => JsonResponse("[]"));
        Services.AddSingleton(api);
        var cut = RenderWithMudProviders<SettingsCommunicationTab>(p =>
            p.Add(c => c.Channels, SampleChannels).Add(c => c.Api, api));
        cut.Find("[data-testid='provider-item-discord']").Click();
        var form = cut.Find("[data-testid='channel-config-form']");
        Assert.NotNull(form);
    }

    [Fact]
    public void CommunicationTab_DiscordForm_ShowsBotTokenField()
    {
        var api = CreateApiClient(_ => JsonResponse("[]"));
        Services.AddSingleton(api);
        var cut = RenderWithMudProviders<SettingsCommunicationTab>(p =>
            p.Add(c => c.Channels, SampleChannels).Add(c => c.Api, api));
        cut.Find("[data-testid='provider-item-discord']").Click();
        var tokenField = cut.Find("[data-testid='field-botToken']");
        Assert.NotNull(tokenField);
    }

    [Fact]
    public void CommunicationTab_SensitiveField_IsMaskedByDefault()
    {
        var api = CreateApiClient(_ => JsonResponse("[]"));
        Services.AddSingleton(api);
        var cut = RenderWithMudProviders<SettingsCommunicationTab>(p =>
            p.Add(c => c.Channels, SampleChannels).Add(c => c.Api, api));
        cut.Find("[data-testid='provider-item-discord']").Click();
        var input = cut.Find("[data-testid='field-botToken'] input");
        Assert.Equal("password", input.GetAttribute("type"));
    }

    [Fact]
    public void CommunicationTab_EyeToggle_RevealsSensitiveField()
    {
        var api = CreateApiClient(_ => JsonResponse("[]"));
        Services.AddSingleton(api);
        var cut = RenderWithMudProviders<SettingsCommunicationTab>(p =>
            p.Add(c => c.Channels, SampleChannels).Add(c => c.Api, api));
        cut.Find("[data-testid='provider-item-discord']").Click();
        cut.Find("[data-testid='toggle-visibility-botToken']").Click();
        var input = cut.Find("[data-testid='field-botToken'] input");
        Assert.Equal("text", input.GetAttribute("type"));
    }

    [Fact]
    public void CommunicationTab_EnabledToggle_IsVisiblePerProvider()
    {
        var api = CreateApiClient(_ => JsonResponse("[]"));
        Services.AddSingleton(api);
        var cut = RenderWithMudProviders<SettingsCommunicationTab>(p =>
            p.Add(c => c.Channels, SampleChannels).Add(c => c.Api, api));
        Assert.NotNull(cut.Find("[data-testid='provider-enable-discord']"));
        Assert.NotNull(cut.Find("[data-testid='provider-enable-slack']"));
    }

    [Fact]
    public void CommunicationTab_StatusDot_IsGreenWhenEnabled()
    {
        var api = CreateApiClient(_ => JsonResponse("[]"));
        Services.AddSingleton(api);
        var cut = RenderWithMudProviders<SettingsCommunicationTab>(p =>
            p.Add(c => c.Channels, SampleChannels).Add(c => c.Api, api));
        var dot = cut.Find("[data-testid='provider-status-discord']");
        Assert.Contains("mud-success", dot.OuterHtml);
    }
}
