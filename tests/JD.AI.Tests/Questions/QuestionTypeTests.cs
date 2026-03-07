using FluentAssertions;
using JD.AI.Core.Questions;

namespace JD.AI.Tests.Questions;

public sealed class QuestionTypeTests
{
    [Theory]
    [InlineData(QuestionType.Text, 0)]
    [InlineData(QuestionType.Confirm, 1)]
    [InlineData(QuestionType.SingleSelect, 2)]
    [InlineData(QuestionType.MultiSelect, 3)]
    [InlineData(QuestionType.Number, 4)]
    public void QuestionType_Values(QuestionType type, int expected) =>
        ((int)type).Should().Be(expected);
}
