using Application.Features.Riftbound.Simulation.Effects;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineBattlefieldChoiceBehaviorTests
    : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void ZaunWarrens_OnConquer_UsesChosenDiscardFromAction()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSessionWithBattlefieldName(engine, 2026032801, "Zaun Warrens");
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var keepCard = BuildCardInstance(
            new RiftboundCard { Id = 960001, Name = "Keep Card", Type = "Spell", Cost = 1, Power = 0 },
            0,
            0
        );
        var chosenDiscard = BuildCardInstance(
            new RiftboundCard { Id = 960002, Name = "Chosen Discard", Type = "Spell", Cost = 5, Power = 0 },
            0,
            0
        );
        var drawCard = BuildCardInstance(
            new RiftboundCard { Id = 960003, Name = "Draw Card", Type = "Unit", Cost = 0, Might = 1 },
            0,
            0
        );
        player.HandZone.Cards.AddRange([keepCard, chosenDiscard]);
        player.MainDeckZone.Cards.Add(drawCard);

        var attacker = BuildUnit(0, 0, "Attacker", 3);
        player.BaseZone.Cards.Add(attacker);
        session.Battlefields[0].Units.Add(BuildUnit(1, 1, "Defender", 1));
        session.Battlefields[0].ControlledByPlayerIndex = 1;

        var moveAction = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.StandardMove
                && x.ActionId.Contains($"move-{attacker.InstanceId}", StringComparison.Ordinal)
                && x.ActionId.Contains("-to-bf-0", StringComparison.Ordinal)
                && x.ActionId.Contains(
                    $"{ZaunWarrensEffect.DiscardChoiceMarker}{chosenDiscard.InstanceId}",
                    StringComparison.Ordinal
                )
            )
            .ActionId;

        Assert.True(engine.ApplyAction(session, moveAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == chosenDiscard.InstanceId);
        Assert.DoesNotContain(player.TrashZone.Cards, x => x.InstanceId == keepCard.InstanceId);
        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == keepCard.InstanceId);
        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == drawCard.InstanceId);
    }

    [Fact]
    public void EmperorsDais_OnConquer_UsesChosenUnitReturnFromAction()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSessionWithBattlefieldName(engine, 2026032802, "Emperor's Dais");
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        player.RunePool.Energy = 1;

        var attacker = BuildUnit(0, 0, "Attacker", 3);
        var returnCandidate = BuildUnit(0, 0, "Return Me", 2);
        var stayCandidate = BuildUnit(0, 0, "Stay Here", 1);
        player.BaseZone.Cards.Add(attacker);
        session.Battlefields[0].Units.AddRange([returnCandidate, stayCandidate, BuildUnit(1, 1, "Defender", 1)]);
        session.Battlefields[0].ControlledByPlayerIndex = 1;

        var moveAction = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.StandardMove
                && x.ActionId.Contains($"move-{attacker.InstanceId}", StringComparison.Ordinal)
                && x.ActionId.Contains("-to-bf-0", StringComparison.Ordinal)
                && x.ActionId.Contains(
                    $"{EmperorsDaisEffect.ReturnUnitChoiceMarker}{returnCandidate.InstanceId}",
                    StringComparison.Ordinal
                )
            )
            .ActionId;

        Assert.True(engine.ApplyAction(session, moveAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == returnCandidate.InstanceId);
        Assert.DoesNotContain(session.Battlefields[0].Units, x => x.InstanceId == returnCandidate.InstanceId);
        Assert.Contains(session.Battlefields[0].Units, x => x.InstanceId == stayCandidate.InstanceId);
        Assert.Contains(
            session.Battlefields[0].Units,
            x => x.ControllerPlayerIndex == player.PlayerIndex && x.Name == "Sand Soldier Token"
        );
    }

    private static GameSession CreateSessionWithBattlefieldName(
        RiftboundSimulationEngine engine,
        int seed,
        string battlefieldName
    )
    {
        var challenger = RiftboundSimulationTestData.BuildDeck(
            9500 + seed,
            "Chaos",
            deck => deck.Battlefields[0].Card!.Name = battlefieldName
        );
        var opponent = RiftboundSimulationTestData.BuildDeck(
            9600 + seed,
            "Order",
            deck => deck.Battlefields[0].Card!.Name = battlefieldName
        );
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                simulationId: 1,
                userId: 9,
                seed: seed,
                challengerDeck: challenger,
                opponentDeck: opponent
            )
        );
        if (session.Battlefields.Count > 0)
        {
            var current = session.Battlefields[0];
            session.Battlefields[0] = new BattlefieldState
            {
                CardId = current.CardId,
                Name = battlefieldName,
                Index = current.Index,
                ControlledByPlayerIndex = current.ControlledByPlayerIndex,
                ContestedByPlayerIndex = current.ContestedByPlayerIndex,
                IsShowdownStaged = current.IsShowdownStaged,
                IsCombatStaged = current.IsCombatStaged,
                Units = current.Units,
                Gear = current.Gear,
                HiddenCards = current.HiddenCards,
            };
        }

        return session;
    }

    private static void ResetPlayer(PlayerState player)
    {
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.RuneDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.LegendZone.Cards.Clear();
        player.ChampionZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
    }
}
