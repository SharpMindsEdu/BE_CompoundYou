using Application.Features.Riftbound.Simulation.Definitions;
using Application.Features.Riftbound.Simulation.Effects;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineLMNWaveBehaviorTests
    : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void LWaveCards246To270_ResolveToSupportedTemplates()
    {
        foreach (var card in BuildWaveCards())
        {
            var template = RiftboundEffectTemplateResolver.Resolve(card);
            Assert.True(template.Supported, $"{card.Name} resolved as unsupported.");
            Assert.NotEqual("unsupported", template.TemplateId);
        }
    }

    [Fact]
    public void LastBreath_ReadiesFriendlyUnit_AndDealsDamageEqualToItsMight()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033101);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var friendly = BuildUnit(0, 0, "Friendly", 4);
        friendly.IsExhausted = true;
        var enemy = BuildUnit(1, 1, "Enemy", 6);
        player.BaseZone.Cards.Add(friendly);
        session.Battlefields[0].Units.Add(enemy);

        var spell = BuildCardInstance(
            new RiftboundCard { Id = 246001, Name = "Last Breath", Type = "Spell", Cost = 0, Power = 0 },
            0,
            0
        );
        player.HandZone.Cards.Add(spell);

        var action = engine.GetLegalActions(session).First(x =>
            x.ActionType == RiftboundActionType.PlayCard
            && x.ActionId.Contains(spell.InstanceId.ToString(), StringComparison.Ordinal)
            && x.ActionId.Contains(friendly.InstanceId.ToString(), StringComparison.Ordinal)
            && x.ActionId.Contains(enemy.InstanceId.ToString(), StringComparison.Ordinal)
        );
        Assert.True(engine.ApplyAction(session, action.ActionId).Succeeded);

        Assert.False(friendly.IsExhausted);
        Assert.Equal(4, enemy.MarkedDamage);
    }

    [Fact]
    public void LastRites_AttachesToFriendlyUnit_AndRecyclesChosenCards()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033102);
        var player = session.Players[0];
        ResetPlayer(player);

        var unit = BuildUnit(0, 0, "Target Unit", 3);
        player.BaseZone.Cards.Add(unit);

        var recycleA = BuildCardInstance(
            new RiftboundCard { Id = 247101, Name = "Recycle A", Type = "Spell", Cost = 1, Power = 0 },
            0,
            0
        );
        var recycleB = BuildCardInstance(
            new RiftboundCard { Id = 247102, Name = "Recycle B", Type = "Spell", Cost = 1, Power = 0 },
            0,
            0
        );
        player.TrashZone.Cards.Add(recycleA);
        player.TrashZone.Cards.Add(recycleB);

        var gear = BuildCardInstance(
            new RiftboundCard { Id = 247001, Name = "Last Rites", Type = "Gear", Cost = 0, Power = 0 },
            0,
            0
        );
        player.HandZone.Cards.Add(gear);

        var action = engine.GetLegalActions(session).First(x =>
            x.ActionType == RiftboundActionType.PlayCard
            && x.ActionId.Contains(gear.InstanceId.ToString(), StringComparison.Ordinal)
            && x.ActionId.Contains(unit.InstanceId.ToString(), StringComparison.Ordinal)
            && x.ActionId.Contains(recycleA.InstanceId.ToString(), StringComparison.Ordinal)
            && x.ActionId.Contains(recycleB.InstanceId.ToString(), StringComparison.Ordinal)
        );
        Assert.True(engine.ApplyAction(session, action.ActionId).Succeeded);

        Assert.Equal(unit.InstanceId, gear.AttachedToInstanceId);
        Assert.Contains(player.MainDeckZone.Cards, x => x.InstanceId == recycleA.InstanceId);
        Assert.Contains(player.MainDeckZone.Cards, x => x.InstanceId == recycleB.InstanceId);
        Assert.DoesNotContain(player.TrashZone.Cards, x => x.InstanceId == recycleA.InstanceId);
        Assert.DoesNotContain(player.TrashZone.Cards, x => x.InstanceId == recycleB.InstanceId);
    }

    [Fact]
    public void LastStand_DoublesMight_AndMarksUnitTemporaryUntilBeginning()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033103);
        var player = session.Players[0];
        ResetPlayer(player);

        var unit = BuildUnit(0, 0, "Friendly Unit", 3);
        player.BaseZone.Cards.Add(unit);
        var spell = BuildCardInstance(
            new RiftboundCard { Id = 248001, Name = "Last Stand", Type = "Spell", Cost = 0, Power = 0 },
            0,
            0
        );
        player.HandZone.Cards.Add(spell);

        var action = engine.GetLegalActions(session).First(x =>
            x.ActionType == RiftboundActionType.PlayCard
            && x.ActionId.Contains(spell.InstanceId.ToString(), StringComparison.Ordinal)
            && x.ActionId.Contains(unit.InstanceId.ToString(), StringComparison.Ordinal)
        );
        Assert.True(engine.ApplyAction(session, action.ActionId).Succeeded);

        Assert.Equal(3, unit.TemporaryMightModifier);
        Assert.Equal("true", unit.EffectData["temporaryUntilBeginning"]);
    }

    [Fact]
    public void LeeSinAscetic_Activate_BuffsSelfAndExhausts()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033104);
        var player = session.Players[0];
        ResetPlayer(player);

        var lee = BuildCardInstance(
            new RiftboundCard { Id = 252001, Name = "Lee Sin, Ascetic", Type = "Unit", Cost = 0, Power = 0, Might = 5 },
            0,
            0
        );
        player.BaseZone.Cards.Add(lee);

        var action = engine.GetLegalActions(session).First(x =>
            x.ActionType == RiftboundActionType.ActivateRune
            && x.ActionId.Contains(lee.InstanceId.ToString(), StringComparison.Ordinal)
        );
        Assert.True(engine.ApplyAction(session, action.ActionId).Succeeded);

        Assert.True(lee.IsExhausted);
        Assert.Equal(1, lee.PermanentMightModifier);
    }

    [Fact]
    public void LeeSinBlindMonk_Activate_PaysOneEnergy_AndBuffsFriendlyUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033105);
        var player = session.Players[0];
        ResetPlayer(player);

        var blindMonk = BuildCardInstance(
            new RiftboundCard { Id = 253001, Name = "Lee Sin, Blind Monk", Type = "Legend", Cost = 0, Power = 0 },
            0,
            0
        );
        var strongest = BuildUnit(0, 0, "Strongest", 4);
        var weaker = BuildUnit(0, 0, "Weaker", 2);
        player.LegendZone.Cards.Add(blindMonk);
        player.BaseZone.Cards.Add(strongest);
        player.BaseZone.Cards.Add(weaker);
        player.RunePool.Energy = 1;

        var action = engine.GetLegalActions(session).First(x =>
            x.ActionType == RiftboundActionType.ActivateRune
            && x.ActionId.Contains(blindMonk.InstanceId.ToString(), StringComparison.Ordinal)
        );
        Assert.True(engine.ApplyAction(session, action.ActionId).Succeeded);

        Assert.Equal(0, player.RunePool.Energy);
        Assert.True(blindMonk.IsExhausted);
        Assert.Equal(1, strongest.PermanentMightModifier);
        Assert.Equal(0, weaker.PermanentMightModifier);
    }

    [Fact]
    public void LaurentDuelist_AsAttacker_GainsAssaultBonus()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033155);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var duelist = BuildCardInstance(
            new RiftboundCard { Id = 250001, Name = "Laurent Duelist", Type = "Unit", Cost = 0, Power = 0, Might = 3 },
            0,
            0
        );
        var enemy = BuildUnit(1, 1, "Enemy", 2);
        session.Battlefields[0].Units.Add(duelist);
        session.Battlefields[0].Units.Add(enemy);
        session.Battlefields[0].ControlledByPlayerIndex = 1;
        session.Battlefields[0].ContestedByPlayerIndex = 0;
        player.BaseZone.Cards.Add(BuildRuneInstance(250101, "Order Rune", "Order", 0));

        var activate = engine.GetLegalActions(session).First(x =>
            x.ActionType == RiftboundActionType.ActivateRune
        );
        Assert.True(engine.ApplyAction(session, activate.ActionId).Succeeded);

        Assert.Equal(2, duelist.TemporaryMightModifier);
    }

    [Fact]
    public void LegionQuartermaster_ReturnsChosenGearToHand_WhenPlayed()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033106);
        var player = session.Players[0];
        ResetPlayer(player);

        var gear = BuildCardInstance(
            new RiftboundCard { Id = 255101, Name = "Spare Gear", Type = "Gear", Cost = 0, Power = 0 },
            0,
            0
        );
        player.BaseZone.Cards.Add(gear);

        var quartermaster = BuildCardInstance(
            new RiftboundCard { Id = 255001, Name = "Legion Quartermaster", Type = "Unit", Cost = 0, Power = 0, Might = 4 },
            0,
            0
        );
        player.HandZone.Cards.Add(quartermaster);

        var action = engine.GetLegalActions(session).First(x =>
            x.ActionId.Contains(quartermaster.InstanceId.ToString(), StringComparison.Ordinal)
            && x.ActionId.Contains(gear.InstanceId.ToString(), StringComparison.Ordinal)
            && x.ActionId.Contains(LegionQuartermasterEffect.ReturnGearMarker, StringComparison.Ordinal)
        );
        Assert.True(engine.ApplyAction(session, action.ActionId).Succeeded);

        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == gear.InstanceId);
        Assert.DoesNotContain(player.BaseZone.Cards, x => x.InstanceId == gear.InstanceId);
    }

    [Fact]
    public void LeonaDetermined_AsAttacker_StunsEnemyUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033107);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var leona = BuildCardInstance(
            new RiftboundCard { Id = 257001, Name = "Leona, Determined", Type = "Unit", Cost = 0, Power = 0, Might = 4 },
            0,
            0
        );
        var enemy = BuildUnit(1, 1, "Enemy Unit", 5);
        session.Battlefields[0].Units.Add(leona);
        session.Battlefields[0].Units.Add(enemy);
        session.Battlefields[0].ControlledByPlayerIndex = 1;
        session.Battlefields[0].ContestedByPlayerIndex = 0;
        player.BaseZone.Cards.Add(BuildRuneInstance(257101, "Calm Rune", "Calm", 0));

        var activate = engine.GetLegalActions(session).First(x =>
            x.ActionType == RiftboundActionType.ActivateRune
        );
        Assert.True(engine.ApplyAction(session, activate.ActionId).Succeeded);

        Assert.True(enemy.IsExhausted);
        Assert.Equal("true", enemy.EffectData["stunnedThisTurn"]);
    }

    [Fact]
    public void LoyalPup_WhenDefending_OffersChoice_AndCanMoveToBattlefield()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033108);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var attacker = BuildUnit(0, 0, "Attacker", 3);
        var defender = BuildUnit(1, 1, "Defender", 3);
        var loyalPup = BuildCardInstance(
            new RiftboundCard { Id = 262001, Name = "Loyal Pup", Type = "Unit", Cost = 0, Power = 0, Might = 3 },
            1,
            1
        );
        session.Battlefields[0].Units.Add(attacker);
        session.Battlefields[0].Units.Add(defender);
        session.Battlefields[0].ControlledByPlayerIndex = 1;
        session.Battlefields[0].ContestedByPlayerIndex = 0;
        opponent.BaseZone.Cards.Add(loyalPup);
        player.BaseZone.Cards.Add(BuildRuneInstance(262101, "Body Rune", "Body", 0));

        var activate = engine.GetLegalActions(session).First(x =>
            x.ActionType == RiftboundActionType.ActivateRune
        );
        Assert.True(engine.ApplyAction(session, activate.ActionId).Succeeded);

        Assert.NotNull(session.PendingChoice);
        Assert.Equal(LoyalPupEffect.PendingChoiceKind, session.PendingChoice!.Kind);

        var chooseMove = engine.GetLegalActions(session).First(x =>
            x.ActionId.Contains("choose-loyal-pup-move", StringComparison.Ordinal)
        );
        Assert.True(engine.ApplyAction(session, chooseMove.ActionId).Succeeded);

        Assert.Contains(session.Battlefields[0].Units, x => x.InstanceId == loyalPup.InstanceId);
        Assert.DoesNotContain(opponent.BaseZone.Cards, x => x.InstanceId == loyalPup.InstanceId);
    }

    [Fact]
    public void LucianGunslinger_AsAttacker_DealsOneDamageToEnemy()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033109);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var lucian = BuildCardInstance(
            new RiftboundCard { Id = 263001, Name = "Lucian, Gunslinger", Type = "Unit", Cost = 0, Power = 0, Might = 2 },
            0,
            0
        );
        var enemy = BuildUnit(1, 1, "Enemy Unit", 4);
        session.Battlefields[0].Units.Add(lucian);
        session.Battlefields[0].Units.Add(enemy);
        session.Battlefields[0].ControlledByPlayerIndex = 1;
        session.Battlefields[0].ContestedByPlayerIndex = 0;
        player.BaseZone.Cards.Add(BuildRuneInstance(263101, "Fury Rune", "Fury", 0));

        var activate = engine.GetLegalActions(session).First(x =>
            x.ActionType == RiftboundActionType.ActivateRune
        );
        Assert.True(engine.ApplyAction(session, activate.ActionId).Succeeded);

        Assert.Equal(1, enemy.MarkedDamage);
        Assert.Equal(1, lucian.TemporaryMightModifier);
    }

    [Fact]
    public void LucianMerciless_OnConquer_ReadiesItself()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033195);
        var player = session.Players[0];
        ResetPlayer(player);

        var lucian = BuildCardInstance(
            new RiftboundCard { Id = 264001, Name = "Lucian, Merciless", Type = "Unit", Cost = 0, Power = 0, Might = 3 },
            0,
            0
        );
        lucian.IsExhausted = true;
        session.Battlefields[0].Units.Add(lucian);
        session.Battlefields[0].ControlledByPlayerIndex = 1;
        session.Battlefields[0].ContestedByPlayerIndex = 0;
        player.BaseZone.Cards.Add(BuildRuneInstance(264101, "Body Rune", "Body", 0));

        var activate = engine.GetLegalActions(session).First(x =>
            x.ActionType == RiftboundActionType.ActivateRune
        );
        Assert.True(engine.ApplyAction(session, activate.ActionId).Succeeded);

        Assert.False(lucian.IsExhausted);
    }

    [Fact]
    public void LuxCrownguard_Activate_AddsTwoEnergy()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033196);
        var player = session.Players[0];
        ResetPlayer(player);

        var lux = BuildCardInstance(
            new RiftboundCard { Id = 266001, Name = "Lux, Crownguard", Type = "Unit", Cost = 0, Power = 0, Might = 2 },
            0,
            0
        );
        player.BaseZone.Cards.Add(lux);

        var action = engine.GetLegalActions(session).First(x =>
            x.ActionType == RiftboundActionType.ActivateRune
            && x.ActionId.Contains(lux.InstanceId.ToString(), StringComparison.Ordinal)
        );
        Assert.True(engine.ApplyAction(session, action.ActionId).Succeeded);

        Assert.True(lux.IsExhausted);
        Assert.Equal(2, player.RunePool.Energy);
    }

    [Fact]
    public void LuxIlluminated_PlayingSpellCostFiveOrMore_GainsPlusThreeMight()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033110);
        var player = session.Players[0];
        ResetPlayer(player);

        var lux = BuildCardInstance(
            new RiftboundCard { Id = 267001, Name = "Lux, Illuminated", Type = "Unit", Cost = 0, Power = 0, Might = 5 },
            0,
            0
        );
        player.BaseZone.Cards.Add(lux);
        for (var i = 0; i < 5; i += 1)
        {
            player.BaseZone.Cards.Add(BuildRuneInstance(267100 + i, $"Mind Rune {i}", "Mind", 0));
        }

        var expensiveSpell = BuildCardInstance(
            new RiftboundCard { Id = 267002, Name = "Big Draw Spell", Type = "Spell", Cost = 5, Power = 0, Effect = "Draw 1." },
            0,
            0
        );
        player.HandZone.Cards.Add(expensiveSpell);

        var play = engine.GetLegalActions(session).First(x =>
            x.ActionId.Contains(expensiveSpell.InstanceId.ToString(), StringComparison.Ordinal)
        );
        Assert.True(engine.ApplyAction(session, play.ActionId).Succeeded);

        Assert.Equal(3, lux.TemporaryMightModifier);
    }

    [Fact]
    public void LuxLadyOfLuminosity_PlayingSpellCostFiveOrMore_DrawsOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033111);
        var player = session.Players[0];
        ResetPlayer(player);

        var luxLegend = BuildCardInstance(
            new RiftboundCard { Id = 268001, Name = "Lux, Lady of Luminosity", Type = "Legend", Cost = 0, Power = 0 },
            0,
            0
        );
        player.LegendZone.Cards.Add(luxLegend);
        for (var i = 0; i < 5; i += 1)
        {
            player.BaseZone.Cards.Add(BuildRuneInstance(268100 + i, $"Mind Rune {i}", "Mind", 0));
        }

        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Drawn Unit", 1));
        var expensiveSpell = BuildCardInstance(
            new RiftboundCard { Id = 268002, Name = "Big Draw Spell", Type = "Spell", Cost = 5, Power = 0, Effect = "Draw 1." },
            0,
            0
        );
        player.HandZone.Cards.Add(expensiveSpell);

        var play = engine.GetLegalActions(session).First(x =>
            x.ActionId.Contains(expensiveSpell.InstanceId.ToString(), StringComparison.Ordinal)
        );
        Assert.True(engine.ApplyAction(session, play.ActionId).Succeeded);

        Assert.Contains(player.HandZone.Cards, x => string.Equals(x.Name, "Drawn Unit", StringComparison.Ordinal));
    }

    [Fact]
    public void MachineEvangel_Deathknell_SpawnsThreeExhaustedRecruitTokens()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033112);
        var player = session.Players[0];
        ResetPlayer(player);

        var machineEvangel = BuildCardInstance(
            new RiftboundCard { Id = 269001, Name = "Machine Evangel", Type = "Unit", Cost = 0, Power = 0, Might = 4 },
            0,
            0
        );
        machineEvangel.MarkedDamage = 4;
        player.BaseZone.Cards.Add(machineEvangel);
        player.BaseZone.Cards.Add(BuildRuneInstance(269101, "Order Rune", "Order", 0));

        var activate = engine.GetLegalActions(session).First(x =>
            x.ActionType == RiftboundActionType.ActivateRune
        );
        Assert.True(engine.ApplyAction(session, activate.ActionId).Succeeded);

        var recruitTokens = player.BaseZone.Cards.Where(x =>
            string.Equals(x.Name, "Recruit Token", StringComparison.Ordinal) && x.IsToken
        ).ToList();
        Assert.Equal(3, recruitTokens.Count);
        Assert.All(recruitTokens, token => Assert.True(token.IsExhausted));
    }

    [Fact]
    public void LonelyPoro_Deathknell_WhenDiedAlone_DrawsOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033125);
        var player = session.Players[0];
        ResetPlayer(player);

        var lonelyPoro = BuildCardInstance(
            new RiftboundCard { Id = 260001, Name = "Lonely Poro", Type = "Unit", Cost = 0, Power = 0, Might = 2 },
            0,
            0
        );
        lonelyPoro.MarkedDamage = 2;
        player.BaseZone.Cards.Add(lonelyPoro);
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Drawn Card", 1));
        player.BaseZone.Cards.Add(BuildRuneInstance(260101, "Calm Rune", "Calm", 0));

        var activate = engine.GetLegalActions(session).First(x =>
            x.ActionType == RiftboundActionType.ActivateRune
        );
        Assert.True(engine.ApplyAction(session, activate.ActionId).Succeeded);

        Assert.Contains(player.HandZone.Cards, x => string.Equals(x.Name, "Drawn Card", StringComparison.Ordinal));
    }

    [Fact]
    public void MaddenedMarauder_OnPlay_MovesChosenBattlefieldUnitToBase()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033113);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var target = BuildUnit(1, 1, "Battlefield Target", 4);
        session.Battlefields[0].Units.Add(target);
        var marauder = BuildCardInstance(
            new RiftboundCard { Id = 270001, Name = "Maddened Marauder", Type = "Unit", Cost = 0, Power = 0, Might = 4 },
            0,
            0
        );
        player.HandZone.Cards.Add(marauder);

        var action = engine.GetLegalActions(session).First(x =>
            x.ActionId.Contains(marauder.InstanceId.ToString(), StringComparison.Ordinal)
            && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal)
        );
        Assert.True(engine.ApplyAction(session, action.ActionId).Succeeded);

        Assert.DoesNotContain(session.Battlefields[0].Units, x => x.InstanceId == target.InstanceId);
        Assert.Contains(opponent.BaseZone.Cards, x => x.InstanceId == target.InstanceId);
    }

    private static GameSession CreateSession(RiftboundSimulationEngine engine, int seed)
    {
        return engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                seed,
                RiftboundSimulationTestData.BuildDeck(seed + 1, "Body"),
                RiftboundSimulationTestData.BuildDeck(seed + 2, "Chaos")
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
            new RiftboundCard { Id = 246, Name = "Last Breath", Type = "Spell" },
            new RiftboundCard { Id = 247, Name = "Last Rites", Type = "Gear", GameplayKeywords = ["Equip"] },
            new RiftboundCard { Id = 248, Name = "Last Stand", Type = "Spell" },
            new RiftboundCard { Id = 249, Name = "Laurent Bladekeeper", Type = "Unit", GameplayKeywords = ["Ganking"] },
            new RiftboundCard { Id = 250, Name = "Laurent Duelist", Type = "Unit", GameplayKeywords = ["Assault"] },
            new RiftboundCard { Id = 251, Name = "Lecturing Yordle", Type = "Unit" },
            new RiftboundCard { Id = 252, Name = "Lee Sin, Ascetic", Type = "Unit", GameplayKeywords = ["Shield"] },
            new RiftboundCard { Id = 253, Name = "Lee Sin, Blind Monk", Type = "Legend" },
            new RiftboundCard { Id = 254, Name = "Lee Sin, Centered", Type = "Unit", GameplayKeywords = ["Accelerate"] },
            new RiftboundCard { Id = 255, Name = "Legion Quartermaster", Type = "Unit" },
            new RiftboundCard { Id = 256, Name = "Legion Rearguard", Type = "Unit", GameplayKeywords = ["Accelerate"] },
            new RiftboundCard { Id = 257, Name = "Leona, Determined", Type = "Unit", GameplayKeywords = ["Shield"] },
            new RiftboundCard { Id = 258, Name = "Leona, Radiant Dawn", Type = "Legend" },
            new RiftboundCard { Id = 259, Name = "Leona, Zealot", Type = "Unit" },
            new RiftboundCard { Id = 260, Name = "Lonely Poro", Type = "Unit", GameplayKeywords = ["Deathknell"] },
            new RiftboundCard { Id = 261, Name = "Long Sword", Type = "Gear", GameplayKeywords = ["Equip", "Quick-Draw", "Reaction"] },
            new RiftboundCard { Id = 262, Name = "Loyal Pup", Type = "Unit" },
            new RiftboundCard { Id = 263, Name = "Lucian, Gunslinger", Type = "Unit", GameplayKeywords = ["Assault"] },
            new RiftboundCard { Id = 264, Name = "Lucian, Merciless", Type = "Unit", GameplayKeywords = ["Weaponmaster"] },
            new RiftboundCard { Id = 265, Name = "Lucian, Purifier", Type = "Legend" },
            new RiftboundCard { Id = 266, Name = "Lux, Crownguard", Type = "Unit", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 267, Name = "Lux, Illuminated", Type = "Unit" },
            new RiftboundCard { Id = 268, Name = "Lux, Lady of Luminosity", Type = "Legend" },
            new RiftboundCard { Id = 269, Name = "Machine Evangel", Type = "Unit", GameplayKeywords = ["Deathknell"] },
            new RiftboundCard { Id = 270, Name = "Maddened Marauder", Type = "Unit", GameplayKeywords = ["Tank"] },
        ];
    }
}
