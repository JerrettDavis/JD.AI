using JD.AI.Dashboard.Wasm;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var gatewayUrl = builder.Configuration["GatewayUrl"];
if (string.IsNullOrEmpty(gatewayUrl))
    gatewayUrl = builder.HostEnvironment.BaseAddress;

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(gatewayUrl) });
builder.Services.AddScoped<GatewayApiClient>();
builder.Services.AddSingleton(new SignalRService(gatewayUrl));
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
    config.SnackbarConfiguration.HideTransitionDuration = 300;
    config.SnackbarConfiguration.ShowTransitionDuration = 200;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});
builder.Services.AddScoped<NavState>();
builder.Services.AddScoped<ThemeService>();

await builder.Build().RunAsync();
