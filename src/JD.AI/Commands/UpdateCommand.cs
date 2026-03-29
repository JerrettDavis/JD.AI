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

    public static bool TryParsePromptIntent(string? input, out UpdateCommand command)
    {
        command = new(UpdateAction.Check, null);
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var text = input.Trim();
        var normalized = text.ToLowerInvariant();

        // Require explicit update/upgrade language so regular prompts are not hijacked.
        if (!normalized.Contains("update", StringComparison.Ordinal) &&
            !normalized.Contains("upgrade", StringComparison.Ordinal))
        {
            return false;
        }

        var target = ExtractVersionTarget(normalized);

        if (normalized.Contains("status", StringComparison.Ordinal) ||
            normalized.Contains("state", StringComparison.Ordinal) ||
            normalized.Contains("configured", StringComparison.Ordinal) ||
            normalized.Contains("config", StringComparison.Ordinal))
        {
            command = new(UpdateAction.Status, null);
            return true;
        }

        if (normalized.Contains("plan", StringComparison.Ordinal) ||
            normalized.Contains("what will", StringComparison.Ordinal) ||
            normalized.Contains("dry run", StringComparison.Ordinal))
        {
            command = new(UpdateAction.Plan, NormalizeTarget(target));
            return true;
        }

        if (normalized.Contains("apply", StringComparison.Ordinal) ||
            normalized.Contains("run it", StringComparison.Ordinal) ||
            normalized.Contains("do it", StringComparison.Ordinal) ||
            normalized.Contains("install", StringComparison.Ordinal))
        {
            command = new(UpdateAction.Apply, NormalizeTarget(target));
            return true;
        }

        command = new(UpdateAction.Check, null);
        return true;
    }

    private static string? ExtractVersionTarget(string normalized)
    {
        var tokens = normalized.Split([' ', '\t', '\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (token.Any(static c => c == '.') && char.IsDigit(token[0]))
                return token.Trim();

            if (string.Equals(token, "latest", StringComparison.OrdinalIgnoreCase))
                return "latest";
        }

        return null;
    }

    private static string? NormalizeTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return "latest";

        return target.Trim();
    }
}
