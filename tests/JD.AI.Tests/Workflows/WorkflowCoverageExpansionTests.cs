using JD.AI.Workflows;

namespace JD.AI.Tests.Workflows;

/// <summary>
/// Tests for WorkflowVersioning, WorkflowMatcher, TagWorkflowMatcher,
/// FileWorkflowCatalog, and InMemoryWorkflowCatalog.
/// </summary>
public sealed class WorkflowVersioningTests
{
    // ── WorkflowSemVersion.TryParse ───────────────────────────────────────────

    [Theory]
    [InlineData("1.0.0", true, 1, 0, 0)]
    [InlineData("2.3", true, 2, 3, 0)]
    [InlineData("0.1.0-alpha", true, 0, 1, 0)]
    [InlineData("1.0.0+build.42", true, 1, 0, 0)]
    [InlineData("1.0.0-beta.1+sha.abc", true, 1, 0, 0)]
    [InlineData("", false, 0, 0, 0)]
    [InlineData("invalid", false, 0, 0, 0)]
    [InlineData("1", false, 0, 0, 0)]
    public void TryParse_ParsesVersionStrings(string input, bool expected, int major, int minor, int patch)
    {
        var ok = WorkflowSemVersion.TryParse(input, out var version);
        Assert.Equal(expected, ok);
        if (expected)
        {
            Assert.Equal(major, version.Major);
            Assert.Equal(minor, version.Minor);
            Assert.Equal(patch, version.Patch);
        }
    }

    [Fact]
    public void TryParse_PreRelease_ParsesIdentifiers()
    {
        Assert.True(WorkflowSemVersion.TryParse("1.2.3-alpha.1", out var v));
        Assert.Equal(2, v.PreRelease.Count);
        Assert.Equal("alpha", v.PreRelease[0]);
        Assert.Equal("1", v.PreRelease[1]);
    }

    [Fact]
    public void TryParse_BuildMetadata_IsCaptured()
    {
        Assert.True(WorkflowSemVersion.TryParse("1.0.0+sha.abc123", out var v));
        Assert.Equal("sha.abc123", v.BuildMetadata);
    }

    // ── Comparison operators ──────────────────────────────────────────────────

    [Fact]
    public void CompareTo_HigherMajor_IsGreater()
    {
        Assert.True(WorkflowSemVersion.TryParse("2.0.0", out var v2));
        Assert.True(WorkflowSemVersion.TryParse("1.0.0", out var v1));
        Assert.True(v2 > v1);
    }

    [Fact]
    public void CompareTo_HigherMinor_IsGreater()
    {
        Assert.True(WorkflowSemVersion.TryParse("1.2.0", out var v2));
        Assert.True(WorkflowSemVersion.TryParse("1.1.0", out var v1));
        Assert.True(v2 > v1);
    }

    [Fact]
    public void CompareTo_HigherPatch_IsGreater()
    {
        Assert.True(WorkflowSemVersion.TryParse("1.0.2", out var v2));
        Assert.True(WorkflowSemVersion.TryParse("1.0.1", out var v1));
        Assert.True(v2 > v1);
    }

    [Fact]
    public void CompareTo_StableGreaterThanPreRelease()
    {
        Assert.True(WorkflowSemVersion.TryParse("1.0.0", out var stable));
        Assert.True(WorkflowSemVersion.TryParse("1.0.0-alpha", out var pre));
        Assert.True(stable > pre);
    }

    [Fact]
    public void CompareTo_Equal_ReturnsZero()
    {
        Assert.True(WorkflowSemVersion.TryParse("1.2.3", out var a));
        Assert.True(WorkflowSemVersion.TryParse("1.2.3", out var b));
        Assert.Equal(0, a.CompareTo(b));
    }

    [Fact]
    public void CaretUpperBound_NonZeroMajor_IncreasesMajor()
    {
        Assert.True(WorkflowSemVersion.TryParse("1.2.3", out var v));
        var bound = v.CaretUpperBound();
        Assert.Equal(2, bound.Major);
        Assert.Equal(0, bound.Minor);
        Assert.Equal(0, bound.Patch);
    }

    [Fact]
    public void CaretUpperBound_ZeroMajor_NonZeroMinor_IncreasesMinor()
    {
        Assert.True(WorkflowSemVersion.TryParse("0.3.0", out var v));
        var bound = v.CaretUpperBound();
        Assert.Equal(0, bound.Major);
        Assert.Equal(4, bound.Minor);
        Assert.Equal(0, bound.Patch);
    }

