using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace JD.AI.E2E.Tests;

/// <summary>
/// Provides shared integration-host information for the E2E test suite.
///
/// The Gateway (and Ollama) must be running before these tests can execute.
/// <see cref="EnsureAvailable"/> calls <c>Skip.If</c> to gracefully
/// skip tests when the stack is unavailable.
///
/// Usage:
/// <code>
/// [Collection("Ollama E2E")]
/// public class MyTests
/// {
///     public MyTests(OllamaTestHost t) { t.EnsureAvailable(); }
/// }
/// </code>
/// </summary>
public sealed class OllamaTestHost : IDisposable
{
    /// <summary>
    /// Base URL for the Gateway REST API.
    /// Defaults to <c>http://localhost:15790</c>; override via the
    /// <c>GATEWAY_BASE_URL</c> environment variable.
    /// </summary>
    public static string GatewayBaseUrl { get; } =
        Environment.GetEnvironmentVariable("GATEWAY_BASE_URL")
        ?? "http://localhost:15790";

    /// <summary>
    /// Base URL for the Ollama API.
    /// Defaults to <c>http://localhost:11434</c>; override via the
    /// <c>OLLAMA_BASE_URL</c> environment variable.
    /// </summary>
    public static string OllamaBaseUrl { get; } =
        Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")
        ?? "http://localhost:11434";

    private readonly HttpClient _gatewayClient;
    private readonly HttpClient _ollamaClient;
    private bool _disposed;

    /// <summary>
    /// Returns <c>true</c> when Ollama is reachable.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Pre-configured <see cref="HttpClient"/> scoped to the Gateway base URL.
    /// </summary>
    public HttpClient GatewayClient => _gatewayClient;

    public OllamaTestHost()
    {
        _gatewayClient = new HttpClient
        {
            BaseAddress = new Uri(GatewayBaseUrl),
            Timeout = TimeSpan.FromSeconds(30),
        };
        _ollamaClient = new HttpClient
        {
            BaseAddress = new Uri(OllamaBaseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        };

        IsAvailable = CheckOllamaAvailable();
    }

    /// <summary>
    /// Throws <see cref="SkipException"/> when Ollama is not available, causing xUnit
    /// to skip the test with an appropriate message.
    /// Call this at the start of any test that requires the live stack.
    /// </summary>
    public void EnsureAvailable()
    {
        // Do NOT throw from constructor — xUnit handles SkipException differently
        // depending on where it originates. Instead, throw here in the test method
        // body so xUnit correctly marks the test as SKIPPED (not FAILED).
        if (!IsAvailable)
            throw new SkipException(
                $"Ollama is not available at {OllamaBaseUrl}. " +
                $"Start Ollama and ensure the Gateway is running at {GatewayBaseUrl} " +
                $"before running these tests.");
    }

    private bool CheckOllamaAvailable()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = _ollamaClient.GetAsync("/api/tags", cts.Token).GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _gatewayClient.Dispose();
        _ollamaClient.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// xUnit collection that shares a single <see cref="OllamaTestHost"/> instance
/// across all E2E tests that declare a constructor dependency on it.
/// </summary>
[CollectionDefinition("Ollama E2E", DisableParallelization = true)]
public sealed class OllamaE2EFixture : ICollectionFixture<OllamaTestHost>
{
    // The ICollectionFixture contract requires this class to be empty;
    // the fixture is provided through the test class constructor parameter.
}
