using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineAncientWarmongerBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void AncientWarmonger_AsAttacker_GainsAssaultMightFromEnemyCount()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031711,
                RiftboundSimulationTestData.BuildDeck(9710, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9711, "Order")
            )
        );

        var player = session.Players[0];
        player.BaseZone.Cards.Clear();
        session.Battlefields[1].Units.Clear();
        session.Battlefields[1].ControlledByPlayerIndex = 1;
        session.Battlefields[1].ContestedByPlayerIndex = 0;

        var warmonger = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9710_100,
                Name = "Ancient Warmonger",
                Type = "Unit",
                Cost = 5,
                Power = 0,
                Might = 4,
                Color = ["Chaos"],
                GameplayKeywords = ["Accelerate", "Assault"],
                Effect = "I have [Assault] equal to the number of enemy units here.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        session.Battlefields[1].Units.Add(warmonger);
        session.Battlefields[1].Units.Add(BuildUnit(1, 1, "Enemy A", 2));
        session.Battlefields[1].Units.Add(BuildUnit(1, 1, "Enemy B", 2));
        player.BaseZone.Cards.Add(BuildRuneInstance(9710_101, "Chaos Rune", "Chaos", ownerPlayer: 0));

        var activateRune = engine
            .GetLegalActions(session)
            .First(a => a.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRune).Succeeded);

        Assert.Equal(2, warmonger.TemporaryMightModifier);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Ancient Warmonger"
                && c.Timing == "WhenAttack"
                && c.Metadata.TryGetValue("assaultInstances", out var assault)
                && assault == "2"
        );
    }
}

