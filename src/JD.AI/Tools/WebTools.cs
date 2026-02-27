using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using HtmlAgilityPack;
using Microsoft.SemanticKernel;

namespace JD.AI.Tui.Tools;

/// <summary>
/// Web tools — fetch URLs and convert HTML to readable text.
/// </summary>
public sealed class WebTools
{
    private static readonly HttpClient SharedClient = new();

    [KernelFunction("web_fetch")]
    [Description("Fetch a URL and return its content as readable text (HTML converted to plain text).")]
    public static async Task<string> WebFetchAsync(
        [Description("The URL to fetch")] string url,
        [Description("Maximum characters to return (default 5000)")] int maxLength = 5000)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("jdai", "1.0"));

            using var response = await SharedClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            string text;
            if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            {
                text = HtmlToText(body);
            }
            else
            {
                text = body;
            }

            if (text.Length > maxLength)
            {
                text = string.Concat(text.AsSpan(0, maxLength), "\n... [truncated]");
            }

            return text;
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching {url}: {ex.Message}";
        }
    }

    private static string HtmlToText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove script and style elements
        var nodesToRemove = doc.DocumentNode
            .SelectNodes("//script|//style|//nav|//header|//footer");
        if (nodesToRemove != null)
        {
            foreach (var node in nodesToRemove)
            {
                node.Remove();
            }
        }

        // Extract text from main content or body
        var mainContent = doc.DocumentNode.SelectSingleNode("//main")
            ?? doc.DocumentNode.SelectSingleNode("//article")
            ?? doc.DocumentNode.SelectSingleNode("//body")
            ?? doc.DocumentNode;

        var sb = new StringBuilder();
        ExtractText(mainContent, sb);

        // Clean up whitespace
        var text = sb.ToString();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private static void ExtractText(HtmlNode node, StringBuilder sb)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.Append(text);
                sb.Append(' ');
            }

            return;
        }

        // Block elements get newlines
        var isBlock = node.Name is "p" or "div" or "br" or "h1" or "h2" or "h3"
            or "h4" or "h5" or "h6" or "li" or "tr" or "blockquote" or "pre";

        if (isBlock)
        {
            sb.AppendLine();
        }

        // Headings get markdown-style prefix
        if (node.Name is "h1" or "h2" or "h3" or "h4")
        {
            var level = node.Name[1] - '0';
            sb.Append(new string('#', level));
            sb.Append(' ');
        }

        // List items get bullet
        if (string.Equals(node.Name, "li", StringComparison.Ordinal))
        {
            sb.Append("- ");
        }

        foreach (var child in node.ChildNodes)
        {
            ExtractText(child, sb);
        }

        if (isBlock)
        {
            sb.AppendLine();
        }
    }
}
