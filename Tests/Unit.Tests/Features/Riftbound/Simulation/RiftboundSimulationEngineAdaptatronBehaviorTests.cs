using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineAdaptatronBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void Adaptatron_OnConquer_KillsGearAndBuffsSelf()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031701,
                RiftboundSimulationTestData.BuildDeck(9610, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9611, "Order")
            )
        );

        var player = session.Players[0];
        var opponent = session.Players[1];
        player.BaseZone.Cards.Clear();
        player.HandZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        opponent.BaseZone.Cards.Clear();
        opponent.TrashZone.Cards.Clear();
        session.Battlefields[1].Units.Clear();
        session.Battlefields[1].ControlledByPlayerIndex = 1;
        session.Battlefields[1].ContestedByPlayerIndex = 0;

        var adaptatron = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9610_100,
                Name = "Adaptatron",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 3,
                Color = ["Calm"],
                Effect = "When I conquer, you may kill a gear. If you do, buff me.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        session.Battlefields[1].Units.Add(adaptatron);

        var enemyGear = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9610_101,
                Name = "Enemy Gear",
                Type = "Gear",
                Cost = 1,
                Power = 0,
                Color = ["Order"],
            },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        opponent.BaseZone.Cards.Add(enemyGear);
        player.BaseZone.Cards.Add(BuildRuneInstance(9610_102, "Calm Rune", "Calm", ownerPlayer: 0));

        var activateAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateAction).Succeeded);

        Assert.Equal(1, adaptatron.PermanentMightModifier);
        Assert.DoesNotContain(opponent.BaseZone.Cards, x => x.InstanceId == enemyGear.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, x => x.InstanceId == enemyGear.InstanceId);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Adaptatron"
                && c.Timing == "WhenConquer"
                && c.Metadata.TryGetValue("killedGear", out var killed)
                && killed == "Enemy Gear"
        );
    }
}

