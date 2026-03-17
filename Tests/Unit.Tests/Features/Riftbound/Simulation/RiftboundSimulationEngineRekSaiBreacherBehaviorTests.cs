using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineRekSaiBreacherBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void RekSaiBreacher_GrantsAccelerateToUnitPlayedFromReveal()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031629,
                RiftboundSimulationTestData.BuildDeck(10015, "Fury"),
                RiftboundSimulationTestData.BuildDeck(10016, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
        player.BaseZone.Cards.Add(BuildRuneInstance(421_010, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(421_011, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(421_012, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(421_013, "Fury Rune", "Fury", ownerPlayer: 0));

        var breacher = BuildCardInstance(
            new RiftboundCard
            {
                Id = 421_100,
                Name = "Rek'Sai, Breacher",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 3,
                Power = 0,
                Might = 3,
                Color = ["Fury"],
                GameplayKeywords = ["Accelerate", "Assault"],
                Effect = "Friendly units played from anywhere other than a player's hand have [ACCELERATE].",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.BaseZone.Cards.Add(breacher);

        var voidRush = BuildCardInstance(
            new RiftboundCard
            {
                Id = 421_101,
                Name = "Void Rush",
                Type = "Spell",
                Supertype = "Signature",
                Cost = 2,
                Power = 1,
                Color = ["Fury", "Order"],
                Effect = "Reveal the top 2 cards of your Main Deck. You may banish one, then play it, reducing its cost by [2]. Draw any you didn't banish.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(voidRush);

        var revealedUnit = BuildCardInstance(
            new RiftboundCard
            {
                Id = 421_102,
                Name = "Burrowed Ally",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 2,
                Color = ["Fury"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var filler = BuildCardInstance(
            new RiftboundCard
            {
                Id = 421_103,
                Name = "Minor Tactic",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Fury"],
                Effect = "Draw 1.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(revealedUnit);
        player.MainDeckZone.Cards.Add(filler);

        var legalActions = engine.GetLegalActions(session);
        var selectedAction = legalActions.FirstOrDefault(a =>
            a.ActionType == RiftboundActionType.PlayCard
            && a.ActionId.Contains(voidRush.InstanceId.ToString(), StringComparison.Ordinal)
            && a.ActionId.Contains(revealedUnit.InstanceId.ToString(), StringComparison.Ordinal)
            && a.ActionId.EndsWith("-accelerate", StringComparison.Ordinal)
        );
        if (selectedAction is null)
        {
            var availableVoidRushActions = legalActions
                .Where(a =>
                    a.ActionType == RiftboundActionType.PlayCard
                    && a.ActionId.Contains(voidRush.InstanceId.ToString(), StringComparison.Ordinal)
                )
                .Select(a => a.ActionId)
                .ToList();
            breacher.EffectData.TryGetValue("grantAccelerateForNonHandPlay", out var breacherAura);
            Assert.Fail(
                $"Expected accelerate action for Void Rush reveal. Available actions: {string.Join(" | ", availableVoidRushActions)}; Breacher aura: {breacherAura ?? "<missing>"}"
            );
        }

        var playAction = selectedAction.ActionId;
        var runeDeckBefore = player.RuneDeckZone.Cards.Count;
        Assert.True(engine.ApplyAction(session, playAction).Succeeded);

        var playedUnit = session.Battlefields.SelectMany(x => x.Units).Single(x =>
            x.InstanceId == revealedUnit.InstanceId
        );
        Assert.False(playedUnit.IsExhausted);
        Assert.Equal(0, player.RunePool.Energy);
        Assert.Equal(runeDeckBefore + 2, player.RuneDeckZone.Cards.Count);
    }
}

