using Application.Features.Riftbound.Simulation.Engine;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public class RiftboundSimulationEngineBehaviorTests
{
    [Fact]
    public void GetLegalActions_InActionPhase_ContainsRuneAndEndTurn()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                simulationId: 1,
                userId: 9,
                seed: 123,
                challengerDeck: RiftboundSimulationTestData.BuildDeck(1, "Chaos"),
                opponentDeck: RiftboundSimulationTestData.BuildDeck(2, "Order")
            )
        );

        var legalActions = engine.GetLegalActions(session);

        Assert.Contains(legalActions, a => a.ActionType == RiftboundActionType.ActivateRune);
        Assert.Contains(legalActions, a => a.ActionId == "end-turn");
    }

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
                && a.ActionId.Contains("-to-bf-0", StringComparison.Ordinal)
            )
            .ActionId;

        var result = engine.ApplyAction(session, playToBattlefield);

        Assert.True(result.Succeeded);
        Assert.Contains(
            session.Battlefields[0].Units,
            unit => unit.ControllerPlayerIndex == 0 && !unit.IsExhausted
        );
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
            .First(a => a.ActionId == $"move-{movable.InstanceId}-to-bf-1")
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
    public void PriorityWindow_AfterMove_AllowsOpponentReactionCard()
    {
        var opponent = RiftboundSimulationTestData.BuildDeck(
            1001,
            "Order",
            deck =>
            {
                foreach (var card in deck.Cards)
                {
                    card.Card!.Type = "Spell";
                    card.Card.Cost = 0;
                    card.Card.Tags = ["Reaction"];
                }
            }
        );

        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                9911,
                RiftboundSimulationTestData.BuildDeck(1000, "Chaos"),
                opponent
            )
        );

        var mover = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Runner", might: 2);
        session.Players[0].BaseZone.Cards.Add(mover);
        session.Battlefields[1].ControlledByPlayerIndex = 1;

        var moveAction = $"move-{mover.InstanceId}-to-bf-1";
        var moveResult = engine.ApplyAction(session, moveAction);
        Assert.True(moveResult.Succeeded);

        var legalAfterMove = engine.GetLegalActions(session);
        Assert.Contains(legalAfterMove, a => a.ActionId == "pass-focus" && a.PlayerIndex == 1);
        Assert.Contains(
            legalAfterMove,
            a => a.ActionType == RiftboundActionType.PlayCard && a.PlayerIndex == 1
        );
    }

    [Fact]
    public void PriorityWindow_AfterMove_DoesNotAllowStandardActions()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                8811,
                RiftboundSimulationTestData.BuildDeck(2000, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(2001, "Order")
            )
        );

        var mover = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Runner", might: 2);
        session.Players[0].BaseZone.Cards.Add(mover);
        session.Battlefields[1].ControlledByPlayerIndex = 1;

        var moveAction = $"move-{mover.InstanceId}-to-bf-1";
        var moveResult = engine.ApplyAction(session, moveAction);
        Assert.True(moveResult.Succeeded);

        var legalAfterMove = engine.GetLegalActions(session);
        Assert.All(legalAfterMove, a => Assert.Equal(1, a.PlayerIndex));
        Assert.DoesNotContain(legalAfterMove, a => a.ActionType == RiftboundActionType.EndTurn);
        Assert.DoesNotContain(legalAfterMove, a => a.ActionType == RiftboundActionType.ActivateRune);
        Assert.DoesNotContain(legalAfterMove, a => a.ActionType == RiftboundActionType.StandardMove);
        Assert.Contains(legalAfterMove, a => a.ActionId == "pass-focus");
    }

    [Fact]
    public void PriorityWindow_OnlyAllowsReactionSpeedCards()
    {
        var opponent = RiftboundSimulationTestData.BuildDeck(
            2101,
            "Order",
            deck =>
            {
                var firstCard = deck.Cards[0].Card!;
                firstCard.Type = "Spell";
                firstCard.Name = "Quick Reaction Spell";
                firstCard.Cost = 0;
                firstCard.Tags = ["Reaction"];

                var secondCard = deck.Cards[1].Card!;
                secondCard.Type = "Spell";
                secondCard.Name = "Slow Spell";
                secondCard.Cost = 0;
                secondCard.Tags = [];
            }
        );

        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                8812,
                RiftboundSimulationTestData.BuildDeck(2100, "Chaos"),
                opponent
            )
        );

        var mover = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Runner", might: 2);
        session.Players[0].BaseZone.Cards.Add(mover);
        session.Battlefields[1].ControlledByPlayerIndex = 1;

        var moveAction = $"move-{mover.InstanceId}-to-bf-1";
        var moveResult = engine.ApplyAction(session, moveAction);
        Assert.True(moveResult.Succeeded);

        var legalAfterMove = engine.GetLegalActions(session);
        Assert.Contains(
            legalAfterMove,
            a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.PlayerIndex == 1
                && a.Description.Contains("Quick Reaction Spell", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            legalAfterMove,
            a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.Description.Contains("Slow Spell", StringComparison.Ordinal)
        );
    }
    
    [Fact]
    public void PriorityWindow_OnlyAllowsActionSpeedCards()
    {
        var opponent = RiftboundSimulationTestData.BuildDeck(
            2101,
            "Order",
            deck =>
            {
                var firstCard = deck.Cards[0].Card!;
                firstCard.Type = "Spell";
                firstCard.Name = "Action Spell";
                firstCard.Cost = 0;
                firstCard.Tags = ["Action"];

                var secondCard = deck.Cards[1].Card!;
                secondCard.Type = "Spell";
                secondCard.Name = "Slow Spell";
                secondCard.Cost = 0;
                secondCard.Tags = [];
            }
        );

        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                8812,
                RiftboundSimulationTestData.BuildDeck(2100, "Chaos"),
                opponent
            )
        );

        var mover = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Runner", might: 2);
        session.Players[0].BaseZone.Cards.Add(mover);
        session.Battlefields[1].ControlledByPlayerIndex = 1;

        var moveAction = $"move-{mover.InstanceId}-to-bf-1";
        var moveResult = engine.ApplyAction(session, moveAction);
        Assert.True(moveResult.Succeeded);

        var legalAfterMove = engine.GetLegalActions(session);
        Assert.Contains(
            legalAfterMove,
            a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.PlayerIndex == 1
                && a.Description.Contains("Action Spell", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            legalAfterMove,
            a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.Description.Contains("Slow Spell", StringComparison.Ordinal)
        );
    }
    
    

    [Fact]
    public void PassFocus_Twice_ResolvesChainAndCleanup()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                8813,
                RiftboundSimulationTestData.BuildDeck(2200, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(2201, "Order")
            )
        );

        var attacker = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Attacker", might: 2);
        var defender = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Defender", might: 1);
        session.Players[0].BaseZone.Cards.Add(attacker);
        session.Battlefields[1].ControlledByPlayerIndex = 1;
        session.Battlefields[1].Units.Add(defender);

        var moveAction = $"move-{attacker.InstanceId}-to-bf-1";
        var moveResult = engine.ApplyAction(session, moveAction);
        Assert.True(moveResult.Succeeded);
        Assert.Equal(RiftboundTurnState.NeutralClosed, session.State);
        Assert.NotEmpty(session.Chain);
        Assert.Equal(0, session.Battlefields[1].ContestedByPlayerIndex);

        var firstPass = engine.ApplyAction(session, "pass-focus");
        Assert.True(firstPass.Succeeded);
        Assert.Equal(RiftboundTurnState.NeutralClosed, session.State);
        Assert.NotEmpty(session.Chain);

        var secondPass = engine.ApplyAction(session, "pass-focus");
        Assert.True(secondPass.Succeeded);
        Assert.Equal(RiftboundTurnState.NeutralOpen, session.State);
        Assert.Empty(session.Chain);
        Assert.Equal(0, session.Battlefields[1].ControlledByPlayerIndex);
        Assert.Null(session.Battlefields[1].ContestedByPlayerIndex);
        Assert.DoesNotContain(
            session.Battlefields[1].Units,
            u => u.ControllerPlayerIndex == 1 && u.InstanceId == defender.InstanceId
        );
    }

    [Fact]
    public void ResolveCleanup_CombatTie_SendsAllUnitsToTrash()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                8899,
                RiftboundSimulationTestData.BuildDeck(100, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(101, "Order")
            )
        );
        var battlefield = session.Battlefields[1];
        battlefield.ContestedByPlayerIndex = 0;
        battlefield.Units.Add(BuildUnit(0, 0, "A", 2));
        battlefield.Units.Add(BuildUnit(1, 1, "B", 2));

        var triggerAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        var result = engine.ApplyAction(session, triggerAction);

        Assert.True(result.Succeeded);
        Assert.Empty(battlefield.Units);
        Assert.Null(battlefield.ControlledByPlayerIndex);
        Assert.True(session.Players[0].TrashZone.Cards.Count > 0);
        Assert.True(session.Players[1].TrashZone.Cards.Count > 0);
        Assert.False(session.Combat.IsOpen);
        Assert.False(session.Showdown.IsOpen);
    }

    [Fact]
    public void ResolveCleanup_CombatWin_CanCompleteDuelAtEightPoints()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                9900,
                RiftboundSimulationTestData.BuildDeck(110, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(111, "Order")
            )
        );

        session.Players[0].Score = 7;
        var battlefield = session.Battlefields[1];
        battlefield.ControlledByPlayerIndex = 1;
        battlefield.ContestedByPlayerIndex = 0;
        battlefield.Units.Add(BuildUnit(0, 0, "Winner", 3));
        battlefield.Units.Add(BuildUnit(1, 1, "Loser", 1));

        var triggerAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        var result = engine.ApplyAction(session, triggerAction);

        Assert.True(result.Succeeded);
        Assert.Equal(8, session.Players[0].Score);
        Assert.Equal(RiftboundTurnPhase.Completed, session.Phase);
        Assert.Equal(0, battlefield.ControlledByPlayerIndex);
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

    private static CardInstance BuildUnit(
        int ownerPlayer,
        int controllerPlayer,
        string name,
        int might
    )
    {
        return new CardInstance
        {
            InstanceId = Guid.NewGuid(),
            CardId = Random.Shared.NextInt64(10_000, 99_999),
            Name = name,
            Type = "Unit",
            OwnerPlayerIndex = ownerPlayer,
            ControllerPlayerIndex = controllerPlayer,
            Cost = 1,
            Might = might,
            Keywords = [],
        };
    }
}
