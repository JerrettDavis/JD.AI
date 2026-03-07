namespace JD.AI.Core.Providers;

/// <summary>
/// Coarse pricing tier used for capability routing preferences.
/// </summary>
public enum ModelCostTier
{
    Unknown = 0,
    Free = 1,
    Budget = 2,
    Standard = 3,
    Premium = 4,
}
