using Bunit;
using FluentAssertions;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace JD.AI.Tests.Dashboard;

public sealed class LocalStorageServiceTests : DashboardBunitTestContext
{
    [Fact]
    public async Task GetAsync_WhenKeyAbsent_ReturnsNull()
    {
        JSInterop.SetupVoid("localStorage.setItem", _ => true);
        JSInterop.Setup<string?>("localStorage.getItem", "openclaw.control.gateway.url").SetResult(null);

        var svc = new LocalStorageService(Services.GetRequiredService<IJSRuntime>());
        var result = await svc.GetAsync("openclaw.control.gateway.url");

        result.Should().BeNull();
    }
}
