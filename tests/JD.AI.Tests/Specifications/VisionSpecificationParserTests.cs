using FluentAssertions;
using JD.AI.Core.Specifications;

namespace JD.AI.Tests.Specifications;

public sealed class VisionSpecificationParserTests
{
    private const string ValidVisionYaml = """
        apiVersion: jdai.upss/v1
        kind: Vision
        id: vision.core
        version: 1
        status: active
        metadata:
          owners:
            - team-core
          reviewers:
            - reviewer-a
          lastReviewed: "2026-01-15"
          changeReason: Initial creation
        problemStatement: Users need AI assistance
        mission: Provide intelligent assistance
        targetUsers:
          - id: user.developer
            name: Developer
            needs:
              - code generation
        valueProposition:
          summary: Intelligent AI assistant
          differentiators:
            - multi-provider support
        successMetrics:
          - id: metric.adoption
            name: Adoption Rate
            target: ">80%"
            measurement: monthly active users
        constraints:
          - must run offline
        nonGoals:
          - replace human judgment
        trace:
          upstream:
            - docs/architecture.md
          downstream:
            capabilities:
              - capability.chat
        """;

    [Fact]
    public void Parse_ValidYaml_DeserializesAllFields()
    {
        var spec = VisionSpecificationParser.Parse(ValidVisionYaml);

        spec.ApiVersion.Should().Be("jdai.upss/v1");
        spec.Kind.Should().Be("Vision");
        spec.Id.Should().Be("vision.core");
        spec.Version.Should().Be(1);
        spec.Status.Should().Be("active");
        spec.ProblemStatement.Should().Be("Users need AI assistance");
        spec.Mission.Should().Be("Provide intelligent assistance");
    }

    [Fact]
    public void Parse_ValidYaml_DeserializesMetadata()
    {
        var spec = VisionSpecificationParser.Parse(ValidVisionYaml);

        spec.Metadata.Should().NotBeNull();
        spec.Metadata.Owners.Should().Contain("team-core");
        spec.Metadata.Reviewers.Should().Contain("reviewer-a");
        spec.Metadata.LastReviewed.Should().Be("2026-01-15");
        spec.Metadata.ChangeReason.Should().Be("Initial creation");
    }

    [Fact]
    public void Parse_ValidYaml_DeserializesTargetUsers()
    {
        var spec = VisionSpecificationParser.Parse(ValidVisionYaml);

        spec.TargetUsers.Should().HaveCount(1);
        spec.TargetUsers[0].Id.Should().Be("user.developer");
        spec.TargetUsers[0].Name.Should().Be("Developer");
        spec.TargetUsers[0].Needs.Should().Contain("code generation");
    }

    [Fact]
    public void Parse_ValidYaml_DeserializesValueProposition()
    {
        var spec = VisionSpecificationParser.Parse(ValidVisionYaml);

        spec.ValueProposition.Summary.Should().Be("Intelligent AI assistant");
        spec.ValueProposition.Differentiators.Should().Contain("multi-provider support");
    }

    [Fact]
    public void Parse_ValidYaml_DeserializesSuccessMetrics()
    {
        var spec = VisionSpecificationParser.Parse(ValidVisionYaml);

        spec.SuccessMetrics.Should().HaveCount(1);
        spec.SuccessMetrics[0].Id.Should().Be("metric.adoption");
        spec.SuccessMetrics[0].Name.Should().Be("Adoption Rate");
        spec.SuccessMetrics[0].Target.Should().Be(">80%");
        spec.SuccessMetrics[0].Measurement.Should().Be("monthly active users");
    }

    [Fact]
    public void Parse_ValidYaml_DeserializesTraceability()
    {
        var spec = VisionSpecificationParser.Parse(ValidVisionYaml);

        spec.Trace.Upstream.Should().Contain("docs/architecture.md");
        spec.Trace.Downstream.Capabilities.Should().Contain("capability.chat");
    }

