using FluentAssertions;
using JD.AI.Workflows;
using JD.AI.Workflows.Steps;
using NSubstitute;
using WorkflowFramework;
using Xunit;

namespace JD.AI.Tests;

public class WorkflowDetectorTests
{
    private readonly AgentWorkflowDetector _detector = new();

    [Theory]
    [InlineData("implement a user login feature with JWT tokens", true)]
    [InlineData("create a REST API for the inventory system", true)]
    [InlineData("scaffold a new microservice for notifications", true)]
    [InlineData("review the pull request changes for security issues", true)]
    [InlineData("deploy the application to staging environment", true)]
    [InlineData("refactor the data access layer to use repository pattern", true)]
    [InlineData("hello", false)]
    [InlineData("what is 2+2?", false)]
    [InlineData("", false)]
    [InlineData("what is the weather like today in the city center", false)]
    public void IsWorkflowRequired_ClassifiesCorrectly(string message, bool expected) =>
        _detector.IsWorkflowRequired(new AgentRequest(message)).Should().Be(expected);
}

public class WorkflowDefinitionTests
{
    [Fact]
    public void StepFactoryMethods_CreateCorrectKinds()
    {
        AgentStepDefinition.RunSkill("analyze").Kind.Should().Be(AgentStepKind.Skill);
        AgentStepDefinition.InvokeTool("git.diff").Kind.Should().Be(AgentStepKind.Tool);
        AgentStepDefinition.Nested("sub-workflow").Kind.Should().Be(AgentStepKind.Nested);

        var loop = AgentStepDefinition.LoopUntil("done", AgentStepDefinition.RunSkill("step1"));
        loop.Kind.Should().Be(AgentStepKind.Loop);
        loop.SubSteps.Should().HaveCount(1);

        var cond = AgentStepDefinition.If("hasErrors", AgentStepDefinition.RunSkill("fix"));
        cond.Kind.Should().Be(AgentStepKind.Conditional);
        cond.SubSteps.Should().HaveCount(1);
    }

    [Fact]
    public void Definition_HasDefaults()
    {
        var def = new AgentWorkflowDefinition();
        def.Version.Should().Be("1.0");
        def.Steps.Should().BeEmpty();
        def.Tags.Should().BeEmpty();
        def.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}

public class WorkflowEmitterTests
{
    private static AgentWorkflowDefinition CreateTestDefinition() => new()
    {
        Name = "test-emit",
        Version = "2.0",
        Steps =
        [
            AgentStepDefinition.RunSkill("analyze"),
            AgentStepDefinition.InvokeTool("git.diff"),
        ],
    };

    [Fact]
    public void Emit_Json_ContainsDefinition()
    {
        var emitter = new WorkflowEmitter();
        var artifact = emitter.Emit(CreateTestDefinition(), WorkflowExportFormat.Json);

        artifact.Format.Should().Be(WorkflowExportFormat.Json);
        artifact.WorkflowName.Should().Be("test-emit");
        artifact.Content.Should().Contain("\"name\":");
        artifact.Content.Should().Contain("analyze");
    }

    [Fact]
    public void Emit_CSharp_ContainsBuilderCode()
    {
        var emitter = new WorkflowEmitter();
        var artifact = emitter.Emit(CreateTestDefinition(), WorkflowExportFormat.CSharp);

        artifact.Format.Should().Be(WorkflowExportFormat.CSharp);
        artifact.Content.Should().Contain("Workflow.Create");
        artifact.Content.Should().Contain("RunSkillStep");
        artifact.Content.Should().Contain("InvokeToolStep");
    }

    [Fact]
    public void Emit_Mermaid_ContainsGraph()
    {
        var emitter = new WorkflowEmitter();
        var artifact = emitter.Emit(CreateTestDefinition(), WorkflowExportFormat.Mermaid);

        artifact.Format.Should().Be(WorkflowExportFormat.Mermaid);
        artifact.Content.Should().Contain("graph TD");
        artifact.Content.Should().Contain("[analyze]");
    }
}

public class FileWorkflowCatalogTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileWorkflowCatalog _catalog;

