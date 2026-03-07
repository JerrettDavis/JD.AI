namespace JD.AI.Core.Tools;

/// <summary>
/// Validates <see cref="ToolLoadout"/> definitions against a registry.
/// </summary>
public static class LoadoutValidator
{
    /// <summary>
    /// Validates the supplied <paramref name="loadout"/> and returns a list of
    /// human-readable error strings. An empty list means the loadout is valid.
    /// </summary>
    /// <remarks>
    /// Checks performed:
    /// <list type="bullet">
    ///   <item>Name is not null/empty.</item>
    ///   <item>Parent loadout exists in <paramref name="registry"/> (when specified).</item>
    ///   <item>No plugin appears in both <c>DefaultPlugins</c> and <c>DisabledPlugins</c>.</item>
    ///   <item>No circular inheritance in the parent chain.</item>
    /// </list>
    /// </remarks>
    public static IReadOnlyList<string> Validate(ToolLoadout loadout, IToolLoadoutRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(registry);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(loadout.Name))
            errors.Add("Name must not be null or empty.");

        if (loadout.ParentLoadoutName is not null &&
            registry.GetLoadout(loadout.ParentLoadoutName) is null)
            errors.Add($"Parent loadout '{loadout.ParentLoadoutName}' was not found in the registry.");

        foreach (var plugin in loadout.DefaultPlugins)
        {
            if (loadout.DisabledPlugins.Contains(plugin))
                errors.Add($"Plugin '{plugin}' appears in both IncludePlugins and ExcludePlugins.");
        }

        if (HasCircularInheritance(loadout, registry))
            errors.Add("Circular inheritance detected in the parent chain.");

        return errors;
    }

    /// <summary>Returns <see langword="true"/> when the loadout passes all validation checks.</summary>
    public static bool IsValid(ToolLoadout loadout, IToolLoadoutRegistry registry) =>
        Validate(loadout, registry).Count == 0;

    private static bool HasCircularInheritance(ToolLoadout loadout, IToolLoadoutRegistry registry)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { loadout.Name };
        var current = loadout.ParentLoadoutName;

        while (current is not null)
        {
            if (!visited.Add(current))
                return true; // cycle found

            var parent = registry.GetLoadout(current);
            if (parent is null)
                break;

            current = parent.ParentLoadoutName;
        }

        return false;
    }
}
