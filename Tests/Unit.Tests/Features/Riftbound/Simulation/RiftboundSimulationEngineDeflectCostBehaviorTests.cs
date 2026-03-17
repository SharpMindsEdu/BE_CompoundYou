using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineDeflectCostBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void DeflectCost_CanBePaidWithSealOfDiscordPower()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031625,
                RiftboundSimulationTestData.BuildDeck(9999, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(10000, "Calm")
            )
        );

        var player = session.Players[0];
        var opponent = session.Players[1];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
        opponent.HandZone.Cards.Clear();
        opponent.BaseZone.Cards.Clear();
        opponent.MainDeckZone.Cards.Clear();
        opponent.TrashZone.Cards.Clear();
        opponent.RunePool.Energy = 0;
        opponent.RunePool.PowerByDomain.Clear();
        player.BaseZone.Cards.Add(BuildRuneInstance(419_010, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(419_011, "Fury Rune", "Fury", ownerPlayer: 0));

        var fallingStar = BuildCardInstance(
            new RiftboundCard
            {
                Id = 419_100,
                Name = "Falling Star",
                Type = "Spell",
                Cost = 2,
                Power = 2,
                Color = ["Fury"],
                GameplayKeywords = ["Action"],
                Effect = "Deal 3 to a unit. Deal 3 to a unit.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(fallingStar);

        var sealOfDiscord = BuildCardInstance(
            new RiftboundCard
            {
                Id = 419_200,
                Name = "Seal of Discord",
                Type = "Gear",
                Cost = 0,
                Power = 1,
                Color = ["Chaos"],
                Effect = ":rb_exhaust:: [Reaction] — [Add] :rb_rune_chaos:.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.BaseZone.Cards.Add(sealOfDiscord);

        var irelia = BuildCardInstance(
            new RiftboundCard
            {
                Id = 419_300,
                Name = "Irelia, Fervent",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 5,
                Power = 0,
                Might = 4,
                Color = ["Calm"],
                Effect = "[Deflect] (Opponents must pay [Rune] to choose me with a spell or ability.) When you choose or ready me, give me +1 [Might] this turn.",
            },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        opponent.BaseZone.Cards.Add(irelia);

        var runeDeckCountBefore = player.RuneDeckZone.Cards.Count;
        var activateSealAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(sealOfDiscord.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateSealAction).Succeeded);
        Assert.Equal(1, ReadPower(player, "Chaos"));

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(fallingStar.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(irelia.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(0, ReadPower(player, "Chaos"));
        Assert.Equal(runeDeckCountBefore + 2, player.RuneDeckZone.Cards.Count);
        Assert.Equal(0, irelia.TemporaryMightModifier);
    }
}

