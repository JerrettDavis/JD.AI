using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Gateway;

/// <summary>
/// Step definitions for session endpoint scenarios.
/// All steps in these scenarios use shared steps from <see cref="SharedGatewaySteps"/>.
/// This file exists as a placeholder for any future session-specific steps.
/// </summary>
[Binding]
public sealed class SessionEndpointSteps
{
    private readonly ScenarioContext _context;
    private readonly SharedGatewaySteps _shared;

    public SessionEndpointSteps(ScenarioContext context, SharedGatewaySteps shared)
    {
        _context = context;
        _shared = shared;
    }

    // All session scenarios currently use shared steps (GET, POST, status checks).
    // Session-specific steps can be added here as needed.
}
