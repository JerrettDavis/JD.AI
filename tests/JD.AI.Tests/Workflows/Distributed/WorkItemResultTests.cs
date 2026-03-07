using FluentAssertions;
using JD.AI.Workflows.Distributed;

namespace JD.AI.Tests.Workflows.Distributed;

/// <summary>
/// Tests for the WorkItemResult enum: values, casting, and coverage of all branches.
/// </summary>
public sealed class WorkItemResultTests
{
    [Fact]
    public void Success_HasExpectedIntValue()
    {
        ((int)WorkItemResult.Success).Should().Be(0);
    }

    [Fact]
    public void Transient_HasExpectedIntValue()
    {
        ((int)WorkItemResult.Transient).Should().Be(1);
    }

    [Fact]
    public void Permanent_HasExpectedIntValue()
    {
        ((int)WorkItemResult.Permanent).Should().Be(2);
    }

    [Fact]
    public void AllValues_AreDistinct()
    {
        var values = Enum.GetValues<WorkItemResult>();
        values.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AllValues_HasExactlyThreeMembers()
    {
        Enum.GetValues<WorkItemResult>().Should().HaveCount(3);
    }

    [Theory]
    [InlineData(WorkItemResult.Success)]
    [InlineData(WorkItemResult.Transient)]
    [InlineData(WorkItemResult.Permanent)]
    public void IsDefined_ReturnsTrueForAllValues(WorkItemResult result)
    {
        Enum.IsDefined(result).Should().BeTrue();
    }

    [Fact]
    public void Success_IsNotTransient()
    {
        WorkItemResult.Success.Should().NotBe(WorkItemResult.Transient);
    }

    [Fact]
    public void Success_IsNotPermanent()
    {
        WorkItemResult.Success.Should().NotBe(WorkItemResult.Permanent);
    }

    [Fact]
    public void Transient_IsNotPermanent()
    {
        WorkItemResult.Transient.Should().NotBe(WorkItemResult.Permanent);
    }
}
