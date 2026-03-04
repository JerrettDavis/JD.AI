using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Gateway;

[Binding]
public sealed class AgentEndpointSteps
{
    private readonly ScenarioContext _context;
    private readonly SharedGatewaySteps _shared;

    public AgentEndpointSteps(ScenarioContext context, SharedGatewaySteps shared)
    {
        _context = context;
        _shared = shared;
    }

    [When(@"I spawn an agent with provider ""(.*)"" and model ""(.*)""")]
    public async Task WhenISpawnAnAgentWithProviderAndModel(string provider, string model)
    {
        var client = _shared.GetOrCreateClient();
        var response = await client.PostAsJsonAsync("/api/agents", new
        {
            Provider = provider,
            Model = model
        });
        _shared.StoreResponse(response);
    }

    [Given(@"I have spawned an agent with provider ""(.*)"" and model ""(.*)""")]
    public async Task GivenIHaveSpawnedAnAgentWithProviderAndModel(string provider, string model)
    {
        var client = _shared.GetOrCreateClient();
        var response = await client.PostAsJsonAsync("/api/agents", new
        {
            Provider = provider,
            Model = model
        });

        // Store the spawned agent ID for later use if the spawn was successful
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(body).RootElement;
            if (json.TryGetProperty("id", out var idProp))
                _context["SpawnedAgentId"] = idProp.GetString()!;
        }

        // Store as current response too
        _shared.StoreResponse(response);
    }

    [When(@"I send a message ""(.*)"" to agent ""(.*)""")]
    public async Task WhenISendAMessageToAgent(string message, string agentId)
    {
        var client = _shared.GetOrCreateClient();
        var response = await client.PostAsJsonAsync($"/api/agents/{agentId}/message", new
        {
            Message = message
        });
        _shared.StoreResponse(response);
    }

    [Then(@"the agents list should not be empty")]
    public async Task ThenTheAgentsListShouldNotBeEmpty()
    {
        var response = _shared.GetResponse();
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;
        json.ValueKind.Should().Be(JsonValueKind.Array);
        json.GetArrayLength().Should().BeGreaterThan(0);
    }
}
