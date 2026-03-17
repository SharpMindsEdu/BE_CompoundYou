using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineRekSaiVoidBurrowerBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void RekSaiVoidBurrower_ActivatedAbility_ExhaustsAndPlaysRevealedCard()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031631,
                RiftboundSimulationTestData.BuildDeck(10019, "Fury"),
                RiftboundSimulationTestData.BuildDeck(10020, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var furyRune = BuildRuneInstance(423_100, "Fury Rune", "Fury", ownerPlayer: 0);
        player.BaseZone.Cards.Add(furyRune);

        var voidBurrower = BuildCardInstance(
            new RiftboundCard
            {
                Id = 423_200,
                Name = "Rek'Sai, Void Burrower",
                Type = "Legend",
                Cost = 0,
                Power = 0,
                Color = ["Fury", "Order"],
                Effect = "When you conquer, you may exhaust me to reveal the top 2 cards of your Main Deck. You may banish one, then play it. Recycle the rest.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.BaseZone.Cards.Add(voidBurrower);

        var revealedUnit = BuildCardInstance(
            new RiftboundCard
            {
                Id = 423_201,
                Name = "Tunnel Fighter",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 2,
                Color = ["Fury"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var recycledCard = BuildCardInstance(
            new RiftboundCard
            {
                Id = 423_202,
                Name = "Keep Cycling",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Order"],
                Effect = "Draw 1.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(revealedUnit);
        player.MainDeckZone.Cards.Add(recycledCard);

        var runeDeckBefore = player.RuneDeckZone.Cards.Count;
        var activateAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(voidBurrower.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateAction).Succeeded);

        Assert.True(voidBurrower.IsExhausted);
        Assert.DoesNotContain(player.MainDeckZone.Cards, c => c.InstanceId == revealedUnit.InstanceId);
        Assert.Contains(
            session.Battlefields.SelectMany(x => x.Units),
            c => c.InstanceId == revealedUnit.InstanceId
        );
        Assert.Single(player.MainDeckZone.Cards, c => c.InstanceId == recycledCard.InstanceId);
        Assert.Equal(runeDeckBefore, player.RuneDeckZone.Cards.Count);
        Assert.True(furyRune.IsExhausted);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Rek'Sai, Void Burrower"
                && c.Timing == "WhenConquer"
                && c.Metadata.TryGetValue("playedFromReveal", out var played)
                && played == "1"
                && c.Metadata.TryGetValue("recycled", out var recycled)
                && recycled == "1"
        );
    }
}

