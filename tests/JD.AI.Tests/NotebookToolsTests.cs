using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class NotebookToolsTests
{
    [Fact]
    public async Task ExecuteCode_InvalidLanguage_ReturnsError()
    {
        var result = await NotebookTools.ExecuteCodeAsync("cobol", "DISPLAY 'HELLO'.");

        Assert.Contains("Unsupported language", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteCode_Python_HelloWorld()
    {
        // This test only passes if python is installed
        var result = await NotebookTools.ExecuteCodeAsync("python", "print('hello from python')");

        if (result.Contains("Failed to start", StringComparison.Ordinal))
        {
            // Python not installed, skip
            return;
        }

        Assert.Contains("hello from python", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteCode_Node_HelloWorld()
    {
        // This test only passes if node is installed
        var result = await NotebookTools.ExecuteCodeAsync("node", "console.log('hello from node')");

        if (result.Contains("Failed to start", StringComparison.Ordinal))
        {
            return;
        }

        Assert.Contains("hello from node", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteCode_Timeout_KillsProcess()
    {
        // Use a language that will definitely hang
        var result = await NotebookTools.ExecuteCodeAsync(
            "python",
            "import time; time.sleep(60)",
            timeoutSeconds: 2);

        if (result.Contains("Failed to start", StringComparison.Ordinal))
        {
            return;
        }

        Assert.Contains("timed out", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteCode_PowerShell_Works()
    {
        var result = await NotebookTools.ExecuteCodeAsync("powershell", "Write-Output 'hello pwsh'");

        if (result.Contains("Failed to start", StringComparison.Ordinal))
        {
            return;
        }

        Assert.Contains("hello pwsh", result, StringComparison.Ordinal);
    }
}
