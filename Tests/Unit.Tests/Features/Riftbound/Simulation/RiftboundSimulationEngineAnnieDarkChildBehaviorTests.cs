using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineAnnieDarkChildBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AnnieDarkChild_EndTurn_ReadiesUpToTwoRunes()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031714,
                RiftboundSimulationTestData.BuildDeck(9740, "Fury"),
                RiftboundSimulationTestData.BuildDeck(9741, "Order")
            )
        );

        var player = session.Players[0];
        player.LegendZone.Cards.Clear();
        player.BaseZone.Cards.Clear();

        var annieLegend = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9740_100,
                Name = "Annie, Dark Child",
                Type = "Legend",
                Cost = 0,
                Power = 0,
                Color = ["Fury", "Chaos"],
                Effect = "At the end of your turn, ready up to 2 runes.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.LegendZone.Cards.Add(annieLegend);

        var runeA = BuildRuneInstance(9740_101, "Fury Rune", "Fury", ownerPlayer: 0);
        var runeB = BuildRuneInstance(9740_102, "Chaos Rune", "Chaos", ownerPlayer: 0);
        runeA.IsExhausted = true;
        runeB.IsExhausted = true;
        player.BaseZone.Cards.Add(runeA);
        player.BaseZone.Cards.Add(runeB);

        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);
        Assert.False(runeA.IsExhausted);
        Assert.False(runeB.IsExhausted);
    }
}

