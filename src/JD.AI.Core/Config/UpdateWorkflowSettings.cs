namespace JD.AI.Core.Config;

public sealed record UpdateWorkflowSettings
{
    public bool Enabled { get; init; } = true;
    public bool AllowPromptTrigger { get; init; } = true;
    public bool RequireApproval { get; init; }
    public UpdateComponentsSettings Components { get; init; } = new();
    public TimeSpan DrainTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan ReconnectTimeout { get; init; } = TimeSpan.FromSeconds(45);

    public static UpdateWorkflowSettings Normalize(UpdateWorkflowSettings? settings)
    {
        settings ??= new UpdateWorkflowSettings();
        return settings with
        {
            Components = UpdateComponentsSettings.Normalize(settings.Components),
            DrainTimeout = settings.DrainTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : settings.DrainTimeout,
            ReconnectTimeout = settings.ReconnectTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(45) : settings.ReconnectTimeout,
        };
    }
}

public sealed record UpdateComponentsSettings
{
    public bool Daemon { get; init; } = true;
    public bool Gateway { get; init; } = true;
    public bool Tui { get; init; } = true;

    public static UpdateComponentsSettings Normalize(UpdateComponentsSettings? settings)
    {
        settings ??= new UpdateComponentsSettings();
        return settings;
    }
}
