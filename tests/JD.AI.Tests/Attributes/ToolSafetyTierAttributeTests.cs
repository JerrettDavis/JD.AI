using FluentAssertions;
using JD.AI.Core.Attributes;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Attributes;

public sealed class ToolSafetyTierAttributeTests
{
    [Theory]
    [InlineData(SafetyTier.AutoApprove)]
    [InlineData(SafetyTier.ConfirmOnce)]
    [InlineData(SafetyTier.AlwaysConfirm)]
    public void Construction_SetsTier(SafetyTier tier)
    {
        var attr = new ToolSafetyTierAttribute(tier);
        attr.Tier.Should().Be(tier);
    }

    [Fact]
    public void Reason_DefaultsToNull()
    {
        var attr = new ToolSafetyTierAttribute(SafetyTier.AutoApprove);
        attr.Reason.Should().BeNull();
    }

    [Fact]
    public void Reason_CanBeSet()
    {
        var attr = new ToolSafetyTierAttribute(SafetyTier.AlwaysConfirm)
        {
            Reason = "Executes arbitrary shell commands",
        };
        attr.Reason.Should().Be("Executes arbitrary shell commands");
    }

    [Fact]
    public void Attribute_IsSealed()
    {
        typeof(ToolSafetyTierAttribute).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Attribute_InheritsFromAttribute()
    {
        typeof(ToolSafetyTierAttribute).Should().BeDerivedFrom<Attribute>();
    }

    [Fact]
    public void AttributeUsage_MethodOnly_SingleInstance()
    {
        var usage = typeof(ToolSafetyTierAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.Should().Be(AttributeTargets.Method);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
    }
}
