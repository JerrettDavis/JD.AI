using System.Net;
using Bunit;
using JD.AI.Dashboard.Wasm.Components;
using JD.AI.Dashboard.Wasm.Models;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace JD.AI.Tests.Dashboard;

public sealed class SkillConfigureDialogBunitTests : DashboardBunitTestContext
{
    private static SkillInfo MakeSkill(Dictionary<string, string>? config = null) => new()
    {
        Id = "github",
        Name = "github",
        Emoji = "\uD83D\uDC19",
        Description = "GitHub ops",
        Category = "Development",
        Status = SkillStatus.NeedsSetup,
        Enabled = true,
        Config = config ?? new() { ["GITHUB_TOKEN"] = "" },
    };

    [Fact]
    public void SkillConfigureDialog_ShowsConfigFields()
    {
        var api = CreateApiClient(_ => new HttpResponseMessage(HttpStatusCode.OK));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var dialogInstance = Substitute.For<IMudDialogInstance>();
        var cut = RenderWithMudProviders<SkillConfigureDialog>(p =>
            p.Add(c => c.Skill, MakeSkill())
             .Add(c => c.Api, api)
             .AddCascadingValue(dialogInstance));

        Assert.NotNull(cut.Find("[data-testid='config-field-GITHUB_TOKEN']"));
    }

    [Fact]
    public void SkillConfigureDialog_SaveButton_Present()
    {
        var api = CreateApiClient(_ => new HttpResponseMessage(HttpStatusCode.OK));
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var dialogInstance = Substitute.For<IMudDialogInstance>();
        var cut = RenderWithMudProviders<SkillConfigureDialog>(p =>
            p.Add(c => c.Skill, MakeSkill())
             .Add(c => c.Api, api)
             .AddCascadingValue(dialogInstance));

        Assert.NotNull(cut.Find("[data-testid='skill-config-save']"));
    }
}
