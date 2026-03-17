using Application.Features.Riftbound.Simulation.Definitions;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
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
    public void GetLegalActions_AccelerateRequiresMatchingPowerColor()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                4568,
                RiftboundSimulationTestData.BuildDeck(61, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(62, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        player.BaseZone.Cards.Add(BuildRuneInstance(620_100, "Mind Rune", "Mind", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(620_101, "Mind Rune", "Mind", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(620_102, "Mind Rune", "Mind", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(620_103, "Mind Rune", "Mind", ownerPlayer: 0));

        var acceleratedUnit = BuildCardInstance(
            new RiftboundCard
            {
                Id = 620_200,
                Name = "Rek'Sai, Breacher",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 3,
                Power = 0,
                Might = 3,
                Color = ["Fury"],
                GameplayKeywords = ["Accelerate", "Assault"],
                Effect = "[ACCELERATE] (You may pay [1] [Fury] as an additional cost to have me enter ready.)",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(acceleratedUnit);

        var actions = engine.GetLegalActions(session);
        Assert.Contains(
            actions,
            a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(acceleratedUnit.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            actions,
            a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(acceleratedUnit.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains("-accelerate-", StringComparison.Ordinal)
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
    public void RealCase_FizzBellowsAndSealOfDiscord_ResolvesExpectedState()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                20260316,
                RiftboundSimulationTestData.BuildDeck(9100, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9200, "Order")
            )
        );

        var player = session.Players[0];
        var opponent = session.Players[1];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var chaosRunes = Enumerable
            .Range(0, 6)
            .Select(i => BuildRuneInstance(100_000 + i, "Chaos Rune", "Chaos", ownerPlayer: 0))
            .ToList();
        var mindRunes = Enumerable
            .Range(0, 4)
            .Select(i => BuildRuneInstance(101_000 + i, "Mind Rune", "Mind", ownerPlayer: 0))
            .ToList();
        player.BaseZone.Cards.AddRange(chaosRunes);
        player.BaseZone.Cards.AddRange(mindRunes);

        var sealCard = new RiftboundCard
        {
            Id = 200_001,
            Cost = 0,
            Power = 1,
            Name = "Seal of Discord",
            Type = "Gear",
            Color = ["Chaos"],
            Effect = ":rb_exhaust:: [Reaction] — [Add] :rb_rune_chaos:. (Abilities that add resources can't be reacted to.)",
        };
        var sealA = BuildCardInstance(sealCard, ownerPlayer: 0, controllerPlayer: 0);
        var sealB = BuildCardInstance(sealCard, ownerPlayer: 0, controllerPlayer: 0);
        player.BaseZone.Cards.Add(sealA);
        player.BaseZone.Cards.Add(sealB);

        var fizzCard = new RiftboundCard
        {
            Id = 300_001,
            Name = "Fizz - Trickster",
            Type = "Unit",
            Supertype = "Champion",
            Cost = 3,
            Power = 1,
            Might = 2,
            Color = ["Chaos"],
            Effect = "When you play me, you may play a spell from your trash with Energy cost no more than :rb_energy_3:, ignoring its Energy cost. Recycle that spell after you play it. (You must still pay its Power cost.)",
        };
        var fizz = BuildCardInstance(fizzCard, ownerPlayer: 0, controllerPlayer: 0);
        player.HandZone.Cards.Add(fizz);

        var bellowsCard = new RiftboundCard
        {
            Id = 400_001,
            Name = "Bellows Breath",
            Type = "Spell",
            Cost = 1,
            Power = 1,
            Color = ["Mind"],
            Effect = "[Action] (Play on your turn or in showdowns.) [Repeat] :rb_energy_1::rb_rune_mind: (You may pay the additional cost to repeat this spell's effect.) Deal 1 to up to three units at the same location.",
        };
        var bellows = BuildCardInstance(bellowsCard, ownerPlayer: 0, controllerPlayer: 0);
        player.TrashZone.Cards.Add(bellows);

        var baseUnitA = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit A", might: 1);
        var baseUnitB = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit B", might: 1);
        var battlefieldUnit = BuildUnit(
            ownerPlayer: 1,
            controllerPlayer: 1,
            name: "Battlefield Unit",
            might: 1
        );
        opponent.BaseZone.Cards.Add(baseUnitA);
        opponent.BaseZone.Cards.Add(baseUnitB);
        session.Battlefields[1].Units.Add(battlefieldUnit);

        var firstChaosRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(chaosRunes[0].InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        var secondChaosRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(chaosRunes[1].InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        var firstMindRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(mindRunes[0].InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        var sealAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(sealA.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;

        Assert.True(engine.ApplyAction(session, firstChaosRuneAction).Succeeded);
        Assert.True(engine.ApplyAction(session, secondChaosRuneAction).Succeeded);
        Assert.True(engine.ApplyAction(session, firstMindRuneAction).Succeeded);
        Assert.True(engine.ApplyAction(session, sealAction).Succeeded);
        Assert.Equal(3, player.RunePool.Energy);
        Assert.Equal(1, ReadPower(player, "Chaos"));
        Assert.Equal(0, ReadPower(player, "Mind"));
        Assert.True(sealA.IsExhausted);
        Assert.False(sealB.IsExhausted);

        var playFizzAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(fizz.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        var playFizzResult = engine.ApplyAction(session, playFizzAction);
        Assert.True(playFizzResult.Succeeded, playFizzResult.ErrorMessage);
        Assert.Equal(RiftboundTurnState.NeutralClosed, session.State);
        Assert.Equal(0, player.RunePool.Energy);
        Assert.Equal(0, ReadPower(player, "Chaos"));
        Assert.Equal(0, ReadPower(player, "Mind"));
        Assert.Equal(9, player.BaseZone.Cards.Count(c => string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(player.BaseZone.Cards, c => c.InstanceId == fizz.InstanceId);
        Assert.DoesNotContain(player.TrashZone.Cards, c => c.InstanceId == bellows.InstanceId);
        Assert.Contains(player.MainDeckZone.Cards, c => c.InstanceId == bellows.InstanceId);

        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        var secondPass = engine.ApplyAction(session, "pass-focus");
        Assert.True(secondPass.Succeeded, secondPass.ErrorMessage);

        Assert.Equal(RiftboundTurnState.NeutralOpen, session.State);
        Assert.Empty(session.Chain);
        Assert.DoesNotContain(opponent.BaseZone.Cards, c => c.InstanceId == baseUnitA.InstanceId);
        Assert.DoesNotContain(opponent.BaseZone.Cards, c => c.InstanceId == baseUnitB.InstanceId);
        Assert.Contains(session.Battlefields[1].Units, c => c.InstanceId == battlefieldUnit.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, c => c.InstanceId == baseUnitA.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, c => c.InstanceId == baseUnitB.InstanceId);
        Assert.DoesNotContain(opponent.TrashZone.Cards, c => c.InstanceId == battlefieldUnit.InstanceId);

        Assert.Contains(
            session.EffectContexts,
            c => c.Source == "Seal of Discord" && c.Timing == "Activate"
        );
        Assert.Contains(
            session.EffectContexts,
            c => c.Source == "Fizz - Trickster" && c.Timing == "WhenPlay"
        );
        Assert.Single(
            session.EffectContexts,
            c =>
                c.Source == "Bellows Breath"
                && c.Metadata.TryGetValue("repeat", out var repeatFlag)
                && repeatFlag == "false"
        );
        Assert.DoesNotContain(
            session.EffectContexts,
            c =>
                c.Source == "Bellows Breath"
                && c.Metadata.TryGetValue("repeat", out var repeatFlag)
                && repeatFlag == "true"
        );
    }

    [Fact]
    public void Fizz_BellowsWithoutRepeat_TargetsOnlyBaseUnits()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031603,
                RiftboundSimulationTestData.BuildDeck(9700, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9800, "Order")
            )
        );

        var player = session.Players[0];
        var opponent = session.Players[1];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var chaosRunes = Enumerable
            .Range(0, 3)
            .Select(i => BuildRuneInstance(200_100 + i, "Chaos Rune", "Chaos", ownerPlayer: 0))
            .ToList();
        var mindRune = BuildRuneInstance(200_200, "Mind Rune", "Mind", ownerPlayer: 0);
        player.BaseZone.Cards.AddRange(chaosRunes);
        player.BaseZone.Cards.Add(mindRune);

        var fizz = BuildCardInstance(
            new RiftboundCard
            {
                Id = 300_100,
                Name = "Fizz - Trickster",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 3,
                Power = 1,
                Might = 2,
                Color = ["Chaos"],
                Effect = "When you play me, you may play a spell from your trash with Energy cost no more than :rb_energy_3:, ignoring its Energy cost. Recycle that spell after you play it. (You must still pay its Power cost.)",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(fizz);

        var bellows = BuildCardInstance(
            new RiftboundCard
            {
                Id = 400_100,
                Name = "Bellows Breath",
                Type = "Spell",
                Cost = 1,
                Power = 1,
                Color = ["Mind"],
                Effect = "[Action] (Play on your turn or in showdowns.) [Repeat] :rb_energy_1::rb_rune_mind: (You may pay the additional cost to repeat this spell's effect.) Deal 1 to up to three units at the same location.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.TrashZone.Cards.Add(bellows);

        var baseUnitA = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit A", might: 1);
        var baseUnitB = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit B", might: 1);
        var battlefieldUnit = BuildUnit(
            ownerPlayer: 1,
            controllerPlayer: 1,
            name: "Battlefield Unit",
            might: 1
        );
        opponent.BaseZone.Cards.Add(baseUnitA);
        opponent.BaseZone.Cards.Add(baseUnitB);
        session.Battlefields[1].Units.Add(battlefieldUnit);

        foreach (var rune in chaosRunes)
        {
            var activateAction = engine
                .GetLegalActions(session)
                .First(a => a.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateAction).Succeeded);
        }

        var playFizzAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(fizz.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playFizzAction).Succeeded);

        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.DoesNotContain(opponent.BaseZone.Cards, c => c.InstanceId == baseUnitA.InstanceId);
        Assert.DoesNotContain(opponent.BaseZone.Cards, c => c.InstanceId == baseUnitB.InstanceId);
        Assert.Contains(session.Battlefields[1].Units, c => c.InstanceId == battlefieldUnit.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, c => c.InstanceId == baseUnitA.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, c => c.InstanceId == baseUnitB.InstanceId);
        Assert.DoesNotContain(opponent.TrashZone.Cards, c => c.InstanceId == battlefieldUnit.InstanceId);

        var bellowsContexts = session
            .EffectContexts.Where(c => c.Source == "Bellows Breath")
            .Where(c => c.Metadata.TryGetValue("repeat", out var repeat) && repeat == "false")
            .ToList();
        Assert.Single(bellowsContexts);
        Assert.Equal("base-1", bellowsContexts[0].Metadata["location"]);
        Assert.DoesNotContain(
            session.EffectContexts,
            c =>
                c.Source == "Bellows Breath"
                && c.Metadata.TryGetValue("repeat", out var repeat)
                && repeat == "true"
        );
    }

    [Fact]
    public void Fizz_BellowsWithoutRepeat_TargetsOnlyBattlefieldUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031604,
                RiftboundSimulationTestData.BuildDeck(9900, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9910, "Order")
            )
        );

        var player = session.Players[0];
        var opponent = session.Players[1];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var chaosRunes = Enumerable
            .Range(0, 3)
            .Select(i => BuildRuneInstance(201_100 + i, "Chaos Rune", "Chaos", ownerPlayer: 0))
            .ToList();
        var mindRune = BuildRuneInstance(201_200, "Mind Rune", "Mind", ownerPlayer: 0);
        player.BaseZone.Cards.AddRange(chaosRunes);
        player.BaseZone.Cards.Add(mindRune);

        var fizz = BuildCardInstance(
            new RiftboundCard
            {
                Id = 301_100,
                Name = "Fizz - Trickster",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 3,
                Power = 1,
                Might = 2,
                Color = ["Chaos"],
                Effect = "When you play me, you may play a spell from your trash with Energy cost no more than :rb_energy_3:, ignoring its Energy cost. Recycle that spell after you play it. (You must still pay its Power cost.)",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(fizz);

        var bellows = BuildCardInstance(
            new RiftboundCard
            {
                Id = 401_100,
                Name = "Bellows Breath",
                Type = "Spell",
                Cost = 1,
                Power = 1,
                Color = ["Mind"],
                Effect = "[Action] (Play on your turn or in showdowns.) [Repeat] :rb_energy_1::rb_rune_mind: (You may pay the additional cost to repeat this spell's effect.) Deal 1 to up to three units at the same location.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.TrashZone.Cards.Add(bellows);

        var battlefieldUnit = BuildUnit(
            ownerPlayer: 1,
            controllerPlayer: 1,
            name: "Battlefield Unit",
            might: 1
        );
        session.Battlefields[1].Units.Add(battlefieldUnit);

        foreach (var rune in chaosRunes)
        {
            var activateAction = engine
                .GetLegalActions(session)
                .First(a => a.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateAction).Succeeded);
        }

        var playFizzAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(fizz.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playFizzAction).Succeeded);

        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.DoesNotContain(session.Battlefields[1].Units, c => c.InstanceId == battlefieldUnit.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, c => c.InstanceId == battlefieldUnit.InstanceId);
        Assert.DoesNotContain(
            opponent.BaseZone.Cards,
            c => string.Equals(c.Type, "Unit", StringComparison.OrdinalIgnoreCase)
        );

        var bellowsContexts = session
            .EffectContexts.Where(c => c.Source == "Bellows Breath")
            .Where(c => c.Metadata.TryGetValue("repeat", out var repeat) && repeat == "false")
            .ToList();
        Assert.Single(bellowsContexts);
        Assert.Equal("bf-1", bellowsContexts[0].Metadata["location"]);
        Assert.DoesNotContain(
            session.EffectContexts,
            c =>
                c.Source == "Bellows Breath"
                && c.Metadata.TryGetValue("repeat", out var repeat)
                && repeat == "true"
        );
    }

    [Fact]
    public void Fizz_WithSecondTrashSpell_PlaysStackedDeckInsteadOfBellows()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031605,
                RiftboundSimulationTestData.BuildDeck(9920, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9930, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var chaosRunes = Enumerable
            .Range(0, 4)
            .Select(i => BuildRuneInstance(202_100 + i, "Chaos Rune", "Chaos", ownerPlayer: 0))
            .ToList();
        var mindRune = BuildRuneInstance(202_200, "Mind Rune", "Mind", ownerPlayer: 0);
        player.BaseZone.Cards.AddRange(chaosRunes);
        player.BaseZone.Cards.Add(mindRune);

        var fizz = BuildCardInstance(
            new RiftboundCard
            {
                Id = 302_100,
                Name = "Fizz - Trickster",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 3,
                Power = 1,
                Might = 2,
                Color = ["Chaos"],
                Effect = "When you play me, you may play a spell from your trash with Energy cost no more than :rb_energy_3:, ignoring its Energy cost. Recycle that spell after you play it. (You must still pay its Power cost.)",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(fizz);

        var bellows = BuildCardInstance(
            new RiftboundCard
            {
                Id = 402_100,
                Name = "Bellows Breath",
                Type = "Spell",
                Cost = 1,
                Power = 1,
                Color = ["Mind"],
                Effect = "[Action] (Play on your turn or in showdowns.) [Repeat] :rb_energy_1::rb_rune_mind: (You may pay the additional cost to repeat this spell's effect.) Deal 1 to up to three units at the same location.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var stackedDeck = BuildCardInstance(
            new RiftboundCard
            {
                Id = 402_200,
                Name = "Stacked Deck",
                Type = "Spell",
                Cost = 1,
                Power = 0,
                Color = ["Mind"],
                Effect = "[Action] (play on your turn or in showdowns.) \nLook at the top 3 cards of your Main Deck. Put 1 into your hand and recycle the rest.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.TrashZone.Cards.Add(bellows);
        player.TrashZone.Cards.Add(stackedDeck);

        foreach (var rune in chaosRunes.Take(3))
        {
            var activateAction = engine
                .GetLegalActions(session)
                .First(a => a.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateAction).Succeeded);
        }

        var playFizzAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(fizz.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playFizzAction).Succeeded);

        var fizzWhenPlay = session
            .EffectContexts.Where(c => c.Source == "Fizz - Trickster" && c.Timing == "WhenPlay")
            .ToList();
        Assert.Single(fizzWhenPlay);
        Assert.True(fizzWhenPlay[0].Metadata.TryGetValue("playedSpell", out var playedSpell));
        Assert.Equal("Stacked Deck", playedSpell);

        Assert.Contains(player.TrashZone.Cards, c => c.InstanceId == bellows.InstanceId);
        Assert.DoesNotContain(player.TrashZone.Cards, c => c.InstanceId == stackedDeck.InstanceId);
        Assert.Contains(player.MainDeckZone.Cards, c => c.InstanceId == stackedDeck.InstanceId);
        Assert.DoesNotContain(
            session.EffectContexts,
            c => c.Source == "Bellows Breath" && c.Timing == "Resolve"
        );
    }

    [Fact]
    public void Bellows_WithExplicitRepeatAction_AppliesRepeat()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031606,
                RiftboundSimulationTestData.BuildDeck(9940, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9950, "Order")
            )
        );

        var player = session.Players[0];
        var opponent = session.Players[1];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var mindRuneA = BuildRuneInstance(203_100, "Mind Rune", "Mind", ownerPlayer: 0);
        var mindRuneB = BuildRuneInstance(203_101, "Mind Rune", "Mind", ownerPlayer: 0);
        player.BaseZone.Cards.Add(mindRuneA);
        player.BaseZone.Cards.Add(mindRuneB);

        var bellows = BuildCardInstance(
            new RiftboundCard
            {
                Id = 403_100,
                Name = "Bellows Breath",
                Type = "Spell",
                Cost = 1,
                Power = 1,
                Color = ["Mind"],
                Effect = "[Action] (Play on your turn or in showdowns.) [Repeat] :rb_energy_1::rb_rune_mind: (You may pay the additional cost to repeat this spell's effect.) Deal 1 to up to three units at the same location.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(bellows);

        var baseUnitA = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit A", might: 1);
        var baseUnitB = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit B", might: 1);
        var battlefieldUnit = BuildUnit(
            ownerPlayer: 1,
            controllerPlayer: 1,
            name: "Battlefield Unit",
            might: 1
        );
        opponent.BaseZone.Cards.Add(baseUnitA);
        opponent.BaseZone.Cards.Add(baseUnitB);
        session.Battlefields[1].Units.Add(battlefieldUnit);

        var firstRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(mindRuneA.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        var secondRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(mindRuneB.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, firstRuneAction).Succeeded);
        Assert.True(engine.ApplyAction(session, secondRuneAction).Succeeded);

        var legalActions = engine.GetLegalActions(session);
        var nonRepeatAction = legalActions
            .Single(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(bellows.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains("-target-units-", StringComparison.Ordinal)
                && !a.ActionId.EndsWith("-repeat", StringComparison.Ordinal)
                && a.ActionId.Contains(baseUnitA.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(baseUnitB.InstanceId.ToString(), StringComparison.Ordinal)
                && !a.ActionId.Contains(battlefieldUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        var repeatAction = legalActions
            .Single(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(bellows.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains("-target-units-", StringComparison.Ordinal)
                && a.ActionId.EndsWith("-repeat", StringComparison.Ordinal)
                && a.ActionId.Contains(baseUnitA.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(baseUnitB.InstanceId.ToString(), StringComparison.Ordinal)
                && !a.ActionId.Contains(battlefieldUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.NotEqual(nonRepeatAction, repeatAction);

        Assert.True(engine.ApplyAction(session, repeatAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.DoesNotContain(opponent.BaseZone.Cards, c => c.InstanceId == baseUnitA.InstanceId);
        Assert.DoesNotContain(opponent.BaseZone.Cards, c => c.InstanceId == baseUnitB.InstanceId);
        Assert.Contains(session.Battlefields[1].Units, c => c.InstanceId == battlefieldUnit.InstanceId);

        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Bellows Breath"
                && c.Metadata.TryGetValue("repeat", out var repeat)
                && repeat == "true"
        );
    }

    [Fact]
    public void Bellows_TargetSelection_ChoosesExactUnitsAndKeepsSameLocation()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031607,
                RiftboundSimulationTestData.BuildDeck(9960, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9970, "Order")
            )
        );

        var player = session.Players[0];
        var opponent = session.Players[1];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var mindRune = BuildRuneInstance(204_100, "Mind Rune", "Mind", ownerPlayer: 0);
        player.BaseZone.Cards.Add(mindRune);

        var bellows = BuildCardInstance(
            new RiftboundCard
            {
                Id = 404_100,
                Name = "Bellows Breath",
                Type = "Spell",
                Cost = 1,
                Power = 1,
                Color = ["Mind"],
                Effect = "[Action] (Play on your turn or in showdowns.) [Repeat] :rb_energy_1::rb_rune_mind: (You may pay the additional cost to repeat this spell's effect.) Deal 1 to up to three units at the same location.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(bellows);

        var baseUnitA = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit A", might: 2);
        var baseUnitB = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit B", might: 2);
        var baseUnitC = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit C", might: 3);
        var baseUnitD = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit D", might: 3);
        var battlefieldUnit = BuildUnit(
            ownerPlayer: 1,
            controllerPlayer: 1,
            name: "Battlefield Unit",
            might: 3
        );
        opponent.BaseZone.Cards.Add(baseUnitA);
        opponent.BaseZone.Cards.Add(baseUnitB);
        opponent.BaseZone.Cards.Add(baseUnitC);
        opponent.BaseZone.Cards.Add(baseUnitD);
        session.Battlefields[1].Units.Add(battlefieldUnit);

        var activateRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(mindRune.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRuneAction).Succeeded);

        var bellowsActions = engine
            .GetLegalActions(session)
            .Where(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(bellows.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains("-target-units-", StringComparison.Ordinal)
                && !a.ActionId.EndsWith("-repeat", StringComparison.Ordinal)
            )
            .ToList();
        Assert.NotEmpty(bellowsActions);
        Assert.DoesNotContain(
            bellowsActions,
            a =>
                a.ActionId.Contains(baseUnitA.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(battlefieldUnit.InstanceId.ToString(), StringComparison.Ordinal)
        );

        var chosenAction = bellowsActions
            .Single(a =>
                a.ActionId.Contains(baseUnitA.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(baseUnitB.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(baseUnitC.InstanceId.ToString(), StringComparison.Ordinal)
                && !a.ActionId.Contains(baseUnitD.InstanceId.ToString(), StringComparison.Ordinal)
                && !a.ActionId.Contains(battlefieldUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;

        Assert.True(engine.ApplyAction(session, chosenAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Equal(1, baseUnitA.MarkedDamage);
        Assert.Equal(1, baseUnitB.MarkedDamage);
        Assert.Equal(1, baseUnitC.MarkedDamage);
        Assert.Equal(0, baseUnitD.MarkedDamage);
        Assert.Equal(0, battlefieldUnit.MarkedDamage);
        Assert.Equal(4, opponent.BaseZone.Cards.Count(c => string.Equals(c.Type, "Unit", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(session.Battlefields[1].Units, c => c.InstanceId == battlefieldUnit.InstanceId);

        var bellowsContexts = session
            .EffectContexts.Where(c => c.Source == "Bellows Breath")
            .Where(c => c.Metadata.TryGetValue("repeat", out var repeat) && repeat == "false")
            .ToList();
        Assert.Single(bellowsContexts);
        Assert.Equal("base-1", bellowsContexts[0].Metadata["location"]);
        Assert.Equal("3", bellowsContexts[0].Metadata["targets"]);
    }

    [Fact]
    public void StackedDeck_WithNocturneInLookWindow_PlaysNocturneForChaosPower()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031611,
                RiftboundSimulationTestData.BuildDeck(9971, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9972, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
        player.BaseZone.Cards.Add(BuildRuneInstance(421_013, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(421_014, "Fury Rune", "Fury", ownerPlayer: 0));

        var chaosRune = BuildRuneInstance(205_100, "Chaos Rune", "Chaos", ownerPlayer: 0);
        player.BaseZone.Cards.Add(chaosRune);

        var stackedDeck = BuildCardInstance(
            new RiftboundCard
            {
                Id = 405_100,
                Name = "Stacked Deck",
                Type = "Spell",
                Cost = 1,
                Power = 0,
                Color = ["Chaos"],
                Effect = "[Action] (play on your turn or in showdowns.) Look at the top 3 cards of your Main Deck. Put 1 into your hand and recycle the rest.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(stackedDeck);

        var nocturne = BuildCardInstance(
            new RiftboundCard
            {
                Id = 405_200,
                Name = "Nocturne, Horrifying",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 4,
                Power = 1,
                Might = 4,
                Color = ["Chaos"],
                Tags = ["Nocturne"],
                GameplayKeywords = ["Ganking"],
                Effect = "[Ganking] As you look at or reveal me from the top of your deck, you may banish me. If you do, you may play me for [Rune].",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var fillerA = BuildCardInstance(
            new RiftboundCard
            {
                Id = 405_201,
                Name = "Filler A",
                Type = "Unit",
                Cost = 2,
                Power = 0,
                Might = 2,
                Color = ["Chaos"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var fillerB = BuildCardInstance(
            new RiftboundCard
            {
                Id = 405_202,
                Name = "Filler B",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 1,
                Color = ["Chaos"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(nocturne);
        player.MainDeckZone.Cards.Add(fillerA);
        player.MainDeckZone.Cards.Add(fillerB);

        var activateRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(chaosRune.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRuneAction).Succeeded);

        var baseRuneCountBefore = player.BaseZone.Cards.Count(c =>
            string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase)
        );
        var runeDeckCountBefore = player.RuneDeckZone.Cards.Count;

        var playStackedDeckAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(stackedDeck.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playStackedDeckAction).Succeeded);

        Assert.DoesNotContain(player.MainDeckZone.Cards, c => c.InstanceId == nocturne.InstanceId);
        Assert.Contains(
            session.Battlefields.SelectMany(b => b.Units),
            c => c.InstanceId == nocturne.InstanceId
        );
        Assert.Equal(
            baseRuneCountBefore - 1,
            player.BaseZone.Cards.Count(c => string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase))
        );
        Assert.Equal(runeDeckCountBefore + 1, player.RuneDeckZone.Cards.Count);

        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Nocturne, Horrifying"
                && c.Timing == "RevealPlay"
                && c.Metadata.TryGetValue("sourceCard", out var source)
                && source == "Stacked Deck"
        );
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Stacked Deck"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("playedFromReveal", out var played)
                && played == "1"
        );
    }

    [Fact]
    public void CalledShot_WithNocturneInLookWindow_PlaysNocturneForChaosPower()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031612,
                RiftboundSimulationTestData.BuildDeck(9973, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9974, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
        player.BaseZone.Cards.Add(BuildRuneInstance(206_100, "Chaos Rune", "Chaos", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(206_101, "Chaos Rune", "Chaos", ownerPlayer: 0));

        var calledShot = BuildCardInstance(
            new RiftboundCard
            {
                Id = 406_100,
                Name = "Called Shot",
                Type = "Spell",
                Cost = 0,
                Power = 1,
                Color = ["Chaos"],
                GameplayKeywords = ["Action", "Repeat"],
                Effect = "[ACTION] (Play on your turn or in showdowns.) [REPEAT] [CHAOS] (You may pay the additional cost to repeat this spell's effect.) Look at the top 2 cards of your Main Deck. Draw one and recycle the other.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(calledShot);

        var nocturne = BuildCardInstance(
            new RiftboundCard
            {
                Id = 406_200,
                Name = "Nocturne, Horrifying",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 4,
                Power = 1,
                Might = 4,
                Color = ["Chaos"],
                Tags = ["Nocturne"],
                GameplayKeywords = ["Ganking"],
                Effect = "[Ganking] As you look at or reveal me from the top of your deck, you may banish me. If you do, you may play me for [Rune].",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var filler = BuildCardInstance(
            new RiftboundCard
            {
                Id = 406_201,
                Name = "Fallback Draw",
                Type = "Unit",
                Cost = 2,
                Power = 0,
                Might = 2,
                Color = ["Chaos"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(nocturne);
        player.MainDeckZone.Cards.Add(filler);

        var legalActions = engine.GetLegalActions(session);
        Assert.Contains(
            legalActions,
            a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(calledShot.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell-repeat", StringComparison.Ordinal)
        );

        var baseRuneCountBefore = player.BaseZone.Cards.Count(c =>
            string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase)
        );
        var runeDeckCountBefore = player.RuneDeckZone.Cards.Count;
        var playCalledShotAction = legalActions
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(calledShot.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell", StringComparison.Ordinal)
            )
            .ActionId;

        Assert.True(engine.ApplyAction(session, playCalledShotAction).Succeeded);

        Assert.DoesNotContain(player.MainDeckZone.Cards, c => c.InstanceId == nocturne.InstanceId);
        Assert.Contains(
            session.Battlefields.SelectMany(b => b.Units),
            c => c.InstanceId == nocturne.InstanceId
        );
        Assert.Contains(player.HandZone.Cards, c => c.InstanceId == filler.InstanceId);
        Assert.Equal(
            baseRuneCountBefore - 2,
            player.BaseZone.Cards.Count(c => string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase))
        );
        Assert.Equal(runeDeckCountBefore + 2, player.RuneDeckZone.Cards.Count);

        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Nocturne, Horrifying"
                && c.Timing == "RevealPlay"
                && c.Metadata.TryGetValue("sourceCard", out var source)
                && source == "Called Shot"
        );
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Called Shot"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("playedFromReveal", out var played)
                && played == "1"
        );
    }

    [Fact]
    public void Discipline_TargetsAnyUnit_BuffsTargetAndDrawsOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031613,
                RiftboundSimulationTestData.BuildDeck(9975, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9976, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var calmRuneA = BuildRuneInstance(207_100, "Calm Rune", "Calm", ownerPlayer: 0);
        var calmRuneB = BuildRuneInstance(207_101, "Calm Rune", "Calm", ownerPlayer: 0);
        player.BaseZone.Cards.Add(calmRuneA);
        player.BaseZone.Cards.Add(calmRuneB);

        var discipline = BuildCardInstance(
            new RiftboundCard
            {
                Id = 407_100,
                Name = "Discipline",
                Type = "Spell",
                Cost = 2,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] Give a unit +2 [Might] this turn. Draw 1.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(discipline);

        var drawCard = BuildCardInstance(
            new RiftboundCard
            {
                Id = 407_101,
                Name = "Draw Target",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 1,
                Color = ["Calm"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(drawCard);

        var myUnit = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Target", might: 2);
        session.Battlefields[1].Units.Add(myUnit);

        foreach (var rune in new[] { calmRuneA, calmRuneB })
        {
            var activateAction = engine
                .GetLegalActions(session)
                .First(a => a.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateAction).Succeeded);
        }

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(discipline.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(myUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(2, myUnit.TemporaryMightModifier);
        Assert.Contains(player.HandZone.Cards, c => c.InstanceId == drawCard.InstanceId);
        Assert.DoesNotContain(player.MainDeckZone.Cards, c => c.InstanceId == drawCard.InstanceId);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Discipline"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("draw", out var draw)
                && draw == "1"
        );
    }

    [Fact]
    public void EnGarde_OnlyFriendlyUnitThere_GivesPlusTwoMight()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031614,
                RiftboundSimulationTestData.BuildDeck(9977, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9978, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var calmRune = BuildRuneInstance(208_100, "Calm Rune", "Calm", ownerPlayer: 0);
        player.BaseZone.Cards.Add(calmRune);

        var enGarde = BuildCardInstance(
            new RiftboundCard
            {
                Id = 408_100,
                Name = "En Garde",
                Type = "Spell",
                Cost = 1,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] Give a friendly unit +1 [Might] this turn, then an additional +1 [Might] this turn if it is the only unit you control there.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(enGarde);

        var friendlyUnit = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Solo Friendly", might: 2);
        player.BaseZone.Cards.Add(friendlyUnit);
        var enemyUnit = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Enemy Unit", might: 2);
        session.Battlefields[1].Units.Add(enemyUnit);

        var activateRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(calmRune.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRuneAction).Succeeded);

        var legalActions = engine.GetLegalActions(session);
        Assert.DoesNotContain(
            legalActions,
            a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(enGarde.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(enemyUnit.InstanceId.ToString(), StringComparison.Ordinal)
        );

        var castAction = legalActions
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(enGarde.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(friendlyUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(2, friendlyUnit.TemporaryMightModifier);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "En Garde"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("onlyFriendlyThere", out var only)
                && only == "true"
        );
    }

    [Fact]
    public void EnGarde_WithAnotherFriendlyThere_GivesPlusOneMight()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031615,
                RiftboundSimulationTestData.BuildDeck(9979, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9980, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var calmRune = BuildRuneInstance(209_100, "Calm Rune", "Calm", ownerPlayer: 0);
        player.BaseZone.Cards.Add(calmRune);

        var enGarde = BuildCardInstance(
            new RiftboundCard
            {
                Id = 409_100,
                Name = "En Garde",
                Type = "Spell",
                Cost = 1,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] Give a friendly unit +1 [Might] this turn, then an additional +1 [Might] this turn if it is the only unit you control there.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(enGarde);

        var targetUnit = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Target Friendly", might: 2);
        var secondFriendly = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Other Friendly", might: 2);
        player.BaseZone.Cards.Add(targetUnit);
        player.BaseZone.Cards.Add(secondFriendly);

        var activateRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(calmRune.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRuneAction).Succeeded);

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(enGarde.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(targetUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(1, targetUnit.TemporaryMightModifier);
        Assert.Equal(0, secondFriendly.TemporaryMightModifier);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "En Garde"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("onlyFriendlyThere", out var only)
                && only == "false"
        );
    }

    [Fact]
    public void Undertitan_RevealedByStackedDeck_AddsTwoEnergy()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031616,
                RiftboundSimulationTestData.BuildDeck(9981, "Order"),
                RiftboundSimulationTestData.BuildDeck(9982, "Chaos")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var orderRune = BuildRuneInstance(210_100, "Order Rune", "Order", ownerPlayer: 0);
        player.BaseZone.Cards.Add(orderRune);

        var stackedDeck = BuildCardInstance(
            new RiftboundCard
            {
                Id = 410_100,
                Name = "Stacked Deck",
                Type = "Spell",
                Cost = 1,
                Power = 0,
                Color = ["Chaos"],
                GameplayKeywords = ["Action"],
                Effect = "[Action] Look at the top 3 cards of your Main Deck. Put 1 into your hand and recycle the rest.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(stackedDeck);

        var undertitan = BuildCardInstance(
            new RiftboundCard
            {
                Id = 410_200,
                Name = "Undertitan",
                Type = "Unit",
                Cost = 6,
                Power = 1,
                Might = 5,
                Color = ["Order"],
                Effect = "When you play me, give your other units +2 [Might] this turn. As I'm revealed from your deck, [Add] [2].",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var fillerA = BuildCardInstance(
            new RiftboundCard
            {
                Id = 410_201,
                Name = "Filler X",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 1,
                Color = ["Order"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var fillerB = BuildCardInstance(
            new RiftboundCard
            {
                Id = 410_202,
                Name = "Filler Y",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 1,
                Color = ["Order"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(undertitan);
        player.MainDeckZone.Cards.Add(fillerA);
        player.MainDeckZone.Cards.Add(fillerB);

        var activateRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(orderRune.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRuneAction).Succeeded);

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(stackedDeck.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(2, player.RunePool.Energy);
        Assert.DoesNotContain(session.Battlefields.SelectMany(x => x.Units), x => x.InstanceId == undertitan.InstanceId);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Undertitan"
                && c.Timing == "Reveal"
                && c.Metadata.TryGetValue("addEnergy", out var added)
                && added == "2"
                && c.Metadata.TryGetValue("sourceCard", out var source)
                && source == "Stacked Deck"
        );
    }

    [Fact]
    public void Undertitan_RevealedByCalledShot_AddsTwoEnergy()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031617,
                RiftboundSimulationTestData.BuildDeck(9983, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9984, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        player.BaseZone.Cards.Add(BuildRuneInstance(211_100, "Chaos Rune", "Chaos", ownerPlayer: 0));

        var calledShot = BuildCardInstance(
            new RiftboundCard
            {
                Id = 411_100,
                Name = "Called Shot",
                Type = "Spell",
                Cost = 0,
                Power = 1,
                Color = ["Chaos"],
                GameplayKeywords = ["Action", "Repeat"],
                Effect = "[ACTION] [REPEAT] [CHAOS] Look at the top 2 cards of your Main Deck. Draw one and recycle the other.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(calledShot);

        var undertitan = BuildCardInstance(
            new RiftboundCard
            {
                Id = 411_200,
                Name = "Undertitan",
                Type = "Unit",
                Cost = 6,
                Power = 1,
                Might = 5,
                Color = ["Order"],
                Effect = "When you play me, give your other units +2 [Might] this turn. As I'm revealed from your deck, [Add] [2].",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var filler = BuildCardInstance(
            new RiftboundCard
            {
                Id = 411_201,
                Name = "Filler Z",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 1,
                Color = ["Chaos"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(undertitan);
        player.MainDeckZone.Cards.Add(filler);

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(calledShot.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(2, player.RunePool.Energy);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Undertitan"
                && c.Timing == "Reveal"
                && c.Metadata.TryGetValue("addEnergy", out var added)
                && added == "2"
                && c.Metadata.TryGetValue("sourceCard", out var source)
                && source == "Called Shot"
        );
    }

    [Fact]
    public void Undertitan_OnPlay_BuffsOtherFriendlyUnitsByTwo()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031618,
                RiftboundSimulationTestData.BuildDeck(9985, "Order"),
                RiftboundSimulationTestData.BuildDeck(9986, "Chaos")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var runes = Enumerable
            .Range(0, 6)
            .Select(i => BuildRuneInstance(212_100 + i, "Order Rune", "Order", ownerPlayer: 0))
            .ToList();
        player.BaseZone.Cards.AddRange(runes);

        var undertitan = BuildCardInstance(
            new RiftboundCard
            {
                Id = 412_100,
                Name = "Undertitan",
                Type = "Unit",
                Cost = 6,
                Power = 1,
                Might = 5,
                Color = ["Order"],
                Effect = "When you play me, give your other units +2 [Might] this turn. As I'm revealed from your deck, [Add] [2].",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(undertitan);

        var friendlyBase = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Base Buddy", might: 2);
        var friendlyBattlefield = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Battle Buddy", might: 2);
        player.BaseZone.Cards.Add(friendlyBase);
        session.Battlefields[0].Units.Add(friendlyBattlefield);

        foreach (var rune in runes)
        {
            var activateAction = engine
                .GetLegalActions(session)
                .First(a => a.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateAction).Succeeded);
        }

        var playAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(undertitan.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playAction).Succeeded);

        Assert.Equal(2, friendlyBase.TemporaryMightModifier);
        Assert.Equal(2, friendlyBattlefield.TemporaryMightModifier);
        Assert.Equal(0, undertitan.TemporaryMightModifier);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Undertitan"
                && c.Timing == "WhenPlay"
                && c.Metadata.TryGetValue("buffedUnits", out var buffed)
                && buffed == "2"
        );
    }

    [Fact]
    public void VoidRush_PlaysRevealedCardWithEnergyReduction_AndDrawsRemaining()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031628,
                RiftboundSimulationTestData.BuildDeck(10013, "Fury"),
                RiftboundSimulationTestData.BuildDeck(10014, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        player.BaseZone.Cards.Add(BuildRuneInstance(420_100, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(420_101, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(420_102, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(420_103, "Fury Rune", "Fury", ownerPlayer: 0));

        var voidRush = BuildCardInstance(
            new RiftboundCard
            {
                Id = 420_200,
                Name = "Void Rush",
                Type = "Spell",
                Supertype = "Signature",
                Cost = 2,
                Power = 1,
                Color = ["Fury", "Order"],
                Effect = "Reveal the top 2 cards of your Main Deck. You may banish one, then play it, reducing its cost by [2]. Draw any you didn't banish.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(voidRush);

        var expensiveUnit = BuildCardInstance(
            new RiftboundCard
            {
                Id = 420_201,
                Name = "Deep Ambusher",
                Type = "Unit",
                Cost = 3,
                Power = 0,
                Might = 4,
                Color = ["Fury"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var fallbackDraw = BuildCardInstance(
            new RiftboundCard
            {
                Id = 420_202,
                Name = "Fallback Draw",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Fury"],
                Effect = "Draw 1.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(expensiveUnit);
        player.MainDeckZone.Cards.Add(fallbackDraw);

        var runeDeckBefore = player.RuneDeckZone.Cards.Count;
        var playAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(voidRush.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(expensiveUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playAction).Succeeded);

        Assert.DoesNotContain(player.MainDeckZone.Cards, c => c.InstanceId == expensiveUnit.InstanceId);
        Assert.Contains(
            session.Battlefields.SelectMany(x => x.Units),
            c => c.InstanceId == expensiveUnit.InstanceId
        );
        Assert.Contains(player.HandZone.Cards, c => c.InstanceId == fallbackDraw.InstanceId);
        Assert.Equal(runeDeckBefore + 1, player.RuneDeckZone.Cards.Count);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Void Rush"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("playedFromReveal", out var played)
                && played == "1"
                && c.Metadata.TryGetValue("drawn", out var drawn)
                && drawn == "1"
        );
    }

    [Fact]
    public void VoidRush_CanChooseWhichRevealedCardToPlay_AndDrawsTheOther()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031638,
                RiftboundSimulationTestData.BuildDeck(10019, "Fury"),
                RiftboundSimulationTestData.BuildDeck(10020, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        player.BaseZone.Cards.Add(BuildRuneInstance(423_100, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(423_101, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(423_102, "Fury Rune", "Fury", ownerPlayer: 0));

        var voidRush = BuildCardInstance(
            new RiftboundCard
            {
                Id = 423_200,
                Name = "Void Rush",
                Type = "Spell",
                Supertype = "Signature",
                Cost = 2,
                Power = 1,
                Color = ["Fury", "Order"],
                Effect = "Reveal the top 2 cards of your Main Deck. You may banish one, then play it, reducing its cost by [2]. Draw any you didn't banish.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(voidRush);

        var firstLooked = BuildCardInstance(
            new RiftboundCard
            {
                Id = 423_201,
                Name = "First Looked Unit",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 2,
                Color = ["Fury"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var secondLooked = BuildCardInstance(
            new RiftboundCard
            {
                Id = 423_202,
                Name = "Second Looked Unit",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 3,
                Color = ["Fury"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(firstLooked);
        player.MainDeckZone.Cards.Add(secondLooked);

        var playAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(voidRush.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(secondLooked.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;

        Assert.True(engine.ApplyAction(session, playAction).Succeeded);

        Assert.Contains(
            session.Battlefields.SelectMany(x => x.Units),
            card => card.InstanceId == secondLooked.InstanceId
        );
        Assert.Contains(player.HandZone.Cards, card => card.InstanceId == firstLooked.InstanceId);
        Assert.DoesNotContain(player.MainDeckZone.Cards, card => card.InstanceId == firstLooked.InstanceId);
        Assert.DoesNotContain(player.MainDeckZone.Cards, card => card.InstanceId == secondLooked.InstanceId);
    }

    [Fact]
    public void RekSaiBreacher_GrantsAccelerateToUnitPlayedFromReveal()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031629,
                RiftboundSimulationTestData.BuildDeck(10015, "Fury"),
                RiftboundSimulationTestData.BuildDeck(10016, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
        player.BaseZone.Cards.Add(BuildRuneInstance(421_010, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(421_011, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(421_012, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(421_013, "Fury Rune", "Fury", ownerPlayer: 0));

        var breacher = BuildCardInstance(
            new RiftboundCard
            {
                Id = 421_100,
                Name = "Rek'Sai, Breacher",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 3,
                Power = 0,
                Might = 3,
                Color = ["Fury"],
                GameplayKeywords = ["Accelerate", "Assault"],
                Effect = "Friendly units played from anywhere other than a player's hand have [ACCELERATE].",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.BaseZone.Cards.Add(breacher);

        var voidRush = BuildCardInstance(
            new RiftboundCard
            {
                Id = 421_101,
                Name = "Void Rush",
                Type = "Spell",
                Supertype = "Signature",
                Cost = 2,
                Power = 1,
                Color = ["Fury", "Order"],
                Effect = "Reveal the top 2 cards of your Main Deck. You may banish one, then play it, reducing its cost by [2]. Draw any you didn't banish.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(voidRush);

        var revealedUnit = BuildCardInstance(
            new RiftboundCard
            {
                Id = 421_102,
                Name = "Burrowed Ally",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 2,
                Color = ["Fury"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var filler = BuildCardInstance(
            new RiftboundCard
            {
                Id = 421_103,
                Name = "Minor Tactic",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Fury"],
                Effect = "Draw 1.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(revealedUnit);
        player.MainDeckZone.Cards.Add(filler);

        var legalActions = engine.GetLegalActions(session);
        var selectedAction = legalActions.FirstOrDefault(a =>
            a.ActionType == RiftboundActionType.PlayCard
            && a.ActionId.Contains(voidRush.InstanceId.ToString(), StringComparison.Ordinal)
            && a.ActionId.Contains(revealedUnit.InstanceId.ToString(), StringComparison.Ordinal)
            && a.ActionId.EndsWith("-accelerate", StringComparison.Ordinal)
        );
        if (selectedAction is null)
        {
            var availableVoidRushActions = legalActions
                .Where(a =>
                    a.ActionType == RiftboundActionType.PlayCard
                    && a.ActionId.Contains(voidRush.InstanceId.ToString(), StringComparison.Ordinal)
                )
                .Select(a => a.ActionId)
                .ToList();
            breacher.EffectData.TryGetValue("grantAccelerateForNonHandPlay", out var breacherAura);
            Assert.Fail(
                $"Expected accelerate action for Void Rush reveal. Available actions: {string.Join(" | ", availableVoidRushActions)}; Breacher aura: {breacherAura ?? "<missing>"}"
            );
        }

        var playAction = selectedAction.ActionId;
        var runeDeckBefore = player.RuneDeckZone.Cards.Count;
        Assert.True(engine.ApplyAction(session, playAction).Succeeded);

        var playedUnit = session.Battlefields.SelectMany(x => x.Units).Single(x =>
            x.InstanceId == revealedUnit.InstanceId
        );
        Assert.False(playedUnit.IsExhausted);
        Assert.Equal(0, player.RunePool.Energy);
        Assert.Equal(runeDeckBefore + 2, player.RuneDeckZone.Cards.Count);
    }

    [Fact]
    public void RekSaiSwarmQueen_ActivatedInBattlefield_PlaysUnitHere_AndRecyclesRemaining()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031630,
                RiftboundSimulationTestData.BuildDeck(10017, "Order"),
                RiftboundSimulationTestData.BuildDeck(10018, "Fury")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var orderRuneA = BuildRuneInstance(422_100, "Order Rune", "Order", ownerPlayer: 0);
        var orderRuneB = BuildRuneInstance(422_101, "Order Rune", "Order", ownerPlayer: 0);
        player.BaseZone.Cards.Add(orderRuneA);
        player.BaseZone.Cards.Add(orderRuneB);

        var swarmQueen = BuildCardInstance(
            new RiftboundCard
            {
                Id = 422_200,
                Name = "Rek'Sai, Swarm Queen",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 5,
                Power = 1,
                Might = 5,
                Color = ["Order"],
                Effect = "When I attack, you may reveal the top 2 cards of your Main Deck. You may banish one, then play it. If it is a unit, you may play it here. Recycle the rest.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        session.Battlefields[0].Units.Add(swarmQueen);

        var revealedUnit = BuildCardInstance(
            new RiftboundCard
            {
                Id = 422_201,
                Name = "Xer'sai Vanguard",
                Type = "Unit",
                Cost = 2,
                Power = 0,
                Might = 3,
                Color = ["Order"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var recycledCard = BuildCardInstance(
            new RiftboundCard
            {
                Id = 422_202,
                Name = "Recycle Me",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 1,
                Color = ["Order"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(revealedUnit);
        player.MainDeckZone.Cards.Add(recycledCard);

        var runeDeckBefore = player.RuneDeckZone.Cards.Count;
        var activateAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(swarmQueen.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateAction).Succeeded);

        Assert.True(swarmQueen.IsExhausted);
        Assert.Contains(session.Battlefields[0].Units, c => c.InstanceId == revealedUnit.InstanceId);
        Assert.Single(player.MainDeckZone.Cards, c => c.InstanceId == recycledCard.InstanceId);
        Assert.Equal(runeDeckBefore, player.RuneDeckZone.Cards.Count);
        Assert.True(orderRuneA.IsExhausted);
        Assert.True(orderRuneB.IsExhausted);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Rek'Sai, Swarm Queen"
                && c.Timing == "WhenAttack"
                && c.Metadata.TryGetValue("playedFromReveal", out var played)
                && played == "1"
                && c.Metadata.TryGetValue("recycled", out var recycled)
                && recycled == "1"
        );
    }

    [Fact]
    public void RekSaiVoidBurrower_ActivatedAbility_ExhaustsAndPlaysRevealedCard()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031631,
                RiftboundSimulationTestData.BuildDeck(10019, "Fury"),
                RiftboundSimulationTestData.BuildDeck(10020, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var furyRune = BuildRuneInstance(423_100, "Fury Rune", "Fury", ownerPlayer: 0);
        player.BaseZone.Cards.Add(furyRune);

        var voidBurrower = BuildCardInstance(
            new RiftboundCard
            {
                Id = 423_200,
                Name = "Rek'Sai, Void Burrower",
                Type = "Legend",
                Cost = 0,
                Power = 0,
                Color = ["Fury", "Order"],
                Effect = "When you conquer, you may exhaust me to reveal the top 2 cards of your Main Deck. You may banish one, then play it. Recycle the rest.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.BaseZone.Cards.Add(voidBurrower);

        var revealedUnit = BuildCardInstance(
            new RiftboundCard
            {
                Id = 423_201,
                Name = "Tunnel Fighter",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 2,
                Color = ["Fury"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var recycledCard = BuildCardInstance(
            new RiftboundCard
            {
                Id = 423_202,
                Name = "Keep Cycling",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Order"],
                Effect = "Draw 1.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(revealedUnit);
        player.MainDeckZone.Cards.Add(recycledCard);

        var runeDeckBefore = player.RuneDeckZone.Cards.Count;
        var activateAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(voidBurrower.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateAction).Succeeded);

        Assert.True(voidBurrower.IsExhausted);
        Assert.DoesNotContain(player.MainDeckZone.Cards, c => c.InstanceId == revealedUnit.InstanceId);
        Assert.Contains(
            session.Battlefields.SelectMany(x => x.Units),
            c => c.InstanceId == revealedUnit.InstanceId
        );
        Assert.Single(player.MainDeckZone.Cards, c => c.InstanceId == recycledCard.InstanceId);
        Assert.Equal(runeDeckBefore, player.RuneDeckZone.Cards.Count);
        Assert.True(furyRune.IsExhausted);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Rek'Sai, Void Burrower"
                && c.Timing == "WhenConquer"
                && c.Metadata.TryGetValue("playedFromReveal", out var played)
                && played == "1"
                && c.Metadata.TryGetValue("recycled", out var recycled)
                && recycled == "1"
        );
    }

    [Fact]
    public void BackToBack_TargetsTwoFriendlyUnits_BuffsSelectedUnits()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031619,
                RiftboundSimulationTestData.BuildDeck(9987, "Order"),
                RiftboundSimulationTestData.BuildDeck(9988, "Chaos")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var orderRunes = Enumerable
            .Range(0, 3)
            .Select(i => BuildRuneInstance(213_100 + i, "Order Rune", "Order", ownerPlayer: 0))
            .ToList();
        player.BaseZone.Cards.AddRange(orderRunes);

        var backToBack = BuildCardInstance(
            new RiftboundCard
            {
                Id = 413_100,
                Name = "Back to Back",
                Type = "Spell",
                Cost = 3,
                Power = 0,
                Color = ["Order"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] Give two friendly units each +2 [Might] this turn.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(backToBack);

        var baseTarget = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Base Target", might: 2);
        var battlefieldTarget = BuildUnit(
            ownerPlayer: 0,
            controllerPlayer: 0,
            name: "Battlefield Target",
            might: 2
        );
        var notSelected = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Not Selected", might: 2);
        player.BaseZone.Cards.Add(baseTarget);
        player.BaseZone.Cards.Add(notSelected);
        session.Battlefields[0].Units.Add(battlefieldTarget);

        foreach (var rune in orderRunes)
        {
            var activateAction = engine
                .GetLegalActions(session)
                .First(a => a.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateAction).Succeeded);
        }

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(backToBack.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(baseTarget.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(battlefieldTarget.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(2, baseTarget.TemporaryMightModifier);
        Assert.Equal(2, battlefieldTarget.TemporaryMightModifier);
        Assert.Equal(0, notSelected.TemporaryMightModifier);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Back to Back"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("targets", out var targets)
                && targets == "2"
        );
    }

    [Fact]
    public void AgainstTheOdds_TargetsOnlyFriendlyBattlefieldUnits_AndBuffsByEnemyCount()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031620,
                RiftboundSimulationTestData.BuildDeck(9989, "Fury"),
                RiftboundSimulationTestData.BuildDeck(9990, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var furyRunes = Enumerable
            .Range(0, 2)
            .Select(i => BuildRuneInstance(214_100 + i, "Fury Rune", "Fury", ownerPlayer: 0))
            .ToList();
        player.BaseZone.Cards.AddRange(furyRunes);

        var againstTheOdds = BuildCardInstance(
            new RiftboundCard
            {
                Id = 414_100,
                Name = "Against the Odds",
                Type = "Spell",
                Cost = 2,
                Power = 0,
                Color = ["Fury"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] Give a friendly unit at a battlefield +2 [Might] this turn for each enemy unit there.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(againstTheOdds);

        var friendlyBaseUnit = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Base Ally", might: 2);
        var friendlyBattlefieldUnit = BuildUnit(
            ownerPlayer: 0,
            controllerPlayer: 0,
            name: "Battle Ally",
            might: 2
        );
        player.BaseZone.Cards.Add(friendlyBaseUnit);
        session.Battlefields[1].Units.Add(friendlyBattlefieldUnit);
        session.Battlefields[1].Units.Add(BuildUnit(1, 1, "Enemy One", 2));
        session.Battlefields[1].Units.Add(BuildUnit(1, 1, "Enemy Two", 2));
        session.Battlefields[1].Units.Add(BuildUnit(1, 1, "Enemy Three", 2));

        foreach (var rune in furyRunes)
        {
            var activateAction = engine
                .GetLegalActions(session)
                .First(a => a.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateAction).Succeeded);
        }

        var legalActions = engine.GetLegalActions(session);
        Assert.DoesNotContain(
            legalActions,
            a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(againstTheOdds.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(friendlyBaseUnit.InstanceId.ToString(), StringComparison.Ordinal)
        );

        var castAction = legalActions
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(againstTheOdds.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(friendlyBattlefieldUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(6, friendlyBattlefieldUnit.TemporaryMightModifier);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Against the Odds"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("enemyUnits", out var enemyUnits)
                && enemyUnits == "3"
                && c.Metadata.TryGetValue("totalBuff", out var totalBuff)
                && totalBuff == "6"
        );
    }

    [Fact]
    public void Meditation_WithoutExhaustAdditionalCost_DrawsOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031621,
                RiftboundSimulationTestData.BuildDeck(9991, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9992, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var calmRunes = Enumerable
            .Range(0, 2)
            .Select(i => BuildRuneInstance(215_100 + i, "Calm Rune", "Calm", ownerPlayer: 0))
            .ToList();
        player.BaseZone.Cards.AddRange(calmRunes);

        var meditation = BuildCardInstance(
            new RiftboundCard
            {
                Id = 415_100,
                Name = "Meditation",
                Type = "Spell",
                Cost = 2,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] As an additional cost to play this, you may exhaust a friendly unit. If you do, draw 2. Otherwise, draw 1.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(meditation);

        player.MainDeckZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard
                {
                    Id = 415_200,
                    Name = "Draw A",
                    Type = "Unit",
                    Cost = 1,
                    Power = 0,
                    Might = 1,
                    Color = ["Calm"],
                },
                ownerPlayer: 0,
                controllerPlayer: 0
            )
        );
        player.MainDeckZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard
                {
                    Id = 415_201,
                    Name = "Draw B",
                    Type = "Unit",
                    Cost = 1,
                    Power = 0,
                    Might = 1,
                    Color = ["Calm"],
                },
                ownerPlayer: 0,
                controllerPlayer: 0
            )
        );

        foreach (var rune in calmRunes)
        {
            var activateAction = engine
                .GetLegalActions(session)
                .First(a => a.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateAction).Succeeded);
        }

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(meditation.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Single(player.HandZone.Cards);
        Assert.Single(player.MainDeckZone.Cards);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Meditation"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("draw", out var draw)
                && draw == "1"
                && c.Metadata.TryGetValue("usedAdditionalCost", out var used)
                && used == "false"
        );
    }

    [Fact]
    public void Meditation_WithExhaustAdditionalCost_DrawsTwo()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031622,
                RiftboundSimulationTestData.BuildDeck(9993, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9994, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var calmRunes = Enumerable
            .Range(0, 2)
            .Select(i => BuildRuneInstance(216_100 + i, "Calm Rune", "Calm", ownerPlayer: 0))
            .ToList();
        player.BaseZone.Cards.AddRange(calmRunes);

        var meditation = BuildCardInstance(
            new RiftboundCard
            {
                Id = 416_100,
                Name = "Meditation",
                Type = "Spell",
                Cost = 2,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] As an additional cost to play this, you may exhaust a friendly unit. If you do, draw 2. Otherwise, draw 1.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(meditation);

        var friendlyUnit = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Meditator", might: 2);
        player.BaseZone.Cards.Add(friendlyUnit);

        player.MainDeckZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard
                {
                    Id = 416_200,
                    Name = "Draw One",
                    Type = "Unit",
                    Cost = 1,
                    Power = 0,
                    Might = 1,
                    Color = ["Calm"],
                },
                ownerPlayer: 0,
                controllerPlayer: 0
            )
        );
        player.MainDeckZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard
                {
                    Id = 416_201,
                    Name = "Draw Two",
                    Type = "Unit",
                    Cost = 1,
                    Power = 0,
                    Might = 1,
                    Color = ["Calm"],
                },
                ownerPlayer: 0,
                controllerPlayer: 0
            )
        );
        player.MainDeckZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard
                {
                    Id = 416_202,
                    Name = "Draw Three",
                    Type = "Unit",
                    Cost = 1,
                    Power = 0,
                    Might = 1,
                    Color = ["Calm"],
                },
                ownerPlayer: 0,
                controllerPlayer: 0
            )
        );

        foreach (var rune in calmRunes)
        {
            var activateAction = engine
                .GetLegalActions(session)
                .First(a => a.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateAction).Succeeded);
        }

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(meditation.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains($"-exhaust-unit-{friendlyUnit.InstanceId}", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.True(friendlyUnit.IsExhausted);
        Assert.Equal(2, player.HandZone.Cards.Count);
        Assert.Single(player.MainDeckZone.Cards);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Meditation"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("draw", out var draw)
                && draw == "2"
                && c.Metadata.TryGetValue("usedAdditionalCost", out var used)
                && used == "true"
                && c.Metadata.TryGetValue("exhaustedUnit", out var exhausted)
                && exhausted == "Meditator"
        );
    }

    [Fact]
    public void AcceptableLosses_KillsSelectedFriendlyGear_AndOneOpponentGear()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031623,
                RiftboundSimulationTestData.BuildDeck(9995, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9996, "Order")
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
        opponent.BaseZone.Cards.Clear();
        opponent.TrashZone.Cards.Clear();

        player.BaseZone.Cards.Add(BuildRuneInstance(217_100, "Chaos Rune", "Chaos", ownerPlayer: 0));

        var acceptableLosses = BuildCardInstance(
            new RiftboundCard
            {
                Id = 417_100,
                Name = "Acceptable Losses",
                Type = "Spell",
                Cost = 1,
                Power = 0,
                Color = ["Chaos"],
                GameplayKeywords = ["Action"],
                Effect = "[ACTION] Each player kills one of their gear.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(acceptableLosses);

        var friendlyGearA = BuildCardInstance(
            new RiftboundCard
            {
                Id = 417_200,
                Name = "Friendly Gear A",
                Type = "Gear",
                Cost = 1,
                Power = 0,
                Color = ["Chaos"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var friendlyGearB = BuildCardInstance(
            new RiftboundCard
            {
                Id = 417_201,
                Name = "Friendly Gear B",
                Type = "Gear",
                Cost = 1,
                Power = 0,
                Color = ["Chaos"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.BaseZone.Cards.Add(friendlyGearA);
        player.BaseZone.Cards.Add(friendlyGearB);

        var opponentGearA = BuildCardInstance(
            new RiftboundCard
            {
                Id = 417_300,
                Name = "Opponent Gear A",
                Type = "Gear",
                Cost = 1,
                Power = 0,
                Color = ["Order"],
            },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        var opponentGearB = BuildCardInstance(
            new RiftboundCard
            {
                Id = 417_301,
                Name = "Opponent Gear B",
                Type = "Gear",
                Cost = 1,
                Power = 0,
                Color = ["Order"],
            },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        opponent.BaseZone.Cards.Add(opponentGearA);
        opponent.BaseZone.Cards.Add(opponentGearB);

        var activateRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains("activate-rune", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRuneAction).Succeeded);

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(acceptableLosses.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(friendlyGearB.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.DoesNotContain(player.BaseZone.Cards, x => x.InstanceId == friendlyGearB.InstanceId);
        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == friendlyGearB.InstanceId);
        Assert.Single(
            opponent.TrashZone.Cards,
            x => string.Equals(x.Type, "Gear", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Single(
            opponent.BaseZone.Cards,
            x => string.Equals(x.Type, "Gear", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Acceptable Losses"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("killedFriendlyGear", out var killedFriendly)
                && killedFriendly == "true"
                && c.Metadata.TryGetValue("killedOpponentGear", out var killedOpponent)
                && killedOpponent == "true"
        );
    }

    [Fact]
    public void FallingStar_CounteredByWindWall_PreventsResolution_AndStillPaysDeflectCost()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031624,
                RiftboundSimulationTestData.BuildDeck(9997, "Fury"),
                RiftboundSimulationTestData.BuildDeck(9998, "Calm")
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

        player.BaseZone.Cards.Add(BuildRuneInstance(218_100, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(218_101, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(218_102, "Fury Rune", "Fury", ownerPlayer: 0));
        opponent.BaseZone.Cards.Add(BuildRuneInstance(218_200, "Calm Rune", "Calm", ownerPlayer: 1));
        opponent.BaseZone.Cards.Add(BuildRuneInstance(218_201, "Calm Rune", "Calm", ownerPlayer: 1));
        opponent.BaseZone.Cards.Add(BuildRuneInstance(218_202, "Calm Rune", "Calm", ownerPlayer: 1));

        var fallingStar = BuildCardInstance(
            new RiftboundCard
            {
                Id = 418_100,
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

        var windWall = BuildCardInstance(
            new RiftboundCard
            {
                Id = 418_200,
                Name = "Wind Wall",
                Type = "Spell",
                Cost = 3,
                Power = 2,
                Color = ["Calm"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] Counter a spell.",
            },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        opponent.HandZone.Cards.Add(windWall);

        var irelia = BuildCardInstance(
            new RiftboundCard
            {
                Id = 418_300,
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
        var castFallingStarAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(fallingStar.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(irelia.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castFallingStarAction).Succeeded);

        Assert.Equal(runeDeckCountBefore + 3, player.RuneDeckZone.Cards.Count);
        Assert.Equal(0, irelia.TemporaryMightModifier);

        var playWindWallAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(windWall.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(fallingStar.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playWindWallAction).Succeeded);

        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Equal(0, irelia.MarkedDamage);
        Assert.Contains(opponent.BaseZone.Cards, x => x.InstanceId == irelia.InstanceId);
        Assert.DoesNotContain(
            session.EffectContexts,
            c => c.Source == "Falling Star" && c.Timing == "Resolve"
        );
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Wind Wall"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("counteredSpell", out var spell)
                && spell == "Falling Star"
        );
    }
    
     [Fact]
    public void FallingStar_WithDisciplineOnIrelia_IncreasesMightBy3_AndStillPaysDeflectCost()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031624,
                RiftboundSimulationTestData.BuildDeck(9997, "Fury"),
                RiftboundSimulationTestData.BuildDeck(9998, "Calm")
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

        player.BaseZone.Cards.Add(BuildRuneInstance(218_100, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(218_101, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(218_102, "Fury Rune", "Fury", ownerPlayer: 0));
        opponent.BaseZone.Cards.Add(BuildRuneInstance(218_200, "Calm Rune", "Calm", ownerPlayer: 1));
        opponent.BaseZone.Cards.Add(BuildRuneInstance(218_201, "Calm Rune", "Calm", ownerPlayer: 1));

        var fallingStar = BuildCardInstance(
            new RiftboundCard
            {
                Id = 418_100,
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

        var discipline = BuildCardInstance(
            new RiftboundCard
            {
                Id = 407_100,
                Name = "Discipline",
                Type = "Spell",
                Cost = 2,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] Give a unit +2 [Might] this turn. Draw 1.",
            },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        opponent.HandZone.Cards.Add(discipline);

        var irelia = BuildCardInstance(
            new RiftboundCard
            {
                Id = 418_300,
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
        var castFallingStarAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(fallingStar.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(irelia.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castFallingStarAction).Succeeded);

        Assert.Equal(runeDeckCountBefore + 3, player.RuneDeckZone.Cards.Count);
        Assert.Equal(0, irelia.TemporaryMightModifier);

        var playDiscipline = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(discipline.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(irelia.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playDiscipline).Succeeded);

        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Equal(3, irelia.TemporaryMightModifier);
        Assert.Equal(6, irelia.MarkedDamage);
        Assert.Contains(opponent.BaseZone.Cards, x => x.InstanceId == irelia.InstanceId);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Irelia, Fervent"
                && c.Timing == "WhenChosen"
                && c.Metadata.TryGetValue("sourceCard", out var sourceCard)
                && sourceCard == "Discipline"
        );
        Assert.DoesNotContain(
            session.EffectContexts,
            c =>
                c.Source == "Irelia, Fervent"
                && c.Timing == "WhenChosen"
                && c.Metadata.TryGetValue("sourceCard", out var sourceCard)
                && sourceCard == "Falling Star"
        );
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Falling Star"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("target", out var target)
                && target == "Irelia, Fervent"
        );
    }

    [Fact]
    public void FallingStar_WithTwoDifferentTargets_DealsThreeToEachTarget()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031627,
                RiftboundSimulationTestData.BuildDeck(10011, "Fury"),
                RiftboundSimulationTestData.BuildDeck(10012, "Order")
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
        player.BaseZone.Cards.Add(BuildRuneInstance(418_410, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(418_411, "Fury Rune", "Fury", ownerPlayer: 0));

        var fallingStar = BuildCardInstance(
            new RiftboundCard
            {
                Id = 418_400,
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

        var unitA = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Target A", might: 4);
        var unitB = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Target B", might: 4);
        opponent.BaseZone.Cards.Add(unitA);
        opponent.BaseZone.Cards.Add(unitB);

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(fallingStar.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains("-target-units-", StringComparison.Ordinal)
                && a.ActionId.Contains(unitA.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(unitB.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Equal(3, unitA.MarkedDamage);
        Assert.Equal(3, unitB.MarkedDamage);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Falling Star"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("target", out var targets)
                && targets.Contains("Target A", StringComparison.Ordinal)
                && targets.Contains("Target B", StringComparison.Ordinal)
        );
    }

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

    [Fact]
    public void PlayCard_WithMulticolorPower_CanRecycleEitherColorRune()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031601,
                RiftboundSimulationTestData.BuildDeck(9300, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9400, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();

        var furyRune = BuildRuneInstance(501_001, "Fury Rune", "Fury", ownerPlayer: 0);
        var mindRune = BuildRuneInstance(501_002, "Mind Rune", "Mind", ownerPlayer: 0);
        player.BaseZone.Cards.Add(furyRune);
        player.BaseZone.Cards.Add(mindRune);

        var multicolorSpell = new RiftboundCard
        {
            Id = 700_001,
            Name = "Split Domain Spell",
            Type = "Spell",
            Cost = 0,
            Power = 2,
            Color = ["Fury", "Mind"],
        };
        var spellInstance = BuildCardInstance(multicolorSpell, ownerPlayer: 0, controllerPlayer: 0);
        player.HandZone.Cards.Add(spellInstance);
        var runeDeckCountBefore = player.RuneDeckZone.Cards.Count;
        var baseRuneCountBefore = player.BaseZone.Cards.Count(c => string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase));

        var playAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(spellInstance.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell", StringComparison.Ordinal)
            )
            .ActionId;

        var result = engine.ApplyAction(session, playAction);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(baseRuneCountBefore - 2, player.BaseZone.Cards.Count(c => string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(runeDeckCountBefore + 2, player.RuneDeckZone.Cards.Count);
    }
    
        [Fact]
    public void PlayCard_WithMulticolorPower_NotEnoughRunes()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031601,
                RiftboundSimulationTestData.BuildDeck(9300, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9400, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();

        var furyRune = BuildRuneInstance(501_001, "Fury Rune", "Fury", ownerPlayer: 0);
        player.BaseZone.Cards.Add(furyRune);

        var multicolorSpell = new RiftboundCard
        {
            Id = 700_001,
            Name = "Split Domain Spell",
            Type = "Spell",
            Cost = 0,
            Power = 2,
            Color = ["Fury", "Mind"],
        };
        var spellInstance = BuildCardInstance(multicolorSpell, ownerPlayer: 0, controllerPlayer: 0);
        player.HandZone.Cards.Add(spellInstance);
        
        Assert.DoesNotContain(spellInstance.InstanceId.ToString(), engine.GetLegalActions(session).Select(x => x.ActionId));
    }
    
    [Fact]
    public void PlayCard_WithPower_CorrectRunes()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031601,
                RiftboundSimulationTestData.BuildDeck(9300, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9400, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();

        var furyRune = BuildRuneInstance(501_001, "Fury Rune", "Fury", ownerPlayer: 0);
        player.BaseZone.Cards.Add(furyRune);

        var multicolorSpell = new RiftboundCard
        {
            Id = 700_001,
            Name = "Split Domain Spell",
            Type = "Spell",
            Cost = 0,
            Power = 1,
            Color = ["Fury"],
        };
        var spellInstance = BuildCardInstance(multicolorSpell, ownerPlayer: 0, controllerPlayer: 0);
        player.HandZone.Cards.Add(spellInstance);
        
        Assert.DoesNotContain(spellInstance.InstanceId.ToString(), engine.GetLegalActions(session).Select(x => x.ActionId));
    }
    
    [Fact]
    public void PlayCard_WithPower_WrongRunes()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031601,
                RiftboundSimulationTestData.BuildDeck(9300, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9400, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();

        var furyRune = BuildRuneInstance(501_001, "Fury Rune", "Fury", ownerPlayer: 0);
        player.BaseZone.Cards.Add(furyRune);

        var multicolorSpell = new RiftboundCard
        {
            Id = 700_001,
            Name = "Split Domain Spell",
            Type = "Spell",
            Cost = 0,
            Power = 1,
            Color = ["Mind"],
        };
        var spellInstance = BuildCardInstance(multicolorSpell, ownerPlayer: 0, controllerPlayer: 0);
        player.HandZone.Cards.Add(spellInstance);
        
        Assert.DoesNotContain(spellInstance.InstanceId.ToString(), engine.GetLegalActions(session).Select(x => x.ActionId));
    }

    [Fact]
    public void PlayCard_WithHiddenPower_CanRecycleAnyRune()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031602,
                RiftboundSimulationTestData.BuildDeck(9500, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9600, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();

        player.BaseZone.Cards.Add(BuildRuneInstance(601_001, "Chaos Rune", "Chaos", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(601_002, "Order Rune", "Order", ownerPlayer: 0));

        var hiddenSpell = new RiftboundCard
        {
            Id = 800_001,
            Name = "Hidden Technique",
            Type = "Spell",
            Cost = 0,
            Power = 2,
            Color = ["Mind"],
            Tags = ["Hidden"],
        };
        var spellInstance = BuildCardInstance(hiddenSpell, ownerPlayer: 0, controllerPlayer: 0);
        player.HandZone.Cards.Add(spellInstance);
        var runeDeckCountBefore = player.RuneDeckZone.Cards.Count;
        var baseRuneCountBefore = player.BaseZone.Cards.Count(c => string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase));

        var playAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(spellInstance.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell", StringComparison.Ordinal)
            )
            .ActionId;

        var result = engine.ApplyAction(session, playAction);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(baseRuneCountBefore - 2, player.BaseZone.Cards.Count(c => string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(runeDeckCountBefore + 2, player.RuneDeckZone.Cards.Count);
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

    private static int ReadPower(PlayerState player, string domain)
    {
        return player.RunePool.PowerByDomain.TryGetValue(domain, out var value) ? value : 0;
    }

    private static CardInstance BuildRuneInstance(
        long cardId,
        string runeName,
        string domain,
        int ownerPlayer
    )
    {
        var runeCard = new RiftboundCard
        {
            Id = cardId,
            Name = runeName,
            Type = "Rune",
            Color = [domain],
        };
        return BuildCardInstance(runeCard, ownerPlayer, ownerPlayer);
    }

    private static CardInstance BuildCardInstance(
        RiftboundCard card,
        int ownerPlayer,
        int controllerPlayer
    )
    {
        var template = RiftboundEffectTemplateResolver.Resolve(card);
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (card.GameplayKeywords is not null)
        {
            foreach (var keyword in card.GameplayKeywords.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                keywords.Add(keyword.Trim());
            }
        }

        if (card.Tags is not null)
        {
            foreach (var tag in card.Tags.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                keywords.Add(tag.Trim());
            }
        }

        foreach (var keyword in template.Keywords)
        {
            keywords.Add(keyword);
        }

        return new CardInstance
        {
            InstanceId = Guid.NewGuid(),
            CardId = card.Id,
            Name = card.Name,
            Type = card.Type ?? "Card",
            OwnerPlayerIndex = ownerPlayer,
            ControllerPlayerIndex = controllerPlayer,
            Cost = card.Cost,
            Power = card.Power,
            ColorDomains = card.Color?.ToList() ?? [],
            Might = card.Might,
            Keywords = keywords.ToList(),
            EffectTemplateId = template.TemplateId,
            EffectData = template.Data.ToDictionary(
                x => x.Key,
                x => x.Value,
                StringComparer.OrdinalIgnoreCase
            ),
        };
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
