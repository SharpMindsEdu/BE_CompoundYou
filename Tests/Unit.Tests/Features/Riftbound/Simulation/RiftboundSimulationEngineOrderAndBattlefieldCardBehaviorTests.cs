using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineOrderAndBattlefieldCardBehaviorTests
{
    [Fact]
    public void BackToBack_BuffsExactlyTwoSelectedFriendlyUnits()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031821,
                RiftboundSimulationTestData.BuildDeck(9961, "Order"),
                RiftboundSimulationTestData.BuildDeck(9962, "Chaos")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();

        var unitA = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Unit A", 2);
        var unitB = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Unit B", 3);
        var unitC = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Unit C", 4);
        player.BaseZone.Cards.Add(unitA);
        player.BaseZone.Cards.Add(unitB);
        player.BaseZone.Cards.Add(unitC);

        var spell = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 9961_100,
                Name = "Back to Back",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Order"],
                GameplayKeywords = ["Reaction"],
                Effect = "Give two friendly units each +2 [Might] this turn.",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(spell);

        var castAction = engine
            .GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.PlayCard
                && x.ActionId.Contains(spell.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(unitA.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(unitB.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(2, unitA.TemporaryMightModifier);
        Assert.Equal(2, unitB.TemporaryMightModifier);
        Assert.Equal(0, unitC.TemporaryMightModifier);
    }

    [Fact]
    public void BFSword_AttachedMightBonus_IsAppliedDuringCombat()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031822,
                RiftboundSimulationTestData.BuildDeck(9971, "Order"),
                RiftboundSimulationTestData.BuildDeck(9972, "Chaos")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();
        session.Battlefields[1].Units.Clear();
        session.Battlefields[1].Gear.Clear();
        session.Battlefields[1].ControlledByPlayerIndex = 1;
        session.Battlefields[1].ContestedByPlayerIndex = 0;
        player.BaseZone.Cards.Add(RiftboundBehaviorTestFactory.BuildRuneInstance(9971_001, "Order Rune", "Order", 0));

        var ally = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Ally", 2);
        var enemy = RiftboundBehaviorTestFactory.BuildUnit(1, 1, "Enemy", 4);
        session.Battlefields[1].Units.Add(ally);
        session.Battlefields[1].Units.Add(enemy);

        var sword = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 9971_100,
                Name = "B.F. Sword",
                Type = "Gear",
                Cost = 4,
                Power = 0,
                Color = ["Order"],
                GameplayKeywords = ["Equip"],
                Effect = "[EQUIP] [ORDER]\n\n+3 :rb_might:",
            },
            0,
            0
        );
        Assert.Equal("3", sword.EffectData["attachedMightBonus"]);
        sword.AttachedToInstanceId = ally.InstanceId;
        session.Battlefields[1].Gear.Add(sword);

        var triggerCombat = engine
            .GetLegalActions(session)
            .First(x => x.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        Assert.True(engine.ApplyAction(session, triggerCombat).Succeeded);

        Assert.Contains(session.Battlefields[1].Units, x => x.InstanceId == ally.InstanceId);
        Assert.DoesNotContain(session.Battlefields[1].Units, x => x.InstanceId == enemy.InstanceId);
    }

    [Fact]
    public void BackAlleyBar_WhenUnitMovesFromHere_GivesPlusOneMightThisTurn()
    {
        var engine = new RiftboundSimulationEngine();
        var challengerDeck = RiftboundSimulationTestData.BuildDeck(
            9981,
            "Order",
            deck =>
            {
                foreach (var battlefield in deck.Battlefields)
                {
                    battlefield.Card!.Name = "Back-Alley Bar";
                }
            }
        );
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031823,
                challengerDeck,
                RiftboundSimulationTestData.BuildDeck(9982, "Mind")
            )
        );
        session.Battlefields[0].Units.Clear();
        session.Battlefields[1].Units.Clear();

        var movingUnit = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Mover", 2);
        session.Battlefields[0].Units.Add(movingUnit);

        var moveAction = engine
            .GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.StandardMove
                && x.ActionId.Contains(movingUnit.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, moveAction).Succeeded);

        Assert.Equal(1, movingUnit.TemporaryMightModifier);
        Assert.Contains(
            session.EffectContexts,
            x => x.Source == "Back-Alley Bar" && x.Timing == "WhenMoveFrom"
        );
    }

    [Fact]
    public void BaitedHook_Activate_KillsFriendlyUnit_AndPlaysEligibleLookedUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031824,
                RiftboundSimulationTestData.BuildDeck(9991, "Order"),
                RiftboundSimulationTestData.BuildDeck(9992, "Chaos")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.BaseZone.Cards.Add(RiftboundBehaviorTestFactory.BuildRuneInstance(9991_001, "Order Rune", "Order", 0));

        var baitedHook = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 9991_100,
                Name = "Baited Hook",
                Type = "Gear",
                Cost = 3,
                Power = 0,
                Color = ["Order"],
                Effect = "[1][Order], [Tap]: Kill a friendly unit. Look at the top 5 cards of your Main Deck...",
            },
            0,
            0
        );
        player.BaseZone.Cards.Add(baitedHook);

        var friendlyToKill = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Friendly Target", 2);
        player.BaseZone.Cards.Add(friendlyToKill);

        var eligible = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 9991_101,
                Name = "Eligible Unit",
                Type = "Unit",
                Cost = 7,
                Power = 2,
                Might = 3,
                Color = ["Order"],
            },
            0,
            0
        );
        var tooBig = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 9991_102,
                Name = "Too Big Unit",
                Type = "Unit",
                Cost = 8,
                Power = 2,
                Might = 6,
                Color = ["Order"],
            },
            0,
            0
        );
        var filler1 = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Filler 1", 1);
        var filler2 = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Filler 2", 1);
        var filler3 = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Filler 3", 1);
        player.MainDeckZone.Cards.AddRange([eligible, tooBig, filler1, filler2, filler3]);

        var activate = engine
            .GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.ActivateRune
                && x.ActionId.Contains(baitedHook.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, activate).Succeeded);

        Assert.DoesNotContain(player.BaseZone.Cards, x => x.InstanceId == friendlyToKill.InstanceId);
        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == friendlyToKill.InstanceId);
        Assert.DoesNotContain(player.MainDeckZone.Cards, x => x.InstanceId == eligible.InstanceId);
        Assert.True(
            player.BaseZone.Cards.Any(x => x.InstanceId == eligible.InstanceId)
                || session.Battlefields.Any(b => b.Units.Any(x => x.InstanceId == eligible.InstanceId))
        );
    }
}

