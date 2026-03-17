using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEnginePassFocusBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void PassFocus_Twice_ResolvesChainAndCleanup()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                8813,
                RiftboundSimulationTestData.BuildDeck(2200, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(2201, "Order")
            )
        );

        var attacker = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Attacker", might: 2);
        var defender = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Defender", might: 1);
        session.Players[0].BaseZone.Cards.Add(attacker);
        session.Battlefields[1].ControlledByPlayerIndex = 1;
        session.Battlefields[1].Units.Add(defender);

        var moveAction = $"move-{attacker.InstanceId}-to-bf-1";
        var moveResult = engine.ApplyAction(session, moveAction);
        Assert.True(moveResult.Succeeded);
        Assert.Equal(RiftboundTurnState.NeutralClosed, session.State);
        Assert.NotEmpty(session.Chain);
        Assert.Equal(0, session.Battlefields[1].ContestedByPlayerIndex);

        var firstPass = engine.ApplyAction(session, "pass-focus");
        Assert.True(firstPass.Succeeded);
        Assert.Equal(RiftboundTurnState.NeutralClosed, session.State);
        Assert.NotEmpty(session.Chain);

        var secondPass = engine.ApplyAction(session, "pass-focus");
        Assert.True(secondPass.Succeeded);
        Assert.Equal(RiftboundTurnState.NeutralOpen, session.State);
        Assert.Empty(session.Chain);
        Assert.Equal(0, session.Battlefields[1].ControlledByPlayerIndex);
        Assert.Null(session.Battlefields[1].ContestedByPlayerIndex);
        Assert.DoesNotContain(
            session.Battlefields[1].Units,
            u => u.ControllerPlayerIndex == 1 && u.InstanceId == defender.InstanceId
        );
    }
}

