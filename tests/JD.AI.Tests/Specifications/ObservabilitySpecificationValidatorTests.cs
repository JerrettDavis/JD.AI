using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class ObservabilitySpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidObservabilitySpecification_RoundTripsFields()
    {
        var spec = ObservabilitySpecificationParser.Parse(ValidObservabilityYaml());

        spec.Id.Should().Be("observability.jdai-gateway");
        spec.ServiceRefs.Should().ContainSingle(serviceRef => serviceRef == "jdai-gateway");
        spec.Metrics.Should().ContainSingle(metric => metric.Name == "gateway_requests_total");
        spec.Alerts.Should().ContainSingle(alert => alert.Name == "gateway_high_error_rate");
    }

    [Fact]
    public void Validate_ValidObservabilitySpecification_ReturnsNoErrors()
    {
        var spec = ObservabilitySpecificationParser.Parse(ValidObservabilityYaml());

        var errors = ObservabilitySpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidObservabilitySpecification_ReturnsErrors()
    {
        var spec = ObservabilitySpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Observability
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            serviceRefs: []
            metrics:
              - name: ""
                type: invalid
                description: Some metric.
            logs:
              - name: app_log
                level: verbose
                format: structured
            traces:
              - name: span1
                spanKind: unknown
                attributes: []
            alerts: []
            trace:
              upstream: []
              downstream:
                operations: []
                governance: []
            """);

        var errors = ObservabilitySpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match observability.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("serviceRefs", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("metrics[0].type", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("logs[0].level", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("traces[0].spanKind", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("alerts must contain at least one alert", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = ObservabilitySpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_MissingOperationsReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/observability/examples/observability.example.yaml",
            ValidObservabilityYaml(operationsRefs: ["tests/missing.cs"]));

        var errors = ObservabilitySpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("tests/missing.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_MissingGovernanceReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/observability/examples/observability.example.yaml",
            ValidObservabilityYaml(governanceRefs: ["src/missing.cs"]));

        var errors = ObservabilitySpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("src/missing.cs", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/observability/schema/observability.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/observability/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: ObservabilityIndex
            entries:
              - id: observability.jdai-gateway
                title: JD.AI Gateway Observability
                path: specs/observability/examples/observability.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/observability/examples/observability.example.yaml", ValidObservabilityYaml());
        _fixture.CreateFile("specs/vision/examples/vision.example.yaml", "vision");
        _fixture.CreateFile("tests/JD.AI.Tests/Specifications/ObservabilitySpecificationRepositoryTests.cs", "test");
        _fixture.CreateFile("src/JD.AI.Core/Specifications/ObservabilitySpecification.cs", "code");
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidObservabilityYaml(
        IReadOnlyList<string>? operationsRefs = null,
        IReadOnlyList<string>? governanceRefs = null)
    {
        var operationsLines = string.Join(Environment.NewLine, (operationsRefs ?? ["tests/JD.AI.Tests/Specifications/ObservabilitySpecificationRepositoryTests.cs"]).Select(item => $"      - {item}"));
        var governanceLines = string.Join(Environment.NewLine, (governanceRefs ?? ["src/JD.AI.Core/Specifications/ObservabilitySpecification.cs"]).Select(item => $"      - {item}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Observability
            id: observability.jdai-gateway
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-observability-agent
              lastReviewed: 2026-03-07
              changeReason: Establish canonical observability specifications for JD.AI.
            serviceRefs:
              - jdai-gateway
            metrics:
              - name: gateway_requests_total
                type: counter
                description: Total number of requests processed by the gateway.
            logs:
              - name: gateway_request_log
                level: info
                format: "method={method} path={path} status={status} duration={duration_ms}ms"
            traces:
              - name: gateway_inbound_request
                spanKind: server
                attributes:
                  - http.method
                  - http.route
                  - http.status_code
            alerts:
              - name: gateway_high_error_rate
                condition: rate(gateway_requests_total{status=~"5.."}[5m]) > 0.05
                severity: critical
            trace:
              upstream:
                - specs/vision/examples/vision.example.yaml
              downstream:
                operations:
            {{operationsLines}}
                governance:
            {{governanceLines}}
            """;
    }
}
