using JD.AI.Commands;

namespace JD.AI.Tests.Commands;

/// <summary>
/// Tests for CLI command handler routing and output.
/// These handlers are internal static classes; internal visibility is granted via InternalsVisibleTo.
/// </summary>
public sealed class CliHandlerTests
{
    // ── AgentsCliHandler ─────────────────────────────────────────────────────

    [Fact]
    public async Task AgentsCliHandler_Help_PrintsUsageAndReturnsZero()
    {
        var (output, _, exit) = await CaptureAgents("help");

        Assert.Equal(0, exit);
        Assert.Contains("jdai agents", output);
    }

    [Fact]
    public async Task AgentsCliHandler_HelpShortFlag_ReturnsZero()
    {
        var (output, _, exit) = await CaptureAgents("--help");

        Assert.Equal(0, exit);
        Assert.Contains("jdai agents", output);
    }

    [Fact]
    public async Task AgentsCliHandler_UnknownSubcommand_WritesErrorAndReturnsOne()
    {
        var (_, error, exit) = await CaptureAgents("nonexistent-subcommand");

        Assert.Equal(1, exit);
        Assert.Contains("nonexistent-subcommand", error);
    }

    [Fact]
    public async Task AgentsCliHandler_Tag_TooFewArgs_ReturnsOne()
    {
        var (_, error, exit) = await CaptureAgents("tag"); // missing name + version

        Assert.Equal(1, exit);
        Assert.Contains("Usage", error);
    }

    [Fact]
    public async Task AgentsCliHandler_Promote_TooFewArgs_ReturnsOne()
    {
        var (_, error, exit) = await CaptureAgents("promote"); // missing name

        Assert.Equal(1, exit);
        Assert.Contains("Usage", error);
    }

    [Fact]
    public async Task AgentsCliHandler_Remove_TooFewArgs_ReturnsOne()
    {
        var (_, error, exit) = await CaptureAgents("remove"); // missing name + version

        Assert.Equal(1, exit);
        Assert.Contains("Usage", error);
    }

    // ── PluginCliHandler ──────────────────────────────────────────────────────

    [Fact]
    public async Task PluginCliHandler_Help_PrintsUsageAndReturnsZero()
    {
        var (output, _, exit) = await CapturePlugin("help");

        Assert.Equal(0, exit);
        Assert.Contains("jdai plugin", output);
    }

    [Fact]
    public async Task PluginCliHandler_HelpFlag_ReturnsZero()
    {
        var (output, _, exit) = await CapturePlugin("--help");

        Assert.Equal(0, exit);
        Assert.Contains("jdai plugin", output);
    }

    [Fact]
    public async Task PluginCliHandler_UnknownSubcommand_WritesErrorAndReturnsOne()
    {
        var (_, error, exit) = await CapturePlugin("bogus-command");

        Assert.Equal(1, exit);
        Assert.Contains("bogus-command", error);
    }

    [Fact]
    public async Task PluginCliHandler_Install_NoArgs_ReturnsOne()
    {
        var (_, error, exit) = await CapturePlugin("install"); // missing path-or-url

        Assert.Equal(1, exit);
        Assert.Contains("Usage", error);
    }

    [Fact]
    public async Task PluginCliHandler_Enable_NoArgs_ReturnsOne()
    {
        var (_, error, exit) = await CapturePlugin("enable");

        Assert.Equal(1, exit);
        Assert.Contains("Usage", error);
    }

    [Fact]
    public async Task PluginCliHandler_Disable_NoArgs_ReturnsOne()
    {
        var (_, error, exit) = await CapturePlugin("disable");

        Assert.Equal(1, exit);
        Assert.Contains("Usage", error);
    }

    [Fact]
    public async Task PluginCliHandler_Uninstall_NoArgs_ReturnsOne()
    {
        var (_, error, exit) = await CapturePlugin("uninstall");

        Assert.Equal(1, exit);
        Assert.Contains("Usage", error);
    }

    [Fact]
    public async Task PluginCliHandler_Info_NoArgs_ReturnsOne()
    {
        var (_, error, exit) = await CapturePlugin("info");

        Assert.Equal(1, exit);
        Assert.Contains("Usage", error);
    }

    // ── PolicyComplianceCliHandler ────────────────────────────────────────────

    [Fact]
    public async Task PolicyComplianceCliHandler_List_PrintsPresetsAndReturnsZero()
    {
        var (output, _, exit) = await CaptureCompliance("list");

        Assert.Equal(0, exit);
        // Should list at least one built-in preset
        Assert.Contains("compliance", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PolicyComplianceCliHandler_Help_ReturnsZero()
    {
        var (output, _, exit) = await CaptureCompliance("help");

        Assert.Equal(0, exit);
        Assert.Contains("jdai policy compliance", output);
    }

    [Fact]
    public async Task PolicyComplianceCliHandler_UnknownSubcommand_ReturnsOne()
    {
        var (_, error, exit) = await CaptureCompliance("unknown-xyz");

        Assert.Equal(1, exit);
        Assert.Contains("unknown-xyz", error);
    }

    [Fact]
    public async Task PolicyComplianceCliHandler_Check_MissingProfile_ReturnsOne()
    {
        var (_, error, exit) = await CaptureCompliance("check"); // missing --profile

        Assert.Equal(1, exit);
        Assert.Contains("--profile", error);
    }

    [Fact]
    public async Task PolicyComplianceCliHandler_Check_UnknownProfile_ReturnsOne()
    {
        var (_, error, exit) = await CaptureCompliance("check", "--profile", "jdai/compliance/nonexistent-profile-xyz");

        Assert.Equal(1, exit);
        Assert.Contains("nonexistent-profile-xyz", error);
    }

    [Fact]
    public async Task PolicyComplianceCliHandler_NoArgs_DefaultsToList()
    {
        var (output, _, exit) = await CaptureCompliance(); // no args → list

        Assert.Equal(0, exit);
        Assert.Contains("compliance", output, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<(string output, string error, int exit)> CaptureAgents(params string[] args)
    {
        using var swOut = new StringWriter();
        using var swErr = new StringWriter();
        var origOut = Console.Out;
        var origErr = Console.Error;
        Console.SetOut(swOut);
        Console.SetError(swErr);
        try
        {
            var exit = await AgentsCliHandler.RunAsync(args);
            return (swOut.ToString(), swErr.ToString(), exit);
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }
    }

    private static async Task<(string output, string error, int exit)> CapturePlugin(params string[] args)
    {
        using var swOut = new StringWriter();
        using var swErr = new StringWriter();
        var origOut = Console.Out;
        var origErr = Console.Error;
        Console.SetOut(swOut);
        Console.SetError(swErr);
        try
        {
            var exit = await PluginCliHandler.RunAsync(args);
            return (swOut.ToString(), swErr.ToString(), exit);
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }
    }

    private static async Task<(string output, string error, int exit)> CaptureCompliance(params string[] args)
    {
        using var swOut = new StringWriter();
        using var swErr = new StringWriter();
        var origOut = Console.Out;
        var origErr = Console.Error;
        Console.SetOut(swOut);
        Console.SetError(swErr);
        try
        {
            var exit = await PolicyComplianceCliHandler.RunAsync(args);
            return (swOut.ToString(), swErr.ToString(), exit);
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }
    }
}
