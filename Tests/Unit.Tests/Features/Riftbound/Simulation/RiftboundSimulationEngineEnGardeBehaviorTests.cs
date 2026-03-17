using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineEnGardeBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void EnGarde_OnlyFriendlyUnitThere_GivesPlusTwoMight()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031614,
                RiftboundSimulationTestData.BuildDeck(9977, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9978, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var calmRune = BuildRuneInstance(208_100, "Calm Rune", "Calm", ownerPlayer: 0);
        player.BaseZone.Cards.Add(calmRune);

        var enGarde = BuildCardInstance(
            new RiftboundCard
            {
                Id = 408_100,
                Name = "En Garde",
                Type = "Spell",
                Cost = 1,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] Give a friendly unit +1 [Might] this turn, then an additional +1 [Might] this turn if it is the only unit you control there.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(enGarde);

        var friendlyUnit = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Solo Friendly", might: 2);
        player.BaseZone.Cards.Add(friendlyUnit);
        var enemyUnit = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Enemy Unit", might: 2);
        session.Battlefields[1].Units.Add(enemyUnit);

        var activateRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(calmRune.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRuneAction).Succeeded);

        var legalActions = engine.GetLegalActions(session);
        Assert.DoesNotContain(
            legalActions,
            a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(enGarde.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(enemyUnit.InstanceId.ToString(), StringComparison.Ordinal)
        );

        var castAction = legalActions
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(enGarde.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(friendlyUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(2, friendlyUnit.TemporaryMightModifier);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "En Garde"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("onlyFriendlyThere", out var only)
                && only == "true"
        );
    }

    [Fact]
    public void EnGarde_WithAnotherFriendlyThere_GivesPlusOneMight()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031615,
                RiftboundSimulationTestData.BuildDeck(9979, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9980, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var calmRune = BuildRuneInstance(209_100, "Calm Rune", "Calm", ownerPlayer: 0);
        player.BaseZone.Cards.Add(calmRune);

        var enGarde = BuildCardInstance(
            new RiftboundCard
            {
                Id = 409_100,
                Name = "En Garde",
                Type = "Spell",
                Cost = 1,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] Give a friendly unit +1 [Might] this turn, then an additional +1 [Might] this turn if it is the only unit you control there.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(enGarde);

        var targetUnit = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Target Friendly", might: 2);
        var secondFriendly = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Other Friendly", might: 2);
        player.BaseZone.Cards.Add(targetUnit);
        player.BaseZone.Cards.Add(secondFriendly);

        var activateRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(calmRune.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRuneAction).Succeeded);

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(enGarde.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(targetUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(1, targetUnit.TemporaryMightModifier);
        Assert.Equal(0, secondFriendly.TemporaryMightModifier);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "En Garde"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("onlyFriendlyThere", out var only)
                && only == "false"
        );
    }
}

