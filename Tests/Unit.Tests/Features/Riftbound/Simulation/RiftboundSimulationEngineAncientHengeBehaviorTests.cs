using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineAncientHengeBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AncientHenge_ActivatedAbility_ConvertsAllEnergyToGenericRunePower()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031710,
                RiftboundSimulationTestData.BuildDeck(9700, "Body"),
                RiftboundSimulationTestData.BuildDeck(9701, "Order")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();
        player.HandZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var henge = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9700_100,
                Name = "Ancient Henge",
                Type = "Gear",
                Cost = 2,
                Power = 1,
                Color = ["Body"],
                GameplayKeywords = ["Reaction"],
                Effect = "[Tap]: [Reaction] — Pay any amount of Energy to [Add] that much [Rune].",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.BaseZone.Cards.Add(henge);
        player.BaseZone.Cards.Add(BuildRuneInstance(9700_101, "Body Rune", "Body", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(9700_102, "Body Rune", "Body", ownerPlayer: 0));

        for (var i = 0; i < 2; i += 1)
        {
            var activateRune = engine
                .GetLegalActions(session)
                .First(a => a.ActionType == RiftboundActionType.ActivateRune && a.ActionId.Contains("activate-rune", StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateRune).Succeeded);
        }

        var activateHenge = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.ActivateRune
                && a.ActionId.Contains(henge.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateHenge).Succeeded);

        Assert.True(henge.IsExhausted);
        Assert.Equal(0, player.RunePool.Energy);
        Assert.True(player.RunePool.PowerByDomain.TryGetValue("__unknown__", out var genericPower));
        Assert.Equal(2, genericPower);
    }
}

