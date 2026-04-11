using System.Net;
using JD.AI.Dashboard.Wasm.Models;
using JD.AI.Dashboard.Wasm.Services;
using FluentAssertions;

namespace JD.AI.Tests.Dashboard;

public sealed class GatewayApiClientAgentTests : DashboardBunitTestContext
{
    [Fact]
    public async Task GetAgentDetailAsync_ReturnsDetail()
    {
        var client = CreateApiClient(req =>
        {
            req.RequestUri!.ToString().Should().Contain("api/v1/agents/agent-1");
            return JsonResponse("""
                {"id":"agent-1","provider":"openai","model":"gpt-5",
                 "systemPrompt":"You are helpful","isDefault":false,
                 "tools":[{"name":"web_search","description":"Search","isAllowed":true}],
                 "assignedSkills":[]}
                """);
        });
        var detail = await client.GetAgentDetailAsync("agent-1");
        detail.Should().NotBeNull();
        detail!.Tools.Should().HaveCount(1);
    }

    [Fact]
    public async Task SetDefaultAgentAsync_PostsToCorrectEndpoint()
    {
        var called = false;
        var client = CreateApiClient(req =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.ToString().Contains("default"))
                called = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await client.SetDefaultAgentAsync("agent-1");
        called.Should().BeTrue();
    }
}
