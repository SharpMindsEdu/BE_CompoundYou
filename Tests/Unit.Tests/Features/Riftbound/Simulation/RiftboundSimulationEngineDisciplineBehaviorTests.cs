using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineDisciplineBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void Discipline_TargetsAnyUnit_BuffsTargetAndDrawsOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031613,
                RiftboundSimulationTestData.BuildDeck(9975, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9976, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var calmRuneA = BuildRuneInstance(207_100, "Calm Rune", "Calm", ownerPlayer: 0);
        var calmRuneB = BuildRuneInstance(207_101, "Calm Rune", "Calm", ownerPlayer: 0);
        player.BaseZone.Cards.Add(calmRuneA);
        player.BaseZone.Cards.Add(calmRuneB);

        var discipline = BuildCardInstance(
            new RiftboundCard
            {
                Id = 407_100,
                Name = "Discipline",
                Type = "Spell",
                Cost = 2,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] Give a unit +2 [Might] this turn. Draw 1.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(discipline);

        var drawCard = BuildCardInstance(
            new RiftboundCard
            {
                Id = 407_101,
                Name = "Draw Target",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 1,
                Color = ["Calm"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(drawCard);

        var myUnit = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Target", might: 2);
        session.Battlefields[1].Units.Add(myUnit);

        foreach (var rune in new[] { calmRuneA, calmRuneB })
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
                && a.ActionId.Contains(discipline.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(myUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(2, myUnit.TemporaryMightModifier);
        Assert.Contains(player.HandZone.Cards, c => c.InstanceId == drawCard.InstanceId);
        Assert.DoesNotContain(player.MainDeckZone.Cards, c => c.InstanceId == drawCard.InstanceId);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Discipline"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("draw", out var draw)
                && draw == "1"
        );
    }
}

