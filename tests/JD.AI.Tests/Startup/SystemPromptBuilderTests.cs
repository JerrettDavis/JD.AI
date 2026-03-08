using JD.AI.Core.Agents;
using JD.AI.Startup;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Startup;

public sealed class SystemPromptBuilderTests
{
    [Fact]
    public async Task BuildAsync_UsesExplicitOverrideBeforeFilePrompt()
    {
        using var fixture = new TempDirectoryFixture();
        var systemFile = fixture.CreateFile("system.txt", "file prompt should not win");

        var options = new CliOptions
        {
            SystemPromptOverride = "override prompt",
            SystemPromptFile = systemFile,
            AppendSystemPrompt = "append tail",
        };

        var prompt = await SystemPromptBuilder.BuildAsync(options, new InstructionsResult(), planMode: false);

        Assert.StartsWith("override prompt", prompt, StringComparison.Ordinal);
        Assert.Contains("append tail", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("file prompt should not win", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_LoadsPromptAndAppendsFileAndPlanModeInstruction()
    {
        using var fixture = new TempDirectoryFixture();
        var systemFile = fixture.CreateFile("system.txt", "base prompt");
        var appendFile = fixture.CreateFile("append.txt", "append from file");

        var options = new CliOptions
        {
            SystemPromptFile = systemFile,
            AppendSystemPrompt = "append inline",
            AppendSystemPromptFile = appendFile,
        };

        var prompt = await SystemPromptBuilder.BuildAsync(options, new InstructionsResult(), planMode: true);

        Assert.Contains("base prompt", prompt, StringComparison.Ordinal);
        Assert.Contains("append inline", prompt, StringComparison.Ordinal);
        Assert.Contains("append from file", prompt, StringComparison.Ordinal);
        Assert.Contains("You are in plan mode.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_UsesDefaultPromptAndInjectsInstructions()
    {
        var instructions = new InstructionsResult();
        instructions.Add(new InstructionFile("AGENTS.md", "/tmp/AGENTS.md", "Follow AGENTS rules"));

        var prompt = await SystemPromptBuilder.BuildAsync(new CliOptions(), instructions, planMode: false);

        Assert.Contains("You are jdai, a helpful AI coding assistant", prompt, StringComparison.Ordinal);
        Assert.Contains("do not simulate tool calls in plain text", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Project Instructions (AGENTS.md)", prompt, StringComparison.Ordinal);
        Assert.Contains("Follow AGENTS rules", prompt, StringComparison.Ordinal);
    }
}
