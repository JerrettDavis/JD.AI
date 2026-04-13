using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Tools;

namespace JD.AI.Tests.Tools;

/// <summary>
/// Additional tests for SubagentTools focusing on static helper methods.
/// Note: SpawnAgentAsync and SpawnTeamAsync are tested via integration/BDD tests
/// that work with the full TeamOrchestrator infrastructure.
/// </summary>
public sealed class SubagentToolsAdditionalTests
{
    // ── ParseAgentConfigs - Additional Coverage ────────────────────────────

    [Fact]
    public void ParseAgentConfigs_HandlesComplexJson()
    {
        var json = """
        [
            {
                "name": "complex-agent",
                "type": "general",
                "prompt": "A very long prompt with special characters: !@#$%^&*()",
                "perspective": "analytical"
            }
        ]
        """;

        var configs = SubagentTools.ParseAgentConfigs(json);

        configs.Should().HaveCount(1);
        configs[0].Name.Should().Be("complex-agent");
        configs[0].Prompt.Should().Contain("special characters");
        configs[0].Perspective.Should().Be("analytical");
    }

    [Fact]
    public void ParseAgentConfigs_WithMissingOptionalFields()
    {
        var json = """
        [
            {
                "name": "minimal",
                "type": "task",
                "prompt": "Do something"
            }
        ]
        """;

        var configs = SubagentTools.ParseAgentConfigs(json);

        configs[0].Perspective.Should().BeNull();
    }

    [Fact]
    public void ParseAgentConfigs_WithWhitespaceInJson()
    {
        var json = """
        [
            {
                "name"   :   "agent"  ,
                "type"  :  "explore"  ,
                "prompt"  :  "test"
            }
        ]
        """;

        var configs = SubagentTools.ParseAgentConfigs(json);

        configs.Should().HaveCount(1);
        configs[0].Type.Should().Be(SubagentType.Explore);
    }

    [Fact]
    public void ParseAgentConfigs_PreservesOrder()
    {
        var json = """
        [
            {"name":"first","type":"explore","prompt":"1"},
            {"name":"second","type":"task","prompt":"2"},
            {"name":"third","type":"review","prompt":"3"}
        ]
        """;

        var configs = SubagentTools.ParseAgentConfigs(json);

        configs[0].Name.Should().Be("first");
        configs[1].Name.Should().Be("second");
        configs[2].Name.Should().Be("third");
    }

    [Fact]
    public void ParseAgentConfigs_WithEmptyPrompt()
    {
        var json = """[{"name":"x","type":"general","prompt":""}]""";

        var configs = SubagentTools.ParseAgentConfigs(json);

        configs[0].Prompt.Should().Be("");
    }

    [Fact]
    public void ParseAgentConfigs_TypeNormalization()
    {
        var testCases = new[] { "EXPLORE", "Explore", "explore", "ExPlOrE" };

        foreach (var typeStr in testCases)
        {
            var json = "[{\"name\":\"test\",\"type\":\"" + typeStr + "\",\"prompt\":\"go\"}]";
            var configs = SubagentTools.ParseAgentConfigs(json);
            configs[0].Type.Should().Be(SubagentType.Explore, $"for input: {typeStr}");
        }
    }

    [Fact]
    public void ParseAgentConfigs_AllValidSubagentTypes()
    {
        var types = new[] { "explore", "task", "plan", "review", "general" };

        foreach (var type in types)
        {
            var json = "[{\"name\":\"agent\",\"type\":\"" + type + "\",\"prompt\":\"test\"}]";
            var configs = SubagentTools.ParseAgentConfigs(json);
            var expected = Enum.Parse<SubagentType>(type, ignoreCase: true);
            configs[0].Type.Should().Be(expected);
        }
    }

    [Fact]
    public void ParseAgentConfigs_LargeArrayHandling()
    {
        var agents = string.Join(",", Enumerable.Range(0, 100)
            .Select(i => "{\"name\":\"agent-" + i + "\",\"type\":\"general\",\"prompt\":\"do " + i + "\"}"));
        var json = "[" + agents + "]";

        var configs = SubagentTools.ParseAgentConfigs(json);

        configs.Should().HaveCount(100);
        configs.Last().Name.Should().Be("agent-99");
    }

    [Fact]
    public void ParseAgentConfigs_WithNestedObjects()
    {
        var json = """
        [
            {
                "name": "nested-test",
                "type": "general",
                "prompt": "test",
                "extra": {"nested":"value"}
            }
        ]
        """;

        // Should parse without throwing, ignoring extra fields
        var configs = SubagentTools.ParseAgentConfigs(json);

        configs.Should().HaveCount(1);
    }

    [Fact]
    public void ParseAgentConfigs_InvalidJson_ThrowsJsonException()
    {
        var act = () => SubagentTools.ParseAgentConfigs("{invalid json");

        act.Should().Throw<System.Text.Json.JsonException>();
    }

    [Fact]
    public void ParseAgentConfigs_MissingRequiredField_Throws()
    {
        var json = """[{"name":"x"}]"""; // Missing type and prompt

        var act = () => SubagentTools.ParseAgentConfigs(json);

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void ParseAgentConfigs_NotAnArray_Throws()
    {
        var json = "{\"name\":\"x\",\"type\":\"task\",\"prompt\":\"go\"}"; // Object, not array

        var act = () => SubagentTools.ParseAgentConfigs(json);

        act.Should().Throw<InvalidOperationException>();
    }
}
