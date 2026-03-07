using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Gateway;

/// <summary>
/// Step definitions for health endpoint scenarios.
/// All steps in these scenarios use shared steps from <see cref="SharedGatewaySteps"/>.
/// This file exists as a placeholder for any future health-specific steps.
/// </summary>
[Binding]
public sealed class HealthEndpointSteps
{
    private readonly ScenarioContext _context;
    private readonly SharedGatewaySteps _shared;

    public HealthEndpointSteps(ScenarioContext context, SharedGatewaySteps shared)
    {
        _context = context;
        _shared = shared;
    }

    // All health scenarios currently use shared steps (GET, status checks, property assertions).
    // Health-specific steps can be added here as needed.
}