    [Fact]
    public void Parse_ValidYaml_DeserializesConstraintsAndNonGoals()
    {
        var spec = VisionSpecificationParser.Parse(ValidVisionYaml);

        spec.Constraints.Should().Contain("must run offline");
        spec.NonGoals.Should().Contain("replace human judgment");
    }

    [Fact]
    public void Parse_MinimalYaml_DefaultsCollectionsToEmpty()
    {
        var yaml = """
            apiVersion: jdai.upss/v1
            kind: Vision
            id: vision.minimal
            """;

        var spec = VisionSpecificationParser.Parse(yaml);

        spec.TargetUsers.Should().BeEmpty();
        spec.SuccessMetrics.Should().BeEmpty();
        spec.Constraints.Should().BeEmpty();
        spec.NonGoals.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NullYaml_ThrowsArgumentNullException()
    {
        var act = () => VisionSpecificationParser.Parse(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseFile_NullPath_ThrowsArgumentNullException()
    {
        var act = () => VisionSpecificationParser.ParseFile(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseFile_NonExistentPath_ThrowsFileNotFoundException()
    {
        var act = () => VisionSpecificationParser.ParseFile("/nonexistent/path.yaml");
        act.Should().Throw<Exception>();
    }

    // ── Index parsing ────────────────────────────────────────────

    [Fact]
    public void ParseIndex_ValidYaml_DeserializesEntries()
    {
        var yaml = """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.core
                title: Core Vision
                path: specs/vision/core.yaml
                status: active
              - id: vision.ext
                title: Extension Vision
                path: specs/vision/ext.yaml
                status: draft
            """;

        var index = VisionSpecificationParser.ParseIndex(yaml);

        index.ApiVersion.Should().Be("jdai.upss/v1");
        index.Kind.Should().Be("VisionIndex");
        index.Entries.Should().HaveCount(2);
        index.Entries[0].Id.Should().Be("vision.core");
        index.Entries[0].Title.Should().Be("Core Vision");
        index.Entries[0].Path.Should().Be("specs/vision/core.yaml");
        index.Entries[0].Status.Should().Be("active");
    }

    [Fact]
    public void ParseIndex_NullYaml_ThrowsArgumentNullException()
    {
        var act = () => VisionSpecificationParser.ParseIndex(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseIndexFile_NullPath_ThrowsArgumentNullException()
    {
        var act = () => VisionSpecificationParser.ParseIndexFile(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Capability index parsing ─────────────────────────────────

    [Fact]
    public void ParseCapabilityIndex_ValidYaml_DeserializesEntries()
    {
        var yaml = """
            apiVersion: jdai.upss/v1
            kind: CapabilityIndex
            entries:
              - id: capability.chat
                path: specs/capabilities/chat.yaml
              - id: capability.search
                path: specs/capabilities/search.yaml
            """;

        var index = VisionSpecificationParser.ParseCapabilityIndex(yaml);

        index.ApiVersion.Should().Be("jdai.upss/v1");
        index.Kind.Should().Be("CapabilityIndex");
        index.Entries.Should().HaveCount(2);
        index.Entries[0].Id.Should().Be("capability.chat");
        index.Entries[0].Path.Should().Be("specs/capabilities/chat.yaml");
    }

    [Fact]
    public void ParseCapabilityIndex_NullYaml_ThrowsArgumentNullException()
    {
        var act = () => VisionSpecificationParser.ParseCapabilityIndex(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseCapabilityIndexFile_NullPath_ThrowsArgumentNullException()
    {
        var act = () => VisionSpecificationParser.ParseCapabilityIndexFile(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseFile_ValidTempFile_RoundTrips()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmpFile, ValidVisionYaml);
            var spec = VisionSpecificationParser.ParseFile(tmpFile);

            spec.Id.Should().Be("vision.core");
            spec.Kind.Should().Be("Vision");
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }
}
