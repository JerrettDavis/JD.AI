using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using NSubstitute;

namespace JD.AI.Tests.Agents;

public sealed class RuntimeModelStateTests
{
    private readonly IProviderRegistry _registry = Substitute.For<IProviderRegistry>();

    private AgentSession CreateSession()
    {
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("initial-model", "Initial", "InitialProvider");
        return new AgentSession(_registry, kernel, model);
    }

    private ProviderModelInfo SetupNewModel(string id = "new-model", string provider = "NewProvider")
    {
        var model = new ProviderModelInfo(id, id, provider);
        _registry.BuildKernel(model).Returns(Kernel.CreateBuilder().Build());
        return model;
    }

    [Fact]
    public void SwitchModel_RecordsHistoryEntry()
    {
        var session = CreateSession();
        var newModel = SetupNewModel();

        session.SwitchModel(newModel);

        session.ModelSwitchHistory.Should().HaveCount(1);
        session.ModelSwitchHistory[0].ModelId.Should().Be("new-model");
        session.ModelSwitchHistory[0].ProviderName.Should().Be("NewProvider");
        session.ModelSwitchHistory[0].SwitchMode.Should().Be("preserve");
    }

    [Fact]
    public void SwitchModel_CreatesForkPoint()
    {
        var session = CreateSession();
        session.History.AddUserMessage("hello");
        session.History.AddAssistantMessage("hi");
        var newModel = SetupNewModel();

        session.SwitchModel(newModel);

        session.ForkPoints.Should().HaveCount(1);
        var fp = session.ForkPoints[0];
        fp.Id.Should().Be(1);
        fp.ModelId.Should().Be("initial-model");
        fp.ProviderName.Should().Be("InitialProvider");
        fp.MessageCount.Should().Be(2);
    }

    [Fact]
    public void SwitchModel_FiresModelChangedEvent()
    {
        var session = CreateSession();
        var newModel = SetupNewModel();
        ProviderModelInfo? received = null;
        session.ModelChanged += (_, m) => received = m;

        session.SwitchModel(newModel);

        received.Should().NotBeNull();
        received!.Id.Should().Be("new-model");
    }

    [Fact]
    public void ModelSwitchHistory_IsOrderedByTimestamp()
    {
        var session = CreateSession();
        var model1 = SetupNewModel("model-1", "P1");
        var model2 = SetupNewModel("model-2", "P2");

        session.SwitchModel(model1);
        session.SwitchModel(model2);

        session.ModelSwitchHistory.Should().HaveCount(2);
        session.ModelSwitchHistory[0].Timestamp
            .Should().BeOnOrBefore(session.ModelSwitchHistory[1].Timestamp);
    }

    [Fact]
    public void ForkPoints_CaptureCorrectTurnIndexAndMessageCount()
    {
        var session = CreateSession();
        session.History.AddUserMessage("msg1");
        session.History.AddAssistantMessage("msg2");
        session.History.AddUserMessage("msg3");

        var newModel = SetupNewModel();
        session.SwitchModel(newModel);

        var fp = session.ForkPoints[0];
        fp.TurnIndex.Should().Be(0);
        fp.MessageCount.Should().Be(3);
    }

    [Fact]
    public void MultipleSwitches_CreateMultipleEntries()
    {
        var session = CreateSession();
        var model1 = SetupNewModel("model-1", "P1");
        var model2 = SetupNewModel("model-2", "P2");
        var model3 = SetupNewModel("model-3", "P3");

        session.SwitchModel(model1, "preserve");
        session.SwitchModel(model2, "compact");
        session.SwitchModel(model3, "fresh");

        session.ModelSwitchHistory.Should().HaveCount(3);
        session.ModelSwitchHistory[0].SwitchMode.Should().Be("preserve");
        session.ModelSwitchHistory[1].SwitchMode.Should().Be("compact");
        session.ModelSwitchHistory[2].SwitchMode.Should().Be("fresh");

        session.ForkPoints.Should().HaveCount(3);
        session.ForkPoints[0].ModelId.Should().Be("initial-model");
        session.ForkPoints[1].ModelId.Should().Be("model-1");
        session.ForkPoints[2].ModelId.Should().Be("model-2");
    }
}
