using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Gateway;

[Binding]
public sealed class RoutingEndpointSteps
{
    private readonly ScenarioContext _context;
    private readonly SharedGatewaySteps _shared;

    public RoutingEndpointSteps(ScenarioContext context, SharedGatewaySteps shared)
    {
        _context = context;
        _shared = shared;
    }

    [When(@"I map channel ""(.*)"" to agent ""(.*)""")]
    public async Task WhenIMapChannelToAgent(string channelId, string agentId)
    {
        var client = _shared.GetOrCreateClient();
        var response = await client.PostAsJsonAsync("/api/routing/map", new
        {
            ChannelId = channelId,
            AgentId = agentId
        });
        _shared.StoreResponse(response);
    }

    [Given(@"I have mapped channel ""(.*)"" to agent ""(.*)""")]
    public async Task GivenIHaveMappedChannelToAgent(string channelId, string agentId)
    {
        var client = _shared.GetOrCreateClient();
        await client.PostAsJsonAsync("/api/routing/map", new
        {
            ChannelId = channelId,
            AgentId = agentId
        });
    }

    [Then(@"the routing mappings should contain channel ""(.*)""")]
    public async Task ThenTheRoutingMappingsShouldContainChannel(string channelId)
    {
        var response = _shared.GetResponse();
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;

        // The mappings endpoint returns a JSON object with channel IDs as keys
        json.ValueKind.Should().Be(JsonValueKind.Object);
        json.TryGetProperty(channelId, out _).Should().BeTrue(
            $"expected routing mappings to contain channel '{channelId}'");
    }
}
