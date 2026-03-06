using FluentAssertions;
using JD.AI.Core.Governance;
using JD.AI.Tests.Fixtures;
using YamlDotNet.Core;

namespace JD.AI.Tests.Governance;

public sealed class PolicyParserTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static string MinimalYaml(string name = "test-policy") => $$"""
        apiVersion: jdai/v1
        kind: Policy
        metadata:
          name: {{name}}
          scope: User
          priority: 10
        spec: {}
        """;

    private static string FullYaml => """
        apiVersion: jdai/v1
        kind: Policy
        metadata:
          name: full-policy
          scope: Global
          priority: 5
        spec:
          tools:
            allowed:
              - read_file
              - write_file
            denied:
              - shell_exec
          providers:
            allowed:
              - openai
            denied:
              - ollama
          models:
            maxContextWindow: 128000
            denied:
              - gpt-4-turbo
          budget:
            maxDailyUsd: 10.00
            maxMonthlyUsd: 100.00
            alertThresholdPercent: 75
          data:
            noExternalProviders:
              - openai
            redactPatterns:
              - "\\d{4}-\\d{4}-\\d{4}-\\d{4}"
          sessions:
            retentionDays: 30
            requireProjectTag: true
          audit:
            enabled: true
            sink: file
        """;

    [Fact]
    public void Parse_MinimalYaml_ReturnsDocumentWithDefaults()
    {
        var doc = PolicyParser.Parse(MinimalYaml());

        doc.ApiVersion.Should().Be("jdai/v1");
        doc.Kind.Should().Be("Policy");
        doc.Metadata.Name.Should().Be("test-policy");
        doc.Metadata.Scope.Should().Be(PolicyScope.User);
        doc.Metadata.Priority.Should().Be(10);
        doc.Spec.Should().NotBeNull();
        doc.Spec.Tools.Should().BeNull();
        doc.Spec.Providers.Should().BeNull();
        doc.Spec.Budget.Should().BeNull();
    }

    [Fact]
    public void Parse_FullYaml_RoundTripsAllFields()
    {
        var doc = PolicyParser.Parse(FullYaml);

        doc.Metadata.Name.Should().Be("full-policy");
        doc.Metadata.Scope.Should().Be(PolicyScope.Global);
        doc.Metadata.Priority.Should().Be(5);

        doc.Spec.Tools.Should().NotBeNull();
        doc.Spec.Tools!.Allowed.Should().Contain("read_file").And.Contain("write_file");
        doc.Spec.Tools.Denied.Should().Contain("shell_exec");

        doc.Spec.Providers.Should().NotBeNull();
        doc.Spec.Providers!.Allowed.Should().Contain("openai");
        doc.Spec.Providers.Denied.Should().Contain("ollama");

        doc.Spec.Models.Should().NotBeNull();
        doc.Spec.Models!.MaxContextWindow.Should().Be(128000);
        doc.Spec.Models.Denied.Should().Contain("gpt-4-turbo");

        doc.Spec.Budget.Should().NotBeNull();
        doc.Spec.Budget!.MaxDailyUsd.Should().Be(10.00m);
        doc.Spec.Budget.MaxMonthlyUsd.Should().Be(100.00m);
        doc.Spec.Budget.AlertThresholdPercent.Should().Be(75);

        doc.Spec.Data.Should().NotBeNull();
        doc.Spec.Data!.NoExternalProviders.Should().Contain("openai");
        doc.Spec.Data.RedactPatterns.Should().HaveCount(1);

        doc.Spec.Sessions.Should().NotBeNull();
        doc.Spec.Sessions!.RetentionDays.Should().Be(30);
        doc.Spec.Sessions.RequireProjectTag.Should().BeTrue();

        doc.Spec.Audit.Should().NotBeNull();
        doc.Spec.Audit!.Enabled.Should().BeTrue();
        doc.Spec.Audit.Sink.Should().Be("file");
    }

    [Fact]
    public void Parse_MissingOptionalFields_UsesDefaults()
    {
        var yaml = """
            apiVersion: jdai/v1
            kind: Policy
            metadata:
              name: minimal
            spec: {}
            """;

        var doc = PolicyParser.Parse(yaml);

        doc.Metadata.Name.Should().Be("minimal");
        doc.Metadata.Scope.Should().Be(PolicyScope.User);
        doc.Metadata.Priority.Should().Be(0);
        doc.Spec.Tools.Should().BeNull();
        doc.Spec.Budget.Should().BeNull();
    }

    [Fact]
    public void Parse_UnknownProperties_AreIgnored()
    {
        var yaml = """
            apiVersion: jdai/v1
            kind: Policy
            metadata:
              name: test
              unknownField: some-value
            spec: {}
            extraTopLevel: ignored
            """;

        var act = () => PolicyParser.Parse(yaml);

        act.Should().NotThrow();
    }

    [Fact]
    public void Parse_InvalidYaml_ThrowsYamlException()
    {
        var invalidYaml = "metadata:\n  name: bad\n    indented: wrong";

        var act = () => PolicyParser.Parse(invalidYaml);

        act.Should().Throw<YamlException>();
    }

    [Fact]
    public void ParseFile_ValidFile_ReturnsDocument()
    {
        var filePath = Path.Combine(_fixture.DirectoryPath, "policy.yaml");
        File.WriteAllText(filePath, MinimalYaml("file-test"));

        var doc = PolicyParser.ParseFile(filePath);

        doc.Metadata.Name.Should().Be("file-test");
    }

    [Fact]
    public void ParseDirectory_WithYamlFiles_ReturnsAllDocuments()
    {
        File.WriteAllText(Path.Combine(_fixture.DirectoryPath, "p1.yaml"), MinimalYaml("p1"));
        File.WriteAllText(Path.Combine(_fixture.DirectoryPath, "p2.yml"), MinimalYaml("p2"));
        File.WriteAllText(Path.Combine(_fixture.DirectoryPath, "readme.txt"), "not a policy");

        var docs = PolicyParser.ParseDirectory(_fixture.DirectoryPath).ToList();

        docs.Should().HaveCount(2);
        docs.Select(d => d.Metadata.Name).Should().Contain(["p1", "p2"]);
    }

    [Fact]
    public void ParseDirectory_NonExistentDirectory_ReturnsEmpty()
    {
        var nonExistent = Path.Combine(_fixture.DirectoryPath, "does-not-exist");

        var docs = PolicyParser.ParseDirectory(nonExistent).ToList();

        docs.Should().BeEmpty();
    }

    [Fact]
    public void ParseDirectory_InvalidFileAmongstValid_SkipsInvalid()
    {
        File.WriteAllText(Path.Combine(_fixture.DirectoryPath, "good.yaml"), MinimalYaml("good"));
        File.WriteAllText(Path.Combine(_fixture.DirectoryPath, "bad.yaml"), "bad:\n  yaml:\n    nope:\n  oops");

        var docs = PolicyParser.ParseDirectory(_fixture.DirectoryPath).ToList();

        docs.Should().HaveCount(1);
        docs[0].Metadata.Name.Should().Be("good");
    }
}
