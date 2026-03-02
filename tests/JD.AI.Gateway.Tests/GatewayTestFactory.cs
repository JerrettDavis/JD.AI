using Microsoft.AspNetCore.Mvc.Testing;

namespace JD.AI.Gateway.Tests;

/// <summary>
/// Custom factory that suppresses <see cref="AggregateException"/> during host
/// shutdown so xUnit does not report spurious "Test Class Cleanup Failure" errors
/// when the <see cref="WebApplicationFactory{TEntryPoint}"/> fixture is disposed.
/// </summary>
public sealed class GatewayTestFactory : WebApplicationFactory<Program>
{
    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        catch (AggregateException)
        {
            // Host shutdown may throw when services race to dispose
        }
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        catch (AggregateException)
        {
            // Host shutdown may throw when services race to dispose
        }

        GC.SuppressFinalize(this);
    }
}
