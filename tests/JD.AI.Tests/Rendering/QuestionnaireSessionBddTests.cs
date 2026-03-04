using JD.AI.Core.Questions;
using JD.AI.Rendering;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Rendering;

[Feature("Questionnaire Session")]
public sealed class QuestionnaireSessionBddTests : TinyBddXunitBase
{
    public QuestionnaireSessionBddTests(ITestOutputHelper output) : base(output) { }

    // ── ValidateText tests ─────────────────────────────────────────────

    [Scenario("ValidateText returns null when validation rules are null"), Fact]
    public async Task ValidateText_NullValidation_Returns_Null()
    {
        string? result = "not null";

        await Given("null validation rules", () => (QuestionValidation?)null)
            .When("validating text", v => { result = QuestionnaireSession.ValidateText(v, "any text"); return v; })
            .Then("the result is null (valid)", _ => result == null)
            .AssertPassed();
    }

    [Scenario("ValidateText returns error when text exceeds maxLength"), Fact]
    public async Task ValidateText_ExceedsMaxLength_Returns_Error()
    {
        string? result = null;

        await Given("a validation rule with maxLength of 10", () => new QuestionValidation { MaxLength = 10 })
            .When("validating text that is too long", v => { result = QuestionnaireSession.ValidateText(v, "this is way too long for the limit"); return v; })
            .Then("the result is an error message", _ => result != null)
            .AssertPassed();
    }

    [Scenario("ValidateText returns null when text is within maxLength"), Fact]
    public async Task ValidateText_WithinMaxLength_Returns_Null()
    {
        string? result = "not null";

        await Given("a validation rule with maxLength of 50", () => new QuestionValidation { MaxLength = 50 })
            .When("validating text within the limit", v => { result = QuestionnaireSession.ValidateText(v, "short text"); return v; })
            .Then("the result is null (valid)", _ => result == null)
            .AssertPassed();
    }

    [Scenario("ValidateText returns error when pattern does not match"), Fact]
    public async Task ValidateText_PatternMismatch_Returns_Error()
    {
        string? result = null;

        await Given("a validation rule with a digits-only pattern", () => new QuestionValidation { Pattern = @"^\d+$" })
            .When("validating text that does not match", v => { result = QuestionnaireSession.ValidateText(v, "not-a-number"); return v; })
            .Then("the result is an error message", _ => result != null)
            .AssertPassed();
    }

    [Scenario("ValidateText returns null when pattern matches"), Fact]
    public async Task ValidateText_PatternMatches_Returns_Null()
    {
        string? result = "not null";

        await Given("a validation rule with a digits-only pattern", () => new QuestionValidation { Pattern = @"^\d+$" })
            .When("validating text that matches the pattern", v => { result = QuestionnaireSession.ValidateText(v, "12345"); return v; })
            .Then("the result is null (valid)", _ => result == null)
            .AssertPassed();
    }

