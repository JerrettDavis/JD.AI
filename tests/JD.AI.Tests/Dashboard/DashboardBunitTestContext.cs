using System.Net;
using System.Text;
using Bunit;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace JD.AI.Tests.Dashboard;

public abstract class DashboardBunitTestContext : TestContext
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

    protected IRenderedFragment RenderWithMudProviders<TComponent>(Action<ComponentParameterCollectionBuilder<TComponent>>? parameters = null)
        where TComponent : IComponent
    {
        var parameterDictionary = BuildParameterDictionary(parameters);

        return Render(renderTreeBuilder =>
        {
            renderTreeBuilder.OpenComponent<MudThemeProvider>(0);
            renderTreeBuilder.CloseComponent();

            renderTreeBuilder.OpenComponent<MudPopoverProvider>(1);
            renderTreeBuilder.CloseComponent();

            renderTreeBuilder.OpenComponent<MudDialogProvider>(2);
            renderTreeBuilder.CloseComponent();

            renderTreeBuilder.OpenComponent<MudSnackbarProvider>(3);
            renderTreeBuilder.CloseComponent();

            renderTreeBuilder.OpenComponent<TComponent>(4);
            foreach (var parameter in parameterDictionary)
                renderTreeBuilder.AddAttribute(5, parameter.Key, parameter.Value);
            renderTreeBuilder.CloseComponent();
        });
    }

    private static Dictionary<string, object?> BuildParameterDictionary<TComponent>(Action<ComponentParameterCollectionBuilder<TComponent>>? parameters)
        where TComponent : IComponent
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (parameters is null)
            return result;

        var builder = new ComponentParameterCollectionBuilder<TComponent>();
        parameters(builder);
        foreach (var parameter in builder.Build())
        {
            if (parameter.Name is not null)
                result[parameter.Name] = parameter.Value;
        }

        return result;
    }

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
}
