using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class ClipboardToolsTests
{
    [Fact]
    public async Task WriteClipboard_ThenRead_RoundTrips()
    {
        // This test requires a clipboard-capable environment
        var testText = $"jdai-test-{Guid.NewGuid():N}";

        var writeResult = await ClipboardTools.WriteClipboardAsync(testText);

        if (writeResult.Contains("Failed", StringComparison.Ordinal))
        {
            // No clipboard available (headless CI)
            return;
        }

        Assert.Contains("Copied", writeResult, StringComparison.Ordinal);

        var readResult = await ClipboardTools.ReadClipboardAsync();
        Assert.Contains(testText, readResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadClipboard_ReturnsNonNull()
    {
        var result = await ClipboardTools.ReadClipboardAsync();

        // Should return something (content or error message), never null
        Assert.NotNull(result);
    }
}
