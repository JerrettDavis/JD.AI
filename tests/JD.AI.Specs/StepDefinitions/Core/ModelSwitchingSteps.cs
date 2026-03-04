using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class ModelSwitchingSteps
{
    private readonly ScenarioContext _context;

    public ModelSwitchingSteps(ScenarioContext context) => _context = context;

    [Given(@"the session has a ModelChanged event handler attached")]
    public void GivenTheSessionHasAModelChangedEventHandlerAttached()
    {
        var session = _context.Get<AgentSession>();
        ProviderModelInfo? firedModel = null;
        session.ModelChanged += (_, model) => firedModel = model;
        _context.Set<Func<ProviderModelInfo?>>(() => firedModel, "modelChangedCapture");
    }

    [When(@"the user switches to model ""([^""]*)"" on provider ""([^""]*)""$")]
    public void WhenTheUserSwitchesToModel(string modelId, string providerName)
    {
        var session = _context.Get<AgentSession>();
        var newModel = new ProviderModelInfo(modelId, modelId, providerName);
        session.SwitchModel(newModel);
    }

    [When(@"the user switches to model ""([^""]*)"" on provider ""([^""]*)"" with mode ""([^""]*)""")]
    public void WhenTheUserSwitchesToModelWithMode(string modelId, string providerName, string mode)
    {
        var session = _context.Get<AgentSession>();
        var newModel = new ProviderModelInfo(modelId, modelId, providerName);
        session.SwitchModel(newModel, mode);
    }

    [Then(@"the model switch history should contain (\d+) entr(?:y|ies)")]
    public void ThenTheModelSwitchHistoryShouldContainEntries(int count)
    {
        var session = _context.Get<AgentSession>();
        session.ModelSwitchHistory.Should().HaveCount(count);
    }

    [Then(@"the latest model switch should be to ""(.*)""")]
    public void ThenTheLatestModelSwitchShouldBeTo(string modelId)
    {
        var session = _context.Get<AgentSession>();
        session.ModelSwitchHistory.Should().NotBeEmpty();
        session.ModelSwitchHistory[^1].ModelId.Should().Be(modelId);
    }

    [Then(@"the latest model switch should have mode ""(.*)""")]
    public void ThenTheLatestModelSwitchShouldHaveMode(string mode)
    {
        var session = _context.Get<AgentSession>();
        session.ModelSwitchHistory.Should().NotBeEmpty();
        session.ModelSwitchHistory[^1].SwitchMode.Should().Be(mode);
    }

    [Then(@"the fork points should contain (\d+) entr(?:y|ies)")]
    public void ThenTheForkPointsShouldContainEntries(int count)
    {
        var session = _context.Get<AgentSession>();
        session.ForkPoints.Should().HaveCount(count);
    }

    [Then(@"the latest fork point should reference model ""(.*)""")]
    public void ThenTheLatestForkPointShouldReferenceModel(string modelId)
    {
        var session = _context.Get<AgentSession>();
        session.ForkPoints.Should().NotBeEmpty();
        session.ForkPoints[^1].ModelId.Should().Be(modelId);
    }

    [Then(@"the ModelChanged event should have fired with model ""(.*)""")]
    public void ThenTheModelChangedEventShouldHaveFiredWithModel(string modelId)
    {
        var capture = _context.Get<Func<ProviderModelInfo?>>("modelChangedCapture");
        var firedModel = capture();
        firedModel.Should().NotBeNull();
        firedModel!.Id.Should().Be(modelId);
    }
}
