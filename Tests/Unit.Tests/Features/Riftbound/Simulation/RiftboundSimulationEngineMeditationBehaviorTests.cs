using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineMeditationBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void Meditation_WithoutExhaustAdditionalCost_DrawsOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031621,
                RiftboundSimulationTestData.BuildDeck(9991, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9992, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var calmRunes = Enumerable
            .Range(0, 2)
            .Select(i => BuildRuneInstance(215_100 + i, "Calm Rune", "Calm", ownerPlayer: 0))
            .ToList();
        player.BaseZone.Cards.AddRange(calmRunes);

        var meditation = BuildCardInstance(
            new RiftboundCard
            {
                Id = 415_100,
                Name = "Meditation",
                Type = "Spell",
                Cost = 2,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] As an additional cost to play this, you may exhaust a friendly unit. If you do, draw 2. Otherwise, draw 1.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(meditation);

        player.MainDeckZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard
                {
                    Id = 415_200,
                    Name = "Draw A",
                    Type = "Unit",
                    Cost = 1,
                    Power = 0,
                    Might = 1,
                    Color = ["Calm"],
                },
                ownerPlayer: 0,
                controllerPlayer: 0
            )
        );
        player.MainDeckZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard
                {
                    Id = 415_201,
                    Name = "Draw B",
                    Type = "Unit",
                    Cost = 1,
                    Power = 0,
                    Might = 1,
                    Color = ["Calm"],
                },
                ownerPlayer: 0,
                controllerPlayer: 0
            )
        );

        foreach (var rune in calmRunes)
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
                && a.ActionId.Contains(meditation.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Single(player.HandZone.Cards);
        Assert.Single(player.MainDeckZone.Cards);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Meditation"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("draw", out var draw)
                && draw == "1"
                && c.Metadata.TryGetValue("usedAdditionalCost", out var used)
                && used == "false"
        );
    }

    [Fact]
    public void Meditation_WithExhaustAdditionalCost_DrawsTwo()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031622,
                RiftboundSimulationTestData.BuildDeck(9993, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9994, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var calmRunes = Enumerable
            .Range(0, 2)
            .Select(i => BuildRuneInstance(216_100 + i, "Calm Rune", "Calm", ownerPlayer: 0))
            .ToList();
        player.BaseZone.Cards.AddRange(calmRunes);

        var meditation = BuildCardInstance(
            new RiftboundCard
            {
                Id = 416_100,
                Name = "Meditation",
                Type = "Spell",
                Cost = 2,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] As an additional cost to play this, you may exhaust a friendly unit. If you do, draw 2. Otherwise, draw 1.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(meditation);

        var friendlyUnit = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Meditator", might: 2);
        player.BaseZone.Cards.Add(friendlyUnit);

        player.MainDeckZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard
                {
                    Id = 416_200,
                    Name = "Draw One",
                    Type = "Unit",
                    Cost = 1,
                    Power = 0,
                    Might = 1,
                    Color = ["Calm"],
                },
                ownerPlayer: 0,
                controllerPlayer: 0
            )
        );
        player.MainDeckZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard
                {
                    Id = 416_201,
                    Name = "Draw Two",
                    Type = "Unit",
                    Cost = 1,
                    Power = 0,
                    Might = 1,
                    Color = ["Calm"],
                },
                ownerPlayer: 0,
                controllerPlayer: 0
            )
        );
        player.MainDeckZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard
                {
                    Id = 416_202,
                    Name = "Draw Three",
                    Type = "Unit",
                    Cost = 1,
                    Power = 0,
                    Might = 1,
                    Color = ["Calm"],
                },
                ownerPlayer: 0,
                controllerPlayer: 0
            )
        );

        foreach (var rune in calmRunes)
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
                && a.ActionId.Contains(meditation.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains($"-exhaust-unit-{friendlyUnit.InstanceId}", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.True(friendlyUnit.IsExhausted);
        Assert.Equal(2, player.HandZone.Cards.Count);
        Assert.Single(player.MainDeckZone.Cards);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Meditation"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("draw", out var draw)
                && draw == "2"
                && c.Metadata.TryGetValue("usedAdditionalCost", out var used)
                && used == "true"
                && c.Metadata.TryGetValue("exhaustedUnit", out var exhausted)
                && exhausted == "Meditator"
        );
    }
}

