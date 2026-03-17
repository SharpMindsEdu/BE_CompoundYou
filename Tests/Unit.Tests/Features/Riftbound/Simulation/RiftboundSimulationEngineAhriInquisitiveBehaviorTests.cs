using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineAhriInquisitiveBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AhriInquisitive_WhenAttackingAppliesMinusTwoToEnemyMinOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031703,
                RiftboundSimulationTestData.BuildDeck(9630, "Mind"),
                RiftboundSimulationTestData.BuildDeck(9631, "Order")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();
        session.Battlefields[1].Units.Clear();
        session.Battlefields[1].ControlledByPlayerIndex = 1;
        session.Battlefields[1].ContestedByPlayerIndex = 0;

        var ahriInquisitive = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9630_100,
                Name = "Ahri, Inquisitive",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 0,
                Power = 0,
                Might = 3,
                Color = ["Mind"],
                Effect = "When I attack or defend, give an enemy unit here -2 [Might] this turn, to a minimum of 1 [Might].",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var enemyUnit = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Enemy Target", might: 3);
        session.Battlefields[1].Units.Add(ahriInquisitive);
        session.Battlefields[1].Units.Add(enemyUnit);
        player.BaseZone.Cards.Add(BuildRuneInstance(9630_101, "Mind Rune", "Mind", ownerPlayer: 0));

        var activateAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateAction).Succeeded);

        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Ahri, Inquisitive"
                && c.Timing == "WhenAttackOrDefend"
                && c.Metadata.TryGetValue("target", out var target)
                && target == "Enemy Target"
                && c.Metadata.TryGetValue("reduction", out var reduction)
                && reduction == "2"
        );
    }
}

