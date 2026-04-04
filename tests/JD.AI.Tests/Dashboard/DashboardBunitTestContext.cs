using System.Net;
using System.Text;
using Bunit;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Xunit;

namespace JD.AI.Tests.Dashboard;

public abstract class DashboardBunitTestContext : BunitContext, IAsyncLifetime
{
    protected DashboardBunitTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    protected GatewayApiClient CreateApiClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var http = new HttpClient(new StubHandler(responder))
        {
            BaseAddress = new Uri("http://localhost/"),
        };

        return new GatewayApiClient(http);
    }

    protected IRenderedComponent<MudProvidersHost> RenderWithMudProviders<TComponent>(Action<ComponentParameterCollectionBuilder<TComponent>>? parameters = null)
        where TComponent : IComponent
        => Render<MudProvidersHost>(host => host.AddChildContent<TComponent>(parameters ?? (_ => { })));

    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync().ConfigureAwait(false);

    protected static HttpResponseMessage JsonResponse(string body, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    protected sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    protected sealed class MudProvidersHost : ComponentBase
    {
        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MudThemeProvider>(0);
            builder.CloseComponent();

            builder.OpenComponent<MudPopoverProvider>(1);
            builder.CloseComponent();

            builder.OpenComponent<MudDialogProvider>(2);
            builder.CloseComponent();

            builder.OpenComponent<MudSnackbarProvider>(3);
            builder.CloseComponent();

            builder.AddContent(4, ChildContent);
        }
    }
}
