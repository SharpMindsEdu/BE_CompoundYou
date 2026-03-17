using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineIreliaFerventBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void IreliaFervent_WhenReadied_GainsMightThisTurn()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031626,
                RiftboundSimulationTestData.BuildDeck(10001, "Calm"),
                RiftboundSimulationTestData.BuildDeck(10002, "Order")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard
                {
                    Id = 420_100,
                    Name = "Irelia, Fervent",
                    Type = "Unit",
                    Supertype = "Champion",
                    Cost = 5,
                    Power = 0,
                    Might = 4,
                    Color = ["Calm"],
                    Effect = "[Deflect] (Opponents must pay [Rune] to choose me with a spell or ability.) When you choose or ready me, give me +1 [Might] this turn.",
                },
                ownerPlayer: 0,
                controllerPlayer: 0
            )
        );

        var irelia = player.BaseZone.Cards.Last();
        irelia.IsExhausted = true;
        irelia.TemporaryMightModifier = 0;

        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);
        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);

        Assert.False(irelia.IsExhausted);
        Assert.Equal(1, irelia.TemporaryMightModifier);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Irelia, Fervent"
                && c.Timing == "WhenReadied"
                && c.Metadata.TryGetValue("magnitude", out var magnitude)
                && magnitude == "1"
        );
    }
}

