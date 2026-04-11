using System.Net;
using JD.AI.Dashboard.Wasm.Models;
using JD.AI.Dashboard.Wasm.Services;
using FluentAssertions;

namespace JD.AI.Tests.Dashboard;

public sealed class GatewayApiClientSkillTests : DashboardBunitTestContext
{
    [Fact]
    public async Task GetSkillsAsync_ReturnsSkillArray()
    {
        var client = CreateApiClient(req =>
        {
            req.RequestUri!.ToString().Should().Contain("api/v1/skills");
            return JsonResponse("""
                [{"id":"github","name":"github","emoji":"\uD83D\uDC19",
                  "description":"GitHub ops","category":"Development",
                  "status":"Ready","enabled":true}]
                """);
        });
        var skills = await client.GetSkillsAsync();
        skills.Should().HaveCount(1);
        skills[0].Name.Should().Be("github");
    }

    [Fact]
    public async Task ToggleSkillAsync_PostsEnableEndpoint()
    {
        var calledPath = "";
        var client = CreateApiClient(req =>
        {
            calledPath = req.RequestUri!.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await client.ToggleSkillAsync("github", true);
        calledPath.Should().Contain("enable");
    }

    [Fact]
    public async Task ToggleSkillAsync_PostsDisableEndpoint()
    {
        var calledPath = "";
        var client = CreateApiClient(req =>
        {
            calledPath = req.RequestUri!.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await client.ToggleSkillAsync("github", false);
        calledPath.Should().Contain("disable");
    }
}
