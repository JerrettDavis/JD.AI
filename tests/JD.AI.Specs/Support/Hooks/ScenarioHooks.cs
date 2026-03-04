using JD.AI.Core.Agents;
using Reqnroll;

namespace JD.AI.Specs.Support.Hooks;

/// <summary>
/// Global hooks that run before/after each scenario.
/// Installs and cleans up the <see cref="SpyAgentOutput"/>.
/// </summary>
[Binding]
public sealed class ScenarioHooks
{
    private readonly ScenarioContext _context;
    private IAgentOutput? _previousOutput;

    public ScenarioHooks(ScenarioContext context)
    {
        _context = context;
    }

    [BeforeScenario]
    public void SetupSpyOutput()
    {
        _previousOutput = AgentOutput.Current;
        var spy = new SpyAgentOutput();
        AgentOutput.Current = spy;
        _context.Set(spy);
    }

    [AfterScenario]
    public void RestoreOutput()
    {
        if (_previousOutput != null)
            AgentOutput.Current = _previousOutput;
    }
}
