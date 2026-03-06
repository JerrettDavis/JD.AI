using JD.AI.Commands;
using JD.AI.Core.Config;

namespace JD.AI.Tests.Commands;

[Collection("DataDirectories")]
public sealed class PolicyCliHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalDataDir;
    private readonly string _originalCurrentDirectory = Directory.GetCurrentDirectory();

    public PolicyCliHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-policy-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _originalDataDir = Environment.GetEnvironmentVariable("JDAI_DATA_DIR") ?? string.Empty;
        Environment.SetEnvironmentVariable("JDAI_DATA_DIR", _tempDir);
        DataDirectories.Reset();
        Directory.SetCurrentDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalCurrentDirectory);
        Environment.SetEnvironmentVariable("JDAI_DATA_DIR",
            string.IsNullOrEmpty(_originalDataDir) ? null : _originalDataDir);
        DataDirectories.Reset();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public async Task PolicySubcommand_HelpAndUnknown_AreHandled()
    {
        var help = await CaptureStdoutAsync(() => PolicySubcommandHandler.RunAsync(["help"]));
        var unknown = await CaptureStderrAsync(() => PolicySubcommandHandler.RunAsync(["bogus"]));

        Assert.Equal(0, help.ExitCode);
        Assert.Contains("jdai policy", help.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, unknown.ExitCode);
        Assert.Contains("Unknown policy command", unknown.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compliance_ListAndHelp_AreRendered()
    {
        var list = await CaptureStdoutAsync(() => PolicyComplianceCliHandler.RunAsync(["list"]));
        var help = await CaptureStdoutAsync(() => PolicyComplianceCliHandler.RunAsync(["help"]));

        Assert.Equal(0, list.ExitCode);
        Assert.Contains("Built-in compliance presets", list.Output, StringComparison.Ordinal);
        Assert.Contains("jdai/compliance/soc2", list.Output, StringComparison.Ordinal);

        Assert.Equal(0, help.ExitCode);
        Assert.Contains("jdai policy compliance", help.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Compliance_Check_ValidAndInvalidProfiles_ReturnExpectedCodes()
    {
        var missing = await CaptureStderrAsync(() => PolicyComplianceCliHandler.RunAsync(["check"]));
        var unknown = await CaptureStderrAsync(
            () => PolicyComplianceCliHandler.RunAsync(["check", "--profile", "not-a-real-profile"]));
        var known = await CaptureStdoutAsync(
            () => PolicyComplianceCliHandler.RunAsync(["check", "--profile", "soc2"]));

        Assert.Equal(1, missing.ExitCode);
        Assert.Contains("--profile is required", missing.Output, StringComparison.Ordinal);

        Assert.Equal(1, unknown.ExitCode);
        Assert.Contains("Unknown or unsupported profile", unknown.Output, StringComparison.Ordinal);

        Assert.Equal(1, known.ExitCode);
        Assert.Contains("Compliance Check — jdai/compliance/soc2", known.Output, StringComparison.Ordinal);
        Assert.Contains("Result:", known.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compliance_UnknownSubcommand_ReturnsOne()
    {
        var unknown = await CaptureStderrAsync(() => PolicyComplianceCliHandler.RunAsync(["bogus"]));

        Assert.Equal(1, unknown.ExitCode);
        Assert.Contains("Unknown compliance command", unknown.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PolicySubcommand_DelegatesToComplianceHandler()
    {
        var result = await CaptureStdoutAsync(() => PolicySubcommandHandler.RunAsync(["compliance", "list"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Built-in compliance presets", result.Output, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string Output)> CaptureStdoutAsync(Func<Task<int>> action)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var code = await action().ConfigureAwait(false);
            return (code, writer.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static async Task<(int ExitCode, string Output)> CaptureStderrAsync(Func<Task<int>> action)
    {
        var original = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            var code = await action().ConfigureAwait(false);
            return (code, writer.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }
}
