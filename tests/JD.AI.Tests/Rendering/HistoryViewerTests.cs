using System.Collections.ObjectModel;
using FluentAssertions;
using JD.AI.Core.Sessions;
using JD.AI.Rendering;
using Xunit;

namespace JD.AI.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="HistoryViewer"/>.
/// Tests cover RenderDetail output, content/thinking truncation, edge cases,
/// and Show early-return paths. Interactive keyboard tests are excluded
/// because they require a real console.
/// </summary>
[Collection("Console")]
public sealed class HistoryViewerTests
{
    // ── Helper: capture Console output ──────────────────────────────

    private static string CaptureConsoleOutput(Action action)
    {
        var saved = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(saved);
        }
    }

    private static TurnRecord CreateTurn(
        int index = 0,
        string role = "assistant",
        string? content = "Hello world",
        string? thinkingText = null,
        string? modelId = "gpt-4",
        string? providerName = "openai",
        long tokensIn = 100,
        long tokensOut = 200,
        long durationMs = 1500,
        Collection<ToolCallRecord>? toolCalls = null,
        Collection<FileTouchRecord>? filesTouched = null)
    {
        return new TurnRecord
        {
            TurnIndex = index,
            Role = role,
            Content = content,
            ThinkingText = thinkingText,
            ModelId = modelId,
            ProviderName = providerName,
            TokensIn = tokensIn,
            TokensOut = tokensOut,
            DurationMs = durationMs,
            ToolCalls = toolCalls ?? [],
            FilesTouched = filesTouched ?? []
        };
    }

    private static SessionInfo CreateSession(
        string? name = "test-session",
        params TurnRecord[] turns)
    {
        var session = new SessionInfo { Name = name };
        foreach (var t in turns)
            session.Turns.Add(t);
        return session;
    }

    // ── Show: early return for empty session ────────────────────────

    [Fact]
    public void Show_ReturnsNull_WhenSessionHasNoTurns()
    {
        var session = CreateSession("empty-session");

        var output = CaptureConsoleOutput(() =>
        {
            var result = HistoryViewer.Show(session);
            result.Should().BeNull();
        });

        output.Should().Contain("No turns in this session.");
    }

    // ── RenderDetail: basic output structure ────────────────────────

    [Fact]
    public void RenderDetail_OutputsDetailHeader()
    {
        var turn = CreateTurn();

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        output.Should().Contain("Details");
    }

    [Fact]
    public void RenderDetail_OutputsRoleAndModelAndProvider()
    {
        var turn = CreateTurn(role: "assistant", modelId: "gpt-4", providerName: "openai");

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        output.Should().Contain("Role:")
            .And.Contain("assistant")
            .And.Contain("Model:")
            .And.Contain("gpt-4")
            .And.Contain("Provider:")
            .And.Contain("openai");
    }

    [Fact]
    public void RenderDetail_OutputsTokenCounts()
    {
        var turn = CreateTurn(tokensIn: 150, tokensOut: 250);

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        output.Should().Contain("150 in / 250 out");
    }

    [Fact]
    public void RenderDetail_OutputsDuration()
    {
        var turn = CreateTurn(durationMs: 2500);

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        output.Should().Contain("2500ms");
    }

    [Fact]
    public void RenderDetail_ShowsDash_WhenModelIdIsNull()
    {
        var turn = CreateTurn(modelId: null);

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        // The source renders null model as "—"
        output.Should().Contain("Model:")
            .And.Subject.Should().Contain("\u2014");
    }

    [Fact]
    public void RenderDetail_ShowsDash_WhenProviderNameIsNull()
    {
        var turn = CreateTurn(providerName: null);

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        output.Should().Contain("Provider:")
            .And.Subject.Should().Contain("\u2014");
    }

    // ── RenderDetail: content truncation at 300 chars ───────────────

    [Fact]
    public void RenderDetail_TruncatesContent_WhenLongerThan300Characters()
    {
        var longContent = new string('x', 400);
        var turn = CreateTurn(content: longContent);

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        // Content should be truncated to 297 chars + "..."
        output.Should().Contain("...");
        output.Should().NotContain(longContent);
    }

    [Fact]
    public void RenderDetail_DoesNotTruncateContent_WhenExactly300Characters()
    {
        var content = new string('y', 300);
        var turn = CreateTurn(content: content);

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        output.Should().Contain(content);
    }

    [Fact]
    public void RenderDetail_DoesNotTruncateContent_WhenShorterThan300Characters()
    {
        var content = new string('z', 100);
        var turn = CreateTurn(content: content);

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        output.Should().Contain(content);
    }

    [Fact]
    public void RenderDetail_ShowsEmptyMarker_WhenContentIsNull()
    {
        var turn = CreateTurn(content: null);

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        output.Should().Contain("(empty)");
    }

    // ── RenderDetail: thinking text truncation at 200 chars ─────────

    [Fact]
    public void RenderDetail_TruncatesThinkingText_WhenLongerThan200Characters()
    {
        var longThinking = new string('t', 250);
        var turn = CreateTurn(thinkingText: longThinking);

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        // ThinkingText should be truncated to 197 chars + "..."
        output.Should().Contain("...");
        output.Should().NotContain(longThinking);
    }

    [Fact]
    public void RenderDetail_DoesNotTruncateThinkingText_WhenExactly200Characters()
    {
        var thinking = new string('u', 200);
        var turn = CreateTurn(thinkingText: thinking);

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        output.Should().Contain(thinking);
    }

    [Fact]
    public void RenderDetail_DoesNotTruncateThinkingText_WhenShorterThan200Characters()
    {
        var thinking = new string('v', 50);
        var turn = CreateTurn(thinkingText: thinking);

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        output.Should().Contain(thinking);
    }

    [Fact]
    public void RenderDetail_OmitsThinkingSection_WhenThinkingTextIsNull()
    {
        var turn = CreateTurn(thinkingText: null);

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        // The thinking emoji marker should not appear
        output.Should().NotContain("\U0001f4ad");
    }

    [Fact]
    public void RenderDetail_OmitsThinkingSection_WhenThinkingTextIsEmpty()
    {
        var turn = CreateTurn(thinkingText: "");

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        output.Should().NotContain("\U0001f4ad");
    }

    // ── RenderDetail: tool calls ────────────────────────────────────

    [Fact]
    public void RenderDetail_ShowsToolCalls_WhenPresent()
    {
        var toolCalls = new Collection<ToolCallRecord>
        {
            new() { ToolName = "file_read", Status = "ok", DurationMs = 120 },
            new() { ToolName = "bash", Status = "error", DurationMs = 500 }
        };
        var turn = CreateTurn(toolCalls: toolCalls);

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        output.Should().Contain("Tool Calls (2):");
        output.Should().Contain("file_read");
        output.Should().Contain("ok");
        output.Should().Contain("120ms");
        output.Should().Contain("bash");
        output.Should().Contain("error");
        output.Should().Contain("500ms");
    }

    [Fact]
    public void RenderDetail_OmitsToolCallSection_WhenNoToolCalls()
    {
        var turn = CreateTurn();

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        output.Should().NotContain("Tool Calls");
    }

    // ── RenderDetail: files touched ─────────────────────────────────

    [Fact]
    public void RenderDetail_ShowsFilesTouched_WhenPresent()
    {
        var files = new Collection<FileTouchRecord>
        {
            new() { FilePath = "src/main.cs", Operation = "write" },
            new() { FilePath = "tests/test.cs", Operation = "read" }
        };
        var turn = CreateTurn(filesTouched: files);

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        output.Should().Contain("Files Touched (2):");
        output.Should().Contain("write: src/main.cs");
        output.Should().Contain("read: tests/test.cs");
    }

    [Fact]
    public void RenderDetail_OmitsFilesTouchedSection_WhenNoFiles()
    {
        var turn = CreateTurn();

        var output = CaptureConsoleOutput(() => HistoryViewer.RenderDetail(turn));

        output.Should().NotContain("Files Touched");
    }

    // ── Render: basic structure (requires try/catch for Console.Clear) ──

    [Fact]
    public void Render_DoesNotThrow_ForSingleTurnSession()
    {
        var session = CreateSession("test", CreateTurn(index: 0, role: "user", content: "Hello"));

        // Console.Clear() and Console.WindowHeight may throw IOException
        // when no real console is attached; that is expected in CI
        var ex = Record.Exception(() =>
        {
            var saved = Console.Out;
            try
            {
                Console.SetOut(TextWriter.Null);
                HistoryViewer.Render(session, 0, false);
            }
            catch (IOException)
            {
                // Console.Clear() throws when no console is attached — acceptable in tests
            }
            finally
            {
                Console.SetOut(saved);
            }
        });

        ex.Should().BeNull();
    }

    [Fact]
    public void Render_DoesNotThrow_WhenShowDetailIsTrue()
    {
        var session = CreateSession("test",
            CreateTurn(index: 0, role: "user", content: "Hello"),
            CreateTurn(index: 1, role: "assistant", content: "Hi there!"));

        var ex = Record.Exception(() =>
        {
            var saved = Console.Out;
            try
            {
                Console.SetOut(TextWriter.Null);
                HistoryViewer.Render(session, 1, true);
            }
            catch (IOException)
            {
                // Console.Clear() / Console.WindowHeight may throw
            }
            finally
            {
                Console.SetOut(saved);
            }
        });

        ex.Should().BeNull();
    }

    // ── Render: content preview truncation at 60 chars ──────────────

    [Fact]
    public void Render_TruncatesPreview_WhenContentLongerThan60Characters()
    {
        var longContent = new string('a', 80);
        var session = CreateSession("test", CreateTurn(index: 0, content: longContent));

        string output;
        try
        {
            output = CaptureConsoleOutput(() => HistoryViewer.Render(session, 0, false));
        }
        catch (IOException)
        {
            // Console.Clear() may throw — skip this assertion in that case
            return;
        }

        // Preview should be truncated to 57 chars + "..."
        output.Should().Contain("...");
        output.Should().NotContain(longContent);
    }

    // ── Render: role display ────────────────────────────────────────

    [Fact]
    public void Render_ShowsCorrectRoleEmoji_ForUserTurn()
    {
        var session = CreateSession("test", CreateTurn(index: 0, role: "user", content: "Hi"));

        string output;
        try
        {
            output = CaptureConsoleOutput(() => HistoryViewer.Render(session, 0, false));
        }
        catch (IOException)
        {
            return;
        }

        // User role is displayed with person emoji
        output.Should().Contain("\U0001f464");
    }

    [Fact]
    public void Render_ShowsCorrectRoleEmoji_ForAssistantTurn()
    {
        var session = CreateSession("test", CreateTurn(index: 0, role: "assistant", content: "Hi"));

        string output;
        try
        {
            output = CaptureConsoleOutput(() => HistoryViewer.Render(session, 0, false));
        }
        catch (IOException)
        {
            return;
        }

        // Assistant role is displayed with robot emoji
        output.Should().Contain("\U0001f916");
    }

    // ── Render: tool call badge ─────────────────────────────────────

    [Fact]
    public void Render_ShowsToolCallCount_WhenPresent()
    {
        var toolCalls = new Collection<ToolCallRecord>
        {
            new() { ToolName = "bash", Status = "ok" },
            new() { ToolName = "read", Status = "ok" }
        };
        var session = CreateSession("test", CreateTurn(index: 0, toolCalls: toolCalls));

        string output;
        try
        {
            output = CaptureConsoleOutput(() => HistoryViewer.Render(session, 0, false));
        }
        catch (IOException)
        {
            return;
        }

        output.Should().Contain("[2 tools]");
    }

    // ── Render: session header ──────────────────────────────────────

    [Fact]
    public void Render_IncludesSessionNameInHeader()
    {
        var session = CreateSession("my-session", CreateTurn(index: 0));

        string output;
        try
        {
            output = CaptureConsoleOutput(() => HistoryViewer.Render(session, 0, false));
        }
        catch (IOException)
        {
            return;
        }

        output.Should().Contain("my-session");
        output.Should().Contain("1 turns");
    }

    [Fact]
    public void Render_FallsBackToId_WhenNameIsNull()
    {
        var session = CreateSession(name: null, CreateTurn(index: 0));

        string output;
        try
        {
            output = CaptureConsoleOutput(() => HistoryViewer.Render(session, 0, false));
        }
        catch (IOException)
        {
            return;
        }

        // Should contain the session Id (a 16-char hex string) instead of name
        output.Should().Contain(session.Id);
    }

    // ── Render: content with newlines replaced in preview ───────────

    [Fact]
    public void Render_ReplacesNewlinesInPreview()
    {
        var session = CreateSession("test", CreateTurn(index: 0, content: "line1\nline2\nline3"));

        string output;
        try
        {
            output = CaptureConsoleOutput(() => HistoryViewer.Render(session, 0, false));
        }
        catch (IOException)
        {
            return;
        }

        // Newlines in preview should be replaced with spaces
        output.Should().Contain("line1 line2 line3");
    }

    // ── Render: null content ────────────────────────────────────────

    [Fact]
    public void Render_HandlesNullContent_WithoutThrowing()
    {
        var session = CreateSession("test", CreateTurn(index: 0, content: null));

        var ex = Record.Exception(() =>
        {
            var saved = Console.Out;
            try
            {
                Console.SetOut(TextWriter.Null);
                HistoryViewer.Render(session, 0, false);
            }
            catch (IOException)
            {
                // Console.Clear() may throw
            }
            finally
            {
                Console.SetOut(saved);
            }
        });

        ex.Should().BeNull();
    }
}
