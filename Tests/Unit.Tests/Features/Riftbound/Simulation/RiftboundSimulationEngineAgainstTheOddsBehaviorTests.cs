using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineAgainstTheOddsBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AgainstTheOdds_TargetsOnlyFriendlyBattlefieldUnits_AndBuffsByEnemyCount()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031620,
                RiftboundSimulationTestData.BuildDeck(9989, "Fury"),
                RiftboundSimulationTestData.BuildDeck(9990, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var furyRunes = Enumerable
            .Range(0, 2)
            .Select(i => BuildRuneInstance(214_100 + i, "Fury Rune", "Fury", ownerPlayer: 0))
            .ToList();
        player.BaseZone.Cards.AddRange(furyRunes);

        var againstTheOdds = BuildCardInstance(
            new RiftboundCard
            {
                Id = 414_100,
                Name = "Against the Odds",
                Type = "Spell",
                Cost = 2,
                Power = 0,
                Color = ["Fury"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] Give a friendly unit at a battlefield +2 [Might] this turn for each enemy unit there.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(againstTheOdds);

        var friendlyBaseUnit = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Base Ally", might: 2);
        var friendlyBattlefieldUnit = BuildUnit(
            ownerPlayer: 0,
            controllerPlayer: 0,
            name: "Battle Ally",
            might: 2
        );
        player.BaseZone.Cards.Add(friendlyBaseUnit);
        session.Battlefields[1].Units.Add(friendlyBattlefieldUnit);
        session.Battlefields[1].Units.Add(BuildUnit(1, 1, "Enemy One", 2));
        session.Battlefields[1].Units.Add(BuildUnit(1, 1, "Enemy Two", 2));
        session.Battlefields[1].Units.Add(BuildUnit(1, 1, "Enemy Three", 2));

        foreach (var rune in furyRunes)
        {
            var activateAction = engine
                .GetLegalActions(session)
                .First(a => a.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateAction).Succeeded);
        }

        var legalActions = engine.GetLegalActions(session);
        Assert.DoesNotContain(
            legalActions,
            a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(againstTheOdds.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(friendlyBaseUnit.InstanceId.ToString(), StringComparison.Ordinal)
        );

        var castAction = legalActions
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(againstTheOdds.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(friendlyBattlefieldUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(6, friendlyBattlefieldUnit.TemporaryMightModifier);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Against the Odds"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("enemyUnits", out var enemyUnits)
                && enemyUnits == "3"
                && c.Metadata.TryGetValue("totalBuff", out var totalBuff)
                && totalBuff == "6"
        );
    }
}

