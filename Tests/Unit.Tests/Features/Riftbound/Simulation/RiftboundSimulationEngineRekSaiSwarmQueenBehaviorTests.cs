using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineRekSaiSwarmQueenBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void RekSaiSwarmQueen_ActivatedInBattlefield_PlaysUnitHere_AndRecyclesRemaining()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031630,
                RiftboundSimulationTestData.BuildDeck(10017, "Order"),
                RiftboundSimulationTestData.BuildDeck(10018, "Fury")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var orderRuneA = BuildRuneInstance(422_100, "Order Rune", "Order", ownerPlayer: 0);
        var orderRuneB = BuildRuneInstance(422_101, "Order Rune", "Order", ownerPlayer: 0);
        player.BaseZone.Cards.Add(orderRuneA);
        player.BaseZone.Cards.Add(orderRuneB);

        var swarmQueen = BuildCardInstance(
            new RiftboundCard
            {
                Id = 422_200,
                Name = "Rek'Sai, Swarm Queen",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 5,
                Power = 1,
                Might = 5,
                Color = ["Order"],
                Effect = "When I attack, you may reveal the top 2 cards of your Main Deck. You may banish one, then play it. If it is a unit, you may play it here. Recycle the rest.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        session.Battlefields[0].Units.Add(swarmQueen);

        var revealedUnit = BuildCardInstance(
            new RiftboundCard
            {
                Id = 422_201,
                Name = "Xer'sai Vanguard",
                Type = "Unit",
                Cost = 2,
                Power = 0,
                Might = 3,
                Color = ["Order"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var recycledCard = BuildCardInstance(
            new RiftboundCard
            {
                Id = 422_202,
                Name = "Recycle Me",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 1,
                Color = ["Order"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(revealedUnit);
        player.MainDeckZone.Cards.Add(recycledCard);

        var runeDeckBefore = player.RuneDeckZone.Cards.Count;
        var activateAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(swarmQueen.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateAction).Succeeded);

        Assert.True(swarmQueen.IsExhausted);
        Assert.Contains(session.Battlefields[0].Units, c => c.InstanceId == revealedUnit.InstanceId);
        Assert.Single(player.MainDeckZone.Cards, c => c.InstanceId == recycledCard.InstanceId);
        Assert.Equal(runeDeckBefore, player.RuneDeckZone.Cards.Count);
        Assert.True(orderRuneA.IsExhausted);
        Assert.True(orderRuneB.IsExhausted);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Rek'Sai, Swarm Queen"
                && c.Timing == "WhenAttack"
                && c.Metadata.TryGetValue("playedFromReveal", out var played)
                && played == "1"
                && c.Metadata.TryGetValue("recycled", out var recycled)
                && recycled == "1"
        );
    }
}