    [Scenario("ValidateText returns custom error message on violation"), Fact]
    public async Task ValidateText_CustomErrorMessage_Returns_Custom()
    {
        string? result = null;

        await Given("a validation rule with maxLength and a custom error", () => new QuestionValidation { MaxLength = 5, ErrorMessage = "Too long!" })
            .When("validating text that exceeds the limit", v => { result = QuestionnaireSession.ValidateText(v, "exceeds limit"); return v; })
            .Then("the result is the custom error message", _ => string.Equals(result, "Too long!", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("ValidateText returns pattern error when no maxLength but pattern mismatches"), Fact]
    public async Task ValidateText_NoMaxLength_PatternMismatch_Returns_Error()
    {
        string? result = null;

        await Given("a validation rule with only a pattern", () => new QuestionValidation { Pattern = @"^[A-Z]+$" })
            .When("validating lowercase text", v => { result = QuestionnaireSession.ValidateText(v, "lowercase"); return v; })
            .Then("the result is a pattern error message", _ => result != null && result.Contains("pattern"))
            .AssertPassed();
    }

    // ── ValidateNumber tests ───────────────────────────────────────────

    [Scenario("ValidateNumber returns null when validation rules are null"), Fact]
    public async Task ValidateNumber_NullValidation_Returns_Null()
    {
        string? result = "not null";

        await Given("null validation rules", () => (QuestionValidation?)null)
            .When("validating a number", v => { result = QuestionnaireSession.ValidateNumber(v, 42); return v; })
            .Then("the result is null (valid)", _ => result == null)
            .AssertPassed();
    }

    [Scenario("ValidateNumber returns error when number is below min"), Fact]
    public async Task ValidateNumber_BelowMin_Returns_Error()
    {
        string? result = null;

        await Given("a validation rule with min of 10", () => new QuestionValidation { Min = 10 })
            .When("validating a number below the minimum", v => { result = QuestionnaireSession.ValidateNumber(v, 5); return v; })
            .Then("the result is an error message", _ => result != null)
            .AssertPassed();
    }

    [Scenario("ValidateNumber returns error when number is above max"), Fact]
    public async Task ValidateNumber_AboveMax_Returns_Error()
    {
        string? result = null;

        await Given("a validation rule with max of 100", () => new QuestionValidation { Max = 100 })
            .When("validating a number above the maximum", v => { result = QuestionnaireSession.ValidateNumber(v, 150); return v; })
            .Then("the result is an error message", _ => result != null)
            .AssertPassed();
    }

    [Scenario("ValidateNumber returns null when number is in range"), Fact]
    public async Task ValidateNumber_InRange_Returns_Null()
    {
        string? result = "not null";

        await Given("a validation rule with min 1 and max 100", () => new QuestionValidation { Min = 1, Max = 100 })
            .When("validating a number within range", v => { result = QuestionnaireSession.ValidateNumber(v, 50); return v; })
            .Then("the result is null (valid)", _ => result == null)
            .AssertPassed();
    }

    [Scenario("ValidateNumber returns null when number is exactly at min"), Fact]
    public async Task ValidateNumber_ExactlyAtMin_Returns_Null()
    {
        string? result = "not null";

        await Given("a validation rule with min 10 and max 100", () => new QuestionValidation { Min = 10, Max = 100 })
            .When("validating a number exactly at the minimum", v => { result = QuestionnaireSession.ValidateNumber(v, 10); return v; })
            .Then("the result is null (valid)", _ => result == null)
            .AssertPassed();
    }

    [Scenario("ValidateNumber returns null when number is exactly at max"), Fact]
    public async Task ValidateNumber_ExactlyAtMax_Returns_Null()
    {
        string? result = "not null";

        await Given("a validation rule with min 1 and max 100", () => new QuestionValidation { Min = 1, Max = 100 })
            .When("validating a number exactly at the maximum", v => { result = QuestionnaireSession.ValidateNumber(v, 100); return v; })
            .Then("the result is null (valid)", _ => result == null)
            .AssertPassed();
    }

    [Scenario("ValidateNumber returns custom error message on min violation"), Fact]
    public async Task ValidateNumber_CustomErrorMessage_Returns_Custom()
    {
        string? result = null;

        await Given("a validation rule with min and a custom error", () => new QuestionValidation { Min = 10, ErrorMessage = "Value too small!" })
            .When("validating a number below the minimum", v => { result = QuestionnaireSession.ValidateNumber(v, 3); return v; })
            .Then("the result is the custom error message", _ => string.Equals(result, "Value too small!", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Run method tests ───────────────────────────────────────────────

    [Scenario("Run throws ArgumentNullException for null request"), Fact]
    public async Task Run_NullRequest_Throws_ArgumentNullException()
    {
        Exception? caught = null;

        await Given("a null request", () => (AskQuestionsRequest?)null)
            .When("calling Run", req =>
            {
                try { QuestionnaireSession.Run(req!); }
                catch (Exception ex) { caught = ex; }
                return req;
            })
            .Then("an ArgumentNullException is thrown", _ => caught is ArgumentNullException)
            .AssertPassed();
    }

    [Scenario("Run returns completed result for empty questions list"), Fact]
    public async Task Run_EmptyQuestions_Returns_Completed()
    {
        AskQuestionsResult? result = null;

        await Given("a request with no questions", () => new AskQuestionsRequest { Questions = [] })
            .When("calling Run", req => { result = QuestionnaireSession.Run(req); return req; })
            .Then("the result is completed", _ => result != null && result.Completed)
            .AssertPassed();
    }
}
