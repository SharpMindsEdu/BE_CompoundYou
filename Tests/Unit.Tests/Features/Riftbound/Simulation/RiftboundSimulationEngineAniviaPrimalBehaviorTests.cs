using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineAniviaPrimalBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AniviaPrimal_WhenAttacking_DealsThreeToAllEnemyUnitsThere()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031713,
                RiftboundSimulationTestData.BuildDeck(9730, "Body"),
                RiftboundSimulationTestData.BuildDeck(9731, "Order")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();
        session.Battlefields[1].Units.Clear();
        session.Battlefields[1].ControlledByPlayerIndex = 1;
        session.Battlefields[1].ContestedByPlayerIndex = 0;

        var anivia = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9730_100,
                Name = "Anivia, Primal",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 7,
                Power = 2,
                Might = 8,
                Color = ["Body"],
                Effect = "When I attack, deal 3 to all enemy units here.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var enemy1 = BuildUnit(1, 1, "Enemy One", 5);
        var enemy2 = BuildUnit(1, 1, "Enemy Two", 5);
        session.Battlefields[1].Units.Add(anivia);
        session.Battlefields[1].Units.Add(enemy1);
        session.Battlefields[1].Units.Add(enemy2);
        player.BaseZone.Cards.Add(BuildRuneInstance(9730_101, "Body Rune", "Body", ownerPlayer: 0));

        var activateRune = engine
            .GetLegalActions(session)
            .First(a => a.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRune).Succeeded);

        Assert.Equal(3, enemy1.MarkedDamage);
        Assert.Equal(3, enemy2.MarkedDamage);
    }
}