    public FileWorkflowCatalogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-catalog-{Guid.NewGuid():N}");
        _catalog = new FileWorkflowCatalog(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task SaveAndGet_RoundTrips()
    {
        var def = new AgentWorkflowDefinition
        {
            Name = "roundtrip",
            Version = "1.0",
            Description = "test",
            Tags = ["a", "b"],
            Steps = [AgentStepDefinition.RunSkill("s1")],
        };

        await _catalog.SaveAsync(def).ConfigureAwait(false);
        var loaded = await _catalog.GetAsync("roundtrip", "1.0").ConfigureAwait(false);

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("roundtrip");
        loaded.Tags.Should().BeEquivalentTo(["a", "b"]);
        loaded.Steps.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetLatest_ReturnsNewestVersion()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "versioned", Version = "1.0" })
            .ConfigureAwait(false);
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "versioned", Version = "2.0" })
            .ConfigureAwait(false);

        var latest = await _catalog.GetAsync("versioned").ConfigureAwait(false);
        latest.Should().NotBeNull();
        latest!.Version.Should().Be("2.0");
    }

    [Fact]
    public async Task List_ReturnsAll()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "a", Version = "1.0" })
            .ConfigureAwait(false);
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "b", Version = "1.0" })
            .ConfigureAwait(false);

        var all = await _catalog.ListAsync().ConfigureAwait(false);
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task Delete_RemovesEntry()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "del", Version = "1.0" })
            .ConfigureAwait(false);

        var deleted = await _catalog.DeleteAsync("del", "1.0").ConfigureAwait(false);
        deleted.Should().BeTrue();

        var result = await _catalog.GetAsync("del", "1.0").ConfigureAwait(false);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Delete_NonExistent_ReturnsFalse()
    {
        var result = await _catalog.DeleteAsync("nope", "1.0").ConfigureAwait(false);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Get_NonExistent_ReturnsNull()
    {
        var result = await _catalog.GetAsync("nope", "1.0").ConfigureAwait(false);
        result.Should().BeNull();
    }
}

public class TagWorkflowMatcherTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileWorkflowCatalog _catalog;

    public TagWorkflowMatcherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-matcher-{Guid.NewGuid():N}");
        _catalog = new FileWorkflowCatalog(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task Match_ReturnsHighestScoring()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition
        {
            Name = "review", Tags = ["review", "code", "pr"],
            Steps = [AgentStepDefinition.RunSkill("s1")],
        }).ConfigureAwait(false);

        await _catalog.SaveAsync(new AgentWorkflowDefinition
        {
            Name = "deploy", Tags = ["deploy", "release"],
            Steps = [AgentStepDefinition.RunSkill("s1")],
        }).ConfigureAwait(false);

        var matcher = new TagWorkflowMatcher(_catalog);

        var match = await matcher.MatchAsync(new AgentRequest("please review the code changes"))
            .ConfigureAwait(false);
        match.Should().NotBeNull();
        match!.Definition.Name.Should().Be("review");
    }

    [Fact]
    public async Task Match_ReturnsNull_WhenNoOverlap()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition
        {
            Name = "deploy", Tags = ["deploy", "release"],
            Steps = [AgentStepDefinition.RunSkill("s1")],
        }).ConfigureAwait(false);

        var matcher = new TagWorkflowMatcher(_catalog);
        var match = await matcher.MatchAsync(new AgentRequest("hello world foo bar baz"))
            .ConfigureAwait(false);
        match.Should().BeNull();
    }

    [Fact]
    public async Task Match_ReturnsNull_WhenCatalogEmpty()
    {
        var matcher = new TagWorkflowMatcher(_catalog);
        var match = await matcher.MatchAsync(new AgentRequest("review the code"))
            .ConfigureAwait(false);
        match.Should().BeNull();
    }
}

public class ValidateStepTests
{
    [Fact]
    public async Task ValidateStep_Passes_WhenPredicateTrue()
    {
        var step = new ValidateStep("check", data => data.Prompt.Length > 3);
        var data = new AgentWorkflowData { Prompt = "hello world" };
        var ctx = new WorkflowContext<AgentWorkflowData>(data);

        await step.ExecuteAsync(ctx).ConfigureAwait(false);

        ctx.IsAborted.Should().BeFalse();
        ctx.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateStep_Aborts_WhenPredicateFalse()
    {
        var step = new ValidateStep("check", data => data.Prompt.Length > 100, "Too short");
        var data = new AgentWorkflowData { Prompt = "hi" };
        var ctx = new WorkflowContext<AgentWorkflowData>(data);

        await step.ExecuteAsync(ctx).ConfigureAwait(false);

        ctx.IsAborted.Should().BeTrue();
        ctx.Errors.Should().ContainSingle(e => e.StepName == "check");
    }
}

public class WorkflowCaptureTests
{
    [Fact]
    public async Task Capture_TracksEvents()
    {
        var capture = new WorkflowExecutionCapture();

        var step = Substitute.For<IStep>();
        step.Name.Returns("test-step");
        var ctx = Substitute.For<IWorkflowContext>();

        await capture.OnStepStartedAsync(ctx, step);
        await capture.OnStepCompletedAsync(ctx, step);

        capture.Events.Should().HaveCount(2);
        capture.CompletedCount.Should().Be(1);
        capture.FailedCount.Should().Be(0);
    }

    [Fact]
    public async Task Capture_TracksFailures()
    {
        var capture = new WorkflowExecutionCapture();

        var step = Substitute.For<IStep>();
        step.Name.Returns("fail-step");
        var ctx = Substitute.For<IWorkflowContext>();

        await capture.OnStepStartedAsync(ctx, step);
        await capture.OnStepFailedAsync(ctx, step, new InvalidOperationException("boom"));

        capture.FailedCount.Should().Be(1);
        capture.Events.Should().Contain(e => e.Error == "boom");
    }
}
