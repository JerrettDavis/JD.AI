using FluentAssertions;
using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Tests;

/// <summary>
/// Tests for Ollama transient error detection and retry classification.
/// </summary>
public sealed class OllamaResilienceTests
{
    [Fact]
    public void IsTransient_ModelRunnerCrash_ReturnsTrue()
    {
        var ex = new HttpRequestException(
            "Ollama API error 500: {\"error\":\"model runner has unexpectedly stopped, " +
            "this may be due to resource limitations or an internal error\"}");

        AgentPoolService.IsTransientOllamaError(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_ResourceLimitations_ReturnsTrue()
    {
        var ex = new InvalidOperationException(
            "Failed due to resource limitations on the host");

        AgentPoolService.IsTransientOllamaError(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_Http500_ReturnsTrue()
    {
        var ex = new HttpRequestException(
            "Response status code does not indicate success: 500 (Internal Server Error).",
            inner: null,
            statusCode: System.Net.HttpStatusCode.InternalServerError);

        AgentPoolService.IsTransientOllamaError(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_Http503_ReturnsTrue()
    {
        var ex = new HttpRequestException(
            "Response status code does not indicate success: 503 (Service Unavailable).",
            inner: null,
            statusCode: System.Net.HttpStatusCode.ServiceUnavailable);

        AgentPoolService.IsTransientOllamaError(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_Http502_ReturnsTrue()
    {
        var ex = new HttpRequestException(
            "Response status code does not indicate success: 502 (Bad Gateway).",
            inner: null,
            statusCode: System.Net.HttpStatusCode.BadGateway);

        AgentPoolService.IsTransientOllamaError(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_SocketException_ReturnsTrue()
    {
        var socketEx = new System.Net.Sockets.SocketException(
            (int)System.Net.Sockets.SocketError.ConnectionRefused);
        var httpEx = new HttpRequestException("Connection refused", socketEx);

        AgentPoolService.IsTransientOllamaError(httpEx).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_IOException_ReturnsTrue()
    {
        var ioEx = new IOException("The response ended prematurely.");
        var httpEx = new HttpRequestException("Error sending request", ioEx);

        AgentPoolService.IsTransientOllamaError(httpEx).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_NestedModelRunner_ReturnsTrue()
    {
        // Semantic Kernel wraps the original error
        var inner = new HttpRequestException(
            "{\"error\":\"model runner has unexpectedly stopped\"}");
        var outer = new InvalidOperationException(
            "Chat completion failed", inner);

        AgentPoolService.IsTransientOllamaError(outer).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_Http400_ReturnsFalse()
    {
        var ex = new HttpRequestException(
            "Bad request: invalid model name",
            inner: null,
            statusCode: System.Net.HttpStatusCode.BadRequest);

        AgentPoolService.IsTransientOllamaError(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_Http404_ReturnsFalse()
    {
        var ex = new HttpRequestException(
            "Model not found",
            inner: null,
            statusCode: System.Net.HttpStatusCode.NotFound);

        AgentPoolService.IsTransientOllamaError(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_ArgumentException_ReturnsFalse()
    {
#pragma warning disable MA0015
        var ex = new ArgumentException("Invalid parameter");
#pragma warning restore MA0015

        AgentPoolService.IsTransientOllamaError(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_NullReferenceException_ReturnsFalse()
    {
        var ex = new InvalidOperationException("Object reference not set");

        AgentPoolService.IsTransientOllamaError(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_Generic500WithError_ReturnsTrue()
    {
        // Generic "500" + "error" in message without specific Ollama text
        var ex = new InvalidOperationException(
            "HTTP request returned status code 500 with error response");

        AgentPoolService.IsTransientOllamaError(ex).Should().BeTrue();
    }

    [Fact]
    public void Constants_HaveSensibleDefaults()
    {
        AgentPoolService.MaxRetries.Should().Be(3);
        AgentPoolService.BaseRetryDelay.Should().Be(TimeSpan.FromSeconds(2));
    }
}