    [Fact]
    public void CaretUpperBound_ZeroMajorMinor_IncreasesPatch()
    {
        Assert.True(WorkflowSemVersion.TryParse("0.0.5", out var v));
        var bound = v.CaretUpperBound();
        Assert.Equal(0, bound.Major);
        Assert.Equal(0, bound.Minor);
        Assert.Equal(6, bound.Patch);
    }

    [Fact]
    public void TildeUpperBound_IncreasesMinor()
    {
        Assert.True(WorkflowSemVersion.TryParse("1.3.0", out var v));
        var bound = v.TildeUpperBound();
        Assert.Equal(1, bound.Major);
        Assert.Equal(4, bound.Minor);
        Assert.Equal(0, bound.Patch);
    }
}

/// <summary>
/// Tests for the WorkflowVersionSelector (via SelectVersion) including ^, ~, range constraints.
/// </summary>
public sealed class WorkflowVersionSelectorTests
{
    private static AgentWorkflowDefinition Def(string name, string version, bool deprecated = false)
        => new() { Name = name, Version = version, IsDeprecated = deprecated };

    [Fact]
    public void SelectVersion_Latest_PicksHighestNonDeprecated()
    {
        var defs = new[]
        {
            Def("wf", "1.0.0"),
            Def("wf", "2.0.0"),
            Def("wf", "3.0.0", deprecated: true),
        };

        var result = WorkflowVersioning.SelectVersion(defs, "latest");
        Assert.NotNull(result);
        Assert.Equal("2.0.0", result.Version);
    }

    [Fact]
    public void SelectVersion_Null_SameAsLatest()
    {
        var defs = new[] { Def("wf", "1.0.0"), Def("wf", "2.0.0") };
        var result = WorkflowVersioning.SelectVersion(defs, null);
        Assert.Equal("2.0.0", result!.Version);
    }

    [Fact]
    public void SelectVersion_ExactVersion_MatchesOnly()
    {
        var defs = new[] { Def("wf", "1.0.0"), Def("wf", "2.0.0") };
        var result = WorkflowVersioning.SelectVersion(defs, "1.0.0");
        Assert.Equal("1.0.0", result!.Version);
    }

    [Fact]
    public void SelectVersion_ExactMismatch_ReturnsNull()
    {
        var defs = new[] { Def("wf", "1.0.0") };
        var result = WorkflowVersioning.SelectVersion(defs, "9.9.9");
        Assert.Null(result);
    }

    [Fact]
    public void SelectVersion_Caret_CompatibleMinorUpgrades()
    {
        var defs = new[]
        {
            Def("wf", "1.0.0"),
            Def("wf", "1.5.0"),
            Def("wf", "2.0.0"), // outside ^1.0.0
        };

        var result = WorkflowVersioning.SelectVersion(defs, "^1.0.0");
        Assert.Equal("1.5.0", result!.Version);
    }

    [Fact]
    public void SelectVersion_Tilde_CompatiblePatchUpgrades()
    {
        var defs = new[]
        {
            Def("wf", "1.2.0"),
            Def("wf", "1.2.5"),
            Def("wf", "1.3.0"), // outside ~1.2.0
        };

        var result = WorkflowVersioning.SelectVersion(defs, "~1.2.0");
        Assert.Equal("1.2.5", result!.Version);
    }

    [Fact]
    public void SelectVersion_Range_GreaterThanOrEqual()
    {
        var defs = new[] { Def("wf", "1.0.0"), Def("wf", "2.0.0"), Def("wf", "3.0.0") };
        var result = WorkflowVersioning.SelectVersion(defs, ">=2.0.0");
        Assert.Equal("3.0.0", result!.Version);
    }

    [Fact]
    public void SelectVersion_EmptyCollection_ReturnsNull()
    {
        var result = WorkflowVersioning.SelectVersion([], "latest");
        Assert.Null(result);
    }

    [Fact]
    public void SelectVersion_AllDeprecated_ReturnsHighestDeprecated()
    {
        var defs = new[]
        {
            Def("wf", "1.0.0", deprecated: true),
            Def("wf", "2.0.0", deprecated: true),
        };
        // Falls back to deprecated when no non-deprecated candidate
        var result = WorkflowVersioning.SelectVersion(defs, "latest");
        Assert.NotNull(result);
    }
}

/// <summary>
/// Tests for DetectBreakingChanges in WorkflowVersioning.
/// </summary>
public sealed class WorkflowBreakingChangeTests
{
    private static AgentStepDefinition MakeStep(string name, AgentStepKind kind = AgentStepKind.Skill, string? target = null) =>
        new() { Name = name, Kind = kind, Target = target ?? name };

