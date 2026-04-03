using System.Net;
using System.Text.Json;
using Bunit;
using JD.AI.Dashboard.Wasm.Components;
using JD.AI.Dashboard.Wasm.Models;

namespace JD.AI.Tests.Dashboard;

public sealed class SettingsRoutingTabBunitTests : DashboardBunitTestContext
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SettingsRoutingTab_AddRuleAndSave_PostsUpdatedRoutingConfig()
    {
        RoutingConfigModel? savedRouting = null;

        var api = CreateApiClient(request =>
        {
            if (request.Method != HttpMethod.Put)
                throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}");

            Assert.Equal("http://localhost/api/gateway/config/routing", request.RequestUri!.ToString());
            savedRouting = JsonSerializer.Deserialize<RoutingConfigModel>(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult(), JsonOptions);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var routing = new RoutingConfigModel
        {
            DefaultAgentId = "jdai-default",
            Rules =
            [
                new RoutingRuleModel { ChannelType = "discord", AgentId = "jdai-default" },
            ],
        };

        var cut = RenderWithMudProviders<SettingsRoutingTab>(parameters => parameters
            .Add(x => x.Api, api)
            .Add(x => x.Routing, routing)
            .Add(x => x.AvailableAgents, new List<AgentDefinition>
            {
                new() { Id = "jdai-default", Provider = "openai", Model = "gpt-5" },
                new() { Id = "jdai-research", Provider = "anthropic", Model = "claude-opus" },
            })
            .Add(x => x.AvailableChannels, new List<ChannelConfigModel>
            {
                new() { Type = "discord", Name = "Discord" },
                new() { Type = "signal", Name = "Signal" },
            }));

        Assert.Single(cut.FindAll("[data-testid='routing-rule']"));

        cut.Find("[data-testid='add-rule-button']").Click();

        Assert.Equal(2, cut.FindAll("[data-testid='routing-rule']").Count);
        Assert.Equal(2, routing.Rules.Count);
        Assert.True(string.IsNullOrEmpty(routing.Rules[1].ChannelType));
        Assert.True(string.IsNullOrEmpty(routing.Rules[1].AgentId));

        cut.Find("[data-testid='save-routing-button']").Click();

        Assert.NotNull(savedRouting);
        Assert.Equal("jdai-default", savedRouting!.DefaultAgentId);
        Assert.Equal(2, savedRouting.Rules.Count);
        Assert.Contains(savedRouting.Rules, r => string.Equals(r.ChannelType, "discord", StringComparison.Ordinal) && string.Equals(r.AgentId, "jdai-default", StringComparison.Ordinal));
        Assert.Contains(savedRouting.Rules, r => string.IsNullOrEmpty(r.ChannelType) && string.IsNullOrEmpty(r.AgentId));
    }
}
