using Bunit;
using JD.AI.Dashboard.Wasm.Components;
using JD.AI.Dashboard.Wasm.Models;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;

namespace JD.AI.Tests.Dashboard;

public sealed class AgentDetailPanelBunitTests : DashboardBunitTestContext
{
    private GatewayApiClient BuildApi(string agentId, string json) =>
        CreateApiClient(req =>
        {
            if (req.RequestUri!.ToString().Contains(agentId))
                return JsonResponse(json);
            throw new InvalidOperationException($"Unexpected: {req.RequestUri}");
        });

    [Fact]
    public void AgentDetailPanel_ShowsOverviewTabByDefault()
    {
        var api = BuildApi("agent-1", """
            {"id":"agent-1","provider":"openai","model":"gpt-5",
             "systemPrompt":"Be helpful","isDefault":false,
             "tools":[],"assignedSkills":[]}
            """);
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<AgentDetailPanel>(p =>
            p.Add(c => c.AgentId, "agent-1")
             .Add(c => c.Api, api)
             .Add(c => c.OnClose, EventCallback.Factory.Create(this, () => { })));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='overview-tab']"));
            Assert.Contains("gpt-5", cut.Markup);
        });
    }

    [Fact]
    public void AgentDetailPanel_ToolsTab_ShowsTools()
    {
        var api = BuildApi("agent-1", """
            {"id":"agent-1","provider":"openai","model":"gpt-5",
             "systemPrompt":"","isDefault":false,
             "tools":[{"name":"web_search","description":"Search","isAllowed":true}],
             "assignedSkills":[]}
            """);
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<AgentDetailPanel>(p =>
            p.Add(c => c.AgentId, "agent-1")
             .Add(c => c.Api, api)
             .Add(c => c.OnClose, EventCallback.Factory.Create(this, () => { })));

        // Wait for data to load
        cut.WaitForAssertion(() => Assert.Contains("gpt-5", cut.Markup));

        // Click Tools tab (MudTabs renders as div.mud-tab)
        var toolsTab = cut.FindAll(".mud-tab").First(t => t.TextContent.Trim().Contains("Tools"));
        toolsTab.Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("web_search", cut.Markup));
    }

    [Fact]
    public void AgentDetailPanel_SkillsTab_ShowsAssignedSkills()
    {
        var api = BuildApi("agent-1", """
            {"id":"agent-1","provider":"openai","model":"gpt-5",
             "systemPrompt":"","isDefault":false,
             "tools":[],
             "assignedSkills":[{"id":"github","name":"github","emoji":"\uD83D\uDC19",
               "description":"GitHub ops","category":"Development",
               "status":"Ready","enabled":true}]}
            """);
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<AgentDetailPanel>(p =>
            p.Add(c => c.AgentId, "agent-1")
             .Add(c => c.Api, api)
             .Add(c => c.OnClose, EventCallback.Factory.Create(this, () => { })));

        // Wait for data to load
        cut.WaitForAssertion(() => Assert.Contains("gpt-5", cut.Markup));

        // Click Skills tab (MudTabs renders as div.mud-tab)
        var skillsTab = cut.FindAll(".mud-tab").First(t => t.TextContent.Trim().Contains("Skills"));
        skillsTab.Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("github", cut.Markup));
    }

    [Fact]
    public void AgentDetailPanel_CloseButton_InvokesOnClose()
    {
        var closed = false;
        var api = BuildApi("agent-1", """
            {"id":"agent-1","provider":"openai","model":"gpt-5",
             "systemPrompt":"","isDefault":false,"tools":[],"assignedSkills":[]}
            """);
        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<AgentDetailPanel>(p =>
            p.Add(c => c.AgentId, "agent-1")
             .Add(c => c.Api, api)
             .Add(c => c.OnClose, EventCallback.Factory.Create(this, () => closed = true)));

        cut.WaitForAssertion(() => cut.Find("[data-testid='detail-close-button']").Click());
        closed.Should().BeTrue();
    }
}
