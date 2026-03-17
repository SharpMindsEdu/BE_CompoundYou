using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineVoidRushBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void VoidRush_PlaysRevealedCardWithEnergyReduction_AndDrawsRemaining()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031628,
                RiftboundSimulationTestData.BuildDeck(10013, "Fury"),
                RiftboundSimulationTestData.BuildDeck(10014, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        player.BaseZone.Cards.Add(BuildRuneInstance(420_100, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(420_101, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(420_102, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(420_103, "Fury Rune", "Fury", ownerPlayer: 0));

        var voidRush = BuildCardInstance(
            new RiftboundCard
            {
                Id = 420_200,
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

        var expensiveUnit = BuildCardInstance(
            new RiftboundCard
            {
                Id = 420_201,
                Name = "Deep Ambusher",
                Type = "Unit",
                Cost = 3,
                Power = 0,
                Might = 4,
                Color = ["Fury"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var fallbackDraw = BuildCardInstance(
            new RiftboundCard
            {
                Id = 420_202,
                Name = "Fallback Draw",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Fury"],
                Effect = "Draw 1.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(expensiveUnit);
        player.MainDeckZone.Cards.Add(fallbackDraw);

        var runeDeckBefore = player.RuneDeckZone.Cards.Count;
        var playAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(voidRush.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(expensiveUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playAction).Succeeded);

        Assert.DoesNotContain(player.MainDeckZone.Cards, c => c.InstanceId == expensiveUnit.InstanceId);
        Assert.Contains(
            session.Battlefields.SelectMany(x => x.Units),
            c => c.InstanceId == expensiveUnit.InstanceId
        );
        Assert.Contains(player.HandZone.Cards, c => c.InstanceId == fallbackDraw.InstanceId);
        Assert.Equal(runeDeckBefore + 1, player.RuneDeckZone.Cards.Count);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Void Rush"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("playedFromReveal", out var played)
                && played == "1"
                && c.Metadata.TryGetValue("drawn", out var drawn)
                && drawn == "1"
        );
    }

    [Fact]
    public void VoidRush_CanChooseWhichRevealedCardToPlay_AndDrawsTheOther()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031638,
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

        player.BaseZone.Cards.Add(BuildRuneInstance(423_100, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(423_101, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(423_102, "Fury Rune", "Fury", ownerPlayer: 0));

        var voidRush = BuildCardInstance(
            new RiftboundCard
            {
                Id = 423_200,
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

        var firstLooked = BuildCardInstance(
            new RiftboundCard
            {
                Id = 423_201,
                Name = "First Looked Unit",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 2,
                Color = ["Fury"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var secondLooked = BuildCardInstance(
            new RiftboundCard
            {
                Id = 423_202,
                Name = "Second Looked Unit",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 3,
                Color = ["Fury"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(firstLooked);
        player.MainDeckZone.Cards.Add(secondLooked);

        var playAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(voidRush.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(secondLooked.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;

        Assert.True(engine.ApplyAction(session, playAction).Succeeded);

        Assert.Contains(
            session.Battlefields.SelectMany(x => x.Units),
            card => card.InstanceId == secondLooked.InstanceId
        );
        Assert.Contains(player.HandZone.Cards, card => card.InstanceId == firstLooked.InstanceId);
        Assert.DoesNotContain(player.MainDeckZone.Cards, card => card.InstanceId == firstLooked.InstanceId);
        Assert.DoesNotContain(player.MainDeckZone.Cards, card => card.InstanceId == secondLooked.InstanceId);
    }
}

