using FluentAssertions;
using JD.AI.Workflows;

namespace JD.AI.Tests;

public class WorkflowVersionParsingTests
{
    [Theory]
    [InlineData("1.0.0", 1, 0, 0, null)]
    [InlineData("2.1.3", 2, 1, 3, null)]
    [InlineData("0.0.1", 0, 0, 1, null)]
    [InlineData("1.0", 1, 0, 0, null)]
    [InlineData("3", 3, 0, 0, null)]
    [InlineData("1.2.3-beta.1", 1, 2, 3, "beta.1")]
    [InlineData("1.0.0-rc1", 1, 0, 0, "rc1")]
    public void Parse_ValidVersions(string input, int major, int minor, int patch, string? pre)
    {
        var v = WorkflowVersion.Parse(input);
        v.Major.Should().Be(major);
        v.Minor.Should().Be(minor);
        v.Patch.Should().Be(patch);
        v.PreRelease.Should().Be(pre);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.2.3.4.5")]
    [InlineData("-1.0.0")]
    public void Parse_InvalidVersions_Throws(string input)
    {
        var act = () => WorkflowVersion.Parse(input);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void TryParse_Null_ReturnsFalse()
    {
        WorkflowVersion.TryParse(null, out _).Should().BeFalse();
    }

    [Fact]
    public void ToString_ProducesCanonicalForm()
    {
        new WorkflowVersion(1, 2, 3).ToString().Should().Be("1.2.3");
        new WorkflowVersion(1, 0, 0, "beta").ToString().Should().Be("1.0.0-beta");
    }
}

public class WorkflowVersionComparisonTests
{
    [Fact]
    public void Major_Version_Ordering()
    {
        var v1 = WorkflowVersion.Parse("1.0.0");
        var v2 = WorkflowVersion.Parse("2.0.0");
        v1.Should().BeLessThan(v2);
    }

    [Fact]
    public void Minor_Version_Ordering()
    {
        var v1 = WorkflowVersion.Parse("1.1.0");
        var v2 = WorkflowVersion.Parse("1.2.0");
        v1.Should().BeLessThan(v2);
    }

    [Fact]
    public void Numeric_Not_Lexicographic_Ordering()
    {
        // This is the critical bug that lexicographic sorting gets wrong
        var v9 = WorkflowVersion.Parse("1.9.0");
        var v10 = WorkflowVersion.Parse("1.10.0");
        v9.Should().BeLessThan(v10);
    }

    [Fact]
    public void PreRelease_LessThan_Release()
    {
        var pre = WorkflowVersion.Parse("1.0.0-beta");
        var rel = WorkflowVersion.Parse("1.0.0");
        pre.Should().BeLessThan(rel);
    }

    [Fact]
    public void Equal_Versions_AreEqual()
    {
        var a = WorkflowVersion.Parse("1.2.3");
        var b = WorkflowVersion.Parse("1.2.3");
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }
}

public class VersionConstraintTests
{
    [Fact]
    public void Wildcard_MatchesAll()
    {
        var c = VersionConstraint.Parse("*");
        c.IsSatisfiedBy(WorkflowVersion.Parse("0.0.1")).Should().BeTrue();
        c.IsSatisfiedBy(WorkflowVersion.Parse("99.99.99")).Should().BeTrue();
    }

    [Fact]
    public void Exact_Match()
    {
        var c = VersionConstraint.Parse("1.2.3");
        c.IsSatisfiedBy(WorkflowVersion.Parse("1.2.3")).Should().BeTrue();
        c.IsSatisfiedBy(WorkflowVersion.Parse("1.2.4")).Should().BeFalse();
    }

    [Fact]
    public void Caret_SameMajor()
    {
        var c = VersionConstraint.Parse("^1.2.0");
        c.IsSatisfiedBy(WorkflowVersion.Parse("1.2.0")).Should().BeTrue();
        c.IsSatisfiedBy(WorkflowVersion.Parse("1.9.9")).Should().BeTrue();
        c.IsSatisfiedBy(WorkflowVersion.Parse("1.1.0")).Should().BeFalse();
        c.IsSatisfiedBy(WorkflowVersion.Parse("2.0.0")).Should().BeFalse();
    }

    [Fact]
    public void Tilde_SameMajorMinor()
    {
        var c = VersionConstraint.Parse("~1.2.0");
        c.IsSatisfiedBy(WorkflowVersion.Parse("1.2.0")).Should().BeTrue();
        c.IsSatisfiedBy(WorkflowVersion.Parse("1.2.9")).Should().BeTrue();
        c.IsSatisfiedBy(WorkflowVersion.Parse("1.3.0")).Should().BeFalse();
    }

    [Fact]
    public void GreaterThanOrEqual()
    {
        var c = VersionConstraint.Parse(">=1.5.0");
        c.IsSatisfiedBy(WorkflowVersion.Parse("1.5.0")).Should().BeTrue();
        c.IsSatisfiedBy(WorkflowVersion.Parse("2.0.0")).Should().BeTrue();
        c.IsSatisfiedBy(WorkflowVersion.Parse("1.4.9")).Should().BeFalse();
    }

    [Fact]
    public void LessThan()
    {
        var c = VersionConstraint.Parse("<2.0.0");
        c.IsSatisfiedBy(WorkflowVersion.Parse("1.9.9")).Should().BeTrue();
        c.IsSatisfiedBy(WorkflowVersion.Parse("2.0.0")).Should().BeFalse();
    }

    [Fact]
    public void CombinedRange()
    {
        var c = VersionConstraint.Parse(">=1.0.0 <2.0.0");
        c.IsSatisfiedBy(WorkflowVersion.Parse("1.0.0")).Should().BeTrue();
        c.IsSatisfiedBy(WorkflowVersion.Parse("1.5.3")).Should().BeTrue();
        c.IsSatisfiedBy(WorkflowVersion.Parse("0.9.0")).Should().BeFalse();
        c.IsSatisfiedBy(WorkflowVersion.Parse("2.0.0")).Should().BeFalse();
    }
}

public class FileWorkflowCatalogSemVerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileWorkflowCatalog _catalog;

    public FileWorkflowCatalogSemVerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-semver-{Guid.NewGuid():N}");
        _catalog = new FileWorkflowCatalog(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task GetLatest_UsesSemanticNotLexicographic()
    {
        // This is the critical test: 10.0.0 > 9.0.0 but "10" < "9" lexicographically
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "app", Version = "1.0.0" });
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "app", Version = "9.0.0" });
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "app", Version = "10.0.0" });

        var latest = await _catalog.GetAsync("app");
        latest.Should().NotBeNull();
        latest!.Version.Should().Be("10.0.0");
    }

    [Fact]
    public async Task GetLatest_HandlesMinorVersionsCorrectly()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "lib", Version = "1.9.0" });
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "lib", Version = "1.10.0" });
        await _catalog.SaveAsync(new AgentWorkflowDefinition { Name = "lib", Version = "1.2.0" });

        var latest = await _catalog.GetAsync("lib");
        latest.Should().NotBeNull();
        latest!.Version.Should().Be("1.10.0");
    }

    [Fact]
    public async Task Deprecation_Properties_Persist()
    {
        await _catalog.SaveAsync(new AgentWorkflowDefinition
        {
            Name = "old-wf",
            Version = "1.0.0",
            Deprecated = true,
            DeprecationMessage = "Use v2.0.0 instead",
        });

        var loaded = await _catalog.GetAsync("old-wf", "1.0.0");
        loaded.Should().NotBeNull();
        loaded!.Deprecated.Should().BeTrue();
        loaded.DeprecationMessage.Should().Be("Use v2.0.0 instead");
    }

    [Fact]
    public void GetSemVer_ParsesVersion()
    {
        var def = new AgentWorkflowDefinition { Version = "2.3.1" };
        var v = def.GetSemVer();
        v.Major.Should().Be(2);
        v.Minor.Should().Be(3);
        v.Patch.Should().Be(1);
    }
}

public class InMemoryWorkflowCatalogSemVerTests
{
    [Fact]
    public async Task GetLatest_UsesSemanticNotLexicographic()
    {
        var catalog = new InMemoryWorkflowCatalog();

        await catalog.SaveAsync(new AgentWorkflowDefinition { Name = "v", Version = "1.0.0" });
        await catalog.SaveAsync(new AgentWorkflowDefinition { Name = "v", Version = "9.0.0" });
        await catalog.SaveAsync(new AgentWorkflowDefinition { Name = "v", Version = "10.0.0" });

        var latest = await catalog.GetAsync("v");
        latest!.Version.Should().Be("10.0.0");
    }
}
