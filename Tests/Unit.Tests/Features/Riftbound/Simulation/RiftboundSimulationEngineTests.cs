using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

public class RiftboundSimulationEngineTests
{
    [Fact]
    public void CreateSession_IsDeterministic_ForSameSeed()
    {
        var engine = new RiftboundSimulationEngine();
        var challenger = BuildDeck(11, "Chaos");
        var opponent = BuildDeck(22, "Order");

        var sessionA = engine.CreateSession(
            new RiftboundSimulationEngineSetup(
                SimulationId: 1,
                RequestedByUserId: 77,
                Seed: 12345,
                RulesetVersion: "test-ruleset",
                ChallengerDeck: challenger,
                OpponentDeck: opponent,
                ChallengerPolicy: "heuristic",
                OpponentPolicy: "heuristic"
            )
        );
        var sessionB = engine.CreateSession(
            new RiftboundSimulationEngineSetup(
                SimulationId: 2,
                RequestedByUserId: 77,
                Seed: 12345,
                RulesetVersion: "test-ruleset",
                ChallengerDeck: challenger,
                OpponentDeck: opponent,
                ChallengerPolicy: "heuristic",
                OpponentPolicy: "heuristic"
            )
        );

        Assert.Equal(
            sessionA.Battlefields.Select(x => x.CardId).ToArray(),
            sessionB.Battlefields.Select(x => x.CardId).ToArray()
        );
        Assert.Equal(
            sessionA.Players[0].HandZone.Cards.Select(x => x.CardId).ToArray(),
            sessionB.Players[0].HandZone.Cards.Select(x => x.CardId).ToArray()
        );
        Assert.Equal(5, sessionA.Players[0].HandZone.Cards.Count);
        Assert.Equal(4, sessionA.Players[1].HandZone.Cards.Count);
        Assert.Equal(RiftboundTurnPhase.Action, sessionA.Phase);
    }

    [Fact]
    public void EndTurn_GivesPlayerTwoExtraRuneOnTheirFirstTurn()
    {
        var engine = new RiftboundSimulationEngine();
        var challenger = BuildDeck(101, "Chaos");
        var opponent = BuildDeck(202, "Order");

        var session = engine.CreateSession(
            new RiftboundSimulationEngineSetup(
                SimulationId: 1,
                RequestedByUserId: 77,
                Seed: 4567,
                RulesetVersion: "test-ruleset",
                ChallengerDeck: challenger,
                OpponentDeck: opponent,
                ChallengerPolicy: "heuristic",
                OpponentPolicy: "heuristic"
            )
        );

        var result = engine.ApplyAction(session, "end-turn");

        Assert.True(result.Succeeded);
        Assert.Equal(1, session.TurnPlayerIndex);
        Assert.Equal(3, session.Players[1].BaseZone.Cards.Count(x => x.Type == "Rune"));
    }

    private static RiftboundDeck BuildDeck(long deckId, string domain)
    {
        var legend = new RiftboundCard
        {
            Id = deckId * 10 + 1,
            Name = $"{domain} Legend",
            Type = "Legend",
            Color = [domain],
        };
        var champion = new RiftboundCard
        {
            Id = deckId * 10 + 2,
            Name = $"{domain} Champion",
            Type = "Champion",
            Color = [domain],
        };

        var mainCards = Enumerable
            .Range(0, 40)
            .Select(i =>
                new RiftboundDeckCard
                {
                    DeckId = deckId,
                    CardId = deckId * 1000 + i,
                    Quantity = 1,
                    Card = new RiftboundCard
                    {
                        Id = deckId * 1000 + i,
                        Name = $"{domain} Unit {i:00}",
                        Type = "Unit",
                        Color = [domain],
                        Might = 1,
                        Cost = 1,
                    },
                }
            )
            .ToList();

        var runeNames = new[]
        {
            "Fury Rune",
            "Calm Rune",
            "Mind Rune",
            "Body Rune",
            "Chaos Rune",
            "Order Rune",
        };
        var runes = runeNames
            .Select((name, i) =>
                new RiftboundDeckRune
                {
                    DeckId = deckId,
                    CardId = deckId * 2000 + i,
                    Quantity = 2,
                    Card = new RiftboundCard
                    {
                        Id = deckId * 2000 + i,
                        Name = name,
                        Type = "Rune",
                        Color = [domain],
                    },
                }
            )
            .ToList();

        var battlefields = Enumerable
            .Range(0, 3)
            .Select(i =>
                new RiftboundDeckBattlefield
                {
                    DeckId = deckId,
                    CardId = deckId * 3000 + i,
                    Card = new RiftboundCard
                    {
                        Id = deckId * 3000 + i,
                        Name = $"{domain} Battlefield {i}",
                        Type = "Battlefield",
                        Color = [domain],
                    },
                }
            )
            .ToList();

        return new RiftboundDeck
        {
            Id = deckId,
            Name = $"{domain} Test Deck",
            OwnerId = 1,
            LegendId = legend.Id,
            ChampionId = champion.Id,
            Legend = legend,
            Champion = champion,
            Cards = mainCards,
            Runes = runes,
            Battlefields = battlefields,
            Colors = [domain.ToUpperInvariant()],
            Shares = [],
            Ratings = [],
            Comments = [],
        };
    }
}
