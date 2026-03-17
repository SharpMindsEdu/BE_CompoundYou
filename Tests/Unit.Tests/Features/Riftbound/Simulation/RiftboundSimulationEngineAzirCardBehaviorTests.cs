using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineAzirCardBehaviorTests
{
    [Fact]
    public void AzirAscendant_Activate_SwapsWithFriendlyUnit_AndCanMoveEquipment()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031811,
                RiftboundSimulationTestData.BuildDeck(9931, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9932, "Order")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();
        session.Battlefields[0].Units.Clear();
        session.Battlefields[0].Gear.Clear();
        player.BaseZone.Cards.Add(RiftboundBehaviorTestFactory.BuildRuneInstance(9931_001, "Calm Rune", "Calm", 0));

        var azir = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 9931_100,
                Name = "Azir, Ascendant",
                Type = "Unit",
                Cost = 6,
                Power = 1,
                Might = 6,
                Color = ["Calm"],
                GameplayKeywords = ["Action"],
                Effect = "[Calm]: [Action] — Choose a unit you control. Move me to its location and it to my original location.",
            },
            0,
            0
        );
        player.BaseZone.Cards.Add(azir);

        var targetUnit = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Target Unit", 3);
        session.Battlefields[0].Units.Add(targetUnit);

        var equipment = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 9931_101,
                Name = "Target Equipment",
                Type = "Gear",
                Cost = 0,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Equip"],
                Effect = "[Equip]",
            },
            0,
            0
        );
        equipment.AttachedToInstanceId = targetUnit.InstanceId;
        session.Battlefields[0].Gear.Add(equipment);

        var activate = engine
            .GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.ActivateRune
                && x.ActionId.Contains(azir.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, activate).Succeeded);

        Assert.Contains(session.Battlefields[0].Units, x => x.InstanceId == azir.InstanceId);
        Assert.Contains(player.BaseZone.Cards, x => x.InstanceId == targetUnit.InstanceId);
        Assert.Equal(azir.InstanceId, equipment.AttachedToInstanceId);
    }

    [Fact]
    public void AzirEmperorOfTheSands_ActivatedAbility_WorksOnlyAfterEquipmentPlay()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031812,
                RiftboundSimulationTestData.BuildDeck(9941, "Order"),
                RiftboundSimulationTestData.BuildDeck(9942, "Chaos")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();
        player.LegendZone.Cards.Clear();
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Add(RiftboundBehaviorTestFactory.BuildRuneInstance(9941_001, "Order Rune", "Order", 0));

        var emperor = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 9941_100,
                Name = "Azir, Emperor of the Sands",
                Type = "Legend",
                Cost = 0,
                Power = 0,
                Color = ["Calm", "Order"],
                GameplayKeywords = ["Weaponmaster"],
                Effect = "Your Sand Soldiers have [Weaponmaster]. [1], [Tap]: Play a 2 [Might] Sand Soldier unit token to your base. Use only if you've played an Equipment this turn.",
            },
            0,
            0
        );
        player.LegendZone.Cards.Add(emperor);
        player.BaseZone.Cards.Add(RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Equip Target", 2));

        var abilityAction = engine
            .GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.ActivateRune
                && x.ActionId.Contains(emperor.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, abilityAction).Succeeded);
        Assert.DoesNotContain(
            player.BaseZone.Cards,
            x => string.Equals(x.Name, "Sand Soldier Token", StringComparison.OrdinalIgnoreCase)
        );

        var equipment = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 9941_101,
                Name = "Practice Blade",
                Type = "Gear",
                Cost = 0,
                Power = 0,
                Color = ["Order"],
                GameplayKeywords = ["Equip"],
                Effect = "[Equip]",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(equipment);
        var equipAction = engine
            .GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.PlayCard
                && x.ActionId.Contains(equipment.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, equipAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        var energyRune = player.BaseZone.Cards.First(x =>
            string.Equals(x.Type, "Rune", StringComparison.OrdinalIgnoreCase)
        );
        energyRune.IsExhausted = false;
        var secondAbilityAction = engine
            .GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.ActivateRune
                && x.ActionId.Contains(emperor.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, secondAbilityAction).Succeeded);

        var token = player.BaseZone.Cards.FirstOrDefault(x =>
            string.Equals(x.Name, "Sand Soldier Token", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(token);
        Assert.Contains(token!.Keywords, x => string.Equals(x, "Weaponmaster", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AzirSovereign_WhenAttacking_MovesFriendlyTokenUnitsToItsBattlefield()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031813,
                RiftboundSimulationTestData.BuildDeck(9951, "Order"),
                RiftboundSimulationTestData.BuildDeck(9952, "Calm")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();
        session.Battlefields[0].Units.Clear();
        session.Battlefields[1].Units.Clear();
        session.Battlefields[1].ControlledByPlayerIndex = 1;
        session.Battlefields[1].ContestedByPlayerIndex = 0;
        player.BaseZone.Cards.Add(RiftboundBehaviorTestFactory.BuildRuneInstance(9951_001, "Order Rune", "Order", 0));

        var sovereign = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 9951_100,
                Name = "Azir, Sovereign",
                Type = "Unit",
                Cost = 4,
                Power = 0,
                Might = 4,
                Color = ["Order"],
                GameplayKeywords = ["Accelerate"],
                Effect = "When I attack, you may move any number of your token units to this battlefield.",
            },
            0,
            0
        );
        session.Battlefields[1].Units.Add(sovereign);
        session.Battlefields[1].Units.Add(RiftboundBehaviorTestFactory.BuildUnit(1, 1, "Enemy Unit", 4));

        var tokenInBase = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Mech Token", 3, isToken: true);
        var tokenInOtherBattlefield = RiftboundBehaviorTestFactory.BuildUnit(
            0,
            0,
            "Sand Soldier Token",
            2,
            isToken: true
        );
        player.BaseZone.Cards.Add(tokenInBase);
        session.Battlefields[0].Units.Add(tokenInOtherBattlefield);

        var activateRune = engine
            .GetLegalActions(session)
            .First(x => x.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRune).Succeeded);

        Assert.Contains(session.Battlefields[1].Units, x => x.InstanceId == tokenInBase.InstanceId);
        Assert.Contains(
            session.Battlefields[1].Units,
            x => x.InstanceId == tokenInOtherBattlefield.InstanceId
        );
        Assert.DoesNotContain(player.BaseZone.Cards, x => x.InstanceId == tokenInBase.InstanceId);
        Assert.DoesNotContain(
            session.Battlefields[0].Units,
            x => x.InstanceId == tokenInOtherBattlefield.InstanceId
        );
    }
}


