using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineArenaBarBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void ArenaBar_ActivatedAbility_BuffsAnExhaustedFriendlyUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031721,
                RiftboundSimulationTestData.BuildDeck(9810, "Body"),
                RiftboundSimulationTestData.BuildDeck(9811, "Order")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();

        var arenaBar = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9810_100,
                Name = "Arena Bar",
                Type = "Gear",
                Cost = 3,
                Power = 0,
                Color = ["Body"],
                Effect = "[Tap]: Buff an exhausted friendly unit.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.BaseZone.Cards.Add(arenaBar);

        var exhaustedUnit = BuildUnit(0, 0, "Exhausted Ally", 2);
        exhaustedUnit.IsExhausted = true;
        player.BaseZone.Cards.Add(exhaustedUnit);

        var activateAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.ActivateRune
                && a.ActionId.Contains(arenaBar.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateAction).Succeeded);

        Assert.True(arenaBar.IsExhausted);
        Assert.Equal(1, exhaustedUnit.PermanentMightModifier);
    }
}

