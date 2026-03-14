using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;

namespace Unit.Tests.Features.Riftbound.Simulation;

internal static class RiftboundSimulationTestData
{
    private static readonly string[] BasicRuneNames =
    [
        "Fury Rune",
        "Calm Rune",
        "Mind Rune",
        "Body Rune",
        "Chaos Rune",
        "Order Rune",
    ];

    public static RiftboundDeck BuildDeck(
        long deckId,
        string domain,
        Action<RiftboundDeck>? configure = null
    )
    {
        var legend = new RiftboundCard
        {
            Id = deckId * 10 + 1,
            Name = $"{domain} Legend",
            Type = "Legend",
            Color = [domain],
            Tags = [$"Champion:{domain}"],
        };
        var champion = new RiftboundCard
        {
            Id = deckId * 10 + 2,
            Name = $"{domain} Champion",
            Type = "Champion",
            Color = [domain],
            Tags = [$"Champion:{domain}"],
        };

        var cards = Enumerable
            .Range(0, 40)
            .Select(i =>
                new RiftboundDeckCard
                {
                    DeckId = deckId,
                    CardId = deckId * 1_000 + i,
                    Quantity = 1,
                    Card = new RiftboundCard
                    {
                        Id = deckId * 1_000 + i,
                        Name = $"{domain} Unit {i:00}",
                        Type = "Unit",
                        Color = [domain],
                        Cost = 1,
                        Might = 1,
                    },
                }
            )
            .ToList();

        var runes = BasicRuneNames
            .Select((name, i) =>
                new RiftboundDeckRune
                {
                    DeckId = deckId,
                    CardId = deckId * 2_000 + i,
                    Quantity = 2,
                    Card = new RiftboundCard
                    {
                        Id = deckId * 2_000 + i,
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
                    CardId = deckId * 3_000 + i,
                    Card = new RiftboundCard
                    {
                        Id = deckId * 3_000 + i,
                        Name = $"{domain} Battlefield {i}",
                        Type = "Battlefield",
                        Color = [domain],
                    },
                }
            )
            .ToList();

        var deck = new RiftboundDeck
        {
            Id = deckId,
            Name = $"{domain} Deck {deckId}",
            OwnerId = 1,
            IsPublic = false,
            Colors = [domain.ToUpperInvariant()],
            LegendId = legend.Id,
            ChampionId = champion.Id,
            Legend = legend,
            Champion = champion,
            Cards = cards,
            Runes = runes,
            Battlefields = battlefields,
            Shares = [],
            Ratings = [],
            Comments = [],
        };

        configure?.Invoke(deck);
        return deck;
    }

    public static RiftboundSimulationEngineSetup BuildSetup(
        long simulationId,
        long userId,
        long seed,
        RiftboundDeck challengerDeck,
        RiftboundDeck opponentDeck,
        string challengerPolicy = "heuristic",
        string opponentPolicy = "heuristic"
    )
    {
        return new RiftboundSimulationEngineSetup(
            simulationId,
            userId,
            seed,
            "test-ruleset",
            challengerDeck,
            opponentDeck,
            challengerPolicy,
            opponentPolicy
        );
    }
}
