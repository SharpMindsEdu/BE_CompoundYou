using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineCalledShotBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void CalledShot_WithNocturneInLookWindow_PlaysNocturneForChaosPower()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031612,
                RiftboundSimulationTestData.BuildDeck(9973, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9974, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
        player.BaseZone.Cards.Add(BuildRuneInstance(206_100, "Chaos Rune", "Chaos", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(206_101, "Chaos Rune", "Chaos", ownerPlayer: 0));

        var calledShot = BuildCardInstance(
            new RiftboundCard
            {
                Id = 406_100,
                Name = "Called Shot",
                Type = "Spell",
                Cost = 0,
                Power = 1,
                Color = ["Chaos"],
                GameplayKeywords = ["Action", "Repeat"],
                Effect = "[ACTION] (Play on your turn or in showdowns.) [REPEAT] [CHAOS] (You may pay the additional cost to repeat this spell's effect.) Look at the top 2 cards of your Main Deck. Draw one and recycle the other.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(calledShot);

        var nocturne = BuildCardInstance(
            new RiftboundCard
            {
                Id = 406_200,
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
        var filler = BuildCardInstance(
            new RiftboundCard
            {
                Id = 406_201,
                Name = "Fallback Draw",
                Type = "Unit",
                Cost = 2,
                Power = 0,
                Might = 2,
                Color = ["Chaos"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(nocturne);
        player.MainDeckZone.Cards.Add(filler);

        var legalActions = engine.GetLegalActions(session);
        Assert.Contains(
            legalActions,
            a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(calledShot.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell-repeat", StringComparison.Ordinal)
        );

        var baseRuneCountBefore = player.BaseZone.Cards.Count(c =>
            string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase)
        );
        var runeDeckCountBefore = player.RuneDeckZone.Cards.Count;
        var playCalledShotAction = legalActions
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(calledShot.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell", StringComparison.Ordinal)
            )
            .ActionId;

        Assert.True(engine.ApplyAction(session, playCalledShotAction).Succeeded);

        Assert.DoesNotContain(player.MainDeckZone.Cards, c => c.InstanceId == nocturne.InstanceId);
        Assert.Contains(
            session.Battlefields.SelectMany(b => b.Units),
            c => c.InstanceId == nocturne.InstanceId
        );
        Assert.Contains(player.HandZone.Cards, c => c.InstanceId == filler.InstanceId);
        Assert.Equal(
            baseRuneCountBefore - 2,
            player.BaseZone.Cards.Count(c => string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase))
        );
        Assert.Equal(runeDeckCountBefore + 2, player.RuneDeckZone.Cards.Count);

        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Nocturne, Horrifying"
                && c.Timing == "RevealPlay"
                && c.Metadata.TryGetValue("sourceCard", out var source)
                && source == "Called Shot"
        );
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Called Shot"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("playedFromReveal", out var played)
                && played == "1"
        );
    }
}

