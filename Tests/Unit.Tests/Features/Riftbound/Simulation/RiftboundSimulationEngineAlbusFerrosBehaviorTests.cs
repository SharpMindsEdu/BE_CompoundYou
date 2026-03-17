using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineAlbusFerrosBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AlbusFerros_OnPlay_SpendsBuffsAndChannelsRunesExhausted()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031706,
                RiftboundSimulationTestData.BuildDeck(9660, "Order"),
                RiftboundSimulationTestData.BuildDeck(9661, "Chaos")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var albus = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9660_100,
                Name = "Albus Ferros",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 3,
                Color = ["Order"],
                Effect = "When you play me, spend any number of buffs. For each buff spent, channel 1 rune exhausted.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(albus);

        var buffedA = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Buffed A", might: 2);
        var buffedB = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Buffed B", might: 2);
        buffedA.PermanentMightModifier = 1;
        buffedB.PermanentMightModifier = 2;
        player.BaseZone.Cards.Add(buffedA);
        player.BaseZone.Cards.Add(buffedB);

        var runeDeckBefore = player.RuneDeckZone.Cards.Count;
        var playAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(albus.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playAction).Succeeded);

        Assert.Equal(0, buffedA.PermanentMightModifier);
        Assert.Equal(0, buffedB.PermanentMightModifier);
        Assert.Equal(runeDeckBefore - 3, player.RuneDeckZone.Cards.Count);
        Assert.Equal(
            3,
            player.BaseZone.Cards.Count(c =>
                string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase) && c.IsExhausted
            )
        );
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Albus Ferros"
                && c.Timing == "WhenPlay"
                && c.Metadata.TryGetValue("buffsSpent", out var buffs)
                && buffs == "3"
                && c.Metadata.TryGetValue("channeledRunesExhausted", out var channeled)
                && channeled == "3"
        );
    }
}

