using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineResolveCleanupBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void ResolveCleanup_CombatTie_SendsAllUnitsToTrash()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                8899,
                RiftboundSimulationTestData.BuildDeck(100, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(101, "Order")
            )
        );
        var battlefield = session.Battlefields[1];
        battlefield.ContestedByPlayerIndex = 0;
        battlefield.Units.Add(BuildUnit(0, 0, "A", 2));
        battlefield.Units.Add(BuildUnit(1, 1, "B", 2));

        var triggerAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        var result = engine.ApplyAction(session, triggerAction);

        Assert.True(result.Succeeded);
        Assert.Empty(battlefield.Units);
        Assert.Null(battlefield.ControlledByPlayerIndex);
        Assert.True(session.Players[0].TrashZone.Cards.Count > 0);
        Assert.True(session.Players[1].TrashZone.Cards.Count > 0);
        Assert.False(session.Combat.IsOpen);
        Assert.False(session.Showdown.IsOpen);
    }

    [Fact]
    public void ResolveCleanup_CombatWin_CanCompleteDuelAtEightPoints()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                9900,
                RiftboundSimulationTestData.BuildDeck(110, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(111, "Order")
            )
        );

        session.Players[0].Score = 7;
        var battlefield = session.Battlefields[1];
        battlefield.ControlledByPlayerIndex = 1;
        battlefield.ContestedByPlayerIndex = 0;
        battlefield.Units.Add(BuildUnit(0, 0, "Winner", 3));
        battlefield.Units.Add(BuildUnit(1, 1, "Loser", 1));

        var triggerAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        var result = engine.ApplyAction(session, triggerAction);

        Assert.True(result.Succeeded);
        Assert.Equal(8, session.Players[0].Score);
        Assert.Equal(RiftboundTurnPhase.Completed, session.Phase);
        Assert.Equal(0, battlefield.ControlledByPlayerIndex);
    }
}

