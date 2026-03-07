using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

public sealed class PolicyContextTests
{
    [Fact]
    public void Default_AllNull()
    {
        var ctx = new PolicyContext();
        ctx.UserId.Should().BeNull();
        ctx.ProjectPath.Should().BeNull();
        ctx.ProviderName.Should().BeNull();
        ctx.ModelId.Should().BeNull();
        ctx.RoleName.Should().BeNull();
        ctx.Groups.Should().BeNull();
    }

    [Fact]
    public void AllProperties_Roundtrip()
    {
        var ctx = new PolicyContext(
            UserId: "user-42",
            ProjectPath: "/repo",
            ProviderName: "OpenAI",
            ModelId: "gpt-4o",
            RoleName: "admin",
            Groups: ["devs", "ops"]);
        ctx.UserId.Should().Be("user-42");
        ctx.ProjectPath.Should().Be("/repo");
        ctx.ProviderName.Should().Be("OpenAI");
        ctx.ModelId.Should().Be("gpt-4o");
        ctx.RoleName.Should().Be("admin");
        ctx.Groups.Should().HaveCount(2);
    }

    [Fact]
    public void RecordEquality()
    {
        var a = new PolicyContext(UserId: "u1", ProjectPath: "/p");
        var b = new PolicyContext(UserId: "u1", ProjectPath: "/p");
        a.Should().Be(b);
    }

    [Fact]
    public void RecordInequality()
    {
        var a = new PolicyContext(UserId: "u1");
        var b = new PolicyContext(UserId: "u2");
        a.Should().NotBe(b);
    }
}
