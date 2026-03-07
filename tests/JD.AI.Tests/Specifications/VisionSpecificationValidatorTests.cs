using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class VisionSpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidVisionSpecification_RoundTripsFields()
    {
        var spec = VisionSpecificationParser.Parse(ValidVisionYaml());

        spec.ApiVersion.Should().Be("jdai.upss/v1");
        spec.Kind.Should().Be("Vision");
        spec.Id.Should().Be("vision.jdai.product");
        spec.Metadata.Owners.Should().Contain("JerrettDavis");
        spec.TargetUsers.Should().HaveCount(2);
        spec.SuccessMetrics.Should().HaveCount(2);
        spec.Trace.Upstream.Should().Contain("docs/index.md");
    }

    [Fact]
    public void Validate_ValidVisionSpecification_ReturnsNoErrors()
    {
        var spec = VisionSpecificationParser.Parse(ValidVisionYaml());

        var errors = VisionSpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidVisionSpecification_ReturnsFieldErrors()
    {
        var spec = VisionSpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Vision
            id: invalid-id
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: not-a-date
              changeReason: ""
            problemStatement: ""
            mission: ""
            targetUsers: []
            valueProposition:
              summary: ""
              differentiators: []
            successMetrics: []
            constraints: []
            nonGoals: []
            trace:
              upstream: []
              downstream:
                capabilities:
                  - bad-capability
            """);

        var errors = VisionSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match vision.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("version must be greater", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("status must be one of", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("metadata.owners", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("metadata.reviewers", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("metadata.lastReviewed", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("problemStatement", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("trace.downstream.capabilities", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_ValidRepositoryArtifacts_ReturnNoErrors()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("docs/index.md", "# Docs");
        _fixture.CreateFile("specs/vision/schema/vision.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.jdai.product
                title: JD.AI Product Vision
                path: specs/vision/examples/vision.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/vision/examples/vision.example.yaml", ValidVisionYaml());

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_MissingCapabilityIndex_FailsWhenCapabilityRefsExist()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("docs/index.md", "# Docs");
        _fixture.CreateFile("specs/vision/schema/vision.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.jdai.product
                title: JD.AI Product Vision
                path: specs/vision/examples/vision.example.yaml
                status: draft
            """);
        _fixture.CreateFile(
            "specs/vision/examples/vision.example.yaml",
            ValidVisionYaml(["capability.agent-workflow"]));

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error =>
            error.Contains("specs/capabilities/index.yaml is missing", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_UnknownCapabilityReference_Fails()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("docs/index.md", "# Docs");
        _fixture.CreateFile("specs/vision/schema/vision.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.jdai.product
                title: JD.AI Product Vision
                path: specs/vision/examples/vision.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/capabilities/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: CapabilityIndex
            entries:
              - id: capability.other-flow
                path: specs/capabilities/examples/other.yaml
            """);
        _fixture.CreateFile(
            "specs/vision/examples/vision.example.yaml",
            ValidVisionYaml(["capability.agent-workflow"]));

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error =>
            error.Contains("capability.agent-workflow", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_MissingUpstreamReference_Fails()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/vision/schema/vision.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.jdai.product
                title: JD.AI Product Vision
                path: specs/vision/examples/vision.example.yaml
                status: draft
            """);
        _fixture.CreateFile(
            "specs/vision/examples/vision.example.yaml",
            ValidVisionYaml(upstream: ["docs/missing.md"]));

        var errors = VisionSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error =>
            error.Contains("docs/missing.md", StringComparison.Ordinal));
    }

    private static string ValidVisionYaml(
        IReadOnlyList<string>? capabilities = null,
        IReadOnlyList<string>? upstream = null)
    {
        var upstreamLines = string.Join(
            Environment.NewLine,
            (upstream ?? ["docs/index.md"]).Select(item => $"    - {item}"));

        var capabilityItems = capabilities ?? Array.Empty<string>();
        var capabilityBlock = capabilityItems.Count == 0
            ? "    capabilities: []"
            : "    capabilities:" + Environment.NewLine + string.Join(
                Environment.NewLine,
                capabilityItems.Select(item => $"      - {item}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Vision
            id: vision.jdai.product
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-vision-architect
              lastReviewed: 2026-03-07
              changeReason: Establish canonical repo-native product intent for JD.AI.
            problemStatement: Product intent is currently spread across issues, docs, and code without a single canonical blueprint.
            mission: Enable safe, agent-driven software delivery from repo-native specifications.
            targetUsers:
              - id: user.product-owner
                name: Product owners
                needs:
                  - Define product direction in a traceable, enforceable format.
              - id: user.delivery-agent
                name: Delivery agents
                needs:
                  - Read canonical product intent and trace implementations back to it.
            valueProposition:
              summary: JD.AI should let teams define products in-repo so humans and agents share one authoritative blueprint.
              differentiators:
                - Repo-native specs stay versioned beside code and tests.
                - Machine-readable structure enables validation, drift detection, and agent workflows.
            successMetrics:
              - id: metric.traceability-coverage
                name: Traceability coverage
                target: ">=95% of implementation artifacts map back to product intent."
                measurement: Traceability audit across specs, code, tests, and deployment assets.
              - id: metric.agent-delivery-readiness
                name: Agent delivery readiness
                target: "All new capabilities can be scaffolded from validated specs."
                measurement: Agent workflow dry-runs succeed without manual reconstruction of product intent.
            constraints:
              - Specifications must be human-readable Markdown plus machine-readable YAML/JSON.
              - Validation must run inside repository CI without external services.
            nonGoals:
              - Replace human approval for high-risk architectural or release decisions.
              - Encode sprint planning or delivery scheduling inside the vision spec.
            trace:
              upstream:
            {{upstreamLines}}
              downstream:
            {{capabilityBlock}}
            """;
    }
}
