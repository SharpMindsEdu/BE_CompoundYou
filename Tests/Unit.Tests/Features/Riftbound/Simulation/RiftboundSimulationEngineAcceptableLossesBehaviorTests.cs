using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineAcceptableLossesBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AcceptableLosses_KillsSelectedFriendlyGear_AndOneOpponentGear()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031623,
                RiftboundSimulationTestData.BuildDeck(9995, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9996, "Order")
            )
        );

        var player = session.Players[0];
        var opponent = session.Players[1];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
        opponent.BaseZone.Cards.Clear();
        opponent.TrashZone.Cards.Clear();

        player.BaseZone.Cards.Add(BuildRuneInstance(217_100, "Chaos Rune", "Chaos", ownerPlayer: 0));

        var acceptableLosses = BuildCardInstance(
            new RiftboundCard
            {
                Id = 417_100,
                Name = "Acceptable Losses",
                Type = "Spell",
                Cost = 1,
                Power = 0,
                Color = ["Chaos"],
                GameplayKeywords = ["Action"],
                Effect = "[ACTION] Each player kills one of their gear.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(acceptableLosses);

        var friendlyGearA = BuildCardInstance(
            new RiftboundCard
            {
                Id = 417_200,
                Name = "Friendly Gear A",
                Type = "Gear",
                Cost = 1,
                Power = 0,
                Color = ["Chaos"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var friendlyGearB = BuildCardInstance(
            new RiftboundCard
            {
                Id = 417_201,
                Name = "Friendly Gear B",
                Type = "Gear",
                Cost = 1,
                Power = 0,
                Color = ["Chaos"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.BaseZone.Cards.Add(friendlyGearA);
        player.BaseZone.Cards.Add(friendlyGearB);

        var opponentGearA = BuildCardInstance(
            new RiftboundCard
            {
                Id = 417_300,
                Name = "Opponent Gear A",
                Type = "Gear",
                Cost = 1,
                Power = 0,
                Color = ["Order"],
            },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        var opponentGearB = BuildCardInstance(
            new RiftboundCard
            {
                Id = 417_301,
                Name = "Opponent Gear B",
                Type = "Gear",
                Cost = 1,
                Power = 0,
                Color = ["Order"],
            },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        opponent.BaseZone.Cards.Add(opponentGearA);
        opponent.BaseZone.Cards.Add(opponentGearB);

        var activateRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains("activate-rune", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRuneAction).Succeeded);

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(acceptableLosses.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(friendlyGearB.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.DoesNotContain(player.BaseZone.Cards, x => x.InstanceId == friendlyGearB.InstanceId);
        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == friendlyGearB.InstanceId);
        Assert.Single(
            opponent.TrashZone.Cards,
            x => string.Equals(x.Type, "Gear", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Single(
            opponent.BaseZone.Cards,
            x => string.Equals(x.Type, "Gear", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Acceptable Losses"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("killedFriendlyGear", out var killedFriendly)
                && killedFriendly == "true"
                && c.Metadata.TryGetValue("killedOpponentGear", out var killedOpponent)
                && killedOpponent == "true"
        );
    }
}

