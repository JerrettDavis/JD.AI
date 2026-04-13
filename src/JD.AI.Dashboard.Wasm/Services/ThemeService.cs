using Microsoft.JSInterop;

namespace JD.AI.Dashboard.Wasm.Services;

public enum ThemeMode { System, Light, Dark }

public sealed class ThemeService(IJSRuntime js)
{
    public ThemeMode Mode { get; private set; } = ThemeMode.System;
    public bool IsDarkMode { get; private set; } = true;
    public event EventHandler? OnChanged;

    public async Task InitAsync()
    {
        var raw = await js.InvokeAsync<string?>("localStorage.getItem", "jd-theme");
        Mode = raw switch
        {
            "light"  => ThemeMode.Light,
            "dark"   => ThemeMode.Dark,
            _        => ThemeMode.System,
        };
        IsDarkMode = Mode switch
        {
            ThemeMode.Dark  => true,
            ThemeMode.Light => false,
            _               => await js.InvokeAsync<bool>("jdMatchesDark"),
        };
    }

    public async Task SetModeAsync(ThemeMode mode)
    {
        Mode = mode;
        IsDarkMode = mode switch
        {
            ThemeMode.Dark  => true,
            ThemeMode.Light => false,
            _               => await js.InvokeAsync<bool>("jdMatchesDark"),
        };
        var value = mode switch
        {
            ThemeMode.Light => "light",
            ThemeMode.Dark  => "dark",
            _               => "system",
        };
        await js.InvokeVoidAsync("localStorage.setItem", "jd-theme", value);
        OnChanged?.Invoke(this, EventArgs.Empty);
    }
}
