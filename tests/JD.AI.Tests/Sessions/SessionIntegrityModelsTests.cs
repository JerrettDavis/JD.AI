using System.Collections.ObjectModel;
using FluentAssertions;
using JD.AI.Core.Sessions;

namespace JD.AI.Tests.Sessions;

public sealed class SessionIntegrityModelsTests
{
    [Fact]
    public void IntegrityResult_Construction()
    {
        var issues = new ReadOnlyCollection<string>(["issue 1", "issue 2"]);
        var result = new IntegrityResult(10, 2, issues);

        result.SessionsChecked.Should().Be(10);
        result.MismatchesFound.Should().Be(2);
        result.Issues.Should().HaveCount(2);
    }

    [Fact]
    public void IntegrityResult_NoIssues()
    {
        var result = new IntegrityResult(5, 0, new ReadOnlyCollection<string>([]));

        result.SessionsChecked.Should().Be(5);
        result.MismatchesFound.Should().Be(0);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void IntegrityResult_RecordEquality()
    {
        var issues = new ReadOnlyCollection<string>(["a"]);
        var a = new IntegrityResult(1, 1, issues);
        var b = new IntegrityResult(1, 1, issues);
        a.Should().Be(b);
    }

    [Fact]
    public void IntegrityResult_RecordInequality()
    {
        var issues1 = new ReadOnlyCollection<string>(["a"]);
        var issues2 = new ReadOnlyCollection<string>(["b"]);
        var a = new IntegrityResult(1, 1, issues1);
        var b = new IntegrityResult(1, 1, issues2);
        a.Should().NotBe(b);
    }

    [Fact]
    public void IntegrityResult_IssuesAreReadOnly()
    {
        var result = new IntegrityResult(1, 0, new ReadOnlyCollection<string>([]));
        result.Issues.Should().BeAssignableTo<IReadOnlyCollection<string>>();
    }
}
