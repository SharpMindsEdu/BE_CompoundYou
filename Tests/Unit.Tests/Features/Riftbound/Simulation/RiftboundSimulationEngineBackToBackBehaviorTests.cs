using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineBackToBackBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void BackToBack_TargetsTwoFriendlyUnits_BuffsSelectedUnits()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031619,
                RiftboundSimulationTestData.BuildDeck(9987, "Order"),
                RiftboundSimulationTestData.BuildDeck(9988, "Chaos")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var orderRunes = Enumerable
            .Range(0, 3)
            .Select(i => BuildRuneInstance(213_100 + i, "Order Rune", "Order", ownerPlayer: 0))
            .ToList();
        player.BaseZone.Cards.AddRange(orderRunes);

        var backToBack = BuildCardInstance(
            new RiftboundCard
            {
                Id = 413_100,
                Name = "Back to Back",
                Type = "Spell",
                Cost = 3,
                Power = 0,
                Color = ["Order"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] Give two friendly units each +2 [Might] this turn.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(backToBack);

        var baseTarget = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Base Target", might: 2);
        var battlefieldTarget = BuildUnit(
            ownerPlayer: 0,
            controllerPlayer: 0,
            name: "Battlefield Target",
            might: 2
        );
        var notSelected = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Not Selected", might: 2);
        player.BaseZone.Cards.Add(baseTarget);
        player.BaseZone.Cards.Add(notSelected);
        session.Battlefields[0].Units.Add(battlefieldTarget);

        foreach (var rune in orderRunes)
        {
            var activateAction = engine
                .GetLegalActions(session)
                .First(a => a.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateAction).Succeeded);
        }

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(backToBack.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(baseTarget.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(battlefieldTarget.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(2, baseTarget.TemporaryMightModifier);
        Assert.Equal(2, battlefieldTarget.TemporaryMightModifier);
        Assert.Equal(0, notSelected.TemporaryMightModifier);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Back to Back"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("targets", out var targets)
                && targets == "2"
        );
    }
}

