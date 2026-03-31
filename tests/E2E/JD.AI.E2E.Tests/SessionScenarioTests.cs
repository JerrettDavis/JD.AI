using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace JD.AI.E2E.Tests;

/// <summary>
/// End-to-end tests that exercise the full Gateway API surface via HTTP.
/// These tests require a running Gateway (and Ollama when model inference is
/// involved) and are gated by <see cref="OllamaTestHost"/>.
/// All tests in this class are skipped when Ollama is not reachable.
[TestCaseOrderer("Xunit.DependencyInjection.TestCaseOrderer", "Microsoft.NET.Test.Sdk")]
[Trait("Category", "E2E")]
[Collection("Ollama E2E")]
public sealed class SessionScenarioTests : IDisposable
{
    private readonly OllamaTestHost _host;

    public SessionScenarioTests(OllamaTestHost host)
    {
        _host = host;
        _host.EnsureAvailable();
    }

    [Fact]
    public async Task ListSessions_ReturnsOk()
    {
        var response = await _host.GatewayClient.GetAsync("/api/sessions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessions = await response.Content.ReadFromJsonAsync<JsonElement>();
        sessions.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task ListSessions_WithLimit_ReturnsOk()
    {
        var response = await _host.GatewayClient.GetAsync("/api/sessions?limit=5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessions = await response.Content.ReadFromJsonAsync<JsonElement>();
        sessions.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetSession_NotFound_Returns404()
    {
        var response = await _host.GatewayClient.GetAsync("/api/sessions/nonexistent-session-id-12345");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAuditEvents_ReturnsOk()
    {
        var response = await _host.GatewayClient.GetAsync("/api/audit/events");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.TryGetProperty("totalCount", out _).Should().BeTrue();
        json.TryGetProperty("count", out _).Should().BeTrue();
        json.TryGetProperty("events", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetAuditEvents_WithFilters_ReturnsOk()
    {
        var response = await _host.GatewayClient.GetAsync(
            "/api/audit/events?action=tool.invoke&severity=info&limit=10&offset=0");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetGatewayStatus_ReturnsOk()
    {
        var response = await _host.GatewayClient.GetAsync("/api/gateway/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.TryGetProperty("status", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetAgents_ReturnsOk()
    {
        var response = await _host.GatewayClient.GetAsync("/api/agents");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var agents = await response.Content.ReadFromJsonAsync<JsonElement>();
        agents.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetProviders_ReturnsOk()
    {
        var response = await _host.GatewayClient.GetAsync("/api/providers");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var providers = await response.Content.ReadFromJsonAsync<JsonElement>();
        providers.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetChannels_ReturnsOk()
    {
        var response = await _host.GatewayClient.GetAsync("/api/channels");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var channels = await response.Content.ReadFromJsonAsync<JsonElement>();
        channels.ValueKind.Should().Be(JsonValueKind.Array);
    }

    public void Dispose()
    {
        // HttpClient is owned by OllamaTestHost and disposed there.
    }
}
