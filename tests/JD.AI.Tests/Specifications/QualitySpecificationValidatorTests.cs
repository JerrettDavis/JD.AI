using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class QualitySpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidQualitySpecification_RoundTripsFields()
    {
        var spec = QualitySpecificationParser.Parse(ValidQualityYaml());

        spec.Id.Should().Be("quality.api-response-time");
        spec.Category.Should().Be("performance");
        spec.Slos.Should().ContainSingle(slo => slo.Name == "P99 API Response Time");
        spec.Slis.Should().ContainSingle(sli => sli.Metric == "http_request_duration_seconds");
    }

    [Fact]
    public void Validate_ValidQualitySpecification_ReturnsNoErrors()
    {
        var spec = QualitySpecificationParser.Parse(ValidQualityYaml());

        var errors = QualitySpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidQualitySpecification_ReturnsErrors()
    {
        var spec = QualitySpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Quality
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            category: unknown
            slos: []
            slis: []
            errorBudgets: []
            scalabilityExpectations: []
            trace:
              upstream: []
              downstream:
                testing: []
                observability: []
                operations: []
            """);

        var errors = QualitySpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match quality.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("category must be one of", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("slos must contain at least one SLO", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("slis must contain at least one SLI", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("trace.upstream", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_BlankSloName_ReturnsError()
    {
        var spec = QualitySpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Quality
            id: quality.blank-slo
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-quality-nfr-agent
              lastReviewed: 2026-03-07
              changeReason: Test blank SLO name.
            category: performance
            slos:
              - name: ""
                target: "<=500ms"
            slis:
              - name: Latency
                metric: http_request_duration_seconds
            errorBudgets: []
            scalabilityExpectations: []
            trace:
              upstream:
                - specs/vision/examples/vision.example.yaml
              downstream:
                testing: []
                observability: []
                operations: []
            """);

        var errors = QualitySpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("slos[0].name is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = QualitySpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_MissingUpstreamReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/quality/examples/quality.example.yaml",
            ValidQualityYaml(upstreamRefs: ["specs/vision/missing.yaml"]));

        var errors = QualitySpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("specs/vision/missing.yaml", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/vision/examples/vision.example.yaml", "vision");
        _fixture.CreateFile("specs/quality/schema/quality.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/quality/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: QualityIndex
            entries:
              - id: quality.api-response-time
                title: API Response Time Quality
                path: specs/quality/examples/quality.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/quality/examples/quality.example.yaml", ValidQualityYaml());
        _fixture.CreateFile("tests/JD.AI.Tests/Specifications/QualitySpecificationRepositoryTests.cs", "test");
        _fixture.CreateFile("src/JD.AI.Core/Specifications/QualitySpecification.cs", "code");
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidQualityYaml(IReadOnlyList<string>? upstreamRefs = null)
    {
        var upstreamLines = string.Join(Environment.NewLine, (upstreamRefs ?? ["specs/vision/examples/vision.example.yaml"]).Select(item => $"      - {item}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Quality
            id: quality.api-response-time
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-quality-nfr-agent
              lastReviewed: 2026-03-07
              changeReason: Establish canonical quality specifications for JD.AI.
            category: performance
            slos:
              - name: P99 API Response Time
                target: "<=500ms"
                description: 99th percentile response time must remain at or below 500ms.
            slis:
              - name: API Response Latency
                metric: http_request_duration_seconds
                unit: milliseconds
            errorBudgets:
              - sloRef: P99 API Response Time
                budget: "0.1%"
                window: 30d
            scalabilityExpectations:
              - dimension: concurrent-users
                current: "100"
                target: "1000"
            trace:
              upstream:
            {{upstreamLines}}
              downstream:
                testing:
                  - tests/JD.AI.Tests/Specifications/QualitySpecificationRepositoryTests.cs
                observability: []
                operations: []
            """;
    }
}
