using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineArmedAssailantBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void ArmedAssailant_OnPlay_ReattachesEquipmentToSelf()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031723,
                RiftboundSimulationTestData.BuildDeck(9830, "Fury"),
                RiftboundSimulationTestData.BuildDeck(9831, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.BaseZone.Cards.Add(BuildRuneInstance(9830_102, "Fury Rune", "Fury", ownerPlayer: 0));

        var oldUnit = BuildUnit(0, 0, "Old Unit", 2);
        player.BaseZone.Cards.Add(oldUnit);

        var equip = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9830_101,
                Name = "Shared Equipment",
                Type = "Gear",
                Cost = 0,
                Power = 0,
                Color = ["Fury"],
                GameplayKeywords = ["Equip"],
                Effect = "[Equip]",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        equip.AttachedToInstanceId = oldUnit.InstanceId;
        player.BaseZone.Cards.Add(equip);

        var assailant = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9830_100,
                Name = "Armed Assailant",
                Type = "Unit",
                Cost = 0,
                Power = 1,
                Might = 6,
                Color = ["Fury"],
                GameplayKeywords = ["Accelerate", "Equip", "Weaponmaster"],
                Effect = "[Weaponmaster]",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        Assert.Equal("named.armed-assailant", assailant.EffectTemplateId);
        player.HandZone.Cards.Add(assailant);

        var playResult = engine.ApplyAction(session, $"play-{assailant.InstanceId}-to-base");
        Assert.True(
            playResult.Succeeded,
            string.Join(" | ", playResult.LegalActions.Select(x => x.ActionId))
        );
        Assert.Equal(assailant.InstanceId, equip.AttachedToInstanceId);
    }
}

