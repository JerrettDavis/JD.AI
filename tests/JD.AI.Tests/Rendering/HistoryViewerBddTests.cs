using FluentAssertions;
using JD.AI.Core.Sessions;
using JD.AI.Rendering;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace JD.AI.Tests.Rendering;

[Feature("History Viewer")]
[Collection("DataDirectories")]
public sealed class HistoryViewerBddTests : TinyBddXunitBase
{
    public HistoryViewerBddTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Empty session view shows a friendly message and exits"), Fact]
    public async Task Show_WithEmptySession_ReturnsNull()
    {
        int? rollbackIndex = -1;
        string output = string.Empty;

        await Given("a session with no turns", () => new SessionInfo { Name = "Empty Session" })
            .When("the history viewer is opened", session =>
            {
                output = CaptureStdout(() => rollbackIndex = HistoryViewer.Show(session));
                return session;
            })
            .Then("it displays the empty-state message and returns null", _ =>
            {
                rollbackIndex.Should().BeNull();
                output.Should().Contain("No turns in this session.");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Detail rendering includes tools files and trimmed content"), Fact]
    public async Task RenderDetail_ShowsExpectedSections()
    {
        string output = string.Empty;

        await Given("an assistant turn with tools files and long text", BuildDetailedTurn)
            .When("rendering details for the turn", turn =>
            {
                output = CaptureStdout(() => HistoryViewer.RenderDetail(turn));
                return turn;
            })
            .Then("the details include tool calls files touched and content", _ =>
            {
                output.Should().Contain("Tool Calls (1):");
                output.Should().Contain("Files Touched (1):");
                output.Should().Contain("Content:");
                output.Should().Contain("...");
                return true;
            })
            .AssertPassed();
    }

    private static TurnRecord BuildDetailedTurn()
    {
        var turn = new TurnRecord
        {
            TurnIndex = 7,
            Role = "assistant",
            Content = new string('c', 340),
            ThinkingText = new string('t', 260),
            ModelId = "gpt-5.3-codex",
            ProviderName = "OpenAI Codex",
            TokensIn = 200,
            TokensOut = 120,
            DurationMs = 950,
        };
        turn.ToolCalls.Add(new ToolCallRecord
        {
            ToolName = "write_file",
            Status = "ok",
            DurationMs = 88,
        });
        turn.FilesTouched.Add(new FileTouchRecord
        {
            Operation = "write",
            FilePath = "src/JD.AI/Startup/ProviderOrchestrator.cs",
        });
        return turn;
    }

    private static string CaptureStdout(Action action)
    {
        var old = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            action();
            return sw.ToString();
        }
        finally
        {
            Console.SetOut(old);
        }
    }
}
