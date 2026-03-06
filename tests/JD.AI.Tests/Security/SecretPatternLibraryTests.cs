using JD.AI.Core.Security;

namespace JD.AI.Tests.Security;

public sealed class SecretPatternLibraryTests
{
    [Theory]
    [InlineData("AKIAIOSFODNN7EXAMPLE", nameof(SecretPatternLibrary.AwsAccessKeyId))]
    [InlineData("ghp_" + "aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrR", nameof(SecretPatternLibrary.GitHubClassicPat))]
    [InlineData("sk-" + "aBcDeFgHiJkLmNoPqRsTuVwXyZ01234567890123456789012", nameof(SecretPatternLibrary.OpenAiKey))]
    [InlineData("hf_" + "aBcDeFgHiJkLmNoPqRsTuVwXyZaBcDeFgHi0a", nameof(SecretPatternLibrary.HuggingFaceToken))]
    [InlineData("sk_test_xFaKeStRiPeKe" + "y12345678", nameof(SecretPatternLibrary.StripeSecretKey))]
    public void All_ContainsPatternForWellKnownSecretTypes(string secret, string patternName)
    {
        _ = patternName; // used as test display name
        var redactor = new Core.Governance.DataRedactor(SecretPatternLibrary.All);
        var redacted = redactor.Redact($"My key is {secret} and more text");
        Assert.NotEqual($"My key is {secret} and more text", redacted);
    }

    [Fact]
    public void All_IsNonEmpty() => Assert.NotEmpty(SecretPatternLibrary.All);

    [Fact]
    public void HighConfidence_IsSubsetOfAll() =>
        Assert.All(SecretPatternLibrary.HighConfidence, p => Assert.Contains(p, SecretPatternLibrary.All));

    [Fact]
    public void PlainText_IsNotRedacted()
    {
        var redactor = new Core.Governance.DataRedactor(SecretPatternLibrary.All);
        const string text = "Hello, this is plain text with no secrets.";
        Assert.Equal(text, redactor.Redact(text));
    }
}
