using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class PolicySpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidPolicySpecification_RoundTripsFields()
    {
        var spec = PolicySpecificationParser.Parse(ValidPolicyYaml());

        spec.Id.Should().Be("policy.api-security-baseline");
        spec.PolicyType.Should().Be("security");
        spec.Severity.Should().Be("high");
        spec.Scope.Should().Contain("src/JD.AI.Core");
        spec.Rules.Should().ContainSingle(rule => rule.Id == "require-auth-on-endpoints");
        spec.Enforcement.Mode.Should().Be("enforce");
    }

    [Fact]
    public void Validate_ValidPolicySpecification_ReturnsNoErrors()
    {
        var spec = PolicySpecificationParser.Parse(ValidPolicyYaml());

        var errors = PolicySpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidPolicySpecification_ReturnsErrors()
    {
        var spec = PolicySpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Policy
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            policyType: unknown
            severity: extreme
            scope: []
            rules: []
            exceptions: []
            enforcement:
              mode: invalid
            trace:
              upstream: []
              downstream:
                ci: []
                enforcement: []
                operations: []
            """);

        var errors = PolicySpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match policy.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("policyType", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("severity", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("scope", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("rules must contain at least one rule", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("enforcement.mode", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = PolicySpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_MissingEnforcementReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/policies/examples/policies.example.yaml",
            ValidPolicyYaml(enforcementRefs: ["src/missing.cs"]));

        var errors = PolicySpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("src/missing.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_MissingCiReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/policies/examples/policies.example.yaml",
            ValidPolicyYaml(ciRefs: ["tests/missing.cs"]));

        var errors = PolicySpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("tests/missing.cs", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/policies/schema/policies.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/policies/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: PolicyIndex
            entries:
              - id: policy.api-security-baseline
                title: API Security Baseline Policy
                path: specs/policies/examples/policies.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/policies/examples/policies.example.yaml", ValidPolicyYaml());
        _fixture.CreateFile("specs/vision/examples/vision.example.yaml", "vision");
        _fixture.CreateFile("tests/JD.AI.Tests/Specifications/PolicySpecificationRepositoryTests.cs", "test");
        _fixture.CreateFile("src/JD.AI.Core/Specifications/PolicySpecification.cs", "code");
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidPolicyYaml(
        IReadOnlyList<string>? ciRefs = null,
        IReadOnlyList<string>? enforcementRefs = null)
    {
        var ciLines = string.Join(Environment.NewLine, (ciRefs ?? ["tests/JD.AI.Tests/Specifications/PolicySpecificationRepositoryTests.cs"]).Select(item => $"      - {item}"));
        var enforcementLines = string.Join(Environment.NewLine, (enforcementRefs ?? ["src/JD.AI.Core/Specifications/PolicySpecification.cs"]).Select(item => $"      - {item}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Policy
            id: policy.api-security-baseline
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-policy-rules-architect
              lastReviewed: 2026-03-07
              changeReason: Establish canonical policy specifications for JD.AI.
            policyType: security
            severity: high
            scope:
              - src/JD.AI.Core
            rules:
              - id: require-auth-on-endpoints
                description: All public API endpoints must require authentication.
                expression: endpoint.auth != null
            exceptions: []
            enforcement:
              mode: enforce
            trace:
              upstream:
                - specs/vision/examples/vision.example.yaml
              downstream:
                ci:
            {{ciLines}}
                enforcement:
            {{enforcementLines}}
                operations: []
            """;
    }
}
