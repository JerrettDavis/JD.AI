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

        await _catalog.SaveAsync(def);
        var loaded = await _catalog.GetAsync("roundtrip", "1.0");

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("roundtrip");
        loaded.Tags.Should().BeEquivalentTo(["a", "b"]);
        loaded.Steps.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetLatest_ReturnsNewestVersion()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "versioned", Version = "1.0" })
;
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "versioned", Version = "2.0" })
;

        var latest = await _catalog.GetAsync("versioned");
        latest.Should().NotBeNull();
        latest!.Version.Should().Be("2.0");
    }

    [Fact]
    public async Task List_ReturnsAll()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "a", Version = "1.0" })
;
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "b", Version = "1.0" })
;

        var all = await _catalog.ListAsync();
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task Delete_RemovesEntry()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "del", Version = "1.0" })
;

        var deleted = await _catalog.DeleteAsync("del", "1.0");
        deleted.Should().BeTrue();

        var result = await _catalog.GetAsync("del", "1.0");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Delete_NonExistent_ReturnsFalse()
    {
        var result = await _catalog.DeleteAsync("nope", "1.0");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Get_NonExistent_ReturnsNull()
    {
        var result = await _catalog.GetAsync("nope", "1.0");
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
            Name = "review",
            Tags = ["review", "code", "pr"],
            Steps = [AgentStepDefinition.RunSkill("s1")],
        });

        await _catalog.SaveAsync(new AgentWorkflowDefinition
        {
            Name = "deploy",
            Tags = ["deploy", "release"],
            Steps = [AgentStepDefinition.RunSkill("s1")],
        });

        var matcher = new TagWorkflowMatcher(_catalog);

        var match = await matcher.MatchAsync(new AgentRequest("please review the code changes"))
;
        match.Should().NotBeNull();
        match!.Definition.Name.Should().Be("review");
    }

    [Fact]
    public async Task Match_ReturnsNull_WhenNoOverlap()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition
        {
            Name = "deploy",
            Tags = ["deploy", "release"],
            Steps = [AgentStepDefinition.RunSkill("s1")],
        });

        var matcher = new TagWorkflowMatcher(_catalog);
        var match = await matcher.MatchAsync(new AgentRequest("hello world foo bar baz"))
;
        match.Should().BeNull();
    }

    [Fact]
    public async Task Match_ReturnsNull_WhenCatalogEmpty()
    {
        var matcher = new TagWorkflowMatcher(_catalog);
        var match = await matcher.MatchAsync(new AgentRequest("review the code"))
;
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

        await step.ExecuteAsync(ctx);

        ctx.IsAborted.Should().BeFalse();
        ctx.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateStep_Aborts_WhenPredicateFalse()
    {
        var step = new ValidateStep("check", data => data.Prompt.Length > 100, "Too short");
        var data = new AgentWorkflowData { Prompt = "hi" };
        var ctx = new WorkflowContext<AgentWorkflowData>(data);

        await step.ExecuteAsync(ctx);

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

public class InMemoryWorkflowCatalogTests
{
    private readonly InMemoryWorkflowCatalog _catalog = new();

    [Fact]
    public async Task SaveAndGet_RoundTrips()
    {
        var def = new AgentWorkflowDefinition
        {
            Name = "test",
            Version = "1.0",
            Steps = [AgentStepDefinition.RunSkill("s1")],
        };

        await _catalog.SaveAsync(def);
        var loaded = await _catalog.GetAsync("test", "1.0");

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("test");
    }

    [Fact]
    public async Task GetLatest_ReturnsHighestVersion()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "v", Version = "1.0" });
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "v", Version = "2.0" });
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "v", Version = "10.0" });

        var latest = await _catalog.GetAsync("v");
        latest.Should().NotBeNull();
        latest!.Version.Should().Be("10.0", "10.0 > 9.0 > 2.0 with numeric ordering");
    }

    [Fact]
    public async Task List_ReturnsLatestPerName()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "a", Version = "1.0" });
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "a", Version = "2.0" });
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "b", Version = "1.0" });

        var all = await _catalog.ListAsync();
        all.Should().HaveCount(2);
        all.Should().Contain(w => w.Name == "a" && w.Version == "2.0");
        all.Should().Contain(w => w.Name == "b" && w.Version == "1.0");
    }

    [Fact]
    public async Task Delete_ByVersion_RemovesOnly()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "d", Version = "1.0" });
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "d", Version = "2.0" });

        var deleted = await _catalog.DeleteAsync("d", "1.0");
        deleted.Should().BeTrue();

        var remaining = await _catalog.GetAsync("d", "1.0");
        remaining.Should().BeNull();

        var v2 = await _catalog.GetAsync("d", "2.0");
        v2.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_AllVersions_RemovesEntry()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "e", Version = "1.0" });
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "e", Version = "2.0" });

        var deleted = await _catalog.DeleteAsync("e");
        deleted.Should().BeTrue();

        var result = await _catalog.GetAsync("e");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveExistingVersion_Replaces()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition
        {
            Name = "r",
            Version = "1.0",
            Description = "old",
        });
        await _catalog.SaveAsync(new AgentWorkflowDefinition
        {
            Name = "r",
            Version = "1.0",
            Description = "new",
        });

        var loaded = await _catalog.GetAsync("r", "1.0");
        loaded!.Description.Should().Be("new");
    }

    [Fact]
    public async Task Get_CaseInsensitiveName()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "MyWorkflow", Version = "1.0" });

        var loaded = await _catalog.GetAsync("myworkflow", "1.0");
        loaded.Should().NotBeNull();
    }
}

