using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineStackedDeckBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void StackedDeck_WithNocturneInLookWindow_PlaysNocturneForChaosPower()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031611,
                RiftboundSimulationTestData.BuildDeck(9971, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9972, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
        player.BaseZone.Cards.Add(BuildRuneInstance(421_013, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(421_014, "Fury Rune", "Fury", ownerPlayer: 0));

        var chaosRune = BuildRuneInstance(205_100, "Chaos Rune", "Chaos", ownerPlayer: 0);
        player.BaseZone.Cards.Add(chaosRune);

        var stackedDeck = BuildCardInstance(
            new RiftboundCard
            {
                Id = 405_100,
                Name = "Stacked Deck",
                Type = "Spell",
                Cost = 1,
                Power = 0,
                Color = ["Chaos"],
                Effect = "[Action] (play on your turn or in showdowns.) Look at the top 3 cards of your Main Deck. Put 1 into your hand and recycle the rest.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(stackedDeck);

        var nocturne = BuildCardInstance(
            new RiftboundCard
            {
                Id = 405_200,
                Name = "Nocturne, Horrifying",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 4,
                Power = 1,
                Might = 4,
                Color = ["Chaos"],
                Tags = ["Nocturne"],
                GameplayKeywords = ["Ganking"],
                Effect = "[Ganking] As you look at or reveal me from the top of your deck, you may banish me. If you do, you may play me for [Rune].",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var fillerA = BuildCardInstance(
            new RiftboundCard
            {
                Id = 405_201,
                Name = "Filler A",
                Type = "Unit",
                Cost = 2,
                Power = 0,
                Might = 2,
                Color = ["Chaos"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var fillerB = BuildCardInstance(
            new RiftboundCard
            {
                Id = 405_202,
                Name = "Filler B",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 1,
                Color = ["Chaos"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(nocturne);
        player.MainDeckZone.Cards.Add(fillerA);
        player.MainDeckZone.Cards.Add(fillerB);

        var activateRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(chaosRune.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRuneAction).Succeeded);

        var baseRuneCountBefore = player.BaseZone.Cards.Count(c =>
            string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase)
        );
        var runeDeckCountBefore = player.RuneDeckZone.Cards.Count;

        var playStackedDeckAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(stackedDeck.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playStackedDeckAction).Succeeded);

        Assert.DoesNotContain(player.MainDeckZone.Cards, c => c.InstanceId == nocturne.InstanceId);
        Assert.Contains(
            session.Battlefields.SelectMany(b => b.Units),
            c => c.InstanceId == nocturne.InstanceId
        );
        Assert.Equal(
            baseRuneCountBefore - 1,
            player.BaseZone.Cards.Count(c => string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase))
        );
        Assert.Equal(runeDeckCountBefore + 1, player.RuneDeckZone.Cards.Count);

        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Nocturne, Horrifying"
                && c.Timing == "RevealPlay"
                && c.Metadata.TryGetValue("sourceCard", out var source)
                && source == "Stacked Deck"
        );
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Stacked Deck"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("playedFromReveal", out var played)
                && played == "1"
        );
    }
}

