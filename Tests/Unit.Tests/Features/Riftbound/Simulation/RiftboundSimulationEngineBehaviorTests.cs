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