    private static AgentWorkflowDefinition MakeDef(params AgentStepDefinition[] steps)
    {
        var def = new AgentWorkflowDefinition { Name = "wf", Version = "1.0.0" };
        foreach (var s in steps) def.Steps.Add(s);
        return def;
    }

    [Fact]
    public void DetectBreakingChanges_NoChange_ReturnsEmpty()
    {
        var prev = MakeDef(MakeStep("step1"));
        var curr = MakeDef(MakeStep("step1"));
        var changes = WorkflowVersioning.DetectBreakingChanges(prev, curr);
        Assert.Empty(changes);
    }

    [Fact]
    public void DetectBreakingChanges_RemovedStep_IsDetected()
    {
        var prev = MakeDef(MakeStep("step1"), MakeStep("step2"));
        var curr = MakeDef(MakeStep("step1"));
        var changes = WorkflowVersioning.DetectBreakingChanges(prev, curr);
        Assert.Single(changes);
        Assert.Contains("step2", changes[0]);
    }

    [Fact]
    public void DetectBreakingChanges_ChangedKind_IsDetected()
    {
        var prev = MakeDef(MakeStep("step1", AgentStepKind.Skill));
        var curr = MakeDef(MakeStep("step1", AgentStepKind.Tool));
        var changes = WorkflowVersioning.DetectBreakingChanges(prev, curr);
        Assert.Single(changes);
        Assert.Contains("kind", changes[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DetectBreakingChanges_ChangedTarget_IsDetected()
    {
        var prev = MakeDef(new AgentStepDefinition { Name = "s1", Kind = AgentStepKind.Tool, Target = "old.target" });
        var curr = MakeDef(new AgentStepDefinition { Name = "s1", Kind = AgentStepKind.Tool, Target = "new.target" });
        var changes = WorkflowVersioning.DetectBreakingChanges(prev, curr);
        Assert.Single(changes);
        Assert.Contains("target", changes[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DetectBreakingChanges_ChangedCondition_IsDetected()
    {
        var prev = MakeDef(new AgentStepDefinition { Name = "s1", Kind = AgentStepKind.Skill, Condition = "x > 0" });
        var curr = MakeDef(new AgentStepDefinition { Name = "s1", Kind = AgentStepKind.Skill, Condition = "y > 0" });
        var changes = WorkflowVersioning.DetectBreakingChanges(prev, curr);
        Assert.Single(changes);
        Assert.Contains("condition", changes[0], StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Tests for WorkflowMatcher and TagWorkflowMatcher.
/// </summary>
public sealed class WorkflowMatcherTests
{
    private static InMemoryWorkflowCatalog MakeCatalog(params (string name, string[] tags)[] defs)
    {
        var catalog = new InMemoryWorkflowCatalog();
        foreach (var (name, tags) in defs)
        {
            var def = new AgentWorkflowDefinition { Name = name, Version = "1.0.0" };
            foreach (var tag in tags) def.Tags.Add(tag);
            catalog.SaveAsync(def).GetAwaiter().GetResult();
        }
        return catalog;
    }

    [Fact]
    public async Task WorkflowMatcher_ExactName_ReturnsScore1()
    {
        var catalog = MakeCatalog(("deploy-pipeline", ["deploy", "pipeline"]));
        var matcher = new WorkflowMatcher(catalog);

        var result = await matcher.MatchAsync(new AgentRequest("run the deploy-pipeline now"));

        Assert.NotNull(result);
        Assert.Equal(1.0f, result.Score);
        Assert.Equal("deploy-pipeline", result.Definition.Name);
    }

    [Fact]
    public async Task WorkflowMatcher_TagOverlap_ReturnsPartialScore()
    {
        var catalog = MakeCatalog(("code-review", ["review", "code", "pull-request"]));
        var matcher = new WorkflowMatcher(catalog);

        var result = await matcher.MatchAsync(new AgentRequest("please review my code changes"));

        Assert.NotNull(result);
        Assert.True(result.Score > 0f && result.Score < 1f);
    }

    [Fact]
    public async Task WorkflowMatcher_NoMatch_ReturnsNull()
    {
        var catalog = MakeCatalog(("deploy-pipeline", ["deploy"]));
        var matcher = new WorkflowMatcher(catalog);

        var result = await matcher.MatchAsync(new AgentRequest("hello world"));

        Assert.Null(result);
    }

    [Fact]
    public async Task WorkflowMatcher_EmptyCatalog_ReturnsNull()
    {
        var catalog = new InMemoryWorkflowCatalog();
        var matcher = new WorkflowMatcher(catalog);

        var result = await matcher.MatchAsync(new AgentRequest("deploy the app"));

        Assert.Null(result);
    }

    [Fact]
    public async Task WorkflowMatcher_EmptyTags_SkipsTagMatching()
    {
        var catalog = MakeCatalog(("no-tags", []));
        var matcher = new WorkflowMatcher(catalog);

        // "no-tags" not in the message exactly, and no tags to match
        var result = await matcher.MatchAsync(new AgentRequest("run something"));
        Assert.Null(result);
    }

    [Fact]
    public async Task TagWorkflowMatcher_BestTagScore_Wins()
    {
        var catalog = MakeCatalog(
            ("wf-a", ["review", "code"]),      // 2 tags, message has "review" = 0.5
            ("wf-b", ["review", "code", "pr"]) // 3 tags, message has "review" = 0.33
        );
        var matcher = new TagWorkflowMatcher(catalog);

        // Message only matches "review" → wf-a scores 0.5, wf-b scores 0.33 → wf-a wins
        var result = await matcher.MatchAsync(new AgentRequest("please review this"));

        Assert.NotNull(result);
        Assert.Equal("wf-a", result.Definition.Name);
    }

    [Fact]
    public async Task TagWorkflowMatcher_WhitespaceTags_AreFiltered()
    {
        var catalog = MakeCatalog(("wf", [" ", "  ", ""]));
        // All tags are whitespace — no match possible
        var matcher = new TagWorkflowMatcher(catalog);
        var result = await matcher.MatchAsync(new AgentRequest("run wf please"));
        Assert.Null(result);
    }
}

/// <summary>
/// Tests for FileWorkflowCatalog (filesystem-backed).
/// </summary>
public sealed class FileWorkflowCatalogTests : IDisposable
{
    private readonly string _dir;
    private readonly FileWorkflowCatalog _catalog;

    public FileWorkflowCatalogTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"jdai-fwc-{Guid.NewGuid():N}");
        _catalog = new FileWorkflowCatalog(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_AndGetAsync_RoundTrip()
    {
        var def = new AgentWorkflowDefinition
        {
            Name = "my-workflow",
            Version = "1.0.0",
            Description = "Test workflow",
        };

        await _catalog.SaveAsync(def);
        var retrieved = await _catalog.GetAsync("my-workflow");

        Assert.NotNull(retrieved);
        Assert.Equal("my-workflow", retrieved.Name);
        Assert.Equal("1.0.0", retrieved.Version);
        Assert.Equal("Test workflow", retrieved.Description);
    }

    [Fact]
    public async Task SaveAsync_UpdatesTimestamp()
    {
        var def = new AgentWorkflowDefinition { Name = "wf", Version = "1.0.0" };
        var before = DateTime.UtcNow.AddSeconds(-1);
        await _catalog.SaveAsync(def);
        var retrieved = await _catalog.GetAsync("wf");
        Assert.NotNull(retrieved);
        Assert.True(retrieved.UpdatedAt >= before);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllWorkflows()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "wf1", Version = "1.0.0" });
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "wf2", Version = "1.0.0" });

        var list = await _catalog.ListAsync();
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task GetAsync_NonExistentName_ReturnsNull()
    {
        var result = await _catalog.GetAsync("does-not-exist");
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ByName_RemovesAll()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "wf", Version = "1.0.0" });
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "wf", Version = "2.0.0" });

        var deleted = await _catalog.DeleteAsync("wf");
        Assert.True(deleted);

        var list = await _catalog.ListAsync();
        Assert.Empty(list);
    }

    [Fact]
    public async Task DeleteAsync_ByVersion_RemovesSpecificVersion()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "wf", Version = "1.0.0" });
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "wf", Version = "2.0.0" });

        var deleted = await _catalog.DeleteAsync("wf", "1.0.0");
        Assert.True(deleted);

        var remaining = await _catalog.GetAsync("wf");
        Assert.NotNull(remaining);
        Assert.Equal("2.0.0", remaining.Version);
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_ReturnsFalse()
    {
        var deleted = await _catalog.DeleteAsync("nonexistent");
        Assert.False(deleted);
    }

    [Fact]
    public async Task SaveAsync_BreakingChange_WithoutMajorBump_Throws()
    {
        var v1 = new AgentWorkflowDefinition { Name = "wf", Version = "1.0.0" };
        v1.Steps.Add(new AgentStepDefinition { Name = "critical-step", Kind = AgentStepKind.Skill });
        await _catalog.SaveAsync(v1);

        // v1.1.0 removes the critical step — breaking change without major bump
        var v11 = new AgentWorkflowDefinition { Name = "wf", Version = "1.1.0" };
        await Assert.ThrowsAsync<InvalidDataException>(() => _catalog.SaveAsync(v11));
    }

    [Fact]
    public async Task SaveAsync_BreakingChange_WithMajorBump_Succeeds()
    {
        var v1 = new AgentWorkflowDefinition { Name = "wf", Version = "1.0.0" };
        v1.Steps.Add(new AgentStepDefinition { Name = "critical-step", Kind = AgentStepKind.Skill });
        await _catalog.SaveAsync(v1);

        // v2.0.0 removes the critical step — major bump allowed
        var v2 = new AgentWorkflowDefinition { Name = "wf", Version = "2.0.0" };
        await _catalog.SaveAsync(v2);

        var retrieved = await _catalog.GetAsync("wf");
        Assert.NotNull(retrieved);
        Assert.Equal("2.0.0", retrieved.Version);
        Assert.NotEmpty(retrieved.BreakingChanges);
    }

    [Fact]
    public async Task ListAsync_EmptyDir_ReturnsEmpty()
    {
        var list = await _catalog.ListAsync();
        Assert.Empty(list);
    }
}

