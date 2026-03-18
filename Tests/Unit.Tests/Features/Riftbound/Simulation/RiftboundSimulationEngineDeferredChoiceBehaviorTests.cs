using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineDeferredChoiceBehaviorTests
    : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void ForecasterVision_KeepChoice_KeepsTopCard()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032901);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var forecaster = BuildCardInstance(
            new RiftboundCard
            {
                Id = 981001,
                Name = "Forecaster",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 2,
                Color = ["Mind"],
                Effect = "Your Mechs have [Vision].",
            },
            0,
            0
        );
        player.BaseZone.Cards.Add(forecaster);

        var mech = BuildCardInstance(
            new RiftboundCard
            {
                Id = 981002,
                Name = "Test Mech",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 2,
                Color = ["Mind"],
                Tags = ["Mech"],
            },
            0,
            0
        );
        player.HandZone.Cards.Add(mech);

        var topA = BuildCardInstance(
            new RiftboundCard { Id = 981003, Name = "Top A", Type = "Spell", Cost = 1, Power = 0, Color = ["Mind"] },
            0,
            0
        );
        var topB = BuildCardInstance(
            new RiftboundCard { Id = 981004, Name = "Top B", Type = "Spell", Cost = 1, Power = 0, Color = ["Mind"] },
            0,
            0
        );
        player.MainDeckZone.Cards.AddRange([topA, topB]);

        var playMech = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.PlayCard
                && x.ActionId.Contains(mech.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playMech).Succeeded);

        Assert.DoesNotContain(
            engine.GetLegalActions(session),
            x => x.ActionId.Contains("choose-stacked-deck-", StringComparison.Ordinal)
        );
        var keepChoice = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains("choose-vision-keep-", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, keepChoice).Succeeded);

        Assert.Equal(topA.InstanceId, player.MainDeckZone.Cards[0].InstanceId);
        Assert.Contains(
            session.EffectContexts,
            x =>
                x.Source == "Forecaster"
                && x.Timing == "Vision"
                && x.Metadata.TryGetValue("kept", out var kept)
                && kept == "true"
        );
    }

    [Fact]
    public void ForecasterVision_RecycleChoice_MovesTopCardToBottom()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032902);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        player.BaseZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard
                {
                    Id = 982001,
                    Name = "Forecaster",
                    Type = "Unit",
                    Cost = 0,
                    Power = 0,
                    Might = 2,
                    Color = ["Mind"],
                    Effect = "Your Mechs have [Vision].",
                },
                0,
                0
            )
        );
        var mech = BuildCardInstance(
            new RiftboundCard
            {
                Id = 982002,
                Name = "Recycle Mech",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 2,
                Color = ["Mind"],
                Tags = ["Mech"],
            },
            0,
            0
        );
        player.HandZone.Cards.Add(mech);

        var topA = BuildCardInstance(
            new RiftboundCard { Id = 982003, Name = "Top A", Type = "Spell", Cost = 1, Power = 0, Color = ["Mind"] },
            0,
            0
        );
        var topB = BuildCardInstance(
            new RiftboundCard { Id = 982004, Name = "Top B", Type = "Spell", Cost = 1, Power = 0, Color = ["Mind"] },
            0,
            0
        );
        player.MainDeckZone.Cards.AddRange([topA, topB]);

        var playMech = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.PlayCard
                && x.ActionId.Contains(mech.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playMech).Succeeded);
        var recycleChoice = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains("choose-vision-recycle-", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, recycleChoice).Succeeded);

        Assert.Equal(topB.InstanceId, player.MainDeckZone.Cards[0].InstanceId);
        Assert.Equal(topA.InstanceId, player.MainDeckZone.Cards[^1].InstanceId);
        Assert.Contains(
            session.EffectContexts,
            x =>
                x.Source == "Forecaster"
                && x.Timing == "Vision"
                && x.Metadata.TryGetValue("recycled", out var recycled)
                && recycled == "true"
        );
    }

    [Fact]
    public void StackedDeck_ChoiceAppearsOnlyAfterSpellResolutionStarts()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032903);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var stackedDeck = BuildCardInstance(
            new RiftboundCard
            {
                Id = 983001,
                Name = "Stacked Deck",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                Color = ["Chaos"],
                GameplayKeywords = ["Action"],
                Effect = "Look at the top 3 cards of your Main Deck. Put 1 into your hand and recycle the rest.",
            },
            0,
            0
        );
        player.HandZone.Cards.Add(stackedDeck);

        var first = BuildCardInstance(
            new RiftboundCard { Id = 983002, Name = "First", Type = "Unit", Cost = 2, Might = 2, Color = ["Chaos"] },
            0,
            0
        );
        var second = BuildCardInstance(
            new RiftboundCard { Id = 983003, Name = "Second", Type = "Unit", Cost = 1, Might = 1, Color = ["Chaos"] },
            0,
            0
        );
        var third = BuildCardInstance(
            new RiftboundCard { Id = 983004, Name = "Third", Type = "Unit", Cost = 3, Might = 3, Color = ["Chaos"] },
            0,
            0
        );
        player.MainDeckZone.Cards.AddRange([first, second, third]);

        Assert.DoesNotContain(
            engine.GetLegalActions(session),
            x => x.ActionId.Contains("choose-stacked-deck-", StringComparison.Ordinal)
        );

        var playStackedDeck = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.PlayCard
                && x.ActionId.Contains(stackedDeck.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-spell", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playStackedDeck).Succeeded);

        var chooseSecond = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains("choose-stacked-deck-", StringComparison.Ordinal)
                && x.ActionId.Contains(second.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, chooseSecond).Succeeded);

        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == second.InstanceId);
        Assert.Contains(
            session.EffectContexts,
            x =>
                x.Source == "Stacked Deck"
                && x.Timing == "Resolve"
                && x.Metadata.TryGetValue("drawn", out var drawn)
                && drawn == "Second"
        );
    }

    [Fact]
    public void ReaversRow_WhenDefending_CanChooseToNotMoveAnyUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSessionWithBattlefieldName(engine, 2026032904, "Reaver's Row");
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var attacker = BuildUnit(0, 0, "Attacker", 3);
        player.BaseZone.Cards.Add(attacker);
        var defender = BuildUnit(1, 1, "Defender", 2);
        session.Battlefields[0].Units.Add(defender);
        session.Battlefields[0].ControlledByPlayerIndex = 1;

        var moveAction = engine.GetLegalActions(session)
            .First(x => x.ActionId.EndsWith($"move-{attacker.InstanceId}-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, moveAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        var chooseNone = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains("choose-reavers-row-none", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, chooseNone).Succeeded);

        Assert.DoesNotContain(opponent.BaseZone.Cards, x => x.InstanceId == defender.InstanceId);
        Assert.Contains(
            session.EffectContexts,
            x =>
                x.Source == "Reaver's Row"
                && x.Timing == "WhenDefend"
                && x.Metadata.TryGetValue("moved", out var moved)
                && moved == "false"
        );
    }

    private static GameSession CreateSession(RiftboundSimulationEngine engine, int seed)
    {
        return engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                seed,
                RiftboundSimulationTestData.BuildDeck(seed + 11, "Mind"),
                RiftboundSimulationTestData.BuildDeck(seed + 12, "Order")
            )
        );
    }

    private static GameSession CreateSessionWithBattlefieldName(
        RiftboundSimulationEngine engine,
        int seed,
        string battlefieldName
    )
    {
        var challenger = RiftboundSimulationTestData.BuildDeck(
            9800 + seed,
            "Chaos",
            deck => deck.Battlefields[0].Card!.Name = battlefieldName
        );
        var opponent = RiftboundSimulationTestData.BuildDeck(
            9900 + seed,
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
