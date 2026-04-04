namespace JD.AI.Core.Agents;

/// <summary>
/// Validates and parses the numeric version formats supported by the file-backed
/// agent registry.
/// </summary>
public static class AgentVersions
{
    public static bool IsSupported(string? version) => TryParse(version, out _);

    public static string Normalize(string version, string paramName = "version")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version, paramName);

        if (!TryParse(version, out _))
        {
            throw new ArgumentException(
                "Version must use numeric dot-separated components like 1.0 or 1.0.0.",
                paramName);
        }

        return version;
    }

    public static Version ParseOrZero(string version) =>
        TryParse(version, out var parsed) ? parsed : new Version(0, 0, 0);

    private static bool TryParse(string? version, out Version parsed)
    {
        parsed = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(version))
            return false;

        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 3 || parts.Any(part => !int.TryParse(part, out _)))
            return false;

        var padded = parts.Concat(Enumerable.Repeat("0", 3 - parts.Length)).ToArray();
        parsed = new Version(
            int.Parse(padded[0]),
            int.Parse(padded[1]),
            int.Parse(padded[2]));
        return true;
    }
}
