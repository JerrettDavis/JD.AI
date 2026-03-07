using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class DeploymentSpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidDeploymentSpecification_RoundTripsFields()
    {
        var spec = DeploymentSpecificationParser.Parse(ValidDeploymentYaml());

        spec.Id.Should().Be("deployment.jdai-gateway");
        spec.RollbackStrategy.Should().Be("blue-green");
        spec.Environments.Should().HaveCount(2);
        spec.PipelineStages.Should().HaveCount(2);
        spec.PromotionGates.Should().ContainSingle(gate => gate.FromEnv == "staging");
    }

    [Fact]
    public void Validate_ValidDeploymentSpecification_ReturnsNoErrors()
    {
        var spec = DeploymentSpecificationParser.Parse(ValidDeploymentYaml());

        var errors = DeploymentSpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidDeploymentSpecification_ReturnsErrors()
    {
        var spec = DeploymentSpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Deployment
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            environments:
              - name: ""
                type: invalid-type
                region: us-east-1
            pipelineStages:
              - name: ""
                order: 1
                automated: true
            promotionGates: []
            infrastructureRefs: []
            rollbackStrategy: invalid-strategy
            trace:
              upstream: []
              downstream:
                operations: []
                observability: []
            """);

        var errors = DeploymentSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match deployment.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("rollbackStrategy must be one of", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("environments[0].type must be one of", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("environments[0].name is required", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("pipelineStages[0].name is required", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("trace.upstream", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_EmptyEnvironments_ReturnsError()
    {
        var spec = DeploymentSpecificationParser.Parse(ValidDeploymentYaml(environments: "environments: []"));

        var errors = DeploymentSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("environments must contain at least one environment", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_EmptyPipelineStages_ReturnsError()
    {
        var spec = DeploymentSpecificationParser.Parse(ValidDeploymentYaml(pipelineStages: "pipelineStages: []"));

        var errors = DeploymentSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("pipelineStages must contain at least one stage", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = DeploymentSpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_MissingOperationsReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/deployment/examples/deployment.example.yaml",
            ValidDeploymentYaml(operationsRefs: ["src/missing.cs"]));

        var errors = DeploymentSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("src/missing.cs", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/deployment/schema/deployment.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/deployment/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: DeploymentIndex
            entries:
              - id: deployment.jdai-gateway
                title: JD.AI Gateway Deployment
                path: specs/deployment/examples/deployment.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/deployment/examples/deployment.example.yaml", ValidDeploymentYaml());
        _fixture.CreateFile("specs/vision/examples/vision.example.yaml", "vision");
        _fixture.CreateFile("tests/JD.AI.Tests/Specifications/DeploymentSpecificationRepositoryTests.cs", "test");
        _fixture.CreateFile("src/JD.AI.Core/Specifications/DeploymentSpecification.cs", "code");
        _fixture.CreateFile("infra/gateway/main.tf", "infra");
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidDeploymentYaml(
        string? environments = null,
        string? pipelineStages = null,
        IReadOnlyList<string>? operationsRefs = null)
    {
        var operationsLines = string.Join(Environment.NewLine, (operationsRefs ?? ["tests/JD.AI.Tests/Specifications/DeploymentSpecificationRepositoryTests.cs"]).Select(item => $"      - {item}"));

        var envBlock = environments ?? """
            environments:
              - name: staging
                type: staging
                region: us-east-1
              - name: production
                type: production
                region: us-east-1
            """;

        var stagesBlock = pipelineStages ?? """
            pipelineStages:
              - name: Build and Test
                order: 1
                automated: true
              - name: Deploy to Staging
                order: 2
                automated: true
            """;

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Deployment
            id: deployment.jdai-gateway
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-deployment-topology-agent
              lastReviewed: 2026-03-07
              changeReason: Establish canonical deployment specifications for JD.AI.
            {{envBlock}}
            {{stagesBlock}}
            promotionGates:
              - fromEnv: staging
                toEnv: production
                criteria:
                  - All integration tests pass in staging environment.
            infrastructureRefs:
              - infra/gateway/main.tf
            rollbackStrategy: blue-green
            trace:
              upstream:
                - specs/vision/examples/vision.example.yaml
              downstream:
                operations:
            {{operationsLines}}
                observability:
                  - src/JD.AI.Core/Specifications/DeploymentSpecification.cs
            """;
    }
}
