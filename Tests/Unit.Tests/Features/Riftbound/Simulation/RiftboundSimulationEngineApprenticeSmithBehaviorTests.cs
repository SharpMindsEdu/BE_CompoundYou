using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineApprenticeSmithBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void ApprenticeSmith_OnMove_RevealsGearAndDrawsIt()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031718,
                RiftboundSimulationTestData.BuildDeck(9780, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9781, "Order")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();
        player.HandZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();

        var smith = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9780_100,
                Name = "Apprentice Smith",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 2,
                Color = ["Calm"],
                Effect = "When I move, reveal the top card of your Main Deck. If it's a gear, draw it. Otherwise, recycle it.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.BaseZone.Cards.Add(smith);

        var topGear = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9780_101,
                Name = "Top Gear",
                Type = "Gear",
                Cost = 1,
                Power = 0,
                Color = ["Calm"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(topGear);

        var moveAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.StandardMove
                && a.ActionId.Contains(smith.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, moveAction).Succeeded);
        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == topGear.InstanceId);
    }
}

