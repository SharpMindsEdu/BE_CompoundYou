using Application.Features.Riftbound.Simulation.Engine;
using Application.Features.Riftbound.Simulation.Effects;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineBloodAndCWaveBehaviorTests
    : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void BloodMoney_KillsEnemyBattlefieldUnit_AndPlaysOneGold()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032101);
        var player = session.Players[0];
        ResetPlayer(player);
        session.Battlefields[0].ControlledByPlayerIndex = 1;

        var target = BuildUnit(1, 1, "Enemy Two", 2);
        session.Battlefields[0].Units.Add(target);
        var bloodMoney = BuildCardInstance(
            new RiftboundCard { Id = 72101, Name = "Blood Money", Type = "Spell", Cost = 0, Power = 0 },
            0,
            0
        );
        player.HandZone.Cards.Add(bloodMoney);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(bloodMoney.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(2, target.MarkedDamage);
        Assert.Equal(
            1,
            player.BaseZone.Cards.Count(x => string.Equals(x.Name, "Gold Token", StringComparison.OrdinalIgnoreCase))
        );
    }

    [Fact]
    public void BloodMoney_KillsFriendlyBattlefieldUnit_AndPlaysTwoGold()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032102);
        var player = session.Players[0];
        ResetPlayer(player);
        session.Battlefields[0].ControlledByPlayerIndex = 0;

        var target = BuildUnit(0, 0, "Friendly Two", 2);
        session.Battlefields[0].Units.Add(target);
        var bloodMoney = BuildCardInstance(
            new RiftboundCard { Id = 72102, Name = "Blood Money", Type = "Spell", Cost = 0, Power = 0 },
            0,
            0
        );
        player.HandZone.Cards.Add(bloodMoney);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(bloodMoney.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(2, target.MarkedDamage);
        Assert.Equal(
            2,
            player.BaseZone.Cards.Count(x => string.Equals(x.Name, "Gold Token", StringComparison.OrdinalIgnoreCase))
        );
    }

    [Fact]
    public void BloodRush_Repeat_AddsAssaultBonusAndClearsAtEndTurn()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032103);
        var player = session.Players[0];
        ResetPlayer(player);
        player.RunePool.Energy = 1;

        var target = BuildUnit(0, 0, "Target", 3);
        player.BaseZone.Cards.Add(target);
        var bloodRush = BuildCardInstance(
            new RiftboundCard { Id = 72103, Name = "Blood Rush", Type = "Spell", Cost = 0, Power = 0 },
            0,
            0
        );
        player.HandZone.Cards.Add(bloodRush);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(bloodRush.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-repeat", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);
        Assert.Equal("4", target.EffectData["temporaryAssaultBonus"]);
        Assert.Equal(0, player.RunePool.Energy);

        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);
        Assert.False(target.EffectData.ContainsKey("temporaryAssaultBonus"));
    }

    [Fact]
    public void BondsOfStrength_Repeat_BuffsBothTargetsTwice()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032104);
        var player = session.Players[0];
        ResetPlayer(player);
        player.RunePool.Energy = 2;

        var left = BuildUnit(0, 0, "Left", 2);
        var right = BuildUnit(0, 0, "Right", 2);
        player.BaseZone.Cards.Add(left);
        player.BaseZone.Cards.Add(right);

        var bonds = BuildCardInstance(
            new RiftboundCard { Id = 72104, Name = "Bonds of Strength", Type = "Spell", Cost = 0, Power = 0 },
            0,
            0
        );
        player.HandZone.Cards.Add(bonds);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(bonds.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(left.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(right.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-repeat", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(2, left.TemporaryMightModifier);
        Assert.Equal(2, right.TemporaryMightModifier);
        Assert.Equal(0, player.RunePool.Energy);
    }

    [Fact]
    public void BrazenBuccaneer_WithDiscardAdditionalCost_CanBePlayedForTwoLess()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032105);
        var player = session.Players[0];
        ResetPlayer(player);
        player.RunePool.Energy = 4;

        var brazen = BuildCardInstance(
            new RiftboundCard { Id = 72105, Name = "Brazen Buccaneer", Type = "Unit", Cost = 6, Power = 0, Might = 5, Color = ["Fury"] },
            0,
            0
        );
        var discard = BuildCardInstance(
            new RiftboundCard { Id = 72106, Name = "Discard Me", Type = "Spell", Cost = 0, Power = 0 },
            0,
            0
        );
        Assert.Equal("named.brazen-buccaneer", brazen.EffectTemplateId);
        player.HandZone.Cards.Add(brazen);
        player.HandZone.Cards.Add(discard);

        var brazenActions = engine.GetLegalActions(session);
        var action = brazenActions
            .FirstOrDefault(x =>
                x.ActionId.Contains(brazen.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(BrazenBuccaneerEffect.DiscardMarker, StringComparison.Ordinal)
                && x.ActionId.Contains(discard.InstanceId.ToString(), StringComparison.Ordinal)
            )
            ?.ActionId;
        Assert.True(
            action is not null,
            string.Join(Environment.NewLine, brazenActions.Select(x => x.ActionId))
        );
        Assert.True(engine.ApplyAction(session, action!).Succeeded);

        Assert.Equal(0, player.RunePool.Energy);
        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == discard.InstanceId);
        Assert.Contains(player.BaseZone.Cards, x => x.InstanceId == brazen.InstanceId);
    }

    [Fact]
    public void BreakneckMech_EntersReadyAndGrantsGankingToFriendlyMechs()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032106);
        var player = session.Players[0];
        ResetPlayer(player);
        session.Battlefields[0].ControlledByPlayerIndex = 0;
        session.Battlefields[1].ControlledByPlayerIndex = 0;

        var allyMech = BuildCardInstance(
            new RiftboundCard { Id = 72107, Name = "Ally Mech", Type = "Unit", Cost = 0, Power = 0, Might = 3, Tags = ["Mech"] },
            0,
            0
        );
        allyMech.IsExhausted = false;
        session.Battlefields[0].Units.Add(allyMech);

        var breakneck = BuildCardInstance(
            new RiftboundCard { Id = 72108, Name = "Breakneck Mech", Type = "Unit", Cost = 0, Power = 0, Might = 7, Tags = ["Mech"] },
            0,
            0
        );
        player.HandZone.Cards.Add(breakneck);

        var playAction = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(breakneck.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-bf-0", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playAction).Succeeded);
        Assert.False(session.Battlefields[0].Units.Single(x => x.InstanceId == breakneck.InstanceId).IsExhausted);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        var actions = engine.GetLegalActions(session);
        Assert.Contains(
            actions,
            x =>
                x.ActionType == RiftboundActionType.StandardMove
                && x.ActionId.Contains($"move-{allyMech.InstanceId}-to-bf-1", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void BootsOfSwiftness_AttachedUnit_GainsGankingMoveOptions()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032107);
        var player = session.Players[0];
        ResetPlayer(player);
        session.Battlefields[0].ControlledByPlayerIndex = 0;

        var unit = BuildUnit(0, 0, "Runner", 2);
        session.Battlefields[0].Units.Add(unit);
        var boots = BuildCardInstance(
            new RiftboundCard { Id = 72109, Name = "Boots of Swiftness", Type = "Gear", Cost = 0, Power = 0, Color = ["Chaos"], GameplayKeywords = ["Equip"] },
            0,
            0
        );
        player.HandZone.Cards.Add(boots);

        var playAction = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(boots.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(unit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Contains(
            engine.GetLegalActions(session),
            x =>
                x.ActionType == RiftboundActionType.StandardMove
                && x.ActionId.Contains($"move-{unit.InstanceId}-to-bf-1", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void CallToGlory_SpendingBuff_IgnoresCost_ConsumesBuff_AndBuffsTarget()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032108);
        var player = session.Players[0];
        ResetPlayer(player);

        var buffSource = BuildUnit(0, 0, "Buff Source", 2);
        buffSource.PermanentMightModifier = 1;
        var target = BuildUnit(0, 0, "Target", 2);
        player.BaseZone.Cards.Add(buffSource);
        player.BaseZone.Cards.Add(target);

        var callToGlory = BuildCardInstance(
            new RiftboundCard { Id = 72110, Name = "Call to Glory", Type = "Spell", Cost = 3, Power = 0, Color = ["Order"], GameplayKeywords = ["Reaction"] },
            0,
            0
        );
        Assert.Equal("named.call-to-glory", callToGlory.EffectTemplateId);
        player.HandZone.Cards.Add(callToGlory);

        var callActions = engine.GetLegalActions(session);
        var action = callActions
            .FirstOrDefault(x =>
                x.ActionId.Contains(callToGlory.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(CallToGloryEffect.SpendBuffMarker, StringComparison.Ordinal)
                && x.ActionId.Contains(buffSource.InstanceId.ToString(), StringComparison.Ordinal)
            )
            ?.ActionId;
        Assert.True(
            action is not null,
            string.Join(Environment.NewLine, callActions.Select(x => x.ActionId))
        );
        Assert.True(engine.ApplyAction(session, action!).Succeeded);

        Assert.Equal(0, buffSource.PermanentMightModifier);
        Assert.Equal(3, target.TemporaryMightModifier);
    }

    [Fact]
    public void BulletTime_PaysRuneAmount_AndDamagesAllEnemyUnitsAtSelectedBattlefield()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032109);
        var player = session.Players[0];
        ResetPlayer(player);
        player.RunePool.PowerByDomain["Chaos"] = 2;

        session.Battlefields[0].ControlledByPlayerIndex = 1;
        var enemyA = BuildUnit(1, 1, "Enemy A", 4);
        var enemyB = BuildUnit(1, 1, "Enemy B", 4);
        session.Battlefields[0].Units.Add(enemyA);
        session.Battlefields[0].Units.Add(enemyB);
        var spell = BuildCardInstance(
            new RiftboundCard { Id = 72111, Name = "Bullet Time", Type = "Spell", Cost = 0, Power = 0, Color = ["Body", "Chaos"] },
            0,
            0
        );
        player.HandZone.Cards.Add(spell);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(spell.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains("-bullet-time-bf-0-amount-2", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(2, enemyA.MarkedDamage);
        Assert.Equal(2, enemyB.MarkedDamage);
        Assert.Equal(0, player.RunePool.PowerByDomain.GetValueOrDefault("Chaos"));
    }

    [Fact]
    public void CannonBarrage_DamagesOnlyEnemyUnitsInCombat()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032110);
        var player = session.Players[0];
        ResetPlayer(player);

        var enemyInCombat = BuildUnit(1, 1, "Enemy Combat", 3);
        enemyInCombat.Keywords.Add("Attacker");
        var friendlyInCombat = BuildUnit(0, 0, "Friendly Combat", 3);
        friendlyInCombat.Keywords.Add("Defender");
        session.Battlefields[0].Units.Add(enemyInCombat);
        session.Battlefields[0].Units.Add(friendlyInCombat);

        var spell = BuildCardInstance(
            new RiftboundCard { Id = 72112, Name = "Cannon Barrage", Type = "Spell", Cost = 0, Power = 0, Color = ["Body"], GameplayKeywords = ["Reaction"] },
            0,
            0
        );
        player.HandZone.Cards.Add(spell);

        var action = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(spell.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(2, enemyInCombat.MarkedDamage);
        Assert.Equal(0, friendlyInCombat.MarkedDamage);
    }

    [Fact]
    public void CatalystOfAeons_ChannelsUpToTwo_AndDrawsIfNotEnoughRunes()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032111);
        var player = session.Players[0];
        ResetPlayer(player);

        player.RuneDeckZone.Cards.Add(BuildRuneInstance(72113, "Body Rune", "Body", 0));
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Drawn", 1));
        var spell = BuildCardInstance(
            new RiftboundCard { Id = 72114, Name = "Catalyst of Aeons", Type = "Spell", Cost = 0, Power = 0, Color = ["Body"] },
            0,
            0
        );
        player.HandZone.Cards.Add(spell);

        var action = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(spell.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Single(
            player.BaseZone.Cards,
            x => string.Equals(x.Type, "Rune", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Contains(player.HandZone.Cards, x => x.Name == "Drawn");
    }

    [Fact]
    public void CemeteryAttendant_OnPlay_ReturnsUnitFromTrashToHand()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032112);
        var player = session.Players[0];
        ResetPlayer(player);

        var trashedUnit = BuildUnit(0, 0, "Trashed Unit", 4);
        player.TrashZone.Cards.Add(trashedUnit);
        var attendant = BuildCardInstance(
            new RiftboundCard { Id = 72115, Name = "Cemetery Attendant", Type = "Unit", Cost = 0, Power = 0, Might = 3, Color = ["Chaos"] },
            0,
            0
        );
        player.HandZone.Cards.Add(attendant);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(attendant.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.DoesNotContain(player.TrashZone.Cards, x => x.InstanceId == trashedUnit.InstanceId);
        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == trashedUnit.InstanceId);
    }

    [Fact]
    public void Boneshiver_OnConquer_ChannelsOneRuneExhausted()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032113);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        player.RuneDeckZone.Cards.Add(BuildRuneInstance(72116, "Body Rune", "Body", 0));
        var conqueror = BuildUnit(0, 0, "Conqueror", 3);
        player.BaseZone.Cards.Add(conqueror);
        var defender = BuildUnit(1, 1, "Defender", 1);
        session.Battlefields[0].Units.Add(defender);
        session.Battlefields[0].ControlledByPlayerIndex = 1;

        var boneshiver = BuildCardInstance(
            new RiftboundCard { Id = 72117, Name = "Boneshiver", Type = "Gear", Cost = 0, Power = 0, Color = ["Body"], GameplayKeywords = ["Equip"] },
            0,
            0
        );
        Assert.Equal("named.boneshiver", boneshiver.EffectTemplateId);
        player.HandZone.Cards.Add(boneshiver);

        var boneshiverActions = engine.GetLegalActions(session);
        var attachAction = boneshiverActions
            .FirstOrDefault(x =>
                x.ActionId.Contains(boneshiver.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(conqueror.InstanceId.ToString(), StringComparison.Ordinal)
            )
            ?.ActionId;
        Assert.True(
            attachAction is not null,
            string.Join(Environment.NewLine, boneshiverActions.Select(x => x.ActionId))
        );
        Assert.True(engine.ApplyAction(session, attachAction!).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        var moveAction = engine.GetLegalActions(session)
            .First(x => x.ActionId.EndsWith($"move-{conqueror.InstanceId}-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, moveAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Contains(
            player.BaseZone.Cards,
            x => string.Equals(x.Type, "Rune", StringComparison.OrdinalIgnoreCase) && x.IsExhausted
        );
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
}
