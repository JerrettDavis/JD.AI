using System.Collections.ObjectModel;
using FluentAssertions;
using JD.AI.Core.Sessions;

namespace JD.AI.Tests.Sessions;

public sealed class IntegrityResultTests
{
    [Fact]
    public void Construction_AllProperties()
    {
        var issues = new List<string> { "Session abc: missing JSON export" }.AsReadOnly();
        var result = new IntegrityResult(5, 1, issues);
        result.SessionsChecked.Should().Be(5);
        result.MismatchesFound.Should().Be(1);
        result.Issues.Should().HaveCount(1);
        result.Issues[0].Should().Contain("missing JSON export");
    }

    [Fact]
    public void Construction_NoIssues()
    {
        var result = new IntegrityResult(10, 0, new ReadOnlyCollection<string>([]));
        result.SessionsChecked.Should().Be(10);
        result.MismatchesFound.Should().Be(0);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality()
    {
        var issues = new ReadOnlyCollection<string>([]);
        var a = new IntegrityResult(1, 0, issues);
        var b = new IntegrityResult(1, 0, issues);
        a.Should().Be(b);
    }
}
