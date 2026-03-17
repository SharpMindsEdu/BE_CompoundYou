using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineAngleShotBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AngleShot_CanMoveEquipmentBetweenFriendlyUnits_AndDraws()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031712,
                RiftboundSimulationTestData.BuildDeck(9720, "Fury"),
                RiftboundSimulationTestData.BuildDeck(9721, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.RunePool.Energy = 0;

        var angleShot = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9720_100,
                Name = "Angle Shot",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Fury"],
                GameplayKeywords = ["Reaction"],
                Effect = "Choose a unit and an Equipment with the same controller. Attach that Equipment to that unit or detach that Equipment from that unit. Draw 1.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(angleShot);

        var unitA = BuildUnit(0, 0, "Unit A", 2);
        var unitB = BuildUnit(0, 0, "Unit B", 2);
        player.BaseZone.Cards.Add(unitA);
        player.BaseZone.Cards.Add(unitB);

        var equip = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9720_101,
                Name = "Training Sword",
                Type = "Gear",
                Cost = 1,
                Power = 0,
                Color = ["Fury"],
                GameplayKeywords = ["Equip"],
                Effect = "[Equip]",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        equip.AttachedToInstanceId = unitA.InstanceId;
        player.BaseZone.Cards.Add(equip);

        var drawn = BuildUnit(0, 0, "Drawn Unit", 1);
        player.MainDeckZone.Cards.Add(drawn);

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(angleShot.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(unitB.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(equip.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(unitB.InstanceId, equip.AttachedToInstanceId);
        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == drawn.InstanceId);
    }
}

