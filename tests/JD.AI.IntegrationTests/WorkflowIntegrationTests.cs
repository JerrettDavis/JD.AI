using FluentAssertions;
using JD.AI.Workflows;
using JD.AI.Workflows.Steps;
using Microsoft.SemanticKernel;
using WorkflowFramework;
using Xunit;

namespace JD.AI.IntegrationTests;

/// <summary>
/// Integration tests that execute WorkflowFramework workflows against Ollama.
/// Gated behind <c>JDAI_INTEGRATION_TESTS=true</c> and Ollama availability.
/// </summary>
public class WorkflowIntegrationTests : IAsyncLifetime
{
    private Kernel _kernel = null!;

    public async Task InitializeAsync()
    {
        await IntegrationTestGuard.EnsureOllamaAsync().ConfigureAwait(false);
        _kernel = IntegrationTestFactories.CreateOllamaWorkflowKernel();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Detection ────────────────────────────────────────────────────────

    [SkippableFact]
    public void Detector_Identifies_WorkflowWorthy_Requests()
    {
        IntegrationTestGuard.EnsureEnabled();
        var detector = new AgentWorkflowDetector();

        detector.IsWorkflowRequired(new AgentRequest("implement a user login feature with JWT tokens"))
            .Should().BeTrue("contains 'implement' keyword and is substantive");

        detector.IsWorkflowRequired(new AgentRequest("hello"))
            .Should().BeFalse("too short");

        detector.IsWorkflowRequired(new AgentRequest("what is the weather like today in the city center"))
            .Should().BeFalse("no workflow keywords");
    }

    // ── Execution ────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task SingleStep_Workflow_Executes_Against_Ollama()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "single-step",
            Version = "1.0",
            Steps = [AgentStepDefinition.RunSkill("summarize")],
            Tags = ["summarize"],
        };

        // Override the skill prompt to something simple
        var workflow = Workflow.Create<AgentWorkflowData>("single-step")
            .Step(new RunSkillStep("summarize", "Summarize in one sentence: {prompt}"))
            .Build();

        var builder = new AgentWorkflowBuilder(_kernel);
        var data = builder.CreateData("The quick brown fox jumps over the lazy dog.");

        var ctx = new WorkflowContext<AgentWorkflowData>(data);
        var result = await workflow.ExecuteAsync(ctx).ConfigureAwait(false);

