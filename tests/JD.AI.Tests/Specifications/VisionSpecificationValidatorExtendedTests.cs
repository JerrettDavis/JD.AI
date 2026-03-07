using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

/// <summary>
/// Extended edge-case tests for <see cref="VisionSpecificationValidator"/>.
/// Covers ValidateRepository paths: missing schema, wrong index apiVersion/kind,
/// mismatched IDs/statuses, invalid index entries, and upstream HTTP references.
/// </summary>
public sealed class VisionSpecificationValidatorExtendedTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    // ── ValidateRepository — structural errors ─────────────────

    [Fact]
    public void ValidateRepository_NullRepoRoot_ThrowsArgumentNullException()
    {
        var act = () => VisionSpecificationValidator.ValidateRepository(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateRepository_MissingVisionIndex_ReportsError()
    {
        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);
        errors.Should().ContainSingle(e => e.Contains("Missing specs/vision/index.yaml"));
    }

    [Fact]
    public void ValidateRepository_MissingSchema_ReportsWarning()
    {
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.test
                title: Test
                path: specs/vision/test.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/vision/test.yaml", MakeMinimalValidVisionYaml("vision.test"));

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().Contain(e => e.Contains("Missing specs/vision/schema/vision.schema.json"));
    }

    [Fact]
    public void ValidateRepository_WrongIndexApiVersion_ReportsError()
    {
        SetupMinimalRepo("wrong/v2", "VisionIndex");

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().Contain(e => e.Contains("Vision index apiVersion"));
    }

    [Fact]
    public void ValidateRepository_WrongIndexKind_ReportsError()
    {
        SetupMinimalRepo("jdai.upss/v1", "WrongKind");

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().Contain(e => e.Contains("Vision index kind"));
    }

    [Fact]
    public void ValidateRepository_EmptyIndexEntries_ReportsError()
    {
        _fixture.CreateFile("specs/vision/schema/vision.schema.json", "{}");
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries: []
            """);

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().Contain(e => e.Contains("at least one entry"));
    }

    [Fact]
    public void ValidateRepository_InvalidEntryId_ReportsError()
    {
        _fixture.CreateFile("specs/vision/schema/vision.schema.json", "{}");
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: INVALID
                title: Test
                path: specs/vision/test.yaml
                status: draft
            """);

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().Contain(e => e.Contains("does not match vision.<name> convention"));
    }

    [Fact]
    public void ValidateRepository_MissingEntryPath_ReportsError()
    {
        _fixture.CreateFile("specs/vision/schema/vision.schema.json", "{}");
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.test
                title: Test
                path: ""
                status: draft
            """);

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().Contain(e => e.Contains("missing a path"));
    }

    [Fact]
    public void ValidateRepository_MissingSpecFile_ReportsError()
    {
        _fixture.CreateFile("specs/vision/schema/vision.schema.json", "{}");
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.test
                title: Test
                path: specs/vision/nonexistent.yaml
                status: draft
            """);

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().Contain(e => e.Contains("spec file not found"));
    }

    [Fact]
    public void ValidateRepository_MismatchedSpecId_ReportsError()
    {
        _fixture.CreateFile("specs/vision/schema/vision.schema.json", "{}");
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.one
                title: One
                path: specs/vision/one.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/vision/one.yaml", MakeMinimalValidVisionYaml("vision.other"));

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().Contain(e => e.Contains("does not match index id"));
    }

    [Fact]
    public void ValidateRepository_MismatchedSpecStatus_ReportsError()
    {
        _fixture.CreateFile("specs/vision/schema/vision.schema.json", "{}");
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.test
                title: Test
                path: specs/vision/test.yaml
                status: active
            """);
        // Spec has status=draft but index says active
        _fixture.CreateFile("specs/vision/test.yaml", MakeMinimalValidVisionYaml("vision.test"));

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().Contain(e => e.Contains("does not match index status"));
    }

    [Fact]
    public void ValidateRepository_UpstreamHttpUrl_Accepted()
    {
        _fixture.CreateFile("specs/vision/schema/vision.schema.json", "{}");
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.test
                title: Test
                path: specs/vision/test.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/vision/test.yaml",
            MakeMinimalValidVisionYaml("vision.test", upstream: ["https://example.com/spec"]));

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        // HTTP upstream should be accepted, not flagged as missing file
        errors.Should().NotContain(e => e.Contains("https://example.com"));
    }

    [Fact]
    public void ValidateRepository_BlankUpstream_ReportsError()
    {
        _fixture.CreateFile("specs/vision/schema/vision.schema.json", "{}");
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.test
                title: Test
                path: specs/vision/test.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/vision/test.yaml",
            MakeMinimalValidVisionYaml("vision.test", upstream: [" "]));

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().Contain(e => e.Contains("must not be blank"));
    }

    [Fact]
    public void ValidateRepository_ValidCapabilityReferences_NoError()
    {
        _fixture.CreateFile("specs/vision/schema/vision.schema.json", "{}");
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.test
                title: Test
                path: specs/vision/test.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/capabilities/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: CapabilityIndex
            entries:
              - id: capability.chat
                path: specs/capabilities/chat.yaml
            """);
        _fixture.CreateFile("specs/vision/test.yaml",
            MakeMinimalValidVisionYaml("vision.test", capabilities: ["capability.chat"]));

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().NotContain(e => e.Contains("capability"));
    }

    [Fact]
    public void ValidateRepository_NoCapabilityRefs_NoCapabilityErrors()
    {
        _fixture.CreateFile("specs/vision/schema/vision.schema.json", "{}");
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.test
                title: Test
                path: specs/vision/test.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/vision/test.yaml",
            MakeMinimalValidVisionYaml("vision.test"));

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().NotContain(e => e.Contains("capability"));
    }

    // ── Helpers ────────────────────────────────────────────────

    private void SetupMinimalRepo(string apiVersion, string kind)
    {
        _fixture.CreateFile("specs/vision/schema/vision.schema.json", "{}");
        _fixture.CreateFile("specs/vision/index.yaml", $"""
            apiVersion: {apiVersion}
            kind: {kind}
            entries:
              - id: vision.test
                title: Test
                path: specs/vision/test.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/vision/test.yaml", MakeMinimalValidVisionYaml("vision.test"));
    }

    private static string MakeMinimalValidVisionYaml(
        string id,
        IReadOnlyList<string>? upstream = null,
        IReadOnlyList<string>? capabilities = null)
    {
        var upstreamBlock = upstream is null || upstream.Count == 0
            ? "    - docs/readme.md"
            : string.Join("\n", upstream.Select(u => $"    - {u}"));

        var capabilityBlock = capabilities is null || capabilities.Count == 0
            ? "    capabilities: []"
            : "    capabilities:\n" + string.Join("\n", capabilities.Select(c => $"      - {c}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Vision
            id: {{id}}
            version: 1
            status: draft
            metadata:
              owners:
                - team
              reviewers:
                - reviewer
              lastReviewed: "2026-01-01"
              changeReason: test
            problemStatement: test problem
            mission: test mission
            targetUsers:
              - id: user.test
                name: Tester
                needs:
                  - testing
            valueProposition:
              summary: test value
              differentiators:
                - test diff
            successMetrics:
              - id: metric.test
                name: Test Metric
                target: ">0"
                measurement: count
            constraints:
              - test constraint
            nonGoals:
              - test non-goal
            trace:
              upstream:
            {{upstreamBlock}}
              downstream:
            {{capabilityBlock}}
            """;
    }
}
