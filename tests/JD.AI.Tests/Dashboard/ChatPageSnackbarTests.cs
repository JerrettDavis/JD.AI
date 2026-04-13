using Bunit;
using JD.AI.Dashboard.Wasm.Pages;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace JD.AI.Tests.Dashboard;

public sealed class ChatPageSnackbarTests : DashboardBunitTestContext
{
    [Fact]
    public void Chat_WhenAgentListFails_ShowsErrorBoundary()
    {
        var api = CreateApiClient(_ => throw new HttpRequestException("unreachable"));

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        var cut = RenderWithMudProviders<Chat>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='gateway-error-alert']"));
        }, timeout: TimeSpan.FromSeconds(3));
    }
}
