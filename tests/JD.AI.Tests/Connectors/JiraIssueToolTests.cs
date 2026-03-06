using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JD.AI.Connectors.Jira;

namespace JD.AI.Tests.Connectors;

public sealed class JiraIssueToolTests
{
    private static JiraIssueTool CreateTool(HttpClient httpClient)
    {
        var options = new JiraConnectorOptions
        {
            BaseUrl = "https://test.atlassian.net",
            Email = "user@example.com",
            ApiToken = "test-token",
        };
        var auth = new JiraApiKeyAuthProvider(options);
        return new JiraIssueTool(httpClient, auth, options);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsFormattedIssue()
    {
        var payload = JsonSerializer.Serialize(new
        {
            key = "PROJ-1",
            fields = new
            {
                summary = "Test issue",
                description = (string?)null,
                status = new { name = "To Do" },
                assignee = new { displayName = "Alice" },
            },
        });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, payload);
        var client = new HttpClient(handler);
        var tool = CreateTool(client);

        var result = await tool.GetIssueAsync("PROJ-1");

        Assert.Contains("PROJ-1", result);
        Assert.Contains("Test issue", result);
        Assert.Contains("To Do", result);
        Assert.Contains("Alice", result);
    }

    [Fact]
    public async Task SearchIssuesAsync_WithResults_ReturnsFormattedList()
    {
        var payload = JsonSerializer.Serialize(new
        {
            issues = new[]
            {
                new { key = "PROJ-1", fields = new { summary = "First issue" } },
                new { key = "PROJ-2", fields = new { summary = "Second issue" } },
            },
        });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, payload);
        var client = new HttpClient(handler);
        var tool = CreateTool(client);

        var result = await tool.SearchIssuesAsync("project = PROJ");

        Assert.Contains("PROJ-1", result);
        Assert.Contains("PROJ-2", result);
    }

    [Fact]
    public async Task SearchIssuesAsync_NoResults_ReturnsEmptyMessage()
    {
        var payload = JsonSerializer.Serialize(new { issues = Array.Empty<object>() });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, payload);
        var client = new HttpClient(handler);
        var tool = CreateTool(client);

        var result = await tool.SearchIssuesAsync("project = EMPTY");

        Assert.Equal("No issues found.", result);
    }

    [Fact]
    public async Task CreateIssueAsync_Success_ReturnsIssueKey()
    {
        var payload = JsonSerializer.Serialize(new { key = "PROJ-99" });

        var handler = new MockHttpMessageHandler(HttpStatusCode.Created, payload);
        var client = new HttpClient(handler);
        var tool = CreateTool(client);

        var result = await tool.CreateIssueAsync("PROJ", "New feature", "Task");

        Assert.Contains("PROJ-99", result);
    }

    [Fact]
    public async Task GetIssueAsync_EmptyKey_ThrowsArgumentException()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{}");
        var client = new HttpClient(handler);
        var tool = CreateTool(client);

        await Assert.ThrowsAsync<ArgumentException>(() => tool.GetIssueAsync(""));
    }

    [Fact]
    public void Constructor_WithNullClient_ThrowsArgumentNullException()
    {
        var options = new JiraConnectorOptions { BaseUrl = "https://x.atlassian.net" };
        var auth = new JiraApiKeyAuthProvider(options);

        Assert.Throws<ArgumentNullException>(() => new JiraIssueTool(null!, auth, options));
    }

    /// <summary>Minimal mock HTTP handler for deterministic responses.</summary>
    private sealed class MockHttpMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
