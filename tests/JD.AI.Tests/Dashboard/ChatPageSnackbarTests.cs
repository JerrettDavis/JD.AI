using Bunit;
using JD.AI.Dashboard.Wasm.Pages;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace JD.AI.Tests.Dashboard;

public sealed class ChatPageSnackbarTests : DashboardBunitTestContext
{
    [Fact]
    public void Chat_WhenAgentListFails_ShowsNoAgentsWarning()
    {
        var api = CreateApiClient(_ => throw new HttpRequestException("unreachable"));

        Services.AddSingleton(api);
        Services.AddSingleton(new SignalRService("http://localhost"));

        JSInterop.SetupVoid("jdChat.scrollToBottom", _ => true).SetVoidResult();
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);

        var cut = RenderWithMudProviders<Chat>();

        cut.WaitForAssertion(() =>
        {
            var warning = cut.Find("[data-testid='no-agents-warning']");
            Assert.NotNull(warning);
        }, timeout: TimeSpan.FromSeconds(3));
    }
}
