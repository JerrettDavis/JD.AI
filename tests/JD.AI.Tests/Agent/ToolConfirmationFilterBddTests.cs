using JD.AI.Core.Agents;
using Microsoft.SemanticKernel;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Agent;

[Feature("Tool Confirmation Filter")]
public sealed class ToolConfirmationFilterBddTests : TinyBddXunitBase
{
    public ToolConfirmationFilterBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Null arguments returns empty string"), Fact]
    public async Task NullArguments_ReturnsEmpty()
    {
        string? result = null;

        await Given("null arguments", () => (KernelArguments?)null)
            .When("building redacted args", args =>
            {
                result = ToolConfirmationFilter.BuildRedactedArgs(args);
                return args;
            })
            .Then("result is empty string", _ => result != null && result.Length == 0)
            .AssertPassed();
    }

    [Scenario("Empty KernelArguments returns empty string"), Fact]
    public async Task EmptyArguments_ReturnsEmpty()
    {
        string? result = null;

        await Given("empty KernelArguments", () => new KernelArguments())
            .When("building redacted args", args =>
            {
                result = ToolConfirmationFilter.BuildRedactedArgs(args);
                return args;
            })
            .Then("result is empty string", _ => result != null && result.Length == 0)
            .AssertPassed();
    }

    [Scenario("Argument with 'content' key is redacted"), Fact]
    public async Task ContentKey_IsRedacted()
    {
        string? result = null;

        await Given("arguments with content key", () => new KernelArguments { ["content"] = "sensitive data" })
            .When("building redacted args", args =>
            {
                result = ToolConfirmationFilter.BuildRedactedArgs(args);
                return args;
            })
            .Then("value shows [REDACTED]", _ => result!.Contains("content=[REDACTED]", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Argument with 'password' key is redacted"), Fact]
    public async Task PasswordKey_IsRedacted()
    {
        string? result = null;

        await Given("arguments with password key", () => new KernelArguments { ["password"] = "s3cret" })
            .When("building redacted args", args =>
            {
                result = ToolConfirmationFilter.BuildRedactedArgs(args);
                return args;
            })
            .Then("value shows [REDACTED]", _ => result!.Contains("password=[REDACTED]", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Argument with 'token' key is redacted"), Fact]
    public async Task TokenKey_IsRedacted()
    {
        string? result = null;

        await Given("arguments with token key", () => new KernelArguments { ["token"] = "abc123" })
            .When("building redacted args", args =>
            {
                result = ToolConfirmationFilter.BuildRedactedArgs(args);
                return args;
            })
            .Then("value shows [REDACTED]", _ => result!.Contains("token=[REDACTED]", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Argument with 'secret' key is redacted"), Fact]
    public async Task SecretKey_IsRedacted()
    {
        string? result = null;

        await Given("arguments with secret key", () => new KernelArguments { ["secret"] = "top-secret" })
            .When("building redacted args", args =>
            {
                result = ToolConfirmationFilter.BuildRedactedArgs(args);
                return args;
            })
            .Then("value shows [REDACTED]", _ => result!.Contains("secret=[REDACTED]", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Argument with 'code' key is redacted"), Fact]
    public async Task CodeKey_IsRedacted()
    {
        string? result = null;

        await Given("arguments with code key", () => new KernelArguments { ["code"] = "Console.WriteLine()" })
            .When("building redacted args", args =>
            {
                result = ToolConfirmationFilter.BuildRedactedArgs(args);
                return args;
            })
            .Then("value shows [REDACTED]", _ => result!.Contains("code=[REDACTED]", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Long value over 80 chars is truncated with ellipsis"), Fact]
    public async Task LongValue_IsTruncated()
    {
        string? result = null;
        var longValue = new string('x', 100);

        await Given("arguments with long value", () => new KernelArguments { ["query"] = longValue })
            .When("building redacted args", args =>
            {
                result = ToolConfirmationFilter.BuildRedactedArgs(args);
                return args;
            })
            .Then("value is truncated with ellipsis", _ =>
                result!.Contains("...", StringComparison.Ordinal) && !result.Contains(longValue, StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Safe key 'query' preserves its value"), Fact]
    public async Task SafeKey_ValuePreserved()
    {
        string? result = null;

        await Given("arguments with safe key query", () => new KernelArguments { ["query"] = "test value" })
            .When("building redacted args", args =>
            {
                result = ToolConfirmationFilter.BuildRedactedArgs(args);
                return args;
            })
            .Then("value is preserved", _ => result!.Contains("query=test value", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Multiple args with mixed redacted and safe keys"), Fact]
    public async Task MixedArgs_CorrectOutput()
    {
        string? result = null;

        await Given("arguments with mixed keys", () => new KernelArguments
        {
            ["query"] = "search term",
            ["password"] = "s3cret"
        })
            .When("building redacted args", args =>
            {
                result = ToolConfirmationFilter.BuildRedactedArgs(args);
                return args;
            })
            .Then("safe key preserved and sensitive key redacted", _ =>
                result!.Contains("query=search term", StringComparison.Ordinal) &&
                result.Contains("password=[REDACTED]", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Argument with null value shows 'null'"), Fact]
    public async Task NullValue_ShowsNull()
    {
        string? result = null;

        await Given("arguments with null value", () => new KernelArguments { ["query"] = null })
            .When("building redacted args", args =>
            {
                result = ToolConfirmationFilter.BuildRedactedArgs(args);
                return args;
            })
            .Then("value shows null", _ => result!.Contains("query=null", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Argument with exactly 80 chars is not truncated"), Fact]
    public async Task Exactly80Chars_NotTruncated()
    {
        string? result = null;
        var exactValue = new string('a', 80);

        await Given("arguments with exactly 80 char value", () => new KernelArguments { ["query"] = exactValue })
            .When("building redacted args", args =>
            {
                result = ToolConfirmationFilter.BuildRedactedArgs(args);
                return args;
            })
            .Then("value is not truncated", _ =>
                result!.Contains(exactValue, StringComparison.Ordinal) &&
                !result.Contains("...", StringComparison.Ordinal))
            .AssertPassed();
    }
}
