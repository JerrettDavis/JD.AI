using Bunit;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;

namespace JD.AI.Tests.Dashboard;

public sealed class ThemeServiceTests : BunitContext
{
    public ThemeServiceTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task ThemeService_DefaultsToSystemPreference()
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);
        JSInterop.Setup<bool>("jdMatchesDark", _ => true).SetResult(true);

        var svc = new ThemeService(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();

        Assert.Equal(ThemeMode.System, svc.Mode);
        Assert.True(svc.IsDarkMode);
    }

    [Fact]
    public async Task ThemeService_LoadsPersistedLightMode()
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult("light");

        var svc = new ThemeService(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();

        Assert.Equal(ThemeMode.Light, svc.Mode);
        Assert.False(svc.IsDarkMode);
    }

    [Fact]
    public async Task ThemeService_LoadsPersistedDarkMode()
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult("dark");

        var svc = new ThemeService(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();

        Assert.Equal(ThemeMode.Dark, svc.Mode);
        Assert.True(svc.IsDarkMode);
    }

    [Fact]
    public async Task ThemeService_SetModePersistsToLocalStorage()
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);
        JSInterop.Setup<bool>("jdMatchesDark", _ => true).SetResult(false);
        JSInterop.SetupVoid("localStorage.setItem", _ => true).SetVoidResult();

        var svc = new ThemeService(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();
        await svc.SetModeAsync(ThemeMode.Dark);

        var setItemInvocations = JSInterop.Invocations
            .Where(i => string.Equals(i.Identifier, "localStorage.setItem", StringComparison.Ordinal))
            .ToList();
        Assert.Contains(setItemInvocations, i =>
            string.Equals((string)i.Arguments[0]!, "jd-theme", StringComparison.Ordinal) &&
            string.Equals((string)i.Arguments[1]!, "dark", StringComparison.Ordinal));
        Assert.True(svc.IsDarkMode);
    }

    [Fact]
    public async Task ThemeService_RaisesChangedEventOnModeSwitch()
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult("light");
        JSInterop.SetupVoid("localStorage.setItem", _ => true).SetVoidResult();

        var svc = new ThemeService(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();

        var raised = false;
        svc.OnChanged += (_, _) => raised = true;
        await svc.SetModeAsync(ThemeMode.Dark);

        Assert.True(raised);
    }
}
