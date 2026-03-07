using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class InterfaceSpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidInterfaceSpecification_RoundTripsFields()
    {
        var spec = InterfaceSpecificationParser.Parse(ValidInterfaceYaml());

        spec.Id.Should().Be("interface.agent-chat-api");
        spec.InterfaceType.Should().Be("rest");
        spec.Operations.Should().ContainSingle(operation => operation.Name == "SendMessage");
        spec.MessageSchemas.Should().ContainSingle(schema => schema.Name == "ChatMessageRequest");
    }

    [Fact]
    public void Validate_ValidInterfaceSpecification_ReturnsNoErrors()
    {
        var spec = InterfaceSpecificationParser.Parse(ValidInterfaceYaml());

        var errors = InterfaceSpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidInterfaceSpecification_ReturnsErrors()
    {
        var spec = InterfaceSpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Interface
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            interfaceType: invalid
            operations: []
            messageSchemas: []
            compatibilityRules: []
            trace:
              upstream: []
              downstream:
                code: []
                testing: []
                deployment: []
            """);

        var errors = InterfaceSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match interface.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("version must be greater than or equal to 1", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("status must be one of", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("interfaceType must be one of", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("operations must contain at least one operation", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("compatibilityRules must contain at least one rule", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = InterfaceSpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_MissingDownstreamReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/interfaces/examples/interfaces.example.yaml",
            ValidInterfaceYaml(codeRefs: ["src/missing.cs"]));

        var errors = InterfaceSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("src/missing.cs", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/interfaces/schema/interfaces.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/interfaces/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: InterfaceIndex
            entries:
              - id: interface.agent-chat-api
                title: Agent Chat API
                path: specs/interfaces/examples/interfaces.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/interfaces/examples/interfaces.example.yaml", ValidInterfaceYaml());
        _fixture.CreateFile("specs/usecases/examples/usecases.example.yaml", "usecase");
        _fixture.CreateFile("tests/JD.AI.Tests/Specifications/InterfaceSpecificationRepositoryTests.cs", "test");
        _fixture.CreateFile("src/JD.AI.Core/Specifications/InterfaceSpecification.cs", "code");
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidInterfaceYaml(
        IReadOnlyList<string>? codeRefs = null)
    {
        var codeLines = string.Join(Environment.NewLine, (codeRefs ?? ["src/JD.AI.Core/Specifications/InterfaceSpecification.cs"]).Select(item => $"      - {item}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Interface
            id: interface.agent-chat-api
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-interface-contract-architect
              lastReviewed: 2026-03-07
              changeReason: Establish canonical interface specifications for JD.AI.
            interfaceType: rest
            operations:
              - name: SendMessage
                method: POST
                path: /api/chat/messages
                description: Sends a user message to the agent.
            messageSchemas:
              - name: ChatMessageRequest
                format: application/json
                description: Payload containing the user message text.
            compatibilityRules:
              - Existing operation paths must not be removed without a deprecation period.
            trace:
              upstream:
                - specs/usecases/examples/usecases.example.yaml
              downstream:
                code:
            {{codeLines}}
                testing:
                  - tests/JD.AI.Tests/Specifications/InterfaceSpecificationRepositoryTests.cs
                deployment: []
            """;
    }
}
