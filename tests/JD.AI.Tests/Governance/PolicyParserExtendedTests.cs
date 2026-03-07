using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

/// <summary>
/// Extended tests for <see cref="PolicyParser"/> covering null-guard paths
/// and additional edge cases not in <see cref="PolicyParserTests"/>.
/// </summary>
public sealed class PolicyParserExtendedTests
{
    [Fact]
    public void Parse_Null_ThrowsArgumentNull()
    {
        var act = () => PolicyParser.Parse(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseFile_Null_ThrowsArgumentNull()
    {
        var act = () => PolicyParser.ParseFile(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseDirectory_Null_ThrowsArgumentNull()
    {
        var act = () => PolicyParser.ParseDirectory(null!).ToList();
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNullDocument()
    {
        // YamlDotNet deserializes empty string as null for reference types
        var doc = PolicyParser.Parse("");
        doc.Should().BeNull();
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsNullDocument()
    {
        var doc = PolicyParser.Parse("   ");
        doc.Should().BeNull();
    }

    [Fact]
    public void Parse_SpecOnly_DefaultsMetadata()
    {
        var yaml = """
            spec:
              tools:
                denied:
                  - shell_exec
            """;

        var doc = PolicyParser.Parse(yaml);

        doc.Should().NotBeNull();
        doc.Spec.Should().NotBeNull();
        doc.Spec.Tools!.Denied.Should().Contain("shell_exec");
    }

    [Fact]
    public void Parse_MultiplePolicySections_ParsesAll()
    {
        var yaml = """
            apiVersion: jdai/v1
            kind: Policy
            metadata:
              name: multi-section
              scope: Organization
              priority: 50
            spec:
              tools:
                allowed:
                  - read_file
                denied:
                  - shell_exec
              providers:
                allowed:
                  - openai
              models:
                denied:
                  - gpt-3.5-turbo
                maxContextWindow: 200000
              budget:
                maxDailyUsd: 25.00
                maxMonthlyUsd: 500.00
              data:
                redactPatterns:
                  - "\\b\\d{3}-\\d{2}-\\d{4}\\b"
              sessions:
                retentionDays: 90
              audit:
                enabled: true
                sink: webhook
            """;

        var doc = PolicyParser.Parse(yaml);

        doc.Metadata.Scope.Should().Be(PolicyScope.Organization);
        doc.Metadata.Priority.Should().Be(50);
        doc.Spec.Tools!.Allowed.Should().Contain("read_file");
        doc.Spec.Providers!.Allowed.Should().Contain("openai");
        doc.Spec.Models!.Denied.Should().Contain("gpt-3.5-turbo");
        doc.Spec.Models.MaxContextWindow.Should().Be(200_000);
        doc.Spec.Budget!.MaxDailyUsd.Should().Be(25.00m);
        doc.Spec.Sessions!.RetentionDays.Should().Be(90);
        doc.Spec.Audit!.Sink.Should().Be("webhook");
    }
}
