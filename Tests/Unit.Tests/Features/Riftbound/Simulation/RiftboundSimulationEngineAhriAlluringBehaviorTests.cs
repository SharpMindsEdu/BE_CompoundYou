using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineAhriAlluringBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AhriAlluring_WhenHoldingScoresAdditionalPoint()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031702,
                RiftboundSimulationTestData.BuildDeck(9620, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9621, "Order")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();
        session.Battlefields[0].Units.Clear();
        player.Score = 0;
        session.UsedScoringKeys.Clear();

        var ahriAlluring = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9620_100,
                Name = "Ahri, Alluring",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 0,
                Power = 0,
                Might = 4,
                Color = ["Calm"],
                Effect = "When I hold, you score 1 point.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        session.Battlefields[0].Units.Add(ahriAlluring);
        session.Battlefields[0].ControlledByPlayerIndex = 0;

        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);
        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);

        Assert.Equal(2, player.Score);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Ahri, Alluring"
                && c.Timing == "WhenHold"
                && c.Metadata.TryGetValue("bonusScore", out var bonus)
                && bonus == "1"
        );
    }
}

