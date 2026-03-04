using System.Text.Json;
using System.Text.Json.Serialization;

namespace JD.AI.Core.Config;

/// <summary>
/// User-configurable TUI display settings, persisted to <c>~/.jdai/tui-settings.json</c>.
/// </summary>
public sealed record TuiSettings
{
    private const string FileName = "tui-settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>The spinner/progress display style during LLM turns.</summary>
    public SpinnerStyle SpinnerStyle { get; init; } = SpinnerStyle.Normal;

    /// <summary>Color theme used by the terminal renderer.</summary>
    public TuiTheme Theme { get; init; } = TuiTheme.DefaultDark;

    /// <summary>When true, interactive input uses vim-style key bindings.</summary>
    public bool VimMode { get; init; }

    /// <summary>Assistant output rendering style.</summary>
    public OutputStyle OutputStyle { get; init; } = OutputStyle.Rich;

    /// <summary>Load settings from the data directory, returning defaults if not found.</summary>
    public static TuiSettings Load()
    {
        var path = Path.Combine(DataDirectories.Root, FileName);
        if (!File.Exists(path))
            return new TuiSettings();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<TuiSettings>(json, JsonOptions) ?? new TuiSettings();
        }
#pragma warning disable CA1031 // Best-effort deserialization
        catch (Exception)
        {
            return new TuiSettings();
        }
#pragma warning restore CA1031
    }

    /// <summary>Save current settings to the data directory.</summary>
    public void Save()
    {
        var path = Path.Combine(DataDirectories.Root, FileName);
        var dir = Path.GetDirectoryName(path);
        if (dir is not null)
            Directory.CreateDirectory(dir);

        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}
