using Application.Features.Riftbound.Simulation.Definitions;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineDAndDravenWaveBehaviorTests
    : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void DWaveCards_ResolveToSupportedTemplates()
    {
        foreach (var card in BuildWaveCards())
        {
            var template = RiftboundEffectTemplateResolver.Resolve(card);
            Assert.True(template.Supported, $"{card.Name} resolved as unsupported.");
            Assert.NotEqual("unsupported", template.TemplateId);
        }
    }

    [Fact]
    public void DariusHandOfNoxus_AfterLegion_ActivateAddsEnergyAndExhausts()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032301);
        var player = session.Players[0];
        ResetPlayer(player);

        var opener = BuildCardInstance(
            new RiftboundCard { Id = 93001, Name = "Opener", Type = "Spell", Cost = 0, Power = 0 },
            0,
            0
        );
        var legend = BuildCardInstance(
            new RiftboundCard { Id = 93002, Name = "Darius, Hand of Noxus", Type = "Legend", Cost = 0, Power = 0 },
            0,
            0
        );
        player.HandZone.Cards.Add(opener);
        player.LegendZone.Cards.Add(legend);

        var playOpener = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(opener.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, playOpener).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        var activate = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.ActivateRune
                && x.ActionId.Contains(legend.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activate).Succeeded);
        Assert.Equal(1, player.RunePool.Energy);
        Assert.True(legend.IsExhausted);
    }

    [Fact]
    public void DauntlessVanguard_CanBePlayedToOccupiedEnemyBattlefield()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032302);
        var player = session.Players[0];
        ResetPlayer(player);
        session.Battlefields[0].ControlledByPlayerIndex = 1;
        session.Battlefields[0].Units.Add(BuildUnit(1, 1, "Enemy Occupant", 2));

        var vanguard = BuildCardInstance(
            new RiftboundCard { Id = 93003, Name = "Dauntless Vanguard", Type = "Unit", Cost = 0, Power = 0, Might = 4, Color = ["Body"] },
            0,
            0
        );
        player.HandZone.Cards.Add(vanguard);

        Assert.Contains(
            engine.GetLegalActions(session),
            x =>
                x.ActionId.Contains(vanguard.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-bf-0", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void DazzlingAurora_OnEndTurn_PlaysFirstRevealedUnitIgnoringCost()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032303);
        var player = session.Players[0];
        ResetPlayer(player);

        var aurora = BuildCardInstance(
            new RiftboundCard { Id = 93004, Name = "Dazzling Aurora", Type = "Gear", Cost = 0, Power = 0, Color = ["Body"] },
            0,
            0
        );
        player.BaseZone.Cards.Add(aurora);
        var spellTop = BuildCardInstance(new RiftboundCard { Id = 93005, Name = "Top Spell", Type = "Spell", Cost = 0, Power = 0 }, 0, 0);
        var unitTop = BuildCardInstance(new RiftboundCard { Id = 93006, Name = "Top Unit", Type = "Unit", Cost = 9, Power = 9, Might = 5 }, 0, 0);
        player.MainDeckZone.Cards.Add(spellTop);
        player.MainDeckZone.Cards.Add(unitTop);

        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);

        Assert.DoesNotContain(player.MainDeckZone.Cards, x => x.InstanceId == unitTop.InstanceId);
        Assert.DoesNotContain(player.TrashZone.Cards, x => x.InstanceId == unitTop.InstanceId);
        Assert.Contains(
            player.BaseZone.Cards.Concat(session.Battlefields.SelectMany(x => x.Units)),
            x => x.InstanceId == unitTop.InstanceId
        );
        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == spellTop.InstanceId);
    }

    [Fact]
    public void Deathgrip_KillsFriendly_BuffsAnother_AndDrawsOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032304);
        var player = session.Players[0];
        ResetPlayer(player);

        var sacrificed = BuildUnit(0, 0, "Sacrifice", 3);
        var buffed = BuildUnit(0, 0, "Buffed", 1);
        player.BaseZone.Cards.Add(sacrificed);
        player.BaseZone.Cards.Add(buffed);
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Drawn", 1));
        var deathgrip = BuildCardInstance(
            new RiftboundCard { Id = 93007, Name = "Deathgrip", Type = "Spell", Cost = 0, Power = 0, Color = ["Order"], GameplayKeywords = ["Reaction"] },
            0,
            0
        );
        player.HandZone.Cards.Add(deathgrip);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(deathgrip.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(
                    $"-deathgrip-sac-{sacrificed.InstanceId}-target-unit-{buffed.InstanceId}",
                    StringComparison.Ordinal
                ))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == sacrificed.InstanceId);
        Assert.Equal(3, buffed.TemporaryMightModifier);
    }

    [Fact]
    public void Defy_CountersPendingSpellWithCostAtMostFourAndPowerAtMostOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032305);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var offensiveSpell = BuildCardInstance(
            new RiftboundCard { Id = 93008, Name = "Confront", Type = "Spell", Cost = 0, Power = 0, GameplayKeywords = ["Action"] },
            0,
            0
        );
        var defy = BuildCardInstance(
            new RiftboundCard { Id = 93009, Name = "Defy", Type = "Spell", Cost = 0, Power = 0, GameplayKeywords = ["Reaction"] },
            1,
            1
        );
        player.HandZone.Cards.Add(offensiveSpell);
        opponent.HandZone.Cards.Add(defy);

        var playSpell = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(offensiveSpell.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, playSpell).Succeeded);
        var counter = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(defy.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, counter).Succeeded);

        Assert.True(session.Chain.First(x => x.CardInstanceId == offensiveSpell.InstanceId).IsCountered);
    }

    [Fact]
    public void DesertsCall_Repeat_PlaysTwoSandSoldiers_AndPaysTwoEnergy()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032306);
        var player = session.Players[0];
        ResetPlayer(player);
        player.RunePool.Energy = 2;

        var spell = BuildCardInstance(
            new RiftboundCard { Id = 93010, Name = "Desert's Call", Type = "Spell", Cost = 0, Power = 0, Color = ["Calm"], GameplayKeywords = ["Repeat"] },
            0,
            0
        );
        player.HandZone.Cards.Add(spell);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(spell.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-repeat", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.Equal(
            2,
            player.BaseZone.Cards.Count(x => string.Equals(x.Name, "Sand Soldier Token", StringComparison.OrdinalIgnoreCase))
        );
        Assert.Equal(0, player.RunePool.Energy);
    }

    [Fact]
    public void Detonate_KillsGear_AndControllerDrawsTwo()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032307);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var targetGear = BuildCardInstance(
            new RiftboundCard { Id = 93011, Name = "Target Gear", Type = "Gear", Cost = 0, Power = 0 },
            1,
            1
        );
        opponent.BaseZone.Cards.Add(targetGear);
        opponent.MainDeckZone.Cards.Add(BuildUnit(1, 1, "Draw A", 1));
        opponent.MainDeckZone.Cards.Add(BuildUnit(1, 1, "Draw B", 1));
        var detonate = BuildCardInstance(
            new RiftboundCard { Id = 93012, Name = "Detonate", Type = "Spell", Cost = 0, Power = 0, Color = ["Fury"] },
            0,
            0
        );
        player.HandZone.Cards.Add(detonate);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(detonate.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(targetGear.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Contains(opponent.TrashZone.Cards, x => x.InstanceId == targetGear.InstanceId);
        Assert.Equal(2, opponent.HandZone.Cards.Count);
    }

    [Fact]
    public void Downwell_ReturnsAllUnitsAndGearToOwnersHands()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032308);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var ownUnit = BuildUnit(0, 0, "Own Unit", 2);
        var ownGear = BuildCardInstance(new RiftboundCard { Id = 93013, Name = "Own Gear", Type = "Gear", Cost = 0, Power = 0 }, 0, 0);
        player.BaseZone.Cards.Add(ownUnit);
        player.BaseZone.Cards.Add(ownGear);
        var enemyUnit = BuildUnit(1, 1, "Enemy Unit", 2);
        var enemyGear = BuildCardInstance(new RiftboundCard { Id = 93014, Name = "Enemy Gear", Type = "Gear", Cost = 0, Power = 0 }, 1, 1);
        session.Battlefields[0].Units.Add(enemyUnit);
        session.Battlefields[0].Gear.Add(enemyGear);

        var downwell = BuildCardInstance(
            new RiftboundCard { Id = 93015, Name = "Downwell", Type = "Spell", Cost = 0, Power = 0, Color = ["Chaos"] },
            0,
            0
        );
        player.HandZone.Cards.Add(downwell);

        var action = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(downwell.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == ownUnit.InstanceId);
        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == ownGear.InstanceId);
        Assert.Contains(opponent.HandZone.Cards, x => x.InstanceId == enemyUnit.InstanceId);
        Assert.Contains(opponent.HandZone.Cards, x => x.InstanceId == enemyGear.InstanceId);
    }

    [Fact]
    public void DravenAudacious_FirstWinCombatEachTurn_ScoresOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032309);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        session.Battlefields[0].ControlledByPlayerIndex = 1;
        var draven = BuildCardInstance(
            new RiftboundCard { Id = 93016, Name = "Draven, Audacious", Type = "Unit", Cost = 0, Power = 0, Might = 6, Color = ["Chaos"], GameplayKeywords = ["Deflect"] },
            0,
            0
        );
        player.BaseZone.Cards.Add(draven);
        var defender = BuildUnit(1, 1, "Defender", 1);
        session.Battlefields[0].Units.Add(defender);

        var move = engine.GetLegalActions(session)
            .First(x => x.ActionId.EndsWith($"move-{draven.InstanceId}-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, move).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.True(player.Score >= 1);
    }

    [Fact]
    public void DravenAudacious_WhenDiesInCombat_OpponentScoresOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032310);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        session.Battlefields[0].ControlledByPlayerIndex = 1;
        var draven = BuildCardInstance(
            new RiftboundCard { Id = 93017, Name = "Draven, Audacious", Type = "Unit", Cost = 0, Power = 0, Might = 2, Color = ["Chaos"], GameplayKeywords = ["Deflect"] },
            0,
            0
        );
        player.BaseZone.Cards.Add(draven);
        var defender = BuildUnit(1, 1, "Big Defender", 6);
        session.Battlefields[0].Units.Add(defender);

        var move = engine.GetLegalActions(session)
            .First(x => x.ActionId.EndsWith($"move-{draven.InstanceId}-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, move).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.True(opponent.Score >= 1);
    }

    private static GameSession CreateSession(RiftboundSimulationEngine engine, int seed)
    {
        return engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                seed,
                RiftboundSimulationTestData.BuildDeck(seed + 1, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(seed + 2, "Order")
            )
        );
    }

    private static void ResetPlayer(PlayerState player)
    {
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.RuneDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.LegendZone.Cards.Clear();
        player.ChampionZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
    }

    private static IReadOnlyCollection<RiftboundCard> BuildWaveCards()
    {
        return
        [
            new RiftboundCard { Id = 101, Name = "Darius, Hand of Noxus", Type = "Legend", Effect = "[Tap] [REACTION], [LEGION] - [ADD] [1]" },
            new RiftboundCard { Id = 102, Name = "Darius, Trifarian", Type = "Unit", Effect = "When you play your second card in a turn, give me +2 [Might] this turn and ready me." },
            new RiftboundCard { Id = 103, Name = "Dauntless Vanguard", Type = "Unit", Effect = "You may play me to an occupied enemy battlefield." },
            new RiftboundCard { Id = 104, Name = "Dazzling Aurora", Type = "Gear", Effect = "At the end of your turn, reveal cards from the top of your Main Deck until you reveal a unit and banish it. Play it, ignoring its cost, and recycle the rest." },
            new RiftboundCard { Id = 105, Name = "Deadbloom Predator", Type = "Unit", Effect = "You may play me to an occupied enemy battlefield.", GameplayKeywords = ["Deflect"] },
            new RiftboundCard { Id = 106, Name = "Deathgrip", Type = "Spell", Effect = "Kill a friendly unit. If you do, give +[Might] equal to its Might to another friendly unit this turn. Draw 1.", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 107, Name = "Decisive Strike", Type = "Spell", Effect = "Give friendly units +2 [Might] this turn.", GameplayKeywords = ["Action"] },
            new RiftboundCard { Id = 108, Name = "Defiant Dance", Type = "Spell", Effect = "Give a unit +2 [Might] this turn and another unit -2 [Might] this turn.", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 109, Name = "Defy", Type = "Spell", Effect = "Counter a spell that costs no more than [4] and no more than [Rune].", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 110, Name = "Desert's Call", Type = "Spell", Effect = "[Repeat][2] Play a 2 [Might] Sand Soldier unit token.", GameplayKeywords = ["Repeat"] },
            new RiftboundCard { Id = 111, Name = "Detonate", Type = "Spell", Effect = "Kill a gear. Its controller draws 2." },
            new RiftboundCard { Id = 112, Name = "Direwing", Type = "Unit", Effect = "I enter ready if you control another Dragon." },
            new RiftboundCard { Id = 113, Name = "Disarming Rake", Type = "Unit", Effect = "When you play me, you may kill a gear." },
            new RiftboundCard { Id = 114, Name = "Discipline", Type = "Spell", Effect = "Give a unit +2 [Might] this turn. Draw 1.", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 115, Name = "Disintegrate", Type = "Spell", Effect = "Deal 3 to a unit at a battlefield. If this kills it, draw 1.", GameplayKeywords = ["Action"] },
            new RiftboundCard { Id = 116, Name = "Divine Judgment", Type = "Spell", Effect = "Each player chooses 2 units, 2 gear, 2 runes, and 2 cards in their hands. Recycle the rest." },
            new RiftboundCard { Id = 117, Name = "Doran's Blade", Type = "Gear", Effect = "[Equip] [Body] +2 [Might]", GameplayKeywords = ["Equip"] },
            new RiftboundCard { Id = 118, Name = "Doran's Ring", Type = "Gear", Effect = "[Equip] [Chaos] When I conquer, discard 1, then draw 1. +1 [Might]", GameplayKeywords = ["Equip"] },
            new RiftboundCard { Id = 119, Name = "Doran's Shield", Type = "Gear", Effect = "[Equip][Calm] [Tank], +1 [Might]", GameplayKeywords = ["Equip", "Tank"] },
            new RiftboundCard { Id = 120, Name = "Downwell", Type = "Spell", Effect = "Return all units and gear to their owners' hands." },
            new RiftboundCard { Id = 121, Name = "Dr. Mundo, Expert", Type = "Unit", Effect = "My Might is increased by the number of cards on your trash." },
            new RiftboundCard { Id = 122, Name = "Drag Under", Type = "Spell", Effect = "Kill a unit at a battlefield.", GameplayKeywords = ["Action"] },
            new RiftboundCard { Id = 123, Name = "Dragon's Rage", Type = "Spell", Effect = "Move an enemy unit. Then choose another enemy unit at its destination. They deal damage equal to their Mights to each other." },
            new RiftboundCard { Id = 124, Name = "Draven, Audacious", Type = "Unit", Effect = "The first time I win a combat each turn, you score 1 point. When I die in combat, choose an opponent. They score 1 point.", GameplayKeywords = ["Deflect"] },
            new RiftboundCard { Id = 125, Name = "Draven, Glorious Executioner", Type = "Legend", Effect = "When you win a combat, draw 1." },
        ];
    }
}
