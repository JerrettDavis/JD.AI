using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Gateway;

/// <summary>
/// Step definitions for gateway orchestrator scenarios.
/// All steps in these scenarios use shared steps from <see cref="SharedGatewaySteps"/>.
/// The orchestrator is validated through the status and health endpoints.
/// </summary>
[Binding]
public sealed class GatewayOrchestratorSteps
{
    private readonly ScenarioContext _context;
    private readonly SharedGatewaySteps _shared;

    public GatewayOrchestratorSteps(ScenarioContext context, SharedGatewaySteps shared)
    {
        _context = context;
        _shared = shared;
    }

    // All orchestrator scenarios use shared steps (GET, status checks, property assertions).
    // The orchestrator is tested indirectly through the /api/gateway/status endpoint
    // which exposes channels, agents, and routes initialized by the orchestrator.
}
