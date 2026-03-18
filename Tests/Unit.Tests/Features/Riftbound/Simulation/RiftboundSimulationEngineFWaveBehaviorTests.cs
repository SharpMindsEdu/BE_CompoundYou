using Application.Features.Riftbound.Simulation.Definitions;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineFWaveBehaviorTests
    : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void FWaveCards_ResolveToSupportedTemplates()
    {
        foreach (var card in BuildWaveCards())
        {
            var template = RiftboundEffectTemplateResolver.Resolve(card);
            Assert.True(template.Supported, $"{card.Name} resolved as unsupported.");
            Assert.NotEqual("unsupported", template.TemplateId);
        }
    }

    [Fact]
    public void MinotaurReckoner_RemovesMoveToBaseActions()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032601);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var mover = BuildUnit(0, 0, "Mover", 3);
        session.Battlefields[0].Units.Add(mover);
        opponent.BaseZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard
                {
                    Id = 951001,
                    Name = "Minotaur Reckoner",
                    Type = "Unit",
                    Cost = 0,
                    Power = 0,
                    Might = 5,
                    Color = ["Fury"],
                    Effect = "Units can't move to base.",
                },
                1,
                1
            )
        );

        Assert.DoesNotContain(
            engine.GetLegalActions(session),
            x =>
                x.ActionType == RiftboundActionType.StandardMove
                && x.ActionId.Contains(mover.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void FallingComet_DealsSixToBattlefieldUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032602);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var target = BuildUnit(1, 1, "Target", 6);
        session.Battlefields[0].Units.Add(target);

        player.BaseZone.Cards.AddRange(
            Enumerable.Range(0, 5).Select(i => BuildRuneInstance(951100 + i, "Mind Rune", "Mind", 0))
        );
        var comet = BuildCardInstance(
            new RiftboundCard
            {
                Id = 951110,
                Name = "Falling Comet",
                Type = "Spell",
                Cost = 5,
                Power = 0,
                Color = ["Mind"],
                GameplayKeywords = ["Action"],
                Effect = "Deal 6 to a unit at a battlefield.",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(comet);

        foreach (var rune in player.BaseZone.Cards.Where(x => x.Type == "Rune").ToList())
        {
            var activate = engine.GetLegalActions(session)
                .First(x => x.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activate).Succeeded);
        }

        var play = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.PlayCard
                && x.ActionId.Contains(comet.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, play).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.DoesNotContain(session.Battlefields[0].Units, x => x.InstanceId == target.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, x => x.InstanceId == target.InstanceId);
    }

    [Fact]
    public void FeralStrength_Repeat_GivesPlusFourMight()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032603);
        var player = session.Players[0];
        ResetPlayer(player);

        var ally = BuildUnit(0, 0, "Ally", 3);
        player.BaseZone.Cards.Add(ally);
        player.BaseZone.Cards.AddRange(
            Enumerable.Range(0, 4).Select(i => BuildRuneInstance(951200 + i, "Calm Rune", "Calm", 0))
        );
        var feral = BuildCardInstance(
            new RiftboundCard
            {
                Id = 951210,
                Name = "Feral Strength",
                Type = "Spell",
                Cost = 2,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Reaction", "Repeat"],
                Effect = "[Repeat 2] Give a unit +2 [Might] this turn.",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(feral);

        foreach (var rune in player.BaseZone.Cards.Where(x => x.Type == "Rune").ToList())
        {
            var activate = engine.GetLegalActions(session)
                .First(x => x.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activate).Succeeded);
        }

        var play = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.PlayCard
                && x.ActionId.Contains(feral.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(ally.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-repeat", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, play).Succeeded);

        Assert.Equal(4, ally.TemporaryMightModifier);
        Assert.Equal(0, player.RunePool.Energy);
    }

    [Fact]
    public void FindYourCenter_IsDiscountedByTwo_WhenOpponentNearVictory()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032604);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        opponent.Score = 6;
        player.BaseZone.Cards.Add(BuildRuneInstance(951300, "Calm Rune", "Calm", 0));
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Drawn Unit", 1));
        player.RuneDeckZone.Cards.Add(BuildRuneInstance(951301, "Calm Rune", "Calm", 0));

        var center = BuildCardInstance(
            new RiftboundCard
            {
                Id = 951310,
                Name = "Find Your Center",
                Type = "Spell",
                Cost = 3,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Action"],
                Effect = "If an opponent's score is within 3 points of the Victory Score, this costs [2] less. Draw 1 and channel 1 rune exhausted.",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(center);

        var activate = engine.GetLegalActions(session)
            .First(x => x.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        Assert.True(engine.ApplyAction(session, activate).Succeeded);

        var play = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.PlayCard
                && x.ActionId.Contains(center.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, play).Succeeded);

        Assert.Equal(0, player.RunePool.Energy);
        Assert.Contains(player.HandZone.Cards, x => string.Equals(x.Name, "Drawn Unit", StringComparison.Ordinal));
        Assert.Contains(player.BaseZone.Cards, x => x.CardId == 951301 && x.IsExhausted);
    }

    [Fact]
    public void Flash_MovesTwoFriendlyUnitsToBase()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032605);
        var player = session.Players[0];
        ResetPlayer(player);

        var unitA = BuildUnit(0, 0, "Unit A", 2);
        var unitB = BuildUnit(0, 0, "Unit B", 3);
        session.Battlefields[0].Units.Add(unitA);
        session.Battlefields[0].Units.Add(unitB);

        var flash = BuildCardInstance(
            new RiftboundCard
            {
                Id = 951410,
                Name = "Flash",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Chaos"],
                GameplayKeywords = ["Reaction"],
                Effect = "Move up to 2 friendly units to base.",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(flash);

        var play = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.PlayCard
                && x.ActionId.Contains(flash.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(unitA.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(unitB.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, play).Succeeded);

        Assert.DoesNotContain(session.Battlefields[0].Units, x => x.InstanceId == unitA.InstanceId);
        Assert.DoesNotContain(session.Battlefields[0].Units, x => x.InstanceId == unitB.InstanceId);
        Assert.Contains(player.BaseZone.Cards, x => x.InstanceId == unitA.InstanceId);
        Assert.Contains(player.BaseZone.Cards, x => x.InstanceId == unitB.InstanceId);
    }

    [Fact]
    public void GarbageGrabber_Activate_RecyclesThreeAndDrawsOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032606);
        var player = session.Players[0];
        ResetPlayer(player);

        player.RunePool.Energy = 1;
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Top Deck Unit", 1));
        player.TrashZone.Cards.AddRange(
            new[]
            {
                BuildUnit(0, 0, "Trash A", 1),
                BuildUnit(0, 0, "Trash B", 1),
                BuildUnit(0, 0, "Trash C", 1),
                BuildUnit(0, 0, "Trash D", 1),
            }
        );

        var gear = BuildCardInstance(
            new RiftboundCard
            {
                Id = 951510,
                Name = "Garbage Grabber",
                Type = "Gear",
                Cost = 2,
                Power = 0,
                Color = ["Mind"],
                Effect = "Recycle 3 from your trash, [1], [Tap]: Draw 1.",
            },
            0,
            0
        );
        player.BaseZone.Cards.Add(gear);

        var activate = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.ActivateRune
                && x.ActionId.Contains(gear.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, activate).Succeeded);

        Assert.True(gear.IsExhausted);
        Assert.Single(player.TrashZone.Cards);
        Assert.Single(player.HandZone.Cards);
    }

    [Fact]
    public void FlameChompers_WhenDiscarded_CanPlayItselfByPayingFury()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032607);
        var player = session.Players[0];
        ResetPlayer(player);

        player.BaseZone.Cards.Add(BuildRuneInstance(951600, "Fury Rune", "Fury", 0));

        var flame = BuildCardInstance(
            new RiftboundCard
            {
                Id = 951610,
                Name = "Flame Chompers",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 3,
                Color = ["Fury"],
                Effect = "When you discard me, you may pay [Fury] to play me.",
            },
            0,
            0
        );
        var enforcer = BuildCardInstance(
            new RiftboundCard
            {
                Id = 951611,
                Name = "Chemtech Enforcer",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 2,
                Color = ["Fury"],
                GameplayKeywords = ["Assault"],
                Effect = "When you play me, discard 1.",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(flame);
        player.HandZone.Cards.Add(enforcer);

        var play = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.PlayCard
                && x.ActionId.Contains(enforcer.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, play).Succeeded);

        Assert.Contains(player.BaseZone.Cards, x => x.InstanceId == flame.InstanceId);
        Assert.DoesNotContain(player.TrashZone.Cards, x => x.InstanceId == flame.InstanceId);
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
            new RiftboundCard { Id = 151, Name = "Fae Porter", Type = "Unit", Effect = "When I move to a battlefield, you may pay [Chaos] to move a unit you control to the same battlefield." },
            new RiftboundCard { Id = 287, Name = "Minotaur Reckoner", Type = "Unit", Effect = "Units can't move to base." },
            new RiftboundCard { Id = 153, Name = "Falling Comet", Type = "Spell", Effect = "Deal 6 to a unit at a battlefield." },
            new RiftboundCard { Id = 154, Name = "Falling Star", Type = "Spell", Effect = "Deal 3 to a unit. Deal 3 to a unit." },
            new RiftboundCard { Id = 155, Name = "Feral Strength", Type = "Spell", Effect = "[Repeat 2] Give a unit +2 [Might] this turn." },
            new RiftboundCard { Id = 156, Name = "Ferrous Forerunner", Type = "Unit", Effect = "[Deathknell] - Play two 3 [Might] Mech unit tokens to your base.", GameplayKeywords = ["Deathknell"] },
            new RiftboundCard { Id = 157, Name = "Fight or Flight", Type = "Spell", Effect = "Move a unit from a battlefield to its base.", GameplayKeywords = ["Action", "Hidden"] },
            new RiftboundCard { Id = 158, Name = "Final Spark", Type = "Spell", Effect = "Deal 8 to a unit.", GameplayKeywords = ["Action"] },
            new RiftboundCard { Id = 159, Name = "Find Your Center", Type = "Spell", Effect = "If an opponent's score is within 3 points of the Victory Score, this costs [2] less. Draw 1 and channel 1 rune exhausted.", GameplayKeywords = ["Action"] },
            new RiftboundCard { Id = 160, Name = "Fiora, Grand Duelist", Type = "Legend", Effect = "When one of your units becomes [Mighty], you may exhaust me to channel 1 rune exhausted." },
            new RiftboundCard { Id = 161, Name = "Fiora, Peerless", Type = "Unit", Effect = "When I attack or defend one on one, double my Might this combat." },
            new RiftboundCard { Id = 162, Name = "Fiora, Victorious", Type = "Unit", Effect = "While I'm [Mighty], I have [Deflect], [Ganking] and [Shield].", GameplayKeywords = ["Deflect", "Ganking", "Mighty", "Shield"] },
            new RiftboundCard { Id = 163, Name = "Fiora, Worthy", Type = "Unit", Effect = "When a unit you control becomes [Mighty], you may pay [Order] to ready it.", GameplayKeywords = ["Mighty"] },
            new RiftboundCard { Id = 164, Name = "Firestorm", Type = "Spell", Effect = "Deal 3 to all enemy units at a battlefield." },
            new RiftboundCard { Id = 165, Name = "First Mate", Type = "Unit", Effect = "When you play me, ready another unit." },
            new RiftboundCard { Id = 166, Name = "Fizz, Trickster", Type = "Unit", Effect = "When you play me, you may play a spell from your trash with Energy cost no more than [3], ignoring its Energy cost. Recycle that spell after you play it." },
            new RiftboundCard { Id = 167, Name = "Flame Chompers", Type = "Unit", Effect = "When you discard me, you may pay [Fury] to play me." },
            new RiftboundCard { Id = 168, Name = "Flash", Type = "Spell", Effect = "Move up to 2 friendly units to base.", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 169, Name = "Flurry of Blades", Type = "Spell", Effect = "Deal 1 to all units at battlefields.", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 170, Name = "Forecaster", Type = "Unit", Effect = "Your Mechs have [Vision]." },
            new RiftboundCard { Id = 171, Name = "Forge of the Fluft", Type = "Battlefield", Effect = "Friendly legends gain [Tap]: Attach an Equipment you control to a unit you control." },
            new RiftboundCard { Id = 172, Name = "Forge of the Future", Type = "Gear", Effect = "When you play this, play a 1 [Might] Recruit unit token at your base. Kill this: Recycle up to 4 cards from trashes." },
            new RiftboundCard { Id = 173, Name = "Forgefire Cape", Type = "Gear", Effect = "When I attack or defend, deal 2 to all enemy units here. +3 [Might]", GameplayKeywords = ["Equip", "Unique"] },
            new RiftboundCard { Id = 174, Name = "Forgotten Monument", Type = "Battlefield", Effect = "Players can't score here until their third turn." },
            new RiftboundCard { Id = 175, Name = "Fortified Position", Type = "Battlefield", Effect = "When you defend here, choose a unit. It gains [Shield 2] this combat." },
            new RiftboundCard { Id = 176, Name = "Fox-Fire", Type = "Spell", Effect = "Kill any number of units at a battlefield with total Might 4 or less.", GameplayKeywords = ["Action", "Hidden"] },
            new RiftboundCard { Id = 177, Name = "Frigid Touch", Type = "Spell", Effect = "[Repeat][2] Give a unit -2 [Might] this turn.", GameplayKeywords = ["Reaction", "Repeat"] },
            new RiftboundCard { Id = 178, Name = "Frostcoat Cub", Type = "Unit", Effect = "You may pay [Mind] as an additional cost to play me. When you play me, if you paid the additional cost, give a unit -2 this turn." },
            new RiftboundCard { Id = 179, Name = "Fury Rune", Type = "Rune", Color = ["Fury"] },
            new RiftboundCard { Id = 180, Name = "Garbage Grabber", Type = "Gear", Effect = "Recycle 3 from your trash, [1], [Tap]: Draw 1." },
        ];
    }
}
