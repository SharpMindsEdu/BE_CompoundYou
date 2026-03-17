using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineAspirantsClimbBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AspirantsClimb_IncreasesVictoryThresholdByOne()
    {
        var challenger = RiftboundSimulationTestData.BuildDeck(
            9840,
            "Order",
            deck =>
            {
                foreach (var battlefield in deck.Battlefields)
                {
                    battlefield.Card!.Name = "Aspirant's Climb";
                }
            }
        );
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031724,
                challenger,
                RiftboundSimulationTestData.BuildDeck(9841, "Chaos")
            )
        );

        session.Players[0].Score = 8;
        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);
        Assert.NotEqual(RiftboundTurnPhase.Completed, session.Phase);

        session.Players[1].Score = 9;
        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);
        Assert.Equal(RiftboundTurnPhase.Completed, session.Phase);
    }
}

