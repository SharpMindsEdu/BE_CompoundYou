using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineAnnieStubbornBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AnnieStubborn_OnPlay_ReturnsSpellFromTrashToHand()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031716,
                RiftboundSimulationTestData.BuildDeck(9760, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9761, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.BaseZone.Cards.Add(BuildRuneInstance(9760_102, "Chaos Rune", "Chaos", ownerPlayer: 0));

        var annie = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9760_100,
                Name = "Annie, Stubborn",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 0,
                Power = 1,
                Might = 3,
                Color = ["Chaos"],
                Effect = "When you play me, return a spell from your trash to your hand.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(annie);

        var trashedSpell = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9760_101,
                Name = "Trash Spell",
                Type = "Spell",
                Cost = 2,
                Power = 0,
                Color = ["Chaos"],
                Effect = "Draw 1.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.TrashZone.Cards.Add(trashedSpell);

        var playResult = engine.ApplyAction(session, $"play-{annie.InstanceId}-to-base");
        Assert.True(
            playResult.Succeeded,
            string.Join(" | ", playResult.LegalActions.Select(x => x.ActionId))
        );
        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == trashedSpell.InstanceId);
        Assert.DoesNotContain(player.TrashZone.Cards, x => x.InstanceId == trashedSpell.InstanceId);
    }
}