/// <summary>
/// Tests for InMemoryWorkflowCatalog.
/// </summary>
public sealed class InMemoryWorkflowCatalogTests
{
    [Fact]
    public async Task SaveAndGet_RoundTrip()
    {
        var catalog = new InMemoryWorkflowCatalog();
        var def = new AgentWorkflowDefinition { Name = "wf", Version = "1.0.0", Description = "test" };
        await catalog.SaveAsync(def);
        var retrieved = await catalog.GetAsync("wf");
        Assert.NotNull(retrieved);
        Assert.Equal("test", retrieved.Description);
    }

    [Fact]
    public async Task GetAsync_SpecificVersion_ReturnsCorrect()
    {
        var catalog = new InMemoryWorkflowCatalog();
        await catalog.SaveAsync(new AgentWorkflowDefinition { Name = "wf", Version = "1.0.0" });
        await catalog.SaveAsync(new AgentWorkflowDefinition { Name = "wf", Version = "2.0.0" });

        var v1 = await catalog.GetAsync("wf", "1.0.0");
        Assert.NotNull(v1);
        Assert.Equal("1.0.0", v1.Version);
    }

    [Fact]
    public async Task ListAsync_ReturnsLatestPerWorkflow()
    {
        var catalog = new InMemoryWorkflowCatalog();
        await catalog.SaveAsync(new AgentWorkflowDefinition { Name = "wf", Version = "1.0.0" });
        await catalog.SaveAsync(new AgentWorkflowDefinition { Name = "wf", Version = "2.0.0" });

        var list = await catalog.ListAsync();
        Assert.Single(list);
        Assert.Equal("2.0.0", list[0].Version);
    }

