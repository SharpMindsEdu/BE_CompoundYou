using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineAkshanMischievousBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AkshanMischievous_WithAdditionalCost_StealsEnemyEquipmentAndAttachesIt()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031705,
                RiftboundSimulationTestData.BuildDeck(9650, "Body"),
                RiftboundSimulationTestData.BuildDeck(9651, "Order")
            )
        );

        var player = session.Players[0];
        var opponent = session.Players[1];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
        opponent.BaseZone.Cards.Clear();
        opponent.TrashZone.Cards.Clear();

        var bodyRuneA = BuildRuneInstance(9650_100, "Body Rune", "Body", ownerPlayer: 0);
        var bodyRuneB = BuildRuneInstance(9650_101, "Body Rune", "Body", ownerPlayer: 0);
        player.BaseZone.Cards.Add(bodyRuneA);
        player.BaseZone.Cards.Add(bodyRuneB);

        var akshan = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9650_102,
                Name = "Akshan, Mischievous",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 0,
                Power = 0,
                Might = 4,
                Color = ["Body"],
                GameplayKeywords = ["Weaponmaster"],
                Effect = "You may pay [Body][Body] as an additional cost to play me.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(akshan);

        var enemyEquipment = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9650_103,
                Name = "Enemy Equipment",
                Type = "Gear",
                Cost = 1,
                Power = 0,
                Color = ["Order"],
                GameplayKeywords = ["Equip"],
                Effect = "[Equip]",
            },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        opponent.BaseZone.Cards.Add(enemyEquipment);

        var runeDeckBefore = player.RuneDeckZone.Cards.Count;
        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(akshan.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains("-akshan-additional-cost-", StringComparison.Ordinal)
                && a.ActionId.Contains(enemyEquipment.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(runeDeckBefore + 2, player.RuneDeckZone.Cards.Count);
        Assert.DoesNotContain(player.BaseZone.Cards, x => x.InstanceId == bodyRuneA.InstanceId);
        Assert.DoesNotContain(player.BaseZone.Cards, x => x.InstanceId == bodyRuneB.InstanceId);
        Assert.DoesNotContain(opponent.BaseZone.Cards, x => x.InstanceId == enemyEquipment.InstanceId);
        Assert.Contains(player.BaseZone.Cards, x => x.InstanceId == enemyEquipment.InstanceId);
        Assert.Equal(0, enemyEquipment.ControllerPlayerIndex);
        Assert.Equal(akshan.InstanceId, enemyEquipment.AttachedToInstanceId);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Akshan, Mischievous"
                && c.Timing == "WhenPlay"
                && c.Metadata.TryGetValue("paidAdditionalCost", out var paid)
                && paid == "true"
        );
    }
}

