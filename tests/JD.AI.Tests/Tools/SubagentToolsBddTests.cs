using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Orchestration;
using JD.AI.Tools;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Tools;

[Feature("Subagent Tools")]
public sealed class SubagentToolsBddTests : TinyBddXunitBase
{
    public SubagentToolsBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Valid JSON with all fields populates correctly"), Fact]
    public async Task ValidJson_AllFields()
    {
        List<SubagentConfig>? configs = null;
        var json = """
            [{"name":"analyzer","type":"explore","prompt":"Analyze the code","perspective":"optimist"}]
            """;

        await Given("valid JSON with all fields", () => json)
            .When("parsing agent configs", j =>
            {
                configs = SubagentTools.ParseAgentConfigs(j);
                return j;
            })
            .Then("all fields are populated correctly", _ =>
                configs!.Count == 1 &&
                string.Equals(configs[0].Name, "analyzer", StringComparison.Ordinal) &&
                configs[0].Type == SubagentType.Explore &&
                string.Equals(configs[0].Prompt, "Analyze the code", StringComparison.Ordinal) &&
                string.Equals(configs[0].Perspective, "optimist", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Valid JSON with type 'explore' parses as Explore"), Fact]
    public async Task ExploreType_Parsed()
    {
        List<SubagentConfig>? configs = null;
        var json = """[{"name":"scout","type":"explore","prompt":"Find bugs"}]""";

        await Given("valid JSON with explore type", () => json)
            .When("parsing agent configs", j =>
            {
                configs = SubagentTools.ParseAgentConfigs(j);
                return j;
            })
            .Then("type is Explore", _ => configs![0].Type == SubagentType.Explore)
            .AssertPassed();
    }

    [Scenario("Valid JSON with type 'general' parses as General"), Fact]
    public async Task GeneralType_Parsed()
    {
        List<SubagentConfig>? configs = null;
        var json = """[{"name":"helper","type":"general","prompt":"Help me"}]""";

        await Given("valid JSON with general type", () => json)
            .When("parsing agent configs", j =>
            {
                configs = SubagentTools.ParseAgentConfigs(j);
                return j;
            })
            .Then("type is General", _ => configs![0].Type == SubagentType.General)
            .AssertPassed();
    }

    [Scenario("Unknown type defaults to General"), Fact]
    public async Task UnknownType_DefaultsToGeneral()
    {
        List<SubagentConfig>? configs = null;
        var json = """[{"name":"agent1","type":"xyz","prompt":"Do something"}]""";

        await Given("valid JSON with unknown type 'xyz'", () => json)
            .When("parsing agent configs", j =>
            {
                configs = SubagentTools.ParseAgentConfigs(j);
                return j;
            })
            .Then("type defaults to General", _ => configs![0].Type == SubagentType.General)
            .AssertPassed();
    }

    [Scenario("Perspective field parsed correctly"), Fact]
    public async Task PerspectiveField_Parsed()
    {
        List<SubagentConfig>? configs = null;
        var json = """[{"name":"debater","type":"general","prompt":"Argue","perspective":"skeptic"}]""";

        await Given("valid JSON with perspective field", () => json)
            .When("parsing agent configs", j =>
            {
                configs = SubagentTools.ParseAgentConfigs(j);
                return j;
            })
            .Then("perspective is parsed correctly", _ => string.Equals(configs![0].Perspective, "skeptic", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Missing perspective field results in null"), Fact]
    public async Task MissingPerspective_IsNull()
    {
        List<SubagentConfig>? configs = null;
        var json = """[{"name":"worker","type":"task","prompt":"Run tests"}]""";

        await Given("valid JSON without perspective field", () => json)
            .When("parsing agent configs", j =>
            {
                configs = SubagentTools.ParseAgentConfigs(j);
                return j;
            })
            .Then("perspective is null", _ => configs![0].Perspective is null)
            .AssertPassed();
    }

    [Scenario("JSON array with multiple agents returns correct count"), Fact]
    public async Task MultipleAgents_CorrectCount()
    {
        List<SubagentConfig>? configs = null;
        var json = """
            [
                {"name":"a1","type":"explore","prompt":"Task 1"},
                {"name":"a2","type":"task","prompt":"Task 2"},
                {"name":"a3","type":"general","prompt":"Task 3"}
            ]
            """;

        await Given("JSON array with 3 agents", () => json)
            .When("parsing agent configs", j =>
            {
                configs = SubagentTools.ParseAgentConfigs(j);
                return j;
            })
            .Then("returns 3 configs", _ => configs!.Count == 3)
            .AssertPassed();
    }

    [Scenario("Empty JSON array returns empty list"), Fact]
    public async Task EmptyJsonArray_ReturnsEmpty()
    {
        List<SubagentConfig>? configs = null;

        await Given("empty JSON array", () => "[]")
            .When("parsing agent configs", j =>
            {
                configs = SubagentTools.ParseAgentConfigs(j);
                return j;
            })
            .Then("returns empty list", _ => configs!.Count == 0)
            .AssertPassed();
    }

    [Scenario("JSON with missing prompt defaults to empty string"), Fact]
    public async Task MissingPrompt_DefaultsToEmpty()
    {
        List<SubagentConfig>? configs = null;
        var json = """[{"name":"agent","type":"general","prompt":null}]""";

        await Given("JSON with null prompt", () => json)
            .When("parsing agent configs", j =>
            {
                configs = SubagentTools.ParseAgentConfigs(j);
                return j;
            })
            .Then("prompt defaults to empty string", _ => configs![0].Prompt != null && configs![0].Prompt.Length == 0)
            .AssertPassed();
    }

    [Scenario("JSON with missing name defaults to 'agent'"), Fact]
    public async Task MissingName_DefaultsToAgent()
    {
        List<SubagentConfig>? configs = null;
        var json = """[{"name":null,"type":"general","prompt":"Do work"}]""";

        await Given("JSON with null name", () => json)
            .When("parsing agent configs", j =>
            {
                configs = SubagentTools.ParseAgentConfigs(j);
                return j;
            })
            .Then("name defaults to 'agent'", _ => string.Equals(configs![0].Name, "agent", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("QueryTeamContext returns fallback message containing the key"), Fact]
    public async Task QueryTeamContext_ReturnsFallbackWithKey()
    {
        string? result = null;
        var key = "results";

        await Given("a SubagentTools instance", () =>
            {
                // TeamOrchestrator requires complex dependencies,
                // but QueryTeamContext doesn't use it
                return key;
            })
            .When("querying team context", k =>
            {
                // We create the tools with a null-forgiving orchestrator
                // since QueryTeamContext doesn't use _orchestrator
                var tools = new SubagentTools(null!);
                result = tools.QueryTeamContext(k);
                return k;
            })
            .Then("returns fallback message containing the key", _ =>
                result!.Contains(key, StringComparison.Ordinal))
            .AssertPassed();
    }
}
