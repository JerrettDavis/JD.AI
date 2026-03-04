using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

public sealed class DataRedactorTests
{
    [Fact]
    public void Redact_ApiKeyPattern_RedactsMatch()
    {
        var redactor = new DataRedactor(["(?i)api[_-]?key\\s*[:=]\\s*\\S+"]);

        var result = redactor.Redact("Connect using api_key=abc123xyz");

        Assert.Contains("[REDACTED]", result, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123xyz", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_PasswordPattern_RedactsMatch()
    {
        var redactor = new DataRedactor(["(?i)password\\s*[:=]\\s*\\S+"]);

        var result = redactor.Redact("Connecting with password=S3cr3tP@ss");

        Assert.Contains("[REDACTED]", result, StringComparison.Ordinal);
        Assert.DoesNotContain("S3cr3tP@ss", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_NoPatterns_ReturnsInputUnchanged()
    {
        var redactor = new DataRedactor([]);
        const string input = "api_key=should-not-be-redacted";

        var result = redactor.Redact(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void Redact_EmptyInput_ReturnsEmptyString()
    {
        var redactor = new DataRedactor(["(?i)api[_-]?key\\s*[:=]\\s*\\S+"]);

        var result = redactor.Redact(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Redact_MultiplePatterns_AllApplied()
    {
        var redactor = new DataRedactor(
        [
            "(?i)api[_-]?key\\s*[:=]\\s*\\S+",
            "(?i)password\\s*[:=]\\s*\\S+"
        ]);

        var result = redactor.Redact("api_key=mykey password=mypass");

        Assert.Contains("[REDACTED]", result, StringComparison.Ordinal);
        Assert.DoesNotContain("mykey", result, StringComparison.Ordinal);
        Assert.DoesNotContain("mypass", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_ApiKeyWithDash_RedactsMatch()
    {
        var redactor = new DataRedactor(["(?i)api[_-]?key\\s*[:=]\\s*\\S+"]);

        var result = redactor.Redact("api-key: sk-abcdef1234567890");

        Assert.Contains("[REDACTED]", result, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-abcdef1234567890", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_InputWithNoSensitiveContent_ReturnsInputUnchanged()
    {
        var redactor = new DataRedactor(["(?i)api[_-]?key\\s*[:=]\\s*\\S+"]);
        const string input = "This is a safe message with no secrets.";

        var result = redactor.Redact(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void HasSensitiveContent_MatchFound_ReturnsTrue()
    {
        var redactor = new DataRedactor(["(?i)api[_-]?key\\s*[:=]\\s*\\S+"]);

        var hasSensitive = redactor.HasSensitiveContent("api_key=secret123");

        Assert.True(hasSensitive);
    }

    [Fact]
    public void HasSensitiveContent_NoMatch_ReturnsFalse()
    {
        var redactor = new DataRedactor(["(?i)api[_-]?key\\s*[:=]\\s*\\S+"]);

        var hasSensitive = redactor.HasSensitiveContent("This is a normal message");

        Assert.False(hasSensitive);
    }

    [Fact]
    public void HasSensitiveContent_EmptyInput_ReturnsFalse()
    {
        var redactor = new DataRedactor(["(?i)api[_-]?key\\s*[:=]\\s*\\S+"]);

        var hasSensitive = redactor.HasSensitiveContent(string.Empty);

        Assert.False(hasSensitive);
    }

    [Fact]
    public void HasSensitiveContent_NoPatterns_ReturnsFalse()
    {
        var redactor = new DataRedactor([]);

        var hasSensitive = redactor.HasSensitiveContent("api_key=secret");

        Assert.False(hasSensitive);
    }

    [Fact]
    public void Redact_CatastrophicBacktracking_DoesNotThrow()
    {
        // This is a pattern prone to catastrophic backtracking with certain inputs.
        // The Regex timeout of 1 second should prevent it from hanging.
        var catastrophicPattern = "(a+)+b";
        var redactor = new DataRedactor([catastrophicPattern]);

        // Input designed to trigger catastrophic backtracking
        var evilInput = new string('a', 30);

        var exception = Record.Exception(() => redactor.Redact(evilInput));

        Assert.Null(exception);
    }

    [Fact]
    public void HasSensitiveContent_CatastrophicBacktracking_DoesNotThrow()
    {
        var catastrophicPattern = "(a+)+b";
        var redactor = new DataRedactor([catastrophicPattern]);
        var evilInput = new string('a', 30);

        var exception = Record.Exception(() => redactor.HasSensitiveContent(evilInput));

        Assert.Null(exception);
    }

    [Fact]
    public void DataRedactorNone_IsPassThrough()
    {
        const string input = "api_key=secret password=pass123 sensitive data";

        var result = DataRedactor.None.Redact(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void DataRedactorNone_HasSensitiveContent_AlwaysReturnsFalse()
    {
        var hasSensitive = DataRedactor.None.HasSensitiveContent("api_key=topsecret");

        Assert.False(hasSensitive);
    }

    [Fact]
    public void DataRedactorNone_IsSingletonInstance()
    {
        Assert.Same(DataRedactor.None, DataRedactor.None);
    }

    [Fact]
    public void Redact_BlankPatterns_AreIgnored()
    {
        var redactor = new DataRedactor(["", "   ", "(?i)password\\s*[:=]\\s*\\S+"]);
        const string input = "password=s3cret";

        var result = redactor.Redact(input);

        Assert.Contains("[REDACTED]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_MultipleOccurrences_AllRedacted()
    {
        var redactor = new DataRedactor(["(?i)api[_-]?key\\s*[:=]\\s*\\S+"]);

        var result = redactor.Redact("api_key=first and api_key=second");

        Assert.DoesNotContain("first", result, StringComparison.Ordinal);
        Assert.DoesNotContain("second", result, StringComparison.Ordinal);
        Assert.Equal(2, result.Split("[REDACTED]").Length - 1);
    }
}
