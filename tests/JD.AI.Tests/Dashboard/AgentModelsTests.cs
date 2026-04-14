using FluentAssertions;
using JD.AI.Dashboard.Wasm.Models;

namespace JD.AI.Tests.Dashboard;

public sealed class AgentModelsTests
{
    [Fact]
    public void AgentDetailInfo_DefaultsAreEmpty()
    {
        var detail = new AgentDetailInfo();
        detail.Id.Should().BeEmpty();
        detail.Tools.Should().BeEmpty();
        detail.AssignedSkills.Should().BeEmpty();
    }

    [Fact]
    public void SkillInfo_StatusIsReadonly()
    {
        var skill = new SkillInfo { Name = "github", Status = SkillStatus.Ready };
        skill.Name.Should().Be("github");
        skill.Status.Should().Be(SkillStatus.Ready);
    }

    [Fact]
    public void ToolInfo_HasNameAndDescription()
    {
        var tool = new ToolInfo { Name = "web_search", Description = "Search the web" };
        tool.Name.Should().Be("web_search");
        tool.Description.Should().Be("Search the web");
    }
}
