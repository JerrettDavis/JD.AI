using Bunit;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace JD.AI.Tests.Dashboard;

public sealed class NavStateTests : BunitContext
{
    public NavStateTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task NavState_DefaultsAllGroupsExpanded()
    {
        JSInterop.SetupVoid("localStorage.setItem", _ => true);
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);

        var svc = new NavState(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();

        Assert.True(await svc.IsExpandedAsync("control"));
        Assert.True(await svc.IsExpandedAsync("agents"));
        Assert.True(await svc.IsExpandedAsync("settings"));
    }

    [Fact]
    public async Task NavState_PersistsToggleToLocalStorage()
    {
        JSInterop.SetupVoid("localStorage.setItem", _ => true).SetVoidResult();
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);

        var svc = new NavState(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();
        await svc.ToggleAsync("control");

        Assert.False(await svc.IsExpandedAsync("control"));

        var setItemInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "localStorage.setItem")
            .ToList();
        Assert.Contains(setItemInvocations, i => (string)i.Arguments[0]! == "jd-nav-control");
    }

    [Fact]
    public async Task NavState_ReadsPersistedStateOnInit()
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult(null);
        JSInterop.Setup<string?>("localStorage.getItem",
            inv => (string)inv.Arguments[0]! == "jd-nav-agents")
            .SetResult("false");

        var svc = new NavState(Services.GetRequiredService<IJSRuntime>());
        await svc.InitAsync();

        Assert.False(await svc.IsExpandedAsync("agents"));
        Assert.True(await svc.IsExpandedAsync("settings"));
    }
}
