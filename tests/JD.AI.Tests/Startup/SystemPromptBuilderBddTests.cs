using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Startup;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Startup;

[Feature("System Prompt Builder")]
public sealed class SystemPromptBuilderBddTests : TinyBddXunitBase
{
    public SystemPromptBuilderBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Override prompt is used and appended text is preserved"), Fact]
    public async Task OverridePrompt_IsUsed_AndAppendedTextIsPreserved()
    {
        string? result = null;
        await Given("CLI options with a direct system prompt override and append text", () =>
            {
                var opts = new CliOptions
                {
                    SystemPromptOverride = "base override prompt",
                    AppendSystemPrompt = "append this text",
                };
                return (opts, instructions: new InstructionsResult(), planMode: false);
            })
            .When("building the system prompt", async Task (ctx) =>
            {
                result = await SystemPromptBuilder.BuildAsync(
                    ctx.opts,
                    ctx.instructions,
                    ctx.planMode);
            })
            .Then("the override is the base prompt and append text is included", _ =>
            {
                result.Should().NotBeNull();
                result.Should().StartWith("base override prompt");
                result.Should().Contain("append this text");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Prompt file and append file are merged with plan mode guidance"), Fact]
    public async Task PromptAndAppendFiles_AreMerged_WithPlanModeGuidance()
    {
        string? result = null;
        string? root = null;

        await Given("CLI options with prompt files and plan mode enabled", () =>
            {
                root = Path.Combine(Path.GetTempPath(), $"jdai-prompt-{Guid.NewGuid():N}");
                Directory.CreateDirectory(root);

                var basePromptPath = Path.Combine(root, "base.txt");
                var appendPromptPath = Path.Combine(root, "append.txt");

                File.WriteAllText(basePromptPath, "prompt-from-file");
                File.WriteAllText(appendPromptPath, "append-from-file");

                var opts = new CliOptions
                {
                    SystemPromptFile = basePromptPath,
                    AppendSystemPromptFile = appendPromptPath,
                };
                return (opts, instructions: new InstructionsResult(), planMode: true);
            })
            .When("building the prompt from files", async Task (ctx) =>
            {
                result = await SystemPromptBuilder.BuildAsync(
                    ctx.opts,
                    ctx.instructions,
                    ctx.planMode);
            })
            .Then("both files and plan-mode instruction are present", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("prompt-from-file");
                result.Should().Contain("append-from-file");
                result.Should().Contain("You are in plan mode.");
                return true;
            })
            .AssertPassed();

        if (root is not null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Scenario("Default prompt includes project instructions when available"), Fact]
    public async Task DefaultPrompt_IncludesProjectInstructions_WhenAvailable()
    {
        string? result = null;
        await Given("project instructions are loaded", () =>
            {
                var instructions = new InstructionsResult();
                instructions.Add(new InstructionFile(
                    "AGENTS.md",
                    "/tmp/AGENTS.md",
                    "Respect repository guardrails."));
                return (opts: new CliOptions(), instructions, planMode: false);
            })
            .When("building the default system prompt", async Task (ctx) =>
            {
                result = await SystemPromptBuilder.BuildAsync(
                    ctx.opts,
                    ctx.instructions,
                    ctx.planMode);
            })
            .Then("default assistant guidance and instruction payload are both included", _ =>
            {
                result.Should().NotBeNull();
                result.Should().Contain("You are jdai, a helpful AI coding assistant");
                result.Should().ContainEquivalentOf("do not simulate tool calls in plain text");
                result.Should().Contain("# Project Instructions (AGENTS.md)");
                result.Should().Contain("Respect repository guardrails.");
                return true;
            })
            .AssertPassed();
    }
}
