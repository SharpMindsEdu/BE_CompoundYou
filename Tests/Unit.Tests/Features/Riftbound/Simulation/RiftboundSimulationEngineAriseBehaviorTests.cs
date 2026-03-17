using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineAriseBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void Arise_CreatesSandSoldiersForEachEquipment_AndReadiesUpToTwo()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031722,
                RiftboundSimulationTestData.BuildDeck(9820, "Order"),
                RiftboundSimulationTestData.BuildDeck(9821, "Chaos")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();

        var arise = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9820_100,
                Name = "Arise!",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Calm", "Order"],
                Effect = "Play a 2 [Might] Sand Soldier unit token for each Equipment you control. Then ready up to two of them.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(arise);
        player.BaseZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard
                {
                    Id = 9820_101,
                    Name = "Equip A",
                    Type = "Gear",
                    Cost = 0,
                    Power = 0,
                    GameplayKeywords = ["Equip"],
                    Color = ["Order"],
                    Effect = "[Equip]",
                },
                ownerPlayer: 0,
                controllerPlayer: 0
            )
        );
        player.BaseZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard
                {
                    Id = 9820_102,
                    Name = "Equip B",
                    Type = "Gear",
                    Cost = 0,
                    Power = 0,
                    GameplayKeywords = ["Equip"],
                    Color = ["Order"],
                    Effect = "[Equip]",
                },
                ownerPlayer: 0,
                controllerPlayer: 0
            )
        );

        var castAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(arise.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        var soldiers = player.BaseZone.Cards
            .Where(x => string.Equals(x.Name, "Sand Soldier Token", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(2, soldiers.Count);
        Assert.Equal(2, soldiers.Count(x => !x.IsExhausted));
    }
}

