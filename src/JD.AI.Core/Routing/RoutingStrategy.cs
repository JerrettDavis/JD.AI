namespace JD.AI.Core.Routing;

/// <summary>
/// Built-in model routing strategies.
/// </summary>
public enum RoutingStrategy
{
    LocalFirst = 0,
    CostOptimized = 1,
    CapabilityDriven = 2,
    LatencyOptimized = 3,
}