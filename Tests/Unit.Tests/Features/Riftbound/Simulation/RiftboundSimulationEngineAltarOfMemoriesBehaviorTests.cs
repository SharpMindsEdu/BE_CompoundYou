using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineAltarOfMemoriesBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AltarOfMemories_OnFriendlyUnitDeath_ExhaustsDrawsAndPlacesCardBack()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031707,
                RiftboundSimulationTestData.BuildDeck(9670, "Order"),
                RiftboundSimulationTestData.BuildDeck(9671, "Chaos")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var altar = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9670_100,
                Name = "Altar of Memories",
                Type = "Gear",
                Cost = 0,
                Power = 0,
                Color = ["Order"],
                Effect = "When a friendly unit dies, you may exhaust me to draw 1, then put a card from your hand on the top or bottom of your Main Deck.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.BaseZone.Cards.Add(altar);

        var doomed = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Doomed Unit", might: 1);
        doomed.MarkedDamage = 1;
        player.BaseZone.Cards.Add(doomed);
        player.BaseZone.Cards.Add(BuildRuneInstance(9670_101, "Order Rune", "Order", ownerPlayer: 0));

        var handCard = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9670_102,
                Name = "Expensive Hand Card",
                Type = "Spell",
                Cost = 5,
                Power = 0,
                Color = ["Order"],
                Effect = "Draw 1.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var drawCard = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9670_103,
                Name = "Drawn Low Card",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 1,
                Color = ["Order"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(handCard);
        player.MainDeckZone.Cards.Add(drawCard);

        var activateAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateAction).Succeeded);

        Assert.True(altar.IsExhausted);
        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == doomed.InstanceId);
        Assert.Single(player.HandZone.Cards);
        Assert.Equal("Expensive Hand Card", player.HandZone.Cards[0].Name);
        Assert.Equal("Drawn Low Card", player.MainDeckZone.Cards.Last().Name);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Altar of Memories"
                && c.Timing == "WhenFriendlyUnitDies"
                && c.Metadata.TryGetValue("placed", out var placed)
                && placed == "bottom"
        );
    }
}

