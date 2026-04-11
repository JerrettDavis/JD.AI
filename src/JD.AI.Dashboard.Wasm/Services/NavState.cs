using Microsoft.JSInterop;

namespace JD.AI.Dashboard.Wasm.Services;

public sealed class NavState(IJSRuntime js)
{
    private static readonly string[] Groups = ["control", "agents", "settings"];
    private readonly Dictionary<string, bool> _state = [];

    public async Task InitAsync()
    {
        foreach (var group in Groups)
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", $"jd-nav-{group}");
            _state[group] = raw is null || raw == "true";
        }
    }

    public Task<bool> IsExpandedAsync(string group) =>
        Task.FromResult(_state.GetValueOrDefault(group, true));

    public async Task ToggleAsync(string group)
    {
        var next = !_state.GetValueOrDefault(group, true);
        _state[group] = next;
        await js.InvokeVoidAsync("localStorage.setItem", $"jd-nav-{group}", next.ToString().ToLowerInvariant());
    }

    public async Task SetExpandedAsync(string group, bool expanded)
    {
        _state[group] = expanded;
        await js.InvokeVoidAsync("localStorage.setItem", $"jd-nav-{group}", expanded.ToString().ToLowerInvariant());
    }
}
