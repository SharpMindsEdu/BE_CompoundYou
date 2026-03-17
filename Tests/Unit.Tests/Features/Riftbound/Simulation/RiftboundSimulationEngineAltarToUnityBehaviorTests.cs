using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineAltarToUnityBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AltarToUnity_WhenHolding_SpawnsRecruitToken()
    {
        var challenger = RiftboundSimulationTestData.BuildDeck(
            9680,
            "Order",
            deck =>
            {
                foreach (var battlefield in deck.Battlefields)
                {
                    battlefield.Card!.Name = "Altar to Unity";
                }
            }
        );
        var opponent = RiftboundSimulationTestData.BuildDeck(9681, "Chaos");
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(1, 9, 2026031708, challenger, opponent)
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();
        player.Score = 0;
        session.UsedScoringKeys.Clear();

        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);
        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);

        Assert.Equal(1, player.Score);
        Assert.Contains(
            player.BaseZone.Cards,
            c =>
                string.Equals(c.Name, "Recruit Token", StringComparison.OrdinalIgnoreCase)
                && c.Might == 1
                && c.IsExhausted
        );
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Altar to Unity"
                && c.Timing == "WhenHold"
                && c.Metadata.TryGetValue("playedRecruitToken", out var played)
                && played == "true"
        );
    }
}

