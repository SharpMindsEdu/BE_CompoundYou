using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineAnnieFieryBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AnnieFiery_IncreasesSpellDamageByOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031715,
                RiftboundSimulationTestData.BuildDeck(9750, "Fury"),
                RiftboundSimulationTestData.BuildDeck(9751, "Order")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();
        player.HandZone.Cards.Clear();

        var annieFiery = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9750_100,
                Name = "Annie, Fiery",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 0,
                Power = 1,
                Might = 4,
                Color = ["Fury"],
                Effect = "Your spells and abilities deal 1 Bonus Damage.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.BaseZone.Cards.Add(annieFiery);

        var spell = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9750_101,
                Name = "Test Bolt",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Fury"],
                Effect = "Deal 1 damage to an enemy unit.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(spell);

        var enemy = BuildUnit(1, 1, "Enemy", 4);
        session.Battlefields[1].Units.Add(enemy);

        var castAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(spell.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);
        Assert.Equal(2, enemy.MarkedDamage);
    }
}

