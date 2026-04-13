using FluentAssertions;
using JD.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using Xunit;

namespace JD.AI.Tests.Workflows;

public sealed class WorkflowGeneratorAdditionalTests
{
    private static Kernel CreateMockKernel(string chatResponse = "mocked response")
    {
        var chatService = Substitute.For<IChatCompletionService>();
        chatService.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings?>(),
                Arg.Any<Kernel?>(),
                Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, chatResponse)]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        return builder.Build();
    }

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var generator = new WorkflowGenerator();
        generator.Should().NotBeNull();
    }

    [Fact]
    public void Generate_WithDescription_ReturnsDefinition()
    {
        var generator = new WorkflowGenerator();
        var definition = generator.Generate("analyze the input data");

        definition.Should().NotBeNull();
        definition.Name.Should().NotBeEmpty();
        definition.Version.Should().NotBeEmpty();
    }

    [Fact]
    public void Generate_WithName_UsesProvidedName()
    {
        var generator = new WorkflowGenerator();
        var definition = generator.Generate("do something", "custom-workflow");

        definition.Name.Should().Be("custom-workflow");
    }

    [Fact]
    public void Generate_WithEmptyDescription_StillReturnsDefinition()
    {
        var generator = new WorkflowGenerator();
        var definition = generator.Generate("");

        definition.Should().NotBeNull();
        definition.Name.Should().NotBeEmpty();
    }

    [Fact]
    public void Generate_WithLongDescription_TruncatesDescription()
    {
        var generator = new WorkflowGenerator();
        var longDesc = new string('a', 500);
        var definition = generator.Generate(longDesc);

        definition.Description.Length.Should().BeLessThanOrEqualTo(203); // 200 + "..."
    }

    [Fact]
    public void DryRun_WithValidWorkflow_ReturnsResult()
    {
        var generator = new WorkflowGenerator();
        var workflow = new AgentWorkflowDefinition
        {
            Name = "test",
            Steps = [AgentStepDefinition.RunSkill("skill1")],
        };

        var result = generator.DryRun(workflow);

        result.Should().NotBeNull();
        result.WorkflowName.Should().Be("test");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DryRun_WithMissingTools_IdentifiesMissing()
    {
        var generator = new WorkflowGenerator();
        var workflow = new AgentWorkflowDefinition
        {
            Name = "test",
            Steps =
            [
                new AgentStepDefinition
                {
                    Name = "step1",
                    Kind = AgentStepKind.Tool,
                    Target = "unknown.tool",
                },
            ],
        };

        var availableTools = new HashSet<string> { "other.tool" };
        var result = generator.DryRun(workflow, availableTools);

        result.Should().NotBeNull();
    }

    [Fact]
    public void DryRun_WithEmptyWorkflow_ReturnsZeroSteps()
    {
        var generator = new WorkflowGenerator();
        var workflow = new AgentWorkflowDefinition
        {
            Name = "empty",
            Steps = [],
        };

        var result = generator.DryRun(workflow);

        result.TotalSteps.Should().Be(0);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Compose_WithMultipleWorkflows_CreateComposite()
    {
        var generator = new WorkflowGenerator();
        var workflows = new[]
        {
            new AgentWorkflowDefinition
            {
                Name = "wf1",
                Steps = [AgentStepDefinition.RunSkill("skill1")],
            },
            new AgentWorkflowDefinition
            {
                Name = "wf2",
                Steps = [AgentStepDefinition.RunSkill("skill2")],
            },
        };

        var composite = generator.Compose("composite", workflows);

        composite.Should().NotBeNull();
        composite.Name.Should().Be("composite");
        composite.Steps.Should().HaveCount(2);
    }

    [Fact]
    public void Compose_WithEmptyList_ReturnsValidComposite()
    {
        var generator = new WorkflowGenerator();
        var composite = generator.Compose("empty-composite", []);

        composite.Should().NotBeNull();
        composite.Name.Should().Be("empty-composite");
        composite.Steps.Should().BeEmpty();
    }

    [Fact]
    public void Compose_MergesTags()
    {
        var generator = new WorkflowGenerator();
        var workflows = new[]
        {
            new AgentWorkflowDefinition
            {
                Name = "wf1",
                Tags = ["tag1", "tag2"],
                Steps = [],
            },
            new AgentWorkflowDefinition
            {
                Name = "wf2",
                Tags = ["tag2", "tag3"],
                Steps = [],
            },
        };

        var composite = generator.Compose("composite", workflows);

        composite.Tags.Should().Contain(new[] { "tag1", "tag2", "tag3" });
    }

    [Fact]
    public void FormatDryRun_WithValidResult_ReturnsString()
    {
        var result = new WorkflowDryRunResult
        {
            WorkflowName = "test",
            Version = "1.0",
            TotalSteps = 2,
            Steps =
            [
                new DryRunStep
                {
                    Name = "step1",
                    Kind = AgentStepKind.Skill,
                    ToolOrTarget = "skill",
                    Description = "Run a skill",
                    SubSteps = [],
                },
            ],
            MissingTools = [],
            Warnings = [],
            IsValid = true,
        };

        var formatted = WorkflowGenerator.FormatDryRun(result);

        formatted.Should().Contain("test");
        formatted.Should().Contain("1.0");
    }

    [Fact]
    public void FormatDryRun_WithWarnings_IncludesWarnings()
    {
        var result = new WorkflowDryRunResult
        {
            WorkflowName = "test",
            Version = "1.0",
            TotalSteps = 1,
            Steps = [],
            MissingTools = [],
            Warnings = ["Warning 1", "Warning 2"],
            IsValid = false,
        };

        var formatted = WorkflowGenerator.FormatDryRun(result);

        formatted.Should().Contain("Warning 1");
        formatted.Should().Contain("Warning 2");
    }

    [Fact]
    public async Task GenerateAsync_WithMockKernel_ReturnsResult()
    {
        var kernel = CreateMockKernel(
            """
            {
              "name": "test-workflow",
              "version": "1.0",
              "description": "Test workflow",
              "tags": ["test"],
              "steps": []
            }
            """);

        var generator = new WorkflowGenerator();
        var result = await generator.GenerateAsync(
            "create a workflow",
            kernel,
            ct: CancellationToken.None);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_WithInvalidJson_ReturnsFallback()
    {
        var kernel = CreateMockKernel("not valid json");

        var generator = new WorkflowGenerator();
        var result = await generator.GenerateAsync(
            "create workflow",
            kernel,
            ct: CancellationToken.None);

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_WithMarkdownFences_StripsFences()
    {
        var kernel = CreateMockKernel(
            """
            ```json
            {
              "name": "test",
              "version": "1.0",
              "description": "desc",
              "tags": [],
              "steps": []
            }
            ```
            """);

        var generator = new WorkflowGenerator();
        var result = await generator.GenerateAsync(
            "test",
            kernel,
            ct: CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_WithName_UsesProvidedName()
    {
        var kernel = CreateMockKernel(
            """
            {
              "name": "ignored-name",
              "version": "1.0",
              "description": "Test",
              "tags": [],
              "steps": []
            }
            """);

        var generator = new WorkflowGenerator();
        var result = await generator.GenerateAsync(
            "test",
            kernel,
            "override-name",
            ct: CancellationToken.None);

        result.Workflow.Name.Should().Be("override-name");
    }

    [Fact]
    public async Task GenerateAsync_WithAvailableTools_IncludesInPrompt()
    {
        var kernel = CreateMockKernel(
            """
            {
              "name": "test",
              "version": "1.0",
              "description": "Test",
              "tags": [],
              "steps": []
            }
            """);

        var generator = new WorkflowGenerator();
        var tools = new HashSet<string> { "tool1", "tool2" };

        var result = await generator.GenerateAsync(
            "test",
            kernel,
            availableTools: tools,
            ct: CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RefineAsync_WithValidWorkflow_ReturnsRefined()
    {
        var kernel = CreateMockKernel(
            """
            {
              "name": "refined",
              "version": "1.1",
              "description": "Refined workflow",
              "tags": ["refined"],
              "steps": [],
              "changelog": "Added refinement"
            }
            """);

        var workflow = new AgentWorkflowDefinition
        {
            Name = "original",
            Version = "1.0",
            Steps = [],
        };

        var generator = new WorkflowGenerator();
        var result = await generator.RefineAsync(
            workflow,
            "add a step",
            kernel,
            CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RefineAsync_WithInvalidResponse_ReturnsFallback()
    {
        var kernel = CreateMockKernel("invalid response");

        var workflow = new AgentWorkflowDefinition
        {
            Name = "test",
            Version = "1.0",
            Steps = [],
        };

        var generator = new WorkflowGenerator();
        var result = await generator.RefineAsync(
            workflow,
            "change something",
            kernel,
            CancellationToken.None);

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RefineAsync_PreservesOriginalOnError()
    {
        var kernel = CreateMockKernel("bad json {{{");

        var workflow = new AgentWorkflowDefinition
        {
            Name = "original",
            Version = "1.0",
            Description = "Original description",
            Steps = [AgentStepDefinition.RunSkill("skill1")],
        };

        var generator = new WorkflowGenerator();
        var result = await generator.RefineAsync(
            workflow,
            "refine",
            kernel,
            CancellationToken.None);

        result.Workflow.Name.Should().Be("original");
    }
}
