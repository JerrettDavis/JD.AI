using FluentAssertions;
using JD.AI.Core.Security;

namespace JD.AI.Tests.Security;

public sealed class SecurityExceptionTests
{
    [Fact]
    public void DefaultConstructor_CreatesInstance()
    {
        var ex = new SecurityException();
        ex.Message.Should().NotBeNullOrEmpty();
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        var ex = new SecurityException("secret leaked");
        ex.Message.Should().Be("secret leaked");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void MessageAndInnerConstructor_SetsBoth()
    {
        var inner = new InvalidOperationException("root cause");
        var ex = new SecurityException("policy violation", inner);
        ex.Message.Should().Be("policy violation");
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void IsException_Derived()
    {
        var ex = new SecurityException("test");
        ex.Should().BeAssignableTo<Exception>();
    }
}