public class WorkflowMatcherTests
{
    [Fact]
    public async Task ExactNameMatch_ReturnsHighScore()
    {
        var catalog = new InMemoryWorkflowCatalog();
        await catalog.SaveAsync(new AgentWorkflowDefinition
        {
            Name = "App.NextJs.Todo",
            Version = "1.0",
            Tags = ["nextjs"],
            Steps = [AgentStepDefinition.RunSkill("s1")],
        });

        var matcher = new WorkflowMatcher(catalog);
        var match = await matcher.MatchAsync(new AgentRequest("create App.NextJs.Todo application"));

        match.Should().NotBeNull();
        match!.Score.Should().Be(1.0f);
        match.MatchReason.Should().Be("exact");
    }

    [Fact]
    public async Task TagMatch_ReturnsTags()
    {
        var catalog = new InMemoryWorkflowCatalog();
        await catalog.SaveAsync(new AgentWorkflowDefinition
        {
            Name = "CodeReview",
            Version = "1.0",
            Tags = ["review", "code"],
            Steps = [AgentStepDefinition.RunSkill("s1")],
        });

        var matcher = new WorkflowMatcher(catalog);
        var match = await matcher.MatchAsync(new AgentRequest("please review the code changes"));

        match.Should().NotBeNull();
        match!.MatchReason.Should().Be("tags");
    }

    [Fact]
    public async Task PrefersExact_OverTags()
    {
        var catalog = new InMemoryWorkflowCatalog();
        await catalog.SaveAsync(new AgentWorkflowDefinition
        {
            Name = "App.NextJs.Todo",
            Version = "1.0",
            Tags = [],
            Steps = [AgentStepDefinition.RunSkill("s1")],
        });
        await catalog.SaveAsync(new AgentWorkflowDefinition
        {
            Name = "Other",
            Version = "1.0",
            Tags = ["nextjs"],
            Steps = [AgentStepDefinition.RunSkill("s1")],
        });

        var matcher = new WorkflowMatcher(catalog);
        var match = await matcher.MatchAsync(
            new AgentRequest("I want to work on App.NextJs.Todo with nextjs"));

        match.Should().NotBeNull();
        match!.MatchReason.Should().Be("exact");
        match.Definition.Name.Should().Be("App.NextJs.Todo");
    }

    [Fact]
    public async Task EmptyTags_DoNotCauseSpuriousMatch()
    {
        var catalog = new InMemoryWorkflowCatalog();
        await catalog.SaveAsync(new AgentWorkflowDefinition
        {
            Name = "W",
            Version = "1.0",
            Tags = ["", "   "],
            Steps = [AgentStepDefinition.RunSkill("s1")],
        });

        var matcher = new WorkflowMatcher(catalog);
        var match = await matcher.MatchAsync(new AgentRequest("completely unrelated message here"));

        match.Should().BeNull();
    }

    [Fact]
    public async Task NoMatch_ReturnsNull()
    {
        var catalog = new InMemoryWorkflowCatalog();
        await catalog.SaveAsync(new AgentWorkflowDefinition
        {
            Name = "Deploy",
            Version = "1.0",
            Tags = ["deploy"],
            Steps = [AgentStepDefinition.RunSkill("s1")],
        });

        var matcher = new WorkflowMatcher(catalog);
        var match = await matcher.MatchAsync(new AgentRequest("do something completely unrelated"));

        match.Should().BeNull();
    }
}
