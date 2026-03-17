using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineArcaneShiftBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void ArcaneShift_BanishesAndReplaysFriendlyUnit_AndBanishesItself()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031720,
                RiftboundSimulationTestData.BuildDeck(9800, "Mind"),
                RiftboundSimulationTestData.BuildDeck(9801, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.TrashZone.Cards.Clear();

        var arcaneShift = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9800_100,
                Name = "Arcane Shift",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Mind", "Chaos"],
                GameplayKeywords = ["Action"],
                Effect = "Banish a friendly unit, then its owner plays it, ignoring its cost. Deal 3 to an enemy unit at a battlefield. Banish this.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(arcaneShift);

        var friendly = BuildUnit(0, 0, "Friendly Unit", 3);
        player.BaseZone.Cards.Add(friendly);
        var enemy = BuildUnit(1, 1, "Enemy Unit", 4);
        session.Battlefields[1].Units.Add(enemy);

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(arcaneShift.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(friendly.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(enemy.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Contains(player.BaseZone.Cards, x => x.InstanceId == friendly.InstanceId);
        Assert.Equal(3, enemy.MarkedDamage);
        Assert.DoesNotContain(player.TrashZone.Cards, x => x.InstanceId == arcaneShift.InstanceId);
    }
}

