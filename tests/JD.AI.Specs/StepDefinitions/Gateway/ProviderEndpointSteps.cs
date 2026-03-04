using System.Text.Json;
using FluentAssertions;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Gateway;

[Binding]
public sealed class ProviderEndpointSteps
{
    private readonly ScenarioContext _context;
    private readonly SharedGatewaySteps _shared;

    public ProviderEndpointSteps(ScenarioContext context, SharedGatewaySteps shared)
    {
        _context = context;
        _shared = shared;
    }

    [Then(@"each provider should have a name property")]
    public async Task ThenEachProviderShouldHaveANameProperty()
    {
        var response = _shared.GetResponse();
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;

        json.ValueKind.Should().Be(JsonValueKind.Array);

        foreach (var provider in json.EnumerateArray())
        {
            provider.TryGetProperty("name", out _).Should().BeTrue(
                "each provider in the list should have a 'name' property");
        }
    }
}
