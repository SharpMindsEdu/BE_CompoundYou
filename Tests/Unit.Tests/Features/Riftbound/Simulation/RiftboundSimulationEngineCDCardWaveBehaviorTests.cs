using Application.Features.Riftbound.Simulation.Definitions;
using Application.Features.Riftbound.Simulation.Engine;
using Application.Features.Riftbound.Simulation.Effects;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineCDCardWaveBehaviorTests
    : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void CAndDWaveCards_ResolveToSupportedTemplates()
    {
        foreach (var card in BuildWaveCards())
        {
            var template = RiftboundEffectTemplateResolver.Resolve(card);
            Assert.True(template.Supported, $"{card.Name} resolved as unsupported.");
            Assert.NotEqual("unsupported", template.TemplateId);
        }
    }

    [Fact]
    public void Challenge_DealsMutualDamageBetweenChosenUnits()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032201);
        var player = session.Players[0];
        ResetPlayer(player);

        var friendly = BuildUnit(0, 0, "Friendly", 3);
        var enemy = BuildUnit(1, 1, "Enemy", 4);
        player.BaseZone.Cards.Add(friendly);
        session.Battlefields[0].Units.Add(enemy);

        var challenge = BuildCardInstance(
            new RiftboundCard { Id = 81001, Name = "Challenge", Type = "Spell", Cost = 0, Power = 0, Color = ["Body"] },
            0,
            0
        );
        player.HandZone.Cards.Add(challenge);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(challenge.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(friendly.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(enemy.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(4, friendly.MarkedDamage);
        Assert.Equal(3, enemy.MarkedDamage);
    }

    [Fact]
    public void Charm_MovesEnemyUnitToSelectedBattlefield()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032202);
        var player = session.Players[0];
        ResetPlayer(player);

        var enemy = BuildUnit(1, 1, "Enemy", 3);
        session.Battlefields[0].Units.Add(enemy);

        var charm = BuildCardInstance(
            new RiftboundCard { Id = 81002, Name = "Charm", Type = "Spell", Cost = 0, Power = 0, Color = ["Calm"] },
            0,
            0
        );
        player.HandZone.Cards.Add(charm);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(charm.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(enemy.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-bf-1", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.DoesNotContain(session.Battlefields[0].Units, x => x.InstanceId == enemy.InstanceId);
        Assert.Contains(session.Battlefields[1].Units, x => x.InstanceId == enemy.InstanceId);
    }

    [Fact]
    public void ChemtechEnforcer_OnPlay_DiscardsOneCardFromHand()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032203);
        var player = session.Players[0];
        ResetPlayer(player);

        var enforcer = BuildCardInstance(
            new RiftboundCard { Id = 81003, Name = "Chemtech Enforcer", Type = "Unit", Cost = 0, Power = 0, Might = 2, Color = ["Fury"] },
            0,
            0
        );
        var fodder = BuildCardInstance(new RiftboundCard { Id = 81004, Name = "Fodder", Type = "Spell", Cost = 0, Power = 0 }, 0, 0);
        player.HandZone.Cards.Add(enforcer);
        player.HandZone.Cards.Add(fodder);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(enforcer.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == fodder.InstanceId);
    }

    [Fact]
    public void CithriaOfCloudfield_BuffsItself_WhenAnotherFriendlyUnitIsPlayed()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032204);
        var player = session.Players[0];
        ResetPlayer(player);

        var cithria = BuildCardInstance(
            new RiftboundCard { Id = 81005, Name = "Cithria of Cloudfield", Type = "Unit", Cost = 0, Power = 0, Might = 1, Color = ["Body"] },
            0,
            0
        );
        var ally = BuildCardInstance(new RiftboundCard { Id = 81006, Name = "Ally", Type = "Unit", Cost = 0, Power = 0, Might = 2 }, 0, 0);
        player.BaseZone.Cards.Add(cithria);
        player.HandZone.Cards.Add(ally);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(ally.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(1, cithria.PermanentMightModifier);
    }

    [Fact]
    public void Cleave_GrantsTemporaryAssaultBonusToChosenUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032242);
        var player = session.Players[0];
        ResetPlayer(player);

        var target = BuildUnit(0, 0, "Target", 3);
        player.BaseZone.Cards.Add(target);
        var cleave = BuildCardInstance(
            new RiftboundCard { Id = 810064, Name = "Cleave", Type = "Spell", Cost = 0, Power = 0, Color = ["Fury"], GameplayKeywords = ["Action", "Assault"] },
            0,
            0
        );
        player.HandZone.Cards.Add(cleave);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(cleave.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal("3", target.EffectData["temporaryAssaultBonus"]);
    }

    [Fact]
    public void ClothArmor_AttachesToChosenFriendlyUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032243);
        var player = session.Players[0];
        ResetPlayer(player);

        var unit = BuildUnit(0, 0, "Carrier", 2);
        player.BaseZone.Cards.Add(unit);
        var armor = BuildCardInstance(
            new RiftboundCard { Id = 810065, Name = "Cloth Armor", Type = "Gear", Cost = 0, Power = 0, Color = ["Mind"], GameplayKeywords = ["Equip", "Quick-Draw", "Reaction", "Shield"] },
            0,
            0
        );
        player.HandZone.Cards.Add(armor);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(armor.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(unit.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(unit.InstanceId, armor.AttachedToInstanceId);
    }

    [Fact]
    public void ChemtechCask_WhenCastingReactionOnOpponentsTurn_PlaysGoldTokenExhausted()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032241);
        var turnPlayer = session.Players[0];
        var reactivePlayer = session.Players[1];
        ResetPlayer(turnPlayer);
        ResetPlayer(reactivePlayer);

        var cask = BuildCardInstance(
            new RiftboundCard { Id = 810061, Name = "Chemtech Cask", Type = "Gear", Cost = 0, Power = 0, Color = ["Mind"] },
            1,
            1
        );
        reactivePlayer.BaseZone.Cards.Add(cask);
        var opener = BuildCardInstance(new RiftboundCard { Id = 810062, Name = "Opener", Type = "Spell", Cost = 0, Power = 0 }, 0, 0);
        var reaction = BuildCardInstance(
            new RiftboundCard { Id = 810063, Name = "Consult the Past", Type = "Spell", Cost = 0, Power = 0, GameplayKeywords = ["Reaction"] },
            1,
            1
        );
        turnPlayer.HandZone.Cards.Add(opener);
        reactivePlayer.HandZone.Cards.Add(reaction);

        var openerAction = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(opener.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, openerAction).Succeeded);

        var reactionAction = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(reaction.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, reactionAction).Succeeded);

        Assert.True(cask.IsExhausted);
        Assert.Contains(
            reactivePlayer.BaseZone.Cards,
            x => string.Equals(x.Name, "Gold Token", StringComparison.OrdinalIgnoreCase) && x.IsExhausted
        );
    }

    [Fact]
    public void ClockworkKeeper_WithAdditionalCost_PaysCalmPowerAndDrawsOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032205);
        var player = session.Players[0];
        ResetPlayer(player);
        player.RunePool.PowerByDomain["Calm"] = 1;

        var drawn = BuildUnit(0, 0, "Drawn", 1);
        player.MainDeckZone.Cards.Add(drawn);
        var keeper = BuildCardInstance(
            new RiftboundCard { Id = 81007, Name = "Clockwork Keeper", Type = "Unit", Cost = 0, Power = 0, Might = 2, Color = ["Calm"] },
            0,
            0
        );
        player.HandZone.Cards.Add(keeper);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(keeper.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(ClockworkKeeperEffect.AdditionalCostMarker, StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(0, player.RunePool.PowerByDomain.GetValueOrDefault("Calm"));
        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == drawn.InstanceId);
    }

    [Fact]
    public void Confront_DrawsOne_AndUnitsPlayedThisTurnEnterReady()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032251);
        var player = session.Players[0];
        ResetPlayer(player);

        var drawn = BuildUnit(0, 0, "Drawn", 1);
        player.MainDeckZone.Cards.Add(drawn);
        var confront = BuildCardInstance(
            new RiftboundCard { Id = 810071, Name = "Confront", Type = "Spell", Cost = 0, Power = 0, Color = ["Body"], GameplayKeywords = ["Action"] },
            0,
            0
        );
        var unit = BuildCardInstance(
            new RiftboundCard { Id = 810073, Name = "Late Unit", Type = "Unit", Cost = 0, Power = 0, Might = 2 },
            0,
            0
        );
        player.HandZone.Cards.Add(confront);
        player.HandZone.Cards.Add(unit);

        var castConfront = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(confront.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, castConfront).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == drawn.InstanceId);

        var playUnit = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(unit.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, playUnit).Succeeded);

        Assert.False(player.BaseZone.Cards.Single(x => x.InstanceId == unit.InstanceId).IsExhausted);
    }

    [Fact]
    public void ConsultThePast_DrawsTwoCards()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032252);
        var player = session.Players[0];
        ResetPlayer(player);

        var drawA = BuildUnit(0, 0, "A", 1);
        var drawB = BuildUnit(0, 0, "B", 1);
        player.MainDeckZone.Cards.Add(drawA);
        player.MainDeckZone.Cards.Add(drawB);
        var consult = BuildCardInstance(
            new RiftboundCard { Id = 810072, Name = "Consult the Past", Type = "Spell", Cost = 0, Power = 0, Color = ["Mind"], GameplayKeywords = ["Reaction", "Hidden"] },
            0,
            0
        );
        player.HandZone.Cards.Add(consult);

        var cast = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(consult.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, cast).Succeeded);

        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == drawA.InstanceId);
        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == drawB.InstanceId);
    }

    [Fact]
    public void CombatChef_OnPlay_AttachesControlledEquipmentToItself()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032206);
        var player = session.Players[0];
        ResetPlayer(player);

        var gear = BuildCardInstance(
            new RiftboundCard { Id = 81008, Name = "Training Gear", Type = "Gear", Cost = 0, Power = 0, GameplayKeywords = ["Equip"] },
            0,
            0
        );
        player.BaseZone.Cards.Add(gear);
        var chef = BuildCardInstance(
            new RiftboundCard { Id = 81009, Name = "Combat Chef", Type = "Unit", Cost = 0, Power = 0, Might = 5, Color = ["Body"] },
            0,
            0
        );
        player.HandZone.Cards.Add(chef);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(chef.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(chef.InstanceId, gear.AttachedToInstanceId);
    }

    [Fact]
    public void CommanderLedros_SacrificesUnits_AndReducesOrderPowerCost()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032207);
        var player = session.Players[0];
        ResetPlayer(player);
        player.RunePool.PowerByDomain["Order"] = 2;

        var sacrA = BuildUnit(0, 0, "Sac A", 1);
        var sacrB = BuildUnit(0, 0, "Sac B", 1);
        player.BaseZone.Cards.Add(sacrA);
        player.BaseZone.Cards.Add(sacrB);

        var ledros = BuildCardInstance(
            new RiftboundCard
            {
                Id = 81010,
                Name = "Commander Ledros",
                Type = "Unit",
                Cost = 0,
                Power = 4,
                Might = 8,
                Color = ["Order"],
                GameplayKeywords = ["Deflect", "Ganking"],
            },
            0,
            0
        );
        player.HandZone.Cards.Add(ledros);

        var action = engine.GetLegalActions(session)
            .Where(x =>
                x.ActionId.Contains(ledros.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(CommanderLedrosEffect.SacrificeListMarker, StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal))
            .OrderByDescending(x => x.ActionId.Count(ch => ch == ','))
            .First()
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(0, player.RunePool.PowerByDomain.GetValueOrDefault("Order"));
        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == sacrA.InstanceId);
        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == sacrB.InstanceId);
        Assert.Contains(player.BaseZone.Cards, x => x.InstanceId == ledros.InstanceId);
    }

    [Fact]
    public void ConvergentMutation_SetsTargetMightToAnotherFriendlyUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032208);
        var player = session.Players[0];
        ResetPlayer(player);

        var low = BuildUnit(0, 0, "Low", 2);
        var high = BuildUnit(0, 0, "High", 5);
        player.BaseZone.Cards.Add(low);
        player.BaseZone.Cards.Add(high);

        var mutation = BuildCardInstance(
            new RiftboundCard { Id = 81011, Name = "Convergent Mutation", Type = "Spell", Cost = 0, Power = 0, Color = ["Mind"], GameplayKeywords = ["Reaction"] },
            0,
            0
        );
        player.HandZone.Cards.Add(mutation);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(mutation.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains($"-source-unit-{low.InstanceId}", StringComparison.Ordinal)
                && x.ActionId.Contains($"-target-unit-{high.InstanceId}", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(3, low.TemporaryMightModifier);
    }

    [Fact]
    public void CorinaVeraza_WhenMovingToBattlefield_PlaysThreeRecruitTokensThere()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032209);
        var player = session.Players[0];
        ResetPlayer(player);
        session.Battlefields[0].ControlledByPlayerIndex = 0;

        var corina = BuildCardInstance(
            new RiftboundCard { Id = 81012, Name = "Corina Veraza", Type = "Unit", Cost = 0, Power = 0, Might = 6, Color = ["Order"], GameplayKeywords = ["Accelerate"] },
            0,
            0
        );
        player.BaseZone.Cards.Add(corina);

        var move = engine.GetLegalActions(session)
            .First(x => x.ActionId.EndsWith($"move-{corina.InstanceId}-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, move).Succeeded);

        Assert.Equal(
            4,
            session.Battlefields[0].Units.Count(x => x.ControllerPlayerIndex == 0)
        );
    }

    [Fact]
    public void CorruptEnforcer_WhenMovingToBattlefield_DiscardsOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032210);
        var player = session.Players[0];
        ResetPlayer(player);
        session.Battlefields[0].ControlledByPlayerIndex = 0;

        var enforcer = BuildCardInstance(
            new RiftboundCard { Id = 81013, Name = "Corrupt Enforcer", Type = "Unit", Cost = 0, Power = 0, Might = 4, Color = ["Chaos"] },
            0,
            0
        );
        var fodder = BuildCardInstance(new RiftboundCard { Id = 81014, Name = "Discarded", Type = "Spell", Cost = 0, Power = 0 }, 0, 0);
        player.BaseZone.Cards.Add(enforcer);
        player.HandZone.Cards.Add(fodder);

        var move = engine.GetLegalActions(session)
            .First(x => x.ActionId.EndsWith($"move-{enforcer.InstanceId}-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, move).Succeeded);

        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == fodder.InstanceId);
    }

    [Fact]
    public void CrackshotCorsair_AsAttacker_DealsOneToEnemyUnitThere()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032101);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);
        session.Battlefields[0].ControlledByPlayerIndex = 1;

        var corsair = BuildCardInstance(
            new RiftboundCard { Id = 810141, Name = "Crackshot Corsair", Type = "Unit", Cost = 0, Power = 0, Might = 3, Color = ["Body"] },
            0,
            0
        );
        player.BaseZone.Cards.Add(corsair);
        var enemy = BuildUnit(1, 1, "Enemy", 5);
        session.Battlefields[0].Units.Add(enemy);

        var move = engine.GetLegalActions(session)
            .First(x => x.ActionId.EndsWith($"move-{corsair.InstanceId}-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, move).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Equal(1, enemy.MarkedDamage);
    }

    [Fact]
    public void CounterStrike_PreventsNextDamageThisTurn()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032211);
        var player = session.Players[0];
        ResetPlayer(player);

        var unit = BuildUnit(0, 0, "Protected", 4);
        player.BaseZone.Cards.Add(unit);
        var counterStrike = BuildCardInstance(
            new RiftboundCard { Id = 81015, Name = "Counter Strike", Type = "Spell", Cost = 0, Power = 0, Color = ["Body", "Calm"], GameplayKeywords = ["Reaction"] },
            0,
            0
        );
        var fallingStar = BuildCardInstance(
            new RiftboundCard { Id = 81016, Name = "Falling Star", Type = "Spell", Cost = 0, Power = 0, Color = ["Body"] },
            0,
            0
        );
        player.HandZone.Cards.Add(counterStrike);
        player.HandZone.Cards.Add(fallingStar);

        var protect = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(counterStrike.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(unit.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, protect).Succeeded);

        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        var damage = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(fallingStar.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(unit.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, damage).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Equal(0, unit.MarkedDamage);
    }

    [Fact]
    public void CruelPatron_KillsChosenFriendlyUnit_AsAdditionalCost()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032212);
        var player = session.Players[0];
        ResetPlayer(player);

        var victim = BuildUnit(0, 0, "Victim", 2);
        player.BaseZone.Cards.Add(victim);
        var patron = BuildCardInstance(
            new RiftboundCard { Id = 81017, Name = "Cruel Patron", Type = "Unit", Cost = 0, Power = 0, Might = 6, Color = ["Order"] },
            0,
            0
        );
        player.HandZone.Cards.Add(patron);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(patron.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(CruelPatronEffect.SacrificeMarker, StringComparison.Ordinal)
                && x.ActionId.Contains(victim.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.DoesNotContain(player.BaseZone.Cards, x => x.InstanceId == victim.InstanceId);
        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == victim.InstanceId);
    }

    [Fact]
    public void Cull_AttachedUnitConquers_PlaysGoldTokenExhausted()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032121);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var conqueror = BuildUnit(0, 0, "Conqueror", 4);
        player.BaseZone.Cards.Add(conqueror);
        var defender = BuildUnit(1, 1, "Defender", 1);
        session.Battlefields[0].Units.Add(defender);
        session.Battlefields[0].ControlledByPlayerIndex = 1;

        var cull = BuildCardInstance(
            new RiftboundCard { Id = 810181, Name = "Cull", Type = "Gear", Cost = 0, Power = 0, Color = ["Chaos"], GameplayKeywords = ["Equip"] },
            0,
            0
        );
        player.HandZone.Cards.Add(cull);

        var attach = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(cull.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(conqueror.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, attach).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        var move = engine.GetLegalActions(session)
            .First(x => x.ActionId.EndsWith($"move-{conqueror.InstanceId}-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, move).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Contains(
            player.BaseZone.Cards,
            x => string.Equals(x.Name, "Gold Token", StringComparison.OrdinalIgnoreCase) && x.IsExhausted
        );
    }

    [Fact]
    public void CullTheWeak_KillsOneUnitForEachPlayer()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032213);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var friendly = BuildUnit(0, 0, "Friendly", 2);
        var enemy = BuildUnit(1, 1, "Enemy", 2);
        player.BaseZone.Cards.Add(friendly);
        opponent.BaseZone.Cards.Add(enemy);

        var spell = BuildCardInstance(
            new RiftboundCard { Id = 81018, Name = "Cull the Weak", Type = "Spell", Cost = 0, Power = 0, Color = ["Order"] },
            0,
            0
        );
        player.HandZone.Cards.Add(spell);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(spell.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(friendly.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == friendly.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, x => x.InstanceId == enemy.InstanceId);
    }

    [Fact]
    public void DangerZone_Repeat_BuffsMechsTwice_AndPaysAnyRune()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032214);
        var player = session.Players[0];
        ResetPlayer(player);
        player.RunePool.Energy = 1;
        player.RunePool.PowerByDomain["Chaos"] = 1;

        var mech = BuildCardInstance(
            new RiftboundCard { Id = 81019, Name = "Ally Mech", Type = "Unit", Cost = 0, Power = 0, Might = 3, Tags = ["Mech"] },
            0,
            0
        );
        var nonMech = BuildUnit(0, 0, "Non Mech", 3);
        player.BaseZone.Cards.Add(mech);
        player.BaseZone.Cards.Add(nonMech);

        var zone = BuildCardInstance(
            new RiftboundCard { Id = 81020, Name = "Danger Zone", Type = "Spell", Cost = 0, Power = 0, Color = ["Fury", "Mind"], GameplayKeywords = ["Reaction", "Repeat"] },
            0,
            0
        );
        player.HandZone.Cards.Add(zone);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(zone.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-repeat", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(2, mech.TemporaryMightModifier);
        Assert.Equal(0, nonMech.TemporaryMightModifier);
        Assert.Equal(0, player.RunePool.Energy);
        Assert.Equal(0, player.RunePool.PowerByDomain.GetValueOrDefault("Chaos"));
    }

    [Fact]
    public void DangerousDuo_LegionOnSecondCardPlay_BuffsChosenUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032215);
        var player = session.Players[0];
        ResetPlayer(player);

        var opener = BuildCardInstance(new RiftboundCard { Id = 81021, Name = "Opener", Type = "Spell", Cost = 0, Power = 0 }, 0, 0);
        var duo = BuildCardInstance(
            new RiftboundCard { Id = 81022, Name = "Dangerous Duo", Type = "Unit", Cost = 0, Power = 0, Might = 3, Color = ["Fury"], GameplayKeywords = ["Legion"] },
            0,
            0
        );
        var target = BuildUnit(0, 0, "Target", 2);
        player.HandZone.Cards.Add(opener);
        player.HandZone.Cards.Add(duo);
        player.BaseZone.Cards.Add(target);

        var playOpener = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(opener.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, playOpener).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        var duoAction = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(duo.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(DangerousDuoEffect.TargetMarker, StringComparison.Ordinal)
                && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, duoAction).Succeeded);

        Assert.Equal(2, target.TemporaryMightModifier);
    }

    [Fact]
    public void DariusExecutioner_LegionReadiesAndBuffsOtherFriendlyUnitAtBattlefield()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032216);
        var player = session.Players[0];
        ResetPlayer(player);
        session.Battlefields[0].ControlledByPlayerIndex = 0;

        var opener = BuildCardInstance(new RiftboundCard { Id = 81023, Name = "Opener", Type = "Spell", Cost = 0, Power = 0 }, 0, 0);
        player.HandZone.Cards.Add(opener);
        var ally = BuildUnit(0, 0, "Ally", 1);
        session.Battlefields[0].Units.Add(ally);
        var enemy = BuildUnit(1, 1, "Enemy", 3);
        session.Battlefields[1].Units.Add(enemy);

        var darius = BuildCardInstance(
            new RiftboundCard
            {
                Id = 81024,
                Name = "Darius, Executioner",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 1,
                Color = ["Order"],
                GameplayKeywords = ["Legion"],
            },
            0,
            0
        );
        var challenge = BuildCardInstance(
            new RiftboundCard { Id = 81025, Name = "Challenge", Type = "Spell", Cost = 0, Power = 0, Color = ["Body"] },
            0,
            0
        );
        player.HandZone.Cards.Add(darius);
        player.HandZone.Cards.Add(challenge);

        var playOpener = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(opener.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, playOpener).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        var playDarius = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(darius.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, playDarius).Succeeded);
        Assert.False(session.Battlefields[0].Units.Single(x => x.InstanceId == darius.InstanceId).IsExhausted);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        var challengeAction = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(challenge.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(ally.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(enemy.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, challengeAction).Succeeded);

        Assert.True(enemy.MarkedDamage >= 2);
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
            new RiftboundCard { Id = 76, Name = "Challenge", Type = "Spell", Effect = "Choose a friendly unit and an enemy unit. They deal damage equal to their Mights to each other." },
            new RiftboundCard { Id = 77, Name = "Chaos Rune", Type = "Rune", Effect = "-" },
            new RiftboundCard { Id = 78, Name = "Charm", Type = "Spell", Effect = "Move an enemy unit." },
            new RiftboundCard { Id = 79, Name = "Chemtech Cask", Type = "Gear", Effect = "When you play a spell on an opponent's turn, you may exhaust me to play a Gold gear token exhausted." },
            new RiftboundCard { Id = 80, Name = "Chemtech Enforcer", Type = "Unit", Effect = "When you play me, discard 1.", GameplayKeywords = ["Assault"] },
            new RiftboundCard { Id = 81, Name = "Cithria of Cloudfield", Type = "Unit", Effect = "When you play another unit, buff me." },
            new RiftboundCard { Id = 82, Name = "Cleave", Type = "Spell", Effect = "Give a unit [ASSAULT 3] this turn.", GameplayKeywords = ["Action", "Assault"] },
            new RiftboundCard { Id = 83, Name = "Clockwork Keeper", Type = "Unit", Effect = "You may pay [Calm] as an additional cost to play me." },
            new RiftboundCard { Id = 84, Name = "Cloth Armor", Type = "Gear", Effect = "[Shield 2] +0 [Might]", GameplayKeywords = ["Equip", "Quick-Draw", "Reaction", "Shield"] },
            new RiftboundCard { Id = 85, Name = "Combat Chef", Type = "Unit", Effect = "[Weaponmaster]", GameplayKeywords = ["Weaponmaster"] },
            new RiftboundCard { Id = 86, Name = "Commander Ledros", Type = "Unit", Effect = "As you play me, you may kill any number of friendly units as an additional cost.", GameplayKeywords = ["Deflect", "Ganking"] },
            new RiftboundCard { Id = 87, Name = "Confront", Type = "Spell", Effect = "Units you play this turn enter ready. Draw 1.", GameplayKeywords = ["Action"] },
            new RiftboundCard { Id = 88, Name = "Consult the Past", Type = "Spell", Effect = "Draw 2.", GameplayKeywords = ["Hidden", "Reaction"] },
            new RiftboundCard { Id = 89, Name = "Convergent Mutation", Type = "Spell", Effect = "Choose a friendly unit. This turn, increase its Might to the Might of another friendly unit.", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 90, Name = "Corina Veraza", Type = "Unit", Effect = "When I move to a battlefield, play three 1 [Might] Recruit unit tokens here.", GameplayKeywords = ["Accelerate"] },
            new RiftboundCard { Id = 91, Name = "Corrupt Enforcer", Type = "Unit", Effect = "When I move to a battlefield, discard 1. When I win a combat, draw 1." },
            new RiftboundCard { Id = 92, Name = "Counter Strike", Type = "Spell", Effect = "Choose a unit. The next time that unit would be dealt damage this turn, prevent it. Draw 1", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 93, Name = "Crackshot Corsair", Type = "Unit", Effect = "When I attack, deal 1 to an enemy unit here." },
            new RiftboundCard { Id = 94, Name = "Cruel Patron", Type = "Unit", Effect = "As an additional cost to play me, kill a friendly unit." },
            new RiftboundCard { Id = 95, Name = "Cull", Type = "Gear", Effect = "When I conquer, play a Gold gear token exhausted. +1 [Might]", GameplayKeywords = ["Equip"] },
            new RiftboundCard { Id = 96, Name = "Cull the Weak", Type = "Spell", Effect = "Each player kills one of their units." },
            new RiftboundCard { Id = 97, Name = "Danger Zone", Type = "Spell", Effect = "Give your Mechs +1 [Might] this turn.", GameplayKeywords = ["Reaction", "Repeat"] },
            new RiftboundCard { Id = 98, Name = "Dangerous Duo", Type = "Unit", Effect = "When you play me, give a unit +2 [Might] this turn.", GameplayKeywords = ["Legion"] },
            new RiftboundCard { Id = 99, Name = "Daring Poro", Type = "Unit", Effect = "[ASSAULT]", GameplayKeywords = ["Assault"] },
            new RiftboundCard { Id = 100, Name = "Darius, Executioner", Type = "Unit", Effect = "Other friendly units have +1 [Might] here.", GameplayKeywords = ["Legion"] },
        ];
    }
}
