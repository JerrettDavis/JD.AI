using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

namespace JD.AI.Connectors.Jira;

/// <summary>
/// Semantic Kernel tool plugin providing Jira issue operations.
/// Exposes CreateIssue, GetIssue, and SearchIssues as AI-callable functions.
/// </summary>
public sealed class JiraIssueTool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] SearchFields = ["summary", "status", "assignee"];

    private readonly HttpClient _httpClient;
    private readonly JiraApiKeyAuthProvider _auth;
    private readonly JiraConnectorOptions _options;

    /// <summary>Initializes the tool with HTTP client, auth provider, and options.</summary>
    public JiraIssueTool(
        HttpClient httpClient,
        JiraApiKeyAuthProvider auth,
        JiraConnectorOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Gets a Jira issue by key (e.g. PROJECT-123).</summary>
    [KernelFunction("jira_get_issue")]
    [System.ComponentModel.Description("Retrieves a Jira issue by its key. Returns summary, status, assignee, and description.")]
    public async Task<string> GetIssueAsync(
        [System.ComponentModel.Description("The Jira issue key, e.g. 'PROJECT-123'")] string issueKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueKey);

        var request = await BuildRequestAsync(
            HttpMethod.Get,
            $"/rest/api/3/issue/{Uri.EscapeDataString(issueKey)}",
            cancellationToken).ConfigureAwait(false);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var issue = JsonSerializer.Deserialize<JiraIssue>(json, JsonOptions);
        if (issue is null) return "Issue not found.";

        return $"[{issue.Key}] {issue.Fields?.Summary}\n" +
               $"Status: {issue.Fields?.Status?.Name}\n" +
               $"Assignee: {issue.Fields?.Assignee?.DisplayName ?? "Unassigned"}\n" +
               $"Description: {issue.Fields?.Description ?? "(no description)"}";
    }

    /// <summary>Searches Jira issues using JQL.</summary>
    [KernelFunction("jira_search_issues")]
    [System.ComponentModel.Description("Searches Jira issues using JQL (Jira Query Language). Returns issue keys and summaries.")]
    public async Task<string> SearchIssuesAsync(
        [System.ComponentModel.Description("JQL query, e.g. 'project = MYPROJ AND status = Open ORDER BY created DESC'")] string jql,
        [System.ComponentModel.Description("Maximum number of results (1–50)")] int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jql);

        var request = await BuildRequestAsync(
            HttpMethod.Post,
            "/rest/api/3/issue/search",
            cancellationToken,
            new { jql, maxResults = Math.Clamp(maxResults, 1, 50), fields = SearchFields })
            .ConfigureAwait(false);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JiraSearchResult>(JsonOptions, cancellationToken).ConfigureAwait(false);
        if (result?.Issues is null or { Count: 0 }) return "No issues found.";

        var lines = result.Issues.Select(i => $"- [{i.Key}] {i.Fields?.Summary}");
        return string.Join("\n", lines);
    }

    /// <summary>Creates a new Jira issue.</summary>
    [KernelFunction("jira_create_issue")]
    [System.ComponentModel.Description("Creates a new Jira issue. Returns the created issue key.")]
    public async Task<string> CreateIssueAsync(
        [System.ComponentModel.Description("Project key (e.g. 'MYPROJ')")] string projectKey,
        [System.ComponentModel.Description("Issue summary/title")] string summary,
        [System.ComponentModel.Description("Issue type name (e.g. 'Bug', 'Story', 'Task')")] string issueType = "Task",
        [System.ComponentModel.Description("Optional description")] string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        var body = new
        {
            fields = new
            {
                project = new { key = projectKey },
                summary,
                issuetype = new { name = issueType },
                description = description is not null
                    ? new { type = "doc", version = 1, content = new[] { new { type = "paragraph", content = new[] { new { type = "text", text = description } } } } }
                    : null,
            },
        };

        var request = await BuildRequestAsync(HttpMethod.Post, "/rest/api/3/issue", cancellationToken, body)
            .ConfigureAwait(false);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JiraCreateResult>(JsonOptions, cancellationToken).ConfigureAwait(false);
        return result?.Key is not null
            ? $"Created issue: {result.Key}"
            : "Issue created successfully.";
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(
        HttpMethod method,
        string path,
        CancellationToken ct,
        object? body = null)
    {
        var baseUrl = _options.BaseUrl?.TrimEnd('/') ?? throw new InvalidOperationException("Jira BaseUrl is not configured.");
        var request = new HttpRequestMessage(method, $"{baseUrl}{path}");

        var authHeader = await _auth.GetAuthorizationHeaderAsync(ct).ConfigureAwait(false);
        if (authHeader is not null)
        {
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return request;
    }

    // Minimal models for JSON deserialization
    private sealed class JiraIssue
    {
        [JsonPropertyName("key")] public string? Key { get; init; }
        [JsonPropertyName("fields")] public JiraIssueFields? Fields { get; init; }
    }

    private sealed class JiraIssueFields
    {
        [JsonPropertyName("summary")] public string? Summary { get; init; }
        [JsonPropertyName("description")] public string? Description { get; init; }
        [JsonPropertyName("status")] public JiraStatus? Status { get; init; }
        [JsonPropertyName("assignee")] public JiraUser? Assignee { get; init; }
    }

    private sealed class JiraStatus
    {
        [JsonPropertyName("name")] public string? Name { get; init; }
    }

    private sealed class JiraUser
    {
        [JsonPropertyName("displayName")] public string? DisplayName { get; init; }
    }

    private sealed class JiraSearchResult
    {
        [JsonPropertyName("issues")] public List<JiraIssue>? Issues { get; init; }
    }

    private sealed class JiraCreateResult
    {
        [JsonPropertyName("key")] public string? Key { get; init; }
    }
}
