using FluentAssertions;
using JD.AI.Core.Attributes;

namespace JD.AI.Tests.Attributes;

public sealed class ToolPluginAttributeTests
{
    [Fact]
    public void Constructor_SetsName()
    {
        var attr = new ToolPluginAttribute("file");
        attr.Name.Should().Be("file");
    }

    [Fact]
    public void Constructor_NullName_Throws()
    {
        var act = () => new ToolPluginAttribute(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Description_DefaultNull()
    {
        var attr = new ToolPluginAttribute("test");
        attr.Description.Should().BeNull();
    }

    [Fact]
    public void Description_CanBeSet()
    {
        var attr = new ToolPluginAttribute("test") { Description = "File operations" };
        attr.Description.Should().Be("File operations");
    }

    [Fact]
    public void RequiresInjection_DefaultFalse()
    {
        var attr = new ToolPluginAttribute("test");
        attr.RequiresInjection.Should().BeFalse();
    }

    [Fact]
    public void RequiresInjection_CanBeSet()
    {
        var attr = new ToolPluginAttribute("test") { RequiresInjection = true };
        attr.RequiresInjection.Should().BeTrue();
    }

    [Fact]
    public void Order_DefaultZero()
    {
        var attr = new ToolPluginAttribute("test");
        attr.Order.Should().Be(0);
    }

    [Fact]
    public void Order_CanBeSet()
    {
        var attr = new ToolPluginAttribute("test") { Order = 42 };
        attr.Order.Should().Be(42);
    }

    [Fact]
    public void AttributeUsage_ClassOnly_NotInherited_NoMultiple()
    {
        var usage = typeof(ToolPluginAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.Should().Be(AttributeTargets.Class);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
    }
}
