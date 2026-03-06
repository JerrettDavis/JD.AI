using System.Text.Json;
using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Orchestration;
using JD.AI.Tools;

namespace JD.AI.Tests.Tools;

public sealed class SubagentToolsTests
{
    // ── ParseAgentConfigs ────────────────────────────────────────────────

    [Fact]
    public void ParseAgentConfigs_SingleAgent()
    {
        var json = """[{"name":"alpha","type":"explore","prompt":"Find bugs"}]""";
        var configs = SubagentTools.ParseAgentConfigs(json);

        configs.Should().HaveCount(1);
        configs[0].Name.Should().Be("alpha");
        configs[0].Type.Should().Be(SubagentType.Explore);
        configs[0].Prompt.Should().Be("Find bugs");
        configs[0].Perspective.Should().BeNull();
    }

    [Fact]
    public void ParseAgentConfigs_MultipleAgents()
    {
        var json = """
        [
            {"name":"a","type":"task","prompt":"Run tests"},
            {"name":"b","type":"review","prompt":"Check code"},
            {"name":"c","type":"general","prompt":"Summarize"}
        ]
        """;
        var configs = SubagentTools.ParseAgentConfigs(json);

        configs.Should().HaveCount(3);
        configs[0].Type.Should().Be(SubagentType.Task);
        configs[1].Type.Should().Be(SubagentType.Review);
        configs[2].Type.Should().Be(SubagentType.General);
    }

    [Fact]
    public void ParseAgentConfigs_WithPerspective()
    {
        var json = """[{"name":"debater","type":"general","prompt":"Argue for","perspective":"optimistic"}]""";
        var configs = SubagentTools.ParseAgentConfigs(json);

        configs[0].Perspective.Should().Be("optimistic");
    }

    [Fact]
    public void ParseAgentConfigs_UnknownType_DefaultsToGeneral()
    {
        var json = """[{"name":"x","type":"unknown_type","prompt":"Do something"}]""";
        var configs = SubagentTools.ParseAgentConfigs(json);

        configs[0].Type.Should().Be(SubagentType.General);
    }

    [Fact]
    public void ParseAgentConfigs_CaseInsensitiveType()
    {
        var json = """[{"name":"x","type":"EXPLORE","prompt":"Search"}]""";
        var configs = SubagentTools.ParseAgentConfigs(json);

        configs[0].Type.Should().Be(SubagentType.Explore);
    }

    [Fact]
    public void ParseAgentConfigs_EmptyArray_ReturnsEmpty()
    {
        var configs = SubagentTools.ParseAgentConfigs("[]");
        configs.Should().BeEmpty();
    }

    [Fact]
    public void ParseAgentConfigs_InvalidJson_Throws()
    {
        var act = () => SubagentTools.ParseAgentConfigs("not json");
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void ParseAgentConfigs_MissingRequiredField_Throws()
    {
        var json = """[{"name":"x"}]""";
        var act = () => SubagentTools.ParseAgentConfigs(json);
        act.Should().Throw<KeyNotFoundException>();
    }

    [Theory]
    [InlineData("explore")]
    [InlineData("task")]
    [InlineData("plan")]
    [InlineData("review")]
    [InlineData("general")]
    public void ParseAgentConfigs_AllSubagentTypes(string typeStr)
    {
        var json = "[{\"name\":\"a\",\"type\":\"" + typeStr + "\",\"prompt\":\"test\"}]";
        var configs = SubagentTools.ParseAgentConfigs(json);
        Enum.TryParse<SubagentType>(typeStr, ignoreCase: true, out var expected).Should().BeTrue();
        configs[0].Type.Should().Be(expected);
    }

    [Fact]
    public void ParseAgentConfigs_NullNameDefaultsToAgent()
    {
        var json = """[{"name":null,"type":"task","prompt":"test"}]""";
        var configs = SubagentTools.ParseAgentConfigs(json);
        configs[0].Name.Should().Be("agent");
    }

    [Fact]
    public void ParseAgentConfigs_NullPromptDefaultsToEmpty()
    {
        var json = """[{"name":"x","type":"task","prompt":null}]""";
        var configs = SubagentTools.ParseAgentConfigs(json);
        configs[0].Prompt.Should().BeEmpty();
    }
}
