using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEnginePriorityWindowBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
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
}

