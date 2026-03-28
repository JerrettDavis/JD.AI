namespace JD.AI.Commands;

public enum UpdateAction
{
    Check,
    Status,
    Plan,
    Apply,
}

public sealed record UpdateCommand(UpdateAction Action, string? Target)
{
    public static UpdateCommand Parse(string? arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return new(UpdateAction.Check, null);

        var parts = arg.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var verb = parts[0].ToLowerInvariant();
        var target = parts.Length > 1 ? parts[1] : null;

        return verb switch
        {
            "status" => new(UpdateAction.Status, null),
            "check" => new(UpdateAction.Check, null),
            "plan" => new(UpdateAction.Plan, NormalizeTarget(target)),
            "apply" => new(UpdateAction.Apply, NormalizeTarget(target)),
            _ => new(UpdateAction.Check, null),
        };
    }

    private static string? NormalizeTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return "latest";

        return target.Trim();
    }
}
