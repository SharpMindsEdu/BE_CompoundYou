using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineAhriNineTailedFoxBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AhriNineTailedFox_WhenEnemyAttacksControlledBattlefield_AppliesMinusOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031704,
                RiftboundSimulationTestData.BuildDeck(9640, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9641, "Order")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();
        player.LegendZone.Cards.Clear();
        session.Battlefields[0].Units.Clear();
        session.Battlefields[0].ControlledByPlayerIndex = 0;
        session.Battlefields[0].ContestedByPlayerIndex = 1;

        var legendAhri = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9640_100,
                Name = "Ahri, Nine-Tailed Fox",
                Type = "Legend",
                Cost = 0,
                Power = 0,
                Might = 0,
                Color = ["Calm", "Mind"],
                Effect = "When an enemy unit attacks a battlefield you control, give it -1 [Might] this turn, to a minimum of 1 [Might].",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.LegendZone.Cards.Add(legendAhri);

        var friendlyUnit = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Defender", might: 2);
        var enemyAttacker = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Attacker", might: 3);
        session.Battlefields[0].Units.Add(friendlyUnit);
        session.Battlefields[0].Units.Add(enemyAttacker);
        player.BaseZone.Cards.Add(BuildRuneInstance(9640_101, "Calm Rune", "Calm", ownerPlayer: 0));

        var activateAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateAction).Succeeded);

        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Ahri, Nine-Tailed Fox"
                && c.Timing == "WhenEnemyAttacksControlledBattlefield"
                && c.Metadata.TryGetValue("affected", out var affected)
                && affected == "1"
        );
    }
}

