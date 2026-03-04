using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Gateway;

/// <summary>
/// Step definitions for gateway config endpoint scenarios.
/// All steps in these scenarios use shared steps from <see cref="SharedGatewaySteps"/>.
/// This file exists as a placeholder for any future config-specific steps.
/// </summary>
[Binding]
public sealed class ConfigEndpointSteps
{
    private readonly ScenarioContext _context;
    private readonly SharedGatewaySteps _shared;

    public ConfigEndpointSteps(ScenarioContext context, SharedGatewaySteps shared)
    {
        _context = context;
        _shared = shared;
    }

    // All config scenarios currently use shared steps (GET, PUT, status checks, property assertions).
    // Config-specific steps can be added here as needed.
}
