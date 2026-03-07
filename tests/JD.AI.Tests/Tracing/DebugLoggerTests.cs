using FluentAssertions;
using JD.AI.Core.Tracing;

namespace JD.AI.Tests.Tracing;

/// <summary>
/// Tests for <see cref="DebugLogger.Log"/> output formatting and suppression.
/// ParseCategories/Enable/IsEnabled are covered in TraceContextTests.cs.
/// </summary>
public sealed class DebugLoggerOutputTests : IDisposable
{
    private readonly TextWriter _originalStderr;

    public DebugLoggerOutputTests()
    {
        _originalStderr = Console.Error;
        DebugLogger.Enable(DebugCategory.None);
    }

    public void Dispose()
    {
        Console.SetError(_originalStderr);
        DebugLogger.Enable(DebugCategory.None);
    }

    // ── Log suppression ────────────────────────────────────────────────

    [Fact]
    public void Log_CategoryNotEnabled_SuppressesOutput()
    {
        DebugLogger.Enable(DebugCategory.Sessions);

        using var sw = new StringWriter();
        Console.SetError(sw);

        DebugLogger.Log(DebugCategory.Tools, "should not appear");

        sw.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Log_CategoryEnabled_WritesToStderr()
    {
        DebugLogger.Enable(DebugCategory.Tools);

        using var sw = new StringWriter();
        Console.SetError(sw);

        DebugLogger.Log(DebugCategory.Tools, "hello world");

        var output = sw.ToString();
        output.Should().Contain("[DEBUG tools]");
        output.Should().Contain("hello world");
    }

    [Fact]
    public void Log_AllEnabled_AnyCategory_WritesToStderr()
    {
        DebugLogger.Enable(DebugCategory.All);

        using var sw = new StringWriter();
        Console.SetError(sw);

        DebugLogger.Log(DebugCategory.Providers, "provider msg");

        sw.ToString().Should().Contain("[DEBUG providers]");
    }

    [Fact]
    public void Log_NoneEnabled_SuppressesAll()
    {
        DebugLogger.Enable(DebugCategory.None);

        using var sw = new StringWriter();
        Console.SetError(sw);

        DebugLogger.Log(DebugCategory.Tools, "nope");
        DebugLogger.Log(DebugCategory.Agents, "nope");

        sw.ToString().Should().BeEmpty();
    }

    // ── Log with format args ───────────────────────────────────────────

    [Fact]
    public void Log_WithFormatArgs_FormatsCorrectly()
    {
        DebugLogger.Enable(DebugCategory.Agents);

        using var sw = new StringWriter();
        Console.SetError(sw);

        DebugLogger.Log(DebugCategory.Agents, "count={0} name={1}", 42, "test");

        var output = sw.ToString();
        output.Should().Contain("[DEBUG agents]");
        output.Should().Contain("count=42 name=test");
    }

    [Fact]
    public void Log_WithFormatArgs_CategoryDisabled_SuppressesOutput()
    {
        DebugLogger.Enable(DebugCategory.Tools);

        using var sw = new StringWriter();
        Console.SetError(sw);

        DebugLogger.Log(DebugCategory.Sessions, "val={0}", 99);

        sw.ToString().Should().BeEmpty();
    }

    // ── Multiple categories ────────────────────────────────────────────

    [Fact]
    public void Log_MultipleCategoriesEnabled_MatchesAny()
    {
        DebugLogger.Enable(DebugCategory.Tools | DebugCategory.Sessions);

        using var sw = new StringWriter();
        Console.SetError(sw);

        DebugLogger.Log(DebugCategory.Sessions, "session msg");

        sw.ToString().Should().Contain("[DEBUG sessions]");
    }

    [Fact]
    public void Log_CategoryLowercasesEnumName()
    {
        DebugLogger.Enable(DebugCategory.Policies);

        using var sw = new StringWriter();
        Console.SetError(sw);

        DebugLogger.Log(DebugCategory.Policies, "policy check");

        sw.ToString().Should().Contain("[DEBUG policies]");
    }
}
