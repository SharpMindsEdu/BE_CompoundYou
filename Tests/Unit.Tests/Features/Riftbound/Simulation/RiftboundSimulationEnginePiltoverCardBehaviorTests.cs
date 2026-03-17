using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEnginePiltoverCardBehaviorTests
{
    [Fact]
    public void AspiringEngineer_OnPlay_ReturnsGearFromTrashToHand()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031801,
                RiftboundSimulationTestData.BuildDeck(9901, "Mind"),
                RiftboundSimulationTestData.BuildDeck(9902, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.BaseZone.Cards.Add(RiftboundBehaviorTestFactory.BuildRuneInstance(9901_001, "Mind Rune", "Mind", 0));
        player.BaseZone.Cards.Add(RiftboundBehaviorTestFactory.BuildRuneInstance(9901_002, "Calm Rune", "Calm", 0));
        player.BaseZone.Cards.Add(RiftboundBehaviorTestFactory.BuildRuneInstance(9901_003, "Order Rune", "Order", 0));

        var engineer = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 9901_100,
                Name = "Aspiring Engineer",
                Type = "Unit",
                Cost = 3,
                Power = 1,
                Might = 3,
                Color = ["Mind"],
                Effect = "When you play me, return a gear from your trash to your hand.",
            },
            0,
            0
        );
        var trashedGear = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 9901_101,
                Name = "Broken Gear",
                Type = "Gear",
                Cost = 2,
                Power = 0,
                Color = ["Mind"],
                Effect = "",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(engineer);
        player.TrashZone.Cards.Add(trashedGear);

        var playAction = engine
            .GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.PlayCard
                && x.ActionId.Contains(engineer.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playAction).Succeeded);

        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == trashedGear.InstanceId);
        Assert.DoesNotContain(player.TrashZone.Cards, x => x.InstanceId == trashedGear.InstanceId);
    }

    [Fact]
    public void AssemblyRig_Activate_PaysCost_RecyclesUnit_AndCreatesMechToken()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031802,
                RiftboundSimulationTestData.BuildDeck(9911, "Fury"),
                RiftboundSimulationTestData.BuildDeck(9912, "Order")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.BaseZone.Cards.Add(RiftboundBehaviorTestFactory.BuildRuneInstance(9911_001, "Fury Rune", "Fury", 0));

        var rig = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 9911_100,
                Name = "Assembly Rig",
                Type = "Gear",
                Cost = 4,
                Power = 0,
                Color = ["Fury"],
                Effect = "[1][Fury], Recycle a unit from your trash, [Tap]: Play a 3 Mech unit token to your base.",
            },
            0,
            0
        );
        player.BaseZone.Cards.Add(rig);

        var trashedUnit = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Discarded Unit", 4);
        player.TrashZone.Cards.Add(trashedUnit);

        var activateAction = engine
            .GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.ActivateRune
                && x.ActionId.Contains(rig.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateAction).Succeeded);

        Assert.True(rig.IsExhausted);
        Assert.DoesNotContain(player.TrashZone.Cards, x => x.InstanceId == trashedUnit.InstanceId);
        Assert.Contains(player.MainDeckZone.Cards, x => x.InstanceId == trashedUnit.InstanceId);
        Assert.Contains(
            player.BaseZone.Cards,
            x => string.Equals(x.Name, "Mech Token", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void AvaAchiever_WhenAttacking_PaysMind_AndPlaysHiddenUnitToSameBattlefield()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031803,
                RiftboundSimulationTestData.BuildDeck(9921, "Mind"),
                RiftboundSimulationTestData.BuildDeck(9922, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        session.Battlefields[1].Units.Clear();
        session.Battlefields[1].ControlledByPlayerIndex = 1;
        session.Battlefields[1].ContestedByPlayerIndex = 0;
        player.BaseZone.Cards.Add(RiftboundBehaviorTestFactory.BuildRuneInstance(9921_001, "Mind Rune", "Mind", 0));

        var ava = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 9921_100,
                Name = "Ava Achiever",
                Type = "Unit",
                Cost = 5,
                Power = 0,
                Might = 4,
                Color = ["Mind"],
                GameplayKeywords = ["Hidden"],
                Effect = "When I attack, you may pay [Mind] to play a card with [Hidden] from your hand, ignoring its cost. If it’s a unit, play it here.",
            },
            0,
            0
        );
        session.Battlefields[1].Units.Add(ava);
        session.Battlefields[1].Units.Add(RiftboundBehaviorTestFactory.BuildUnit(1, 1, "Enemy", 3));

        var hiddenUnit = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 9921_101,
                Name = "Hidden Ally",
                Type = "Unit",
                Cost = 6,
                Power = 2,
                Might = 5,
                Color = ["Mind"],
                GameplayKeywords = ["Hidden"],
                Effect = "",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(hiddenUnit);

        var activateRune = engine
            .GetLegalActions(session)
            .First(x => x.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRune).Succeeded);

        Assert.DoesNotContain(player.HandZone.Cards, x => x.InstanceId == hiddenUnit.InstanceId);
        Assert.Contains(session.Battlefields[1].Units, x => x.InstanceId == hiddenUnit.InstanceId);
        Assert.Contains(
            session.EffectContexts,
            x => x.Source == "Ava Achiever" && x.Timing == "WhenAttack"
        );
    }
}


