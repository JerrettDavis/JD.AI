using Microsoft.JSInterop;

namespace JD.AI.Dashboard.Wasm.Services;

public sealed class LocalStorageService(IJSRuntime js)
{
    public ValueTask<string?> GetAsync(string key) =>
        js.InvokeAsync<string?>("localStorage.getItem", key);

    public ValueTask SetAsync(string key, string value) =>
        js.InvokeVoidAsync("localStorage.setItem", key, value);
}
