using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineBCardWaveBehaviorTests
{
    [Fact]
    public void BandleTree_ResolvesToNamedTemplate_WithAdditionalHideCapacity()
    {
        var battlefield = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 10_001,
                Name = "Bandle Tree",
                Type = "Battlefield",
                Cost = 0,
                Power = 0,
                Effect = "You may hide an additional card here.",
            },
            0,
            0
        );

        Assert.Equal("named.bandle-tree", battlefield.EffectTemplateId);
        Assert.Equal("1", battlefield.EffectData["additionalHideCapacity"]);
    }

    [Fact]
    public void BardMercurial_WithLegendAdditionalCost_ExhaustsLegend_AndMovesUnitsToOpenBattlefield()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026031901);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        session.Battlefields[0].ControlledByPlayerIndex = 0;
        session.Battlefields[1].ControlledByPlayerIndex = null;
        session.Battlefields[1].ContestedByPlayerIndex = null;
        session.Battlefields[1].Units.Clear();

        var legend = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard { Id = 10_010, Name = "Bard Legend", Type = "Legend", Cost = 0, Power = 0 },
            0,
            0
        );
        player.LegendZone.Cards.Add(legend);

        var baseUnit = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Base Ally", 2);
        var fieldUnit = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Field Ally", 3);
        player.BaseZone.Cards.Add(baseUnit);
        session.Battlefields[0].Units.Add(fieldUnit);

        var bard = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 10_011,
                Name = "Bard, Mercurial",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 4,
                Color = ["Mind"],
                Effect = "You may exhaust your legend as an additional cost to play me.",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(bard);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(bard.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains("-bard-exhaust-legend-", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.True(legend.IsExhausted);
        Assert.Contains(session.Battlefields[1].Units, x => x.InstanceId == baseUnit.InstanceId);
        Assert.Contains(session.Battlefields[1].Units, x => x.InstanceId == fieldUnit.InstanceId);
    }

    [Fact]
    public void BeastBelow_ReturnsSelectedFriendlyAndEnemyUnitToOwnersHands()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026031902);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);
        session.Battlefields[0].ControlledByPlayerIndex = 0;

        var friendly = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Friendly Return", 2);
        var enemy = RiftboundBehaviorTestFactory.BuildUnit(1, 1, "Enemy Return", 3);
        player.BaseZone.Cards.Add(friendly);
        session.Battlefields[0].Units.Add(enemy);

        var beast = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 10_020,
                Name = "Beast Below",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 8,
                Color = ["Chaos"],
                Effect = "When you play me, return another friendly unit and an enemy unit to their owners' hands.",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(beast);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(beast.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(friendly.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(enemy.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == friendly.InstanceId);
        Assert.Contains(opponent.HandZone.Cards, x => x.InstanceId == enemy.InstanceId);
    }

    [Fact]
    public void BilgewaterBully_WhenBuffed_GainsGankingMoveActions()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026031903);
        var player = session.Players[0];
        ResetPlayer(player);
        session.Battlefields[0].ControlledByPlayerIndex = 0;
        session.Battlefields[1].ControlledByPlayerIndex = null;

        var bully = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 10_030,
                Name = "Bilgewater Bully",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 6,
                Color = ["Body"],
                Effect = "While I'm buffed, I have [GANKING].",
            },
            0,
            0
        );
        bully.TemporaryMightModifier = 1;
        session.Battlefields[0].Units.Add(bully);

        var actions = engine.GetLegalActions(session);
        Assert.Contains(
            actions,
            x =>
                x.ActionType == RiftboundActionType.StandardMove
                && x.ActionId.Contains($"move-{bully.InstanceId}-to-bf-1", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void BlackMarketBroker_WhenPlayingFacedownCard_PlaysGoldTokenExhausted()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026031904);
        var player = session.Players[0];
        ResetPlayer(player);

        var broker = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 10_040,
                Name = "Black Market Broker",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 3,
                Color = ["Chaos"],
                Effect = "When you play a card from face down, play a Gold gear token exhausted.",
            },
            0,
            0
        );
        player.BaseZone.Cards.Add(broker);

        var facedownSpell = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 10_041,
                Name = "Hidden Test Spell",
                Type = "Spell",
                Cost = 0,
                Power = 0,
            },
            0,
            0
        );
        facedownSpell.IsFacedown = true;
        player.HandZone.Cards.Add(facedownSpell);

        var action = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(facedownSpell.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Contains(
            player.BaseZone.Cards,
            x =>
                string.Equals(x.Name, "Gold Token", StringComparison.OrdinalIgnoreCase) && x.IsExhausted
        );
    }

    [Fact]
    public void BladeOfTheRuinedKing_AttachesToTarget_AndSacrificesSelectedFriendlyUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026031905);
        var player = session.Players[0];
        ResetPlayer(player);

        var attachTarget = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Attach Target", 3);
        var sacrifice = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Sacrifice Target", 2);
        player.BaseZone.Cards.Add(attachTarget);
        player.BaseZone.Cards.Add(sacrifice);

        var blade = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 10_050,
                Name = "Blade of the Ruined King",
                Type = "Gear",
                Cost = 0,
                Power = 0,
                Color = ["Order"],
                GameplayKeywords = ["Equip"],
                Effect = "+4 [Might]",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(blade);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(blade.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(attachTarget.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(sacrifice.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.DoesNotContain(player.BaseZone.Cards, x => x.InstanceId == sacrifice.InstanceId);
        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == sacrifice.InstanceId);
        Assert.Equal(attachTarget.InstanceId, blade.AttachedToInstanceId);
    }

    [Fact]
    public void BlastCorpsCadet_WithAdditionalCost_DealsTwoDamageToBattlefieldUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026031906);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);
        session.Battlefields[0].ControlledByPlayerIndex = 1;

        player.BaseZone.Cards.Add(RiftboundBehaviorTestFactory.BuildRuneInstance(10_060, "Fury Rune", "Fury", 0));
        var target = RiftboundBehaviorTestFactory.BuildUnit(1, 1, "Battlefield Target", 3);
        session.Battlefields[0].Units.Add(target);

        var cadet = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 10_061,
                Name = "Blast Corps Cadet",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 2,
                Color = ["Fury"],
                Effect = "When you play me, if you paid the additional cost, deal 2 to a unit at a battlefield.",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(cadet);

        var activate = engine.GetLegalActions(session)
            .First(x => x.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        Assert.True(engine.ApplyAction(session, activate).Succeeded);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(cadet.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains("-blast-corps-additional-cost-", StringComparison.Ordinal)
                && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(2, target.MarkedDamage);
    }

    [Fact]
    public void BlastOfPower_KillsBattlefieldUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026031907);
        var player = session.Players[0];
        ResetPlayer(player);
        session.Battlefields[0].ControlledByPlayerIndex = 1;

        var target = RiftboundBehaviorTestFactory.BuildUnit(1, 1, "Kill Target", 4);
        session.Battlefields[0].Units.Add(target);

        var blast = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 10_070,
                Name = "Blast of Power",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Order"],
                GameplayKeywords = ["Action"],
                Effect = "Kill a unit at a battlefield.",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(blast);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(blast.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.DoesNotContain(session.Battlefields[0].Units, x => x.InstanceId == target.InstanceId);
    }

    [Fact]
    public void BlastconeFae_OnPlay_ReducesTargetMightToMinimumOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026031908);
        var player = session.Players[0];
        ResetPlayer(player);

        var target = RiftboundBehaviorTestFactory.BuildUnit(1, 1, "Might Two Unit", 2);
        session.Battlefields[0].Units.Add(target);

        var fae = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 10_080,
                Name = "Blastcone Fae",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 2,
                Color = ["Mind"],
                GameplayKeywords = ["Hidden"],
                Effect = "When you play me, give a unit -2 [Might] this turn, to a minimum of 1 [Might].",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(fae);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(fae.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(1, target.Might.GetValueOrDefault() + target.TemporaryMightModifier);
    }

    [Fact]
    public void BlazingScorcher_HasAcceleratePlayActions()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026031909);
        var player = session.Players[0];
        ResetPlayer(player);
        player.BaseZone.Cards.Add(
            RiftboundBehaviorTestFactory.BuildRuneInstance(10_089, "Fury Rune", "Fury", 0)
        );

        var scorcher = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 10_090,
                Name = "Blazing Scorcher",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 5,
                Color = ["Fury"],
                GameplayKeywords = ["Accelerate"],
                Effect = "[ACCELERATE]",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(scorcher);

        var actions = engine.GetLegalActions(session);
        Assert.Contains(
            actions,
            x =>
                x.ActionId.Contains(scorcher.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains("-accelerate-", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void BlindFury_RevealsOpponentTopCard_AndPlaysItIgnoringCost()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026031910);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var revealed = RiftboundBehaviorTestFactory.BuildUnit(1, 1, "Opponent Top Unit", 3);
        opponent.MainDeckZone.Cards.Add(revealed);

        var blindFury = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 10_100,
                Name = "Blind Fury",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Fury"],
                GameplayKeywords = ["Action"],
            },
            0,
            0
        );
        player.HandZone.Cards.Add(blindFury);

        var action = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(blindFury.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.DoesNotContain(opponent.MainDeckZone.Cards, x => x.InstanceId == revealed.InstanceId);
        Assert.Contains(player.BaseZone.Cards, x => x.InstanceId == revealed.InstanceId);
    }

    [Fact]
    public void BlitzcrankImpassive_PlayedToBattlefield_CanPullEnemyUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026031911);
        var player = session.Players[0];
        ResetPlayer(player);
        session.Battlefields[0].ControlledByPlayerIndex = 0;

        var enemy = RiftboundBehaviorTestFactory.BuildUnit(1, 1, "Pulled Enemy", 3);
        session.Battlefields[1].Units.Add(enemy);

        var blitz = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 10_110,
                Name = "Blitzcrank, Impassive",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 5,
                Color = ["Calm"],
                GameplayKeywords = ["Tank"],
            },
            0,
            0
        );
        player.HandZone.Cards.Add(blitz);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(blitz.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(enemy.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-bf-0", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Contains(session.Battlefields[0].Units, x => x.InstanceId == enemy.InstanceId);
        Assert.DoesNotContain(session.Battlefields[1].Units, x => x.InstanceId == enemy.InstanceId);
    }

    [Fact]
    public void BlitzcrankImpassive_WhenHolding_ReturnsToOwnersHand()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026031912);
        var player = session.Players[0];
        ResetPlayer(player);
        session.Battlefields[0].ControlledByPlayerIndex = 0;

        var blitz = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 10_120,
                Name = "Blitzcrank, Impassive",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 5,
                Color = ["Calm"],
                GameplayKeywords = ["Tank"],
            },
            0,
            0
        );
        session.Battlefields[0].Units.Add(blitz);

        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);
        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);

        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == blitz.InstanceId);
        Assert.DoesNotContain(session.Battlefields[0].Units, x => x.InstanceId == blitz.InstanceId);
    }

    [Fact]
    public void Block_GrantsShieldAndTemporaryTank_UntilEndTurn()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026031913);
        var player = session.Players[0];
        ResetPlayer(player);

        var target = RiftboundBehaviorTestFactory.BuildUnit(0, 0, "Block Target", 2);
        player.BaseZone.Cards.Add(target);

        var block = RiftboundBehaviorTestFactory.BuildCardInstance(
            new RiftboundCard
            {
                Id = 10_130,
                Name = "Block",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Action", "Hidden", "Shield", "Tank"],
            },
            0,
            0
        );
        player.HandZone.Cards.Add(block);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(block.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);
        Assert.Contains(target.Keywords, x => string.Equals(x, "Tank", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, target.ShieldCount);

        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);

        Assert.Equal(0, target.ShieldCount);
        Assert.DoesNotContain(
            target.Keywords,
            x => string.Equals(x, "Tank", StringComparison.OrdinalIgnoreCase)
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
