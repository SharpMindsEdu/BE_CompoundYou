using Application.Features.Riftbound.Simulation.Definitions;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineEAndFWaveBehaviorTests
    : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void EAndFWaveCards_ResolveToSupportedTemplates()
    {
        foreach (var card in BuildWaveCards())
        {
            var template = RiftboundEffectTemplateResolver.Resolve(card);
            Assert.True(template.Supported, $"{card.Name} resolved as unsupported.");
            Assert.NotEqual("unsupported", template.TemplateId);
        }
    }

    [Fact]
    public void DravenVanquisher_WhenWinsCombat_PlaysGoldTokenExhausted()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032401);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        session.Battlefields[0].ControlledByPlayerIndex = 1;
        session.Battlefields[0].Units.Add(BuildUnit(1, 1, "Defender", 1));
        var vanquisher = BuildCardInstance(
            new RiftboundCard { Id = 94001, Name = "Draven, Vanquisher", Type = "Unit", Cost = 0, Power = 0, Might = 4, Color = ["Fury"] },
            0,
            0
        );
        player.BaseZone.Cards.Add(vanquisher);

        var move = engine.GetLegalActions(session)
            .First(x => x.ActionId.EndsWith($"move-{vanquisher.InstanceId}-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, move).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Contains(
            player.BaseZone.Cards,
            x => x.IsToken && string.Equals(x.Name, "Gold Token", StringComparison.OrdinalIgnoreCase) && x.IsExhausted
        );
    }

    [Fact]
    public void EagerApprentice_ReducesSpellEnergyCostByOne_ToMinimumOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032402);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var apprentice = BuildCardInstance(
            new RiftboundCard { Id = 94002, Name = "Eager Apprentice", Type = "Unit", Cost = 0, Power = 0, Might = 3, Color = ["Mind"] },
            0,
            0
        );
        session.Battlefields[0].Units.Add(apprentice);
        player.RunePool.Energy = 1;

        var spell = BuildCardInstance(
            new RiftboundCard { Id = 94003, Name = "Decisive Strike", Type = "Spell", Cost = 2, Power = 0, Color = ["Body", "Order"], GameplayKeywords = ["Action"] },
            0,
            0
        );
        player.HandZone.Cards.Add(spell);

        var action = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(spell.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.Equal(0, player.RunePool.Energy);
    }

    [Fact]
    public void EnergyConduit_Activate_AddsEnergyAndExhausts()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032403);
        var player = session.Players[0];
        ResetPlayer(player);

        var conduit = BuildCardInstance(
            new RiftboundCard { Id = 94004, Name = "Energy Conduit", Type = "Gear", Cost = 0, Power = 0, Color = ["Mind"], GameplayKeywords = ["Reaction"] },
            0,
            0
        );
        player.BaseZone.Cards.Add(conduit);

        var activate = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.ActivateRune
                && x.ActionId.Contains(conduit.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activate).Succeeded);
        Assert.Equal(1, player.RunePool.Energy);
        Assert.True(conduit.IsExhausted);
    }

    [Fact]
    public void Facebreaker_StunsTargets_AndEclipseHeraldTriggers()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032404);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var eclipse = BuildCardInstance(
            new RiftboundCard { Id = 94005, Name = "Eclipse Herald", Type = "Unit", Cost = 0, Power = 0, Might = 7, Color = ["Calm"] },
            0,
            0
        );
        eclipse.IsExhausted = true;
        player.BaseZone.Cards.Add(eclipse);

        var friendly = BuildUnit(0, 0, "Friendly Target", 3);
        var enemy = BuildUnit(1, 1, "Enemy Target", 3);
        session.Battlefields[0].Units.Add(friendly);
        session.Battlefields[0].Units.Add(enemy);

        var facebreaker = BuildCardInstance(
            new RiftboundCard { Id = 94006, Name = "Facebreaker", Type = "Spell", Cost = 0, Power = 0, Color = ["Order"], GameplayKeywords = ["Action", "Hidden"] },
            0,
            0
        );
        player.HandZone.Cards.Add(facebreaker);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(facebreaker.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(friendly.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(enemy.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.True(friendly.IsExhausted);
        Assert.True(enemy.IsExhausted);
        Assert.Equal("true", friendly.EffectData["stunnedThisTurn"]);
        Assert.Equal("true", enemy.EffectData["stunnedThisTurn"]);
        Assert.False(eclipse.IsExhausted);
        Assert.Equal(1, eclipse.TemporaryMightModifier);
    }

    [Fact]
    public void FactoryRecall_ReturnsGearToOwnerHand()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032405);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var targetGear = BuildCardInstance(
            new RiftboundCard { Id = 94007, Name = "Target Gear", Type = "Gear", Cost = 0, Power = 0 },
            1,
            1
        );
        opponent.BaseZone.Cards.Add(targetGear);
        var recall = BuildCardInstance(
            new RiftboundCard { Id = 94008, Name = "Factory Recall", Type = "Spell", Cost = 0, Power = 0, Color = ["Chaos"], GameplayKeywords = ["Action"] },
            0,
            0
        );
        player.HandZone.Cards.Add(recall);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(recall.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(targetGear.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Contains(opponent.HandZone.Cards, x => x.InstanceId == targetGear.InstanceId);
        Assert.DoesNotContain(opponent.BaseZone.Cards, x => x.InstanceId == targetGear.InstanceId);
    }

    [Fact]
    public void FadingMemories_TemporaryUnitDiesAtBeginningPhase()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032406);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var target = BuildUnit(1, 1, "Temporary Enemy", 4);
        session.Battlefields[0].Units.Add(target);
        var fading = BuildCardInstance(
            new RiftboundCard { Id = 94009, Name = "Fading Memories", Type = "Spell", Cost = 0, Power = 0, Color = ["Chaos"] },
            0,
            0
        );
        player.HandZone.Cards.Add(fading);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(fading.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Equal("true", target.EffectData["temporaryUntilBeginning"]);
        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);
        Assert.Contains(opponent.TrashZone.Cards, x => x.InstanceId == target.InstanceId);
    }

    [Fact]
    public void EkkoRecurrent_Deathknell_ReadiesRunes_AndRecyclesSelf()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032407);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var rune = BuildRuneInstance(94010, "Mind Rune", "Mind", 0);
        rune.IsExhausted = true;
        player.BaseZone.Cards.Add(rune);

        var ekko = BuildCardInstance(
            new RiftboundCard { Id = 94011, Name = "Ekko, Recurrent", Type = "Unit", Cost = 0, Power = 0, Might = 5, Color = ["Mind"], GameplayKeywords = ["Accelerate", "Deathknell"] },
            0,
            0
        );
        player.BaseZone.Cards.Add(ekko);
        session.Battlefields[0].ControlledByPlayerIndex = 1;
        session.Battlefields[0].Units.Add(BuildUnit(1, 1, "Big Enemy", 10));

        var move = engine.GetLegalActions(session)
            .First(x => x.ActionId.EndsWith($"move-{ekko.InstanceId}-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, move).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.False(rune.IsExhausted);
        Assert.Contains(player.MainDeckZone.Cards, x => x.InstanceId == ekko.InstanceId);
        Assert.DoesNotContain(player.TrashZone.Cards, x => x.InstanceId == ekko.InstanceId);
    }

    [Fact]
    public void EzrealProdigalExplorer_ActivatesAfterTwoEnemySelections()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032408);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var legend = BuildCardInstance(
            new RiftboundCard { Id = 94012, Name = "Ezreal, Prodigal Explorer", Type = "Legend", Cost = 0, Power = 0, Color = ["Mind", "Chaos"], GameplayKeywords = ["Reaction"] },
            0,
            0
        );
        player.LegendZone.Cards.Add(legend);
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Drawn Unit", 1));

        var gearA = BuildCardInstance(new RiftboundCard { Id = 94013, Name = "Enemy Gear A", Type = "Gear", Cost = 0, Power = 0 }, 1, 1);
        var gearB = BuildCardInstance(new RiftboundCard { Id = 94014, Name = "Enemy Gear B", Type = "Gear", Cost = 0, Power = 0 }, 1, 1);
        opponent.BaseZone.Cards.Add(gearA);
        opponent.BaseZone.Cards.Add(gearB);

        var recallA = BuildCardInstance(
            new RiftboundCard { Id = 94015, Name = "Factory Recall", Type = "Spell", Cost = 0, Power = 0, Color = ["Chaos"], GameplayKeywords = ["Action"] },
            0,
            0
        );
        var recallB = BuildCardInstance(
            new RiftboundCard { Id = 94016, Name = "Factory Recall", Type = "Spell", Cost = 0, Power = 0, Color = ["Chaos"], GameplayKeywords = ["Action"] },
            0,
            0
        );
        player.HandZone.Cards.Add(recallA);
        player.HandZone.Cards.Add(recallB);

        var actionA = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(recallA.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(gearA.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, actionA).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        var actionB = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(recallB.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(gearB.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, actionB).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        var activate = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.ActivateRune
                && x.ActionId.Contains(legend.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activate).Succeeded);
        Assert.True(legend.IsExhausted);
        Assert.Contains(player.HandZone.Cards, x => string.Equals(x.Name, "Drawn Unit", StringComparison.Ordinal));
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
            new RiftboundCard { Id = 126, Name = "Draven, Showboat", Type = "Unit", Effect = "My might is increased by your points." },
            new RiftboundCard { Id = 127, Name = "Draven, Vanquisher", Type = "Unit", Effect = "When I win a combat, play a Gold gear token exhausted. When I attack or defend, you may pay [Fury]. If you do, give me +2 [Might] this turn." },
            new RiftboundCard { Id = 128, Name = "Dropboarder", Type = "Unit", Effect = "When you play me, if you control two or more gear, ready me." },
            new RiftboundCard { Id = 129, Name = "Dune Drake", Type = "Unit", Effect = "When I attack, give me +2 [Might] this turn if there is a ready enemy unit here." },
            new RiftboundCard { Id = 130, Name = "Dunebreaker", Type = "Unit", Effect = "If you have two or fewer cards in your hand, I enter ready. When I hold, draw 2." },
            new RiftboundCard { Id = 131, Name = "Eager Apprentice", Type = "Unit", Effect = "While I'm at a battlefield, the Energy costs for spells you play is reduced by [1], to a minimum of [1]." },
            new RiftboundCard { Id = 132, Name = "Eager Drakehound", Type = "Unit", Effect = "I enter ready." },
            new RiftboundCard { Id = 133, Name = "Eclipse Herald", Type = "Unit", Effect = "When you stun an enemy unit, ready me and give me +1 [Might] this turn." },
            new RiftboundCard { Id = 134, Name = "Edge of Night", Type = "Gear", Effect = "When you play this from face down, attach it to a unit you control. +2 [Might].", GameplayKeywords = ["Equip", "Hidden"] },
            new RiftboundCard { Id = 135, Name = "Ekko, Recurrent", Type = "Unit", Effect = "[Deathknell] - Recycle me to ready your runes.", GameplayKeywords = ["Accelerate", "Deathknell"] },
            new RiftboundCard { Id = 136, Name = "Ember Monk", Type = "Unit", Effect = "When you play a card from [hidden], give me +2 [might] this turn.", GameplayKeywords = ["Hidden"] },
            new RiftboundCard { Id = 137, Name = "Eminent Benefactor", Type = "Unit", Effect = "When I hold, play two Gold gear tokens exhausted." },
            new RiftboundCard { Id = 138, Name = "Emperor's Dais", Type = "Battlefield", Effect = "When you conquer here, you may pay [1] and return a unit you control here to its owner's hand. If you do, play a 2 [Might] Sand Soldier unit token here." },
            new RiftboundCard { Id = 139, Name = "Emperor's Divide", Type = "Spell", Effect = "Move any number of friendly units at a battlefield to their base.", GameplayKeywords = ["Action", "Hidden"] },
            new RiftboundCard { Id = 140, Name = "En Garde", Type = "Spell", Effect = "Give a friendly unit +1 [Might] this turn, then an additional +1 [Might] this turn if it is the only unit you control there.", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 141, Name = "Energy Conduit", Type = "Gear", Effect = "[Tap]: [Reaction] - [Add] [1].", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 142, Name = "Experimental Hexplate", Type = "Gear", Effect = "[Equip][Mind] I am a Mech. +1", GameplayKeywords = ["Equip"] },
            new RiftboundCard { Id = 143, Name = "Eye of the Herald", Type = "Gear", Effect = "[Equip][Order] When I move, play a 1 [Might] Recruit unit token here. +0 [Might]", GameplayKeywords = ["Equip"] },
            new RiftboundCard { Id = 144, Name = "Ezreal, Dashing", Type = "Unit", Effect = "When I attack or defend, deal damage equal to my Might to an enemy unit here. I don't deal combat damage." },
            new RiftboundCard { Id = 145, Name = "Ezreal, Prodigal Explorer", Type = "Legend", Effect = "[Tap]: [Reaction] - Draw 1. Use only if you've chosen enemy units and/or gear twice this turn with spells or unit abilities.", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 146, Name = "Ezreal, Prodigy", Type = "Unit", Effect = "When you play me, discard 1, then draw 2. Optional additional costs you pay cost [1] or [Rune] less." },
            new RiftboundCard { Id = 147, Name = "Facebreaker", Type = "Spell", Effect = "Stun a friendly unit and an enemy unit at the same battlefield.", GameplayKeywords = ["Action", "Hidden"] },
            new RiftboundCard { Id = 148, Name = "Factory Recall", Type = "Spell", Effect = "Return a gear to its owner's hand.", GameplayKeywords = ["Action"] },
            new RiftboundCard { Id = 149, Name = "Fading Memories", Type = "Spell", Effect = "Give a unit at a battlefield or a gear [temporary]." },
            new RiftboundCard { Id = 150, Name = "Fae Dragon", Type = "Unit", Effect = "When you play me, buff up to four friendly units. When you spend a buff, play a Gold gear token exhausted." },
        ];
    }
}

