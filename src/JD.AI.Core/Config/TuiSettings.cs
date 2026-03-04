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

    /// <summary>Maximum percentage of context window the system prompt should use (0-100). Default: 20.</summary>
    public int SystemPromptBudgetPercent { get; init; } = 20;

    /// <summary>System prompt compaction mode: Off, Auto (compact when over budget), Always.</summary>
    public SystemPromptCompaction SystemPromptCompaction { get; init; } = SystemPromptCompaction.Off;

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
            var settings = JsonSerializer.Deserialize<TuiSettings>(json, JsonOptions) ?? new TuiSettings();
            return settings with
            {
                SystemPromptBudgetPercent = Math.Clamp(settings.SystemPromptBudgetPercent, 0, 100),
            };
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
