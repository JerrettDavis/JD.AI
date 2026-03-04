using System.Net.Http.Json;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Gateway;

[Binding]
public sealed class ChannelEndpointSteps
{
    private readonly ScenarioContext _context;
    private readonly SharedGatewaySteps _shared;

    public ChannelEndpointSteps(ScenarioContext context, SharedGatewaySteps shared)
    {
        _context = context;
        _shared = shared;
    }

    [When(@"I send a channel message to ""(.*)"" with conversation ""(.*)"" and content ""(.*)""")]
    public async Task WhenISendAChannelMessageToWithConversationAndContent(
        string channelType, string conversationId, string content)
    {
        var client = _shared.GetOrCreateClient();
        var response = await client.PostAsJsonAsync($"/api/channels/{channelType}/send", new
        {
            ConversationId = conversationId,
            Content = content
        });
        _shared.StoreResponse(response);
    }
}
