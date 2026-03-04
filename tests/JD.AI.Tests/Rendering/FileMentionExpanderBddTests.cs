using JD.AI.Rendering;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Rendering;

[Feature("File Mention Expander")]
public sealed class FileMentionExpanderBddTests : TinyBddXunitBase
{
    public FileMentionExpanderBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Input with no @ mentions returns original text"), Fact]
    public async Task NoMentions_ReturnsOriginalText()
    {
        string? result = null;

        await Given("input with no @ mentions", () => "just some plain text")
            .When("Expand is called", input =>
            {
                result = FileMentionExpander.Expand(input);
                return input;
            })
            .Then("the result is the original text", _ => result == "just some plain text")
            .AssertPassed();
    }

    [Scenario("@ mention of existing temp file injects file content"), Fact]
    public async Task ExistingFile_InjectsContent()
    {
        string? result = null;
        var fileName = $"tinybdd_test_{Guid.NewGuid():N}.txt";

        try
        {
            File.WriteAllText(fileName, "hello world");

            await Given("input with @mention of an existing file", () => $"check @{fileName}")
                .When("Expand is called", input =>
                {
                    result = FileMentionExpander.Expand(input);
                    return input;
                })
                .Then("the result contains the file content", _ =>
                    result != null && result.Contains("hello world") && result.Contains("[File:"))
                .AssertPassed();
        }
        finally
        {
            if (File.Exists(fileName)) File.Delete(fileName);
        }
    }

    [Scenario("@ mention of nonexistent file keeps original mention"), Fact]
    public async Task NonexistentFile_KeepsOriginalMention()
    {
        string? result = null;

        await Given("input with @mention of nonexistent file", () => "check @nonexistent-file.txt")
            .When("Expand is called", input =>
            {
                result = FileMentionExpander.Expand(input);
                return input;
            })
            .Then("the result keeps the original @mention", _ =>
                result != null && result.Contains("@nonexistent-file.txt"))
            .AssertPassed();
    }

    [Scenario("Multiple @ mentions expands all existing files"), Fact]
    public async Task MultipleMentions_ExpandsAllExisting()
    {
        string? result = null;
        var fileName1 = $"tinybdd_a_{Guid.NewGuid():N}.txt";
        var fileName2 = $"tinybdd_b_{Guid.NewGuid():N}.txt";

        try
        {
            File.WriteAllText(fileName1, "content one");
            File.WriteAllText(fileName2, "content two");

            await Given("input with two @mentions of existing files", () => $"@{fileName1} and @{fileName2}")
                .When("Expand is called", input =>
                {
                    result = FileMentionExpander.Expand(input);
                    return input;
                })
                .Then("both file contents are injected", _ =>
                    result != null && result.Contains("content one") && result.Contains("content two"))
                .AssertPassed();
        }
        finally
        {
            if (File.Exists(fileName1)) File.Delete(fileName1);
            if (File.Exists(fileName2)) File.Delete(fileName2);
        }
    }

    [Scenario("Relative path to existing file expands correctly"), Fact]
    public async Task RelativePath_ExpandsCorrectly()
    {
        string? result = null;
        var subDir = $"tinybdd_dir_{Guid.NewGuid():N}";
        var fileName = $"{subDir}/testfile.txt";

        try
        {
            Directory.CreateDirectory(subDir);
            File.WriteAllText(fileName, "nested content");

            await Given("input with relative path @mention", () => $"see @{fileName}")
                .When("Expand is called", input =>
                {
                    result = FileMentionExpander.Expand(input);
                    return input;
                })
                .Then("the result contains the file content", _ =>
                    result != null && result.Contains("nested content"))
                .AssertPassed();
        }
        finally
        {
            if (File.Exists(fileName)) File.Delete(fileName);
            if (Directory.Exists(subDir)) Directory.Delete(subDir);
        }
    }

    [Scenario("Mix of existing and non-existing files expands only existing ones"), Fact]
    public async Task MixedFiles_ExpandsOnlyExisting()
    {
        string? result = null;
        var fileName = $"tinybdd_mix_{Guid.NewGuid():N}.txt";

        try
        {
            File.WriteAllText(fileName, "real content");

            await Given("input with one existing and one non-existing @mention", () =>
                    $"@{fileName} and @no-such-file.txt")
                .When("Expand is called", input =>
                {
                    result = FileMentionExpander.Expand(input);
                    return input;
                })
                .Then("only the existing file is expanded", _ =>
                    result != null &&
                    result.Contains("real content") &&
                    result.Contains("@no-such-file.txt"))
                .AssertPassed();
        }
        finally
        {
            if (File.Exists(fileName)) File.Delete(fileName);
        }
    }
}
