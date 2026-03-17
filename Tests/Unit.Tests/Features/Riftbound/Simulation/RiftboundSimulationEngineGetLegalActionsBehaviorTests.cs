using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineGetLegalActionsBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void GetLegalActions_InActionPhase_ContainsRuneAndEndTurn()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                simulationId: 1,
                userId: 9,
                seed: 123,
                challengerDeck: RiftboundSimulationTestData.BuildDeck(1, "Chaos"),
                opponentDeck: RiftboundSimulationTestData.BuildDeck(2, "Order")
            )
        );

        var legalActions = engine.GetLegalActions(session);

        Assert.Contains(legalActions, a => a.ActionType == RiftboundActionType.ActivateRune);
        Assert.Contains(legalActions, a => a.ActionId == "end-turn");
    }

    [Fact]
    public void GetLegalActions_AccelerateRequiresMatchingPowerColor()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                4568,
                RiftboundSimulationTestData.BuildDeck(61, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(62, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        player.BaseZone.Cards.Add(BuildRuneInstance(620_100, "Mind Rune", "Mind", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(620_101, "Mind Rune", "Mind", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(620_102, "Mind Rune", "Mind", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(620_103, "Mind Rune", "Mind", ownerPlayer: 0));

        var acceleratedUnit = BuildCardInstance(
            new RiftboundCard
            {
                Id = 620_200,
                Name = "Rek'Sai, Breacher",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 3,
                Power = 0,
                Might = 3,
                Color = ["Fury"],
                GameplayKeywords = ["Accelerate", "Assault"],
                Effect = "[ACCELERATE] (You may pay [1] [Fury] as an additional cost to have me enter ready.)",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(acceleratedUnit);

        var actions = engine.GetLegalActions(session);
        Assert.Contains(
            actions,
            a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(acceleratedUnit.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            actions,
            a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(acceleratedUnit.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains("-accelerate-", StringComparison.Ordinal)
        );
    }
}

