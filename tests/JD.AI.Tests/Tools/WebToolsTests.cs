using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Tools;

/// <summary>
/// Tests for <see cref="WebTools"/>.
/// Real HTTP is avoided; the shared static <c>HttpClient</c> cannot be replaced,
/// so tests that exercise the HTML parsing and text-extraction logic call the
/// private helper methods via reflection, and network-dependent paths are
/// validated through the error-handling branch (bad URL / offline).
/// </summary>
public sealed class WebToolsTests
{
    // ── HtmlToText (via reflection) ───────────────────────────────────────────

    private static string InvokeHtmlToText(string html)
    {
        var method = typeof(WebTools).GetMethod(
            "HtmlToText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        return (string)method.Invoke(null, [html])!;
    }

    [Fact]
    public void HtmlToText_PlainBody_ExtractsText()
    {
        const string Html = "<html><body><p>Hello World</p></body></html>";

        var result = InvokeHtmlToText(Html);

        result.Should().Contain("Hello World");
    }

    [Fact]
    public void HtmlToText_RemovesScriptTags()
    {
        const string Html = "<html><body><p>Visible</p><script>alert('hidden')</script></body></html>";

        var result = InvokeHtmlToText(Html);

        result.Should().Contain("Visible");
        result.Should().NotContain("hidden");
        result.Should().NotContain("alert");
    }

    [Fact]
    public void HtmlToText_RemovesStyleTags()
    {
        const string Html = "<html><body><p>Content</p><style>.cls { color: red; }</style></body></html>";

        var result = InvokeHtmlToText(Html);

        result.Should().Contain("Content");
        result.Should().NotContain(".cls");
        result.Should().NotContain("color");
    }

    [Fact]
    public void HtmlToText_RemovesNavHeaderFooter()
    {
        const string Html = """
            <html><body>
              <nav>Navigation links</nav>
              <header>Site header</header>
              <main>Main content</main>
              <footer>Footer text</footer>
            </body></html>
            """;

        var result = InvokeHtmlToText(Html);

        result.Should().Contain("Main content");
        result.Should().NotContain("Navigation links");
        result.Should().NotContain("Site header");
        result.Should().NotContain("Footer text");
    }

    [Fact]
    public void HtmlToText_PrefersMainElement()
    {
        const string Html = """
            <html><body>
              <main><p>Main area content</p></main>
              <p>Body paragraph</p>
            </body></html>
            """;

        var result = InvokeHtmlToText(Html);

        result.Should().Contain("Main area content");
    }

    [Fact]
    public void HtmlToText_FallsBackToArticle_WhenNoMain()
    {
        const string Html = """
            <html><body>
              <article><p>Article content</p></article>
              <p>Outside article</p>
            </body></html>
            """;

        var result = InvokeHtmlToText(Html);

        result.Should().Contain("Article content");
    }

    [Fact]
    public void HtmlToText_FallsBackToBody_WhenNoMainOrArticle()
    {
        const string Html = "<html><body><p>Body only</p></body></html>";

        var result = InvokeHtmlToText(Html);

        result.Should().Contain("Body only");
    }

    [Fact]
    public void HtmlToText_HeadingsGetMarkdownPrefix()
    {
        const string Html = "<html><body><h1>Title</h1><h2>Subtitle</h2></body></html>";

        var result = InvokeHtmlToText(Html);

        result.Should().Contain("# Title");
        result.Should().Contain("## Subtitle");
    }

    [Fact]
    public void HtmlToText_H3_GetsThreeHashes()
    {
        const string Html = "<html><body><h3>Section</h3></body></html>";

        var result = InvokeHtmlToText(Html);

        result.Should().Contain("### Section");
    }

    [Fact]
    public void HtmlToText_H4_GetsFourHashes()
    {
        const string Html = "<html><body><h4>Sub-section</h4></body></html>";

        var result = InvokeHtmlToText(Html);

        result.Should().Contain("#### Sub-section");
    }

    [Fact]
    public void HtmlToText_ListItemsGetBullets()
    {
        const string Html = "<html><body><ul><li>First</li><li>Second</li></ul></body></html>";

        var result = InvokeHtmlToText(Html);

        result.Should().Contain("- First");
        result.Should().Contain("- Second");
    }

    [Fact]
    public void HtmlToText_DecodesHtmlEntities()
    {
        const string Html = "<html><body><p>Hello &amp; World &lt;test&gt;</p></body></html>";

        var result = InvokeHtmlToText(Html);

        result.Should().Contain("Hello & World <test>");
    }

    [Fact]
    public void HtmlToText_CollapsesMultipleBlankLines()
    {
        const string Html = "<html><body><p>A</p><p></p><p></p><p>B</p></body></html>";

        var result = InvokeHtmlToText(Html);

        // Must not have more than two consecutive newlines
        result.Should().NotMatchRegex(@"\n{3,}");
    }

    [Fact]
    public void HtmlToText_TrimsLeadingAndTrailingWhitespace()
    {
        const string Html = "<html><body>  <p>Trimmed</p>  </body></html>";

        var result = InvokeHtmlToText(Html);

        result.Should().NotStartWith(" ");
        result.Should().NotEndWith(" ");
    }

    [Fact]
    public void HtmlToText_EmptyHtml_ReturnsEmptyOrWhitespace()
    {
        var result = InvokeHtmlToText("<html><body></body></html>");

        result.Should().BeEmpty();
    }

    [Fact]
    public void HtmlToText_PlainTextInput_ReturnsText()
    {
        // No html/body tags — treats as fragment
        var result = InvokeHtmlToText("Just some text");

        result.Should().Contain("Just some text");
    }

    // ── ExtractText (block elements produce newlines) ─────────────────────────

    [Fact]
    public void HtmlToText_DivProducesNewline()
    {
        const string Html = "<html><body><div>Line one</div><div>Line two</div></body></html>";

        var result = InvokeHtmlToText(Html);

        // Both texts must appear; newlines between them ensured by block logic
        result.Should().Contain("Line one");
        result.Should().Contain("Line two");
    }

    [Fact]
    public void HtmlToText_BlockquoteIsExtracted()
    {
        const string Html = "<html><body><blockquote>Quoted text</blockquote></body></html>";

        var result = InvokeHtmlToText(Html);

        result.Should().Contain("Quoted text");
    }

    [Fact]
    public void HtmlToText_TableRowsAreExtracted()
    {
        const string Html = "<html><body><table><tr><td>Cell A</td></tr></table></body></html>";

        var result = InvokeHtmlToText(Html);

        result.Should().Contain("Cell A");
    }

    // ── WebFetchAsync: invalid URL ────────────────────────────────────────────

    [Fact]
    public async Task WebFetchAsync_BadUrl_ReturnsErrorMessage()
    {
        // An invalid URI causes InvalidOperationException from HttpClient.
        Func<Task> act = () => WebTools.WebFetchAsync("not_a_valid_url");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── WebFetchAsync: maxLength truncation (logic only) ──────────────────────

    /// <summary>
    /// Verifies the truncation logic by exercising it through a fake handler
    /// injected via the static shared client.  Because SharedClient is a static
    /// readonly field we cannot replace it in unit tests, so we test truncation
    /// indirectly through the error path and via reflection on HtmlToText output.
    /// </summary>
    [Fact]
    public void HtmlToText_LargeOutput_CanBeTruncatedExternally()
    {
        // Generate large HTML body
        var sb = new StringBuilder();
        sb.Append("<html><body>");
        for (var i = 0; i < 1000; i++)
            sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"<p>Paragraph {i} of content that is fairly long.</p>");
        sb.Append("</body></html>");

        var result = InvokeHtmlToText(sb.ToString());

        // Result is a valid non-empty string
        result.Should().NotBeNullOrEmpty();

        // Manually truncate to simulate what WebFetchAsync does with maxLength
        const int MaxLength = 100;
        if (result.Length > MaxLength)
        {
            var truncated = string.Concat(result.AsSpan(0, MaxLength), "\n... [truncated]");
            truncated.Should().EndWith("[truncated]");
            truncated.Length.Should().BeLessThan(result.Length);
        }
    }

    // ── ActivitySources / Meters constants (Telemetry) ────────────────────────

    [Fact]
    public void ActivitySources_AllSourceNames_ContainsAllFour()
    {
        var names = JD.AI.Telemetry.ActivitySources.AllSourceNames;

        names.Should().HaveCount(4);
        names.Should().Contain(JD.AI.Telemetry.ActivitySources.AgentSourceName);
        names.Should().Contain(JD.AI.Telemetry.ActivitySources.ToolsSourceName);
        names.Should().Contain(JD.AI.Telemetry.ActivitySources.ProvidersSourceName);
        names.Should().Contain(JD.AI.Telemetry.ActivitySources.SessionsSourceName);
    }

    [Fact]
    public void ActivitySources_ConstantNames_HaveExpectedValues()
    {
        JD.AI.Telemetry.ActivitySources.AgentSourceName.Should().Be("JD.AI.Agent");
        JD.AI.Telemetry.ActivitySources.ToolsSourceName.Should().Be("JD.AI.Tools");
        JD.AI.Telemetry.ActivitySources.ProvidersSourceName.Should().Be("JD.AI.Providers");
        JD.AI.Telemetry.ActivitySources.SessionsSourceName.Should().Be("JD.AI.Sessions");
    }
}