    [Fact]
    public async Task DeleteAsync_SpecificVersion_RemovesIt()
    {
        var catalog = new InMemoryWorkflowCatalog();
        await catalog.SaveAsync(new AgentWorkflowDefinition { Name = "wf", Version = "1.0.0" });
        await catalog.SaveAsync(new AgentWorkflowDefinition { Name = "wf", Version = "2.0.0" });

        await catalog.DeleteAsync("wf", "1.0.0");
        var remaining = await catalog.GetAsync("wf");
        Assert.Equal("2.0.0", remaining!.Version);
    }

    [Fact]
    public async Task DeleteAsync_AllVersions_RemovesWorkflow()
    {
        var catalog = new InMemoryWorkflowCatalog();
        await catalog.SaveAsync(new AgentWorkflowDefinition { Name = "wf", Version = "1.0.0" });
        await catalog.DeleteAsync("wf");
        var result = await catalog.GetAsync("wf");
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsFalse()
    {
        var catalog = new InMemoryWorkflowCatalog();
        var deleted = await catalog.DeleteAsync("nonexistent");
        Assert.False(deleted);
    }

    [Fact]
    public async Task SaveAsync_BreakingChange_WithoutMajorBump_Throws()
    {
        var catalog = new InMemoryWorkflowCatalog();
        var v1 = new AgentWorkflowDefinition { Name = "wf", Version = "1.0.0" };
        v1.Steps.Add(new AgentStepDefinition { Name = "step1", Kind = AgentStepKind.Skill });
        await catalog.SaveAsync(v1);

        var v11 = new AgentWorkflowDefinition { Name = "wf", Version = "1.1.0" }; // step removed
        await Assert.ThrowsAsync<InvalidDataException>(() => catalog.SaveAsync(v11));
    }

    [Fact]
    public async Task GetAsync_NonExistent_ReturnsNull()
    {
        var catalog = new InMemoryWorkflowCatalog();
        var result = await catalog.GetAsync("nonexistent");
        Assert.Null(result);
    }
}
