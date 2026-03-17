using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineApplyActionBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void ApplyAction_ActivateRune_ExhaustsRuneAndAddsEnergy()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                321,
                RiftboundSimulationTestData.BuildDeck(10, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(20, "Order")
            )
        );
        var player = session.Players[0];
        var actionId = engine
            .GetLegalActions(session)
            .First(a => a.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;

        var result = engine.ApplyAction(session, actionId);

        Assert.True(result.Succeeded);
        Assert.Equal(1, player.RunePool.Energy);
        Assert.Equal(
            1,
            player
                .BaseZone.Cards.Count(c =>
                    string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase)
                    && c.IsExhausted
                )
        );
    }

    [Fact]
    public void ApplyAction_PlayUnitToBase_ConsumesEnergyAndEntersExhausted()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                456,
                RiftboundSimulationTestData.BuildDeck(30, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(40, "Order")
            )
        );
        var player = session.Players[0];

        var activate = engine
            .GetLegalActions(session)
            .First(a => a.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        var activateResult = engine.ApplyAction(session, activate);
        Assert.True(activateResult.Succeeded);

        var playToBase = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        var handCountBefore = player.HandZone.Cards.Count;

        var playResult = engine.ApplyAction(session, playToBase);

        Assert.True(playResult.Succeeded);
        Assert.Equal(handCountBefore - 1, player.HandZone.Cards.Count);
        Assert.Equal(0, player.RunePool.Energy);
        Assert.Equal(RiftboundTurnState.NeutralClosed, session.State);
        Assert.NotEmpty(session.Chain);
        Assert.Contains(
            player.BaseZone.Cards,
            c =>
                string.Equals(c.Type, "Unit", StringComparison.OrdinalIgnoreCase)
                && c.IsExhausted
        );
    }

    [Fact]
    public void ApplyAction_PlayAccelerateUnitToBattlefield_UnitStaysReady()
    {
        var challenger = RiftboundSimulationTestData.BuildDeck(
            50,
            "Chaos",
            deck =>
            {
                foreach (var card in deck.Cards)
                {
                    card.Card!.Cost = 0;
                    card.Card.Tags = ["Accelerate"];
                }
            }
        );
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                4567,
                challenger,
                RiftboundSimulationTestData.BuildDeck(60, "Order")
            )
        );

        var playToBattlefield = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains("-accelerate-to-bf-0", StringComparison.Ordinal)
            )
            .ActionId;
        var runeDeckBefore = session.Players[0].RuneDeckZone.Cards.Count;

        var result = engine.ApplyAction(session, playToBattlefield);

        Assert.True(result.Succeeded);
        Assert.Contains(
            session.Battlefields[0].Units,
            unit => unit.ControllerPlayerIndex == 0 && !unit.IsExhausted
        );
        Assert.Contains(
            session.Players[0].BaseZone.Cards,
            card => string.Equals(card.Type, "Rune", StringComparison.OrdinalIgnoreCase) && card.IsExhausted
        );
        Assert.Equal(0, session.Players[0].RunePool.Energy);
        Assert.Equal(runeDeckBefore + 1, session.Players[0].RuneDeckZone.Cards.Count);
    }

    [Fact]
    public void ApplyAction_PlaySpell_RemovesKilledEnemyUnitDuringCleanup()
    {
        var challenger = RiftboundSimulationTestData.BuildDeck(
            70,
            "Chaos",
            deck =>
            {
                foreach (var card in deck.Cards)
                {
                    card.Card!.Type = "Spell";
                    card.Card.Cost = 0;
                    card.Card.Might = null;
                }
            }
        );
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026,
                challenger,
                RiftboundSimulationTestData.BuildDeck(80, "Order")
            )
        );

        var enemyUnit = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Enemy", might: 1);
        session.Battlefields[0].Units.Add(enemyUnit);

        var spellAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.EndsWith("-spell", StringComparison.Ordinal))
            .ActionId;
        var castResult = engine.ApplyAction(session, spellAction);
        Assert.True(castResult.Succeeded);
        Assert.Equal(RiftboundTurnState.NeutralClosed, session.State);

        var firstPass = engine.ApplyAction(session, "pass-focus");
        Assert.True(firstPass.Succeeded);
        var result = engine.ApplyAction(session, "pass-focus");

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(session.Battlefields.SelectMany(b => b.Units), u => u == enemyUnit);
        Assert.Contains(session.Players[1].TrashZone.Cards, c => c.InstanceId == enemyUnit.InstanceId);
        Assert.Contains(
            session.Players[0].TrashZone.Cards,
            c => string.Equals(c.Type, "Spell", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void ApplyAction_MoveFromBaseToEnemyBattlefield_MarksBattlefieldContested()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                777,
                RiftboundSimulationTestData.BuildDeck(90, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(91, "Order")
            )
        );

        var movable = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Runner", might: 2);
        session.Players[0].BaseZone.Cards.Add(movable);
        session.Battlefields[1].ControlledByPlayerIndex = 1;

        var moveAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.EndsWith($"move-{movable.InstanceId}-to-bf-1", StringComparison.Ordinal))
            .ActionId;
        var result = engine.ApplyAction(session, moveAction);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(session.Players[0].BaseZone.Cards, c => c.InstanceId == movable.InstanceId);
        Assert.Contains(session.Battlefields[1].Units, c => c.InstanceId == movable.InstanceId);
        Assert.True(movable.IsExhausted);
        Assert.Equal(0, session.Battlefields[1].ContestedByPlayerIndex);
        Assert.Equal(RiftboundTurnState.NeutralClosed, session.State);
        Assert.Equal(1, engine.GetLegalActions(session).Select(a => a.PlayerIndex).Distinct().Single());
        Assert.Contains(engine.GetLegalActions(session), a => a.ActionId == "pass-focus");
    }

    [Fact]
    public void ApplyAction_WithIllegalAction_ReturnsFailure()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                1122,
                RiftboundSimulationTestData.BuildDeck(120, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(121, "Order")
            )
        );

        var result = engine.ApplyAction(session, "not-a-legal-action");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not legal", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }
}

