using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using JD.AI.Dashboard.Wasm;
using JD.AI.Dashboard.Wasm.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var gatewayUrl = builder.Configuration["GatewayUrl"];
if (string.IsNullOrEmpty(gatewayUrl))
    gatewayUrl = builder.HostEnvironment.BaseAddress;

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(gatewayUrl) });
builder.Services.AddScoped<GatewayApiClient>();
builder.Services.AddSingleton(new SignalRService(gatewayUrl));
builder.Services.AddMudServices();

await builder.Build().RunAsync();
