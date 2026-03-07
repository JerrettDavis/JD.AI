using FluentAssertions;
using JD.AI.Core.Agents;

namespace JD.AI.Tests.Agents;

public sealed class AgentDefinitionModelTests
{
    [Fact]
    public void AgentDefinition_DefaultValues()
    {
        var def = new AgentDefinition();
        def.Name.Should().Be(string.Empty);
        def.DisplayName.Should().BeNull();
        def.Description.Should().BeNull();
        def.Version.Should().Be("1.0");
        def.Model.Should().BeNull();
        def.Loadout.Should().BeNull();
        def.SystemPrompt.Should().BeNull();
        def.Workflows.Should().BeEmpty();
        def.Tags.Should().BeEmpty();
        def.IsDeprecated.Should().BeFalse();
        def.MigrationGuidance.Should().BeNull();
        def.Environment.Should().BeNull();
        def.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        def.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AgentDefinition_CustomValues()
    {
        var def = new AgentDefinition
        {
            Name = "pr-reviewer",
            DisplayName = "PR Reviewer",
            Description = "Reviews PRs",
            Version = "2.1",
            Loadout = "developer",
            SystemPrompt = "You are a reviewer",
            IsDeprecated = true,
            MigrationGuidance = "Use v3",
            Environment = "production",
        };

        def.Name.Should().Be("pr-reviewer");
        def.DisplayName.Should().Be("PR Reviewer");
        def.Version.Should().Be("2.1");
        def.IsDeprecated.Should().BeTrue();
        def.MigrationGuidance.Should().Be("Use v3");
        def.Environment.Should().Be("production");
    }

    [Fact]
    public void AgentDefinition_WorkflowsAndTags_AreMutable()
    {
        var def = new AgentDefinition();
        def.Workflows.Add("wf1");
        def.Tags.Add("tag1");

        def.Workflows.Should().ContainSingle().Which.Should().Be("wf1");
        def.Tags.Should().ContainSingle().Which.Should().Be("tag1");
    }

    [Fact]
    public void AgentModelSpec_AllNullByDefault()
    {
        var spec = new AgentModelSpec();
        spec.Provider.Should().BeNull();
        spec.Id.Should().BeNull();
        spec.MaxOutputTokens.Should().BeNull();
        spec.Temperature.Should().BeNull();
    }

    [Fact]
    public void AgentModelSpec_CanSetAllProperties()
    {
        var spec = new AgentModelSpec
        {
            Provider = "Anthropic",
            Id = "claude-opus-4",
            MaxOutputTokens = 8096,
            Temperature = 0.7,
        };

        spec.Provider.Should().Be("Anthropic");
        spec.Id.Should().Be("claude-opus-4");
        spec.MaxOutputTokens.Should().Be(8096);
        spec.Temperature.Should().Be(0.7);
    }

    [Fact]
    public void AgentDefinition_WithModelSpec()
    {
        var def = new AgentDefinition
        {
            Name = "agent-with-model",
            Model = new AgentModelSpec
            {
                Provider = "OpenAI",
                Id = "gpt-4o",
            },
        };

        def.Model.Should().NotBeNull();
        def.Model!.Provider.Should().Be("OpenAI");
    }
}