        result.IsSuccess.Should().BeTrue();
        result.Data.StepOutputs.Should().ContainKey("summarize");
        result.Data.StepOutputs["summarize"].Should().NotBeNullOrWhiteSpace();
        result.Data.FinalResult.Should().NotBeNullOrWhiteSpace();
    }

    // ── Multi-step pipeline (isolation & data flow) ─────────────────────

    [SkippableFact]
    public async Task MultiStep_Pipeline_Passes_Data_Between_Steps()
    {
        var workflow = Workflow.Create<AgentWorkflowData>("multi-step")
            .Step(new RunSkillStep("analyze", "List three key points about: {prompt}"))
            .Step(new RunSkillStep("summarize", "Summarize these key points in one sentence: {previous}"))
            .Build();

        var builder = new AgentWorkflowBuilder(_kernel);
        var data = builder.CreateData("Functional programming emphasizes immutability and pure functions.");

        var ctx = new WorkflowContext<AgentWorkflowData>(data);
        var result = await workflow.ExecuteAsync(ctx).ConfigureAwait(false);

        result.IsSuccess.Should().BeTrue();
        result.Data.StepOutputs.Should().ContainKey("analyze");
        result.Data.StepOutputs.Should().ContainKey("summarize");

        // The second step should have different content since it summarized the first step's output
        result.Data.StepOutputs["summarize"].Should().NotBeNullOrWhiteSpace();
    }

    // ── Event capture (observability) ───────────────────────────────────

    [SkippableFact]
    public async Task Capture_Records_Step_Events_During_Execution()
    {
        var capture = new WorkflowExecutionCapture();

        var workflow = Workflow.Create<AgentWorkflowData>("captured")
            .Step(new RunSkillStep("greet", "Say hello in one word: {prompt}"))
            .WithEvents(capture)
            .Build();

        var builder = new AgentWorkflowBuilder(_kernel);
        var data = builder.CreateData("world");

        var ctx = new WorkflowContext<AgentWorkflowData>(data);
        await workflow.ExecuteAsync(ctx).ConfigureAwait(false);

        capture.CompletedCount.Should().BeGreaterThanOrEqualTo(1);
        capture.FailedCount.Should().Be(0);

        var events = capture.Events;
        events.Should().Contain(e => e.StepName == "greet" && e.Kind == StepEventKind.Started);
        events.Should().Contain(e => e.StepName == "greet" && e.Kind == StepEventKind.Completed);
    }

    // ── Builder from definition ─────────────────────────────────────────

    [SkippableFact]
    public async Task Builder_Creates_Executable_Workflow_From_Definition()
    {
        var definition = new AgentWorkflowDefinition
        {
            Name = "builder-test",
            Version = "1.0",
            Steps =
            [
                AgentStepDefinition.RunSkill("Reply with exactly 'OK': {prompt}"),
            ],
        };

        var builder = new AgentWorkflowBuilder(_kernel);
        var capture = new WorkflowExecutionCapture();
        var workflow = builder.BuildWithCapture(definition, capture);
        var data = builder.CreateData("test");

        var ctx = new WorkflowContext<AgentWorkflowData>(data);
        var result = await workflow.ExecuteAsync(ctx).ConfigureAwait(false);

        result.IsSuccess.Should().BeTrue();
        capture.CompletedCount.Should().BeGreaterThanOrEqualTo(1);
    }

    // ── Catalog: save, retrieve, list ───────────────────────────────────

    [SkippableFact]
    public async Task Catalog_Persists_And_Retrieves_Workflows()
    {
        IntegrationTestGuard.EnsureEnabled();

        using var tempDir = new TempTestDirectory("jdai-wf-test");
        var catalog = tempDir.CreateWorkflowCatalog();

            var definition = new AgentWorkflowDefinition
            {
                Name = "test-workflow",
                Version = "1.0",
                Description = "A test workflow",
                Tags = ["test", "demo"],
                Steps =
                [
                    AgentStepDefinition.RunSkill("step1"),
                    AgentStepDefinition.InvokeTool("tool1"),
                ],
            };

            await catalog.SaveAsync(definition).ConfigureAwait(false);

            // Retrieve by name + version
            var retrieved = await catalog.GetAsync("test-workflow", "1.0").ConfigureAwait(false);
            retrieved.Should().NotBeNull();
            retrieved!.Name.Should().Be("test-workflow");
            retrieved.Steps.Should().HaveCount(2);
            retrieved.Tags.Should().Contain("test");

            // Retrieve latest (no version)
            var latest = await catalog.GetAsync("test-workflow").ConfigureAwait(false);
            latest.Should().NotBeNull();
            latest!.Version.Should().Be("1.0");

            // List all
            var all = await catalog.ListAsync().ConfigureAwait(false);
            all.Should().HaveCount(1);

            // Save v2 and verify both exist
            definition.Version = "2.0";
            await catalog.SaveAsync(definition).ConfigureAwait(false);

            all = await catalog.ListAsync().ConfigureAwait(false);
            all.Should().HaveCount(2);

            // Delete v1
            var deleted = await catalog.DeleteAsync("test-workflow", "1.0").ConfigureAwait(false);
            deleted.Should().BeTrue();
            all = await catalog.ListAsync().ConfigureAwait(false);
            all.Should().HaveCount(1);
    }

    // ── Matcher: tag-based workflow reuse ────────────────────────────────

    [SkippableFact]
    public async Task Matcher_Finds_Workflow_By_Tag_Overlap()
    {
        IntegrationTestGuard.EnsureEnabled();

        using var tempDir = new TempTestDirectory("jdai-wf-match");
        var catalog = tempDir.CreateWorkflowCatalog();

            await catalog.SaveAsync(new AgentWorkflowDefinition
            {
                Name = "code-review",
                Tags = ["review", "code", "pr"],
                Steps = [AgentStepDefinition.RunSkill("review-code")],
            }).ConfigureAwait(false);

            await catalog.SaveAsync(new AgentWorkflowDefinition
            {
                Name = "deploy",
                Tags = ["deploy", "release", "infrastructure"],
                Steps = [AgentStepDefinition.RunSkill("deploy-app")],
            }).ConfigureAwait(false);

            var matcher = new TagWorkflowMatcher(catalog);

            var match = await matcher.MatchAsync(
                new AgentRequest("Please review the code in this PR")).ConfigureAwait(false);
            match.Should().NotBeNull();
            match!.Definition.Name.Should().Be("code-review");
            match.Score.Should().BeGreaterThan(0);

            var deployMatch = await matcher.MatchAsync(
                new AgentRequest("deploy the new release to production infrastructure")).ConfigureAwait(false);
            deployMatch.Should().NotBeNull();
            deployMatch!.Definition.Name.Should().Be("deploy");
    }

    // ── Emitter: JSON, C#, Mermaid ─────────────────────────────────────

    [SkippableFact]
    public void Emitter_Produces_All_Formats()
    {
        IntegrationTestGuard.EnsureEnabled();

        var definition = new AgentWorkflowDefinition
        {
            Name = "emitter-test",
            Steps =
            [
                AgentStepDefinition.RunSkill("analyze"),
                AgentStepDefinition.InvokeTool("git.diff"),
            ],
        };

        var emitter = new WorkflowEmitter();

        var json = emitter.Emit(definition, WorkflowExportFormat.Json);
        json.Content.Should().Contain("emitter-test");
        json.Content.Should().Contain("analyze");

        var csharp = emitter.Emit(definition, WorkflowExportFormat.CSharp);
        csharp.Content.Should().Contain("Workflow.Create");
        csharp.Content.Should().Contain("RunSkillStep");

        var mermaid = emitter.Emit(definition, WorkflowExportFormat.Mermaid);
        mermaid.Content.Should().Contain("graph TD");
        mermaid.Content.Should().Contain("analyze");
    }

    // ── Replay: re-execute a saved workflow ─────────────────────────────

    [SkippableFact]
    public async Task Replay_ReExecutes_Saved_Workflow_Successfully()
    {
        using var tempDir = new TempTestDirectory("jdai-wf-replay");
        var catalog = tempDir.CreateWorkflowCatalog();

            // First execution: build, run, save
            var definition = new AgentWorkflowDefinition
            {
                Name = "replay-test",
                Version = "1.0",
                Steps = [AgentStepDefinition.RunSkill("greet")],
            };

            await catalog.SaveAsync(definition).ConfigureAwait(false);

            var builder = new AgentWorkflowBuilder(_kernel);
            var workflow1 = Workflow.Create<AgentWorkflowData>("replay-test")
                .Step(new RunSkillStep("greet", "Say hello briefly: {prompt}"))
                .Build();

            var data1 = builder.CreateData("world");
            var result1 = await workflow1.ExecuteAsync(new WorkflowContext<AgentWorkflowData>(data1))
                .ConfigureAwait(false);
            result1.IsSuccess.Should().BeTrue();

            // Replay: retrieve from catalog and re-execute
            var retrieved = await catalog.GetAsync("replay-test").ConfigureAwait(false);
            retrieved.Should().NotBeNull();

            // Rebuild the workflow with same structure
            var workflow2 = Workflow.Create<AgentWorkflowData>("replay-test")
                .Step(new RunSkillStep("greet", "Say hello briefly: {prompt}"))
                .Build();

            var data2 = builder.CreateData("world");
            var result2 = await workflow2.ExecuteAsync(new WorkflowContext<AgentWorkflowData>(data2))
                .ConfigureAwait(false);

            result2.IsSuccess.Should().BeTrue();
            result2.Data.StepOutputs.Should().ContainKey("greet");
    }

    // ── Validation step aborts workflow ─────────────────────────────────

    [SkippableFact]
    public async Task ValidateStep_Aborts_Workflow_On_Failure()
    {
        var workflow = Workflow.Create<AgentWorkflowData>("validated")
            .Step(new ValidateStep(
                "check-prompt",
                data => data.Prompt.Length > 5,
                "Prompt too short"))
            .Step(new RunSkillStep("greet", "Say hello: {prompt}"))
            .Build();

        var builder = new AgentWorkflowBuilder(_kernel);
        var data = builder.CreateData("hi");

        var ctx = new WorkflowContext<AgentWorkflowData>(data);
        var result = await workflow.ExecuteAsync(ctx).ConfigureAwait(false);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.StepName == "check-prompt");

        // The greet step should NOT have executed
        data.StepOutputs.Should().NotContainKey("greet");
    }

    // ── Isolation: separate workflows don't share state ─────────────────

    [SkippableFact]
    public async Task Separate_Workflows_Are_Isolated()
    {
        var workflow1 = Workflow.Create<AgentWorkflowData>("wf1")
            .Step(new RunSkillStep("step1", "Reply with 'A': {prompt}"))
            .Build();

        var workflow2 = Workflow.Create<AgentWorkflowData>("wf2")
            .Step(new RunSkillStep("step1", "Reply with 'B': {prompt}"))
            .Build();

        var builder = new AgentWorkflowBuilder(_kernel);

        var data1 = builder.CreateData("test");
        var data2 = builder.CreateData("test");

        var task1 = workflow1.ExecuteAsync(new WorkflowContext<AgentWorkflowData>(data1));
        var task2 = workflow2.ExecuteAsync(new WorkflowContext<AgentWorkflowData>(data2));

        var results = await Task.WhenAll(task1, task2).ConfigureAwait(false);

        results[0].IsSuccess.Should().BeTrue();
        results[1].IsSuccess.Should().BeTrue();

        // Each workflow has its own data — outputs should be in separate dictionaries
        data1.StepOutputs.Should().ContainKey("step1");
        data2.StepOutputs.Should().ContainKey("step1");

        // They should not be the same object
        data1.Should().NotBeSameAs(data2);
    }
}
