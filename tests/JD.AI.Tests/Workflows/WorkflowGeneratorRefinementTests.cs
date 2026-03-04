using FluentAssertions;
using JD.AI.Workflows;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Tests.Workflows;

public class WorkflowGeneratorRefinementTests
{
    private readonly WorkflowGenerator _generator = new();

    [Fact]
    public void Generate_ReturnsWorkflow_WithSteps()
    {
        var workflow = _generator.Generate("Clone the repo, run tests, deploy to staging");

        workflow.Should().NotBeNull();
        workflow.Steps.Should().NotBeEmpty();
        workflow.Version.Should().Be("1.0");
        workflow.Tags.Should().NotBeNull();
    }

    [Fact]
    public void Generate_DetectsLoopPatterns()
    {
        var workflow = _generator.Generate("For each file in the directory, read it and analyze it");

        workflow.Steps.Should().Contain(s => s.Kind == AgentStepKind.Loop);
    }

    [Fact]
    public void Generate_DetectsConditionalPatterns()
    {
        var workflow = _generator.Generate("Check if tests pass, then deploy");

        workflow.Steps.Should().Contain(s => s.Kind == AgentStepKind.Conditional);
    }

    [Fact]
    public void Generate_WithCustomName_UsesProvidedName()
    {
        var workflow = _generator.Generate("Build and test", "my-pipeline");

        workflow.Name.Should().Be("my-pipeline");
    }

    [Fact]
    public void ExtractFromHistory_EmptyHistory_ReturnsEmptySteps()
    {
        var messages = new List<ChatMessageContent>().AsReadOnly();

        var workflow = _generator.ExtractFromHistory(messages, 10, "test-workflow");

        workflow.Name.Should().Be("test-workflow");
        workflow.Steps.Should().BeEmpty();
    }

    [Fact]
    public void ExtractFromHistory_WithToolCalls_ExtractsSteps()
    {
        var messages = new List<ChatMessageContent>();

        // Add user message
        messages.Add(new ChatMessageContent(AuthorRole.User, "Fix the bug"));

        // Add assistant message with tool call
        var assistantMsg = new ChatMessageContent(AuthorRole.Assistant, [
            new FunctionCallContent("read_file", "file", "call1",
                new KernelArguments { ["path"] = "src/Program.cs" }),
        ]);
        messages.Add(assistantMsg);

        // Add another tool call
        var assistantMsg2 = new ChatMessageContent(AuthorRole.Assistant, [
            new FunctionCallContent("edit_file", "file", "call2",
                new KernelArguments { ["path"] = "src/Program.cs" }),
        ]);
        messages.Add(assistantMsg2);

        var workflow = _generator.ExtractFromHistory(messages.AsReadOnly(), 10, "bug-fix");

        workflow.Steps.Should().HaveCountGreaterThanOrEqualTo(2);
        workflow.Tags.Should().Contain("files");
    }

    [Fact]
    public void ExtractFromHistory_RepeatedToolCalls_CreatesLoop()
    {
        var messages = new List<ChatMessageContent>();
        messages.Add(new ChatMessageContent(AuthorRole.User, "Process all files"));

        // 4 consecutive read_file calls
        for (var i = 0; i < 4; i++)
        {
            messages.Add(new ChatMessageContent(AuthorRole.Assistant, [
                new FunctionCallContent("read_file", "file", $"call{i}",
                    new KernelArguments { ["path"] = $"file{i}.cs" }),
            ]));
        }

        var workflow = _generator.ExtractFromHistory(messages.AsReadOnly(), 10, "batch-read");

        workflow.Steps.Should().Contain(s => s.Kind == AgentStepKind.Loop);
    }

    [Fact]
    public void ExtractFromHistory_WithName_UsesCustomName()
    {
        var messages = new List<ChatMessageContent>
        {
            new(AuthorRole.User, "Hello"),
        };

        var workflow = _generator.ExtractFromHistory(messages.AsReadOnly(), 10, "custom-name");

        workflow.Name.Should().Be("custom-name");
    }

    [Fact]
    public void ExtractFromHistory_WithoutName_GeneratesTimestampName()
    {
        var messages = new List<ChatMessageContent>
        {
            new(AuthorRole.User, "Hello"),
        };

        var workflow = _generator.ExtractFromHistory(messages.AsReadOnly(), 10);

        workflow.Name.Should().StartWith("Extracted ");
    }

    [Fact]
    public void Compose_CombinesWorkflows_IntoNested()
    {
        var wf1 = _generator.Generate("Build the project", "build");
        var wf2 = _generator.Generate("Run the tests", "test");

        var composite = _generator.Compose("ci-pipeline", [wf1, wf2]);

        composite.Name.Should().Be("ci-pipeline");
        composite.Steps.Should().HaveCount(2);
        composite.Steps.Should().OnlyContain(s => s.Kind == AgentStepKind.Nested);
    }

    [Fact]
    public void DryRun_ValidatesToolAvailability()
    {
        var workflow = _generator.Generate("Read the file and grep for patterns");
        var availableTools = new HashSet<string>(StringComparer.Ordinal) { "file-read_file" };

        var result = _generator.DryRun(workflow, availableTools);

        result.WorkflowName.Should().NotBeEmpty();
        result.TotalSteps.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DryRun_WithNoTools_StillWorks()
    {
        var workflow = _generator.Generate("Think about the problem");

        var result = _generator.DryRun(workflow);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void FormatDryRun_ProducesReadableOutput()
    {
        var workflow = _generator.Generate("Clone, test, deploy");
        var result = _generator.DryRun(workflow);

        var output = WorkflowGenerator.FormatDryRun(result);

        output.Should().Contain("Dry Run");
        output.Should().Contain("Steps:");
    }
}
