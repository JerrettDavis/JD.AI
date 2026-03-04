namespace JD.AI.Specs.UI.Support;

/// <summary>
/// Provides the base URL for the dashboard under test.
/// In a full integration scenario this would wrap a WebApplicationFactory
/// to host the Blazor WASM app. For now it exposes a configurable base URL
/// that defaults to the standard Gateway development address.
/// </summary>
public sealed class DashboardFixture : IDisposable
{
    public string BaseUrl { get; } =
        Environment.GetEnvironmentVariable("DASHBOARD_BASE_URL")
        ?? "https://localhost:5001";

    public void Dispose()
    {
        // Cleanup if needed when WebApplicationFactory is wired up
    }
}
