using Application.Features.Riftbound.Simulation.Definitions;
using Application.Features.Riftbound.Simulation.Services;
using Domain.Entities.Riftbound;

namespace Unit.Tests.Features.Riftbound.Simulation;

public class RiftboundDeckSimulationReadinessServiceTests
{
    [Fact]
    public void Evaluate_ReturnsReady_WhenDeckMeetsDuelRequirements()
    {
        var registry = new TestRegistry(card => true);
        var sut = new RiftboundDeckSimulationReadinessService(registry);
        var deck = BuildValidDeck();

        var result = sut.Evaluate(deck);

        Assert.True(result.IsSimulationReady);
        Assert.Empty(result.ValidationIssues);
        Assert.Empty(result.UnsupportedCards);
    }

    [Fact]
    public void Evaluate_ReturnsValidationIssues_WhenRuneOrBattlefieldRulesFail()
    {
        var registry = new TestRegistry(card => true);
        var sut = new RiftboundDeckSimulationReadinessService(registry);
        var deck = BuildValidDeck();
        deck.Runes = deck.Runes.Take(11).ToList();
        deck.Battlefields = deck.Battlefields.Take(2).ToList();

        var result = sut.Evaluate(deck);

        Assert.False(result.IsSimulationReady);
        Assert.Contains(
            result.ValidationIssues,
            issue => issue.Contains("exactly 12 rune cards", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Contains(
            result.ValidationIssues,
            issue => issue.Contains("exactly 3 distinct battlefields", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Evaluate_ReportsUnsupportedCards_WhenRegistryDoesNotSupportAllCards()
    {
        var registry = new TestRegistry(card => !string.Equals(card.Name, "Unsupported Unit", StringComparison.Ordinal));
        var sut = new RiftboundDeckSimulationReadinessService(registry);
        var deck = BuildValidDeck();
        deck.Cards[0].Card = new RiftboundCard
        {
            Id = 2_000,
            Name = "Unsupported Unit",
            Type = "Unit",
            Color = ["Chaos"],
        };

        var result = sut.Evaluate(deck);

        Assert.False(result.IsSimulationReady);
        Assert.Contains("Unsupported Unit", result.UnsupportedCards);
    }

    [Fact]
    public void Evaluate_ReturnsIssue_WhenCopyLimitIsExceeded()
    {
        var registry = new TestRegistry(card => true);
        var sut = new RiftboundDeckSimulationReadinessService(registry);
        var deck = BuildValidDeck();
        deck.Cards[0].Quantity = 2;
        deck.Cards[1].Quantity = 2;
        deck.Cards[1].Card!.Name = deck.Cards[0].Card!.Name;

        var result = sut.Evaluate(deck);

        Assert.False(result.IsSimulationReady);
        Assert.Contains(
            result.ValidationIssues,
            issue => issue.Contains("Copy limit exceeded", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Evaluate_ReturnsIssue_WhenSignatureCardsExceedLimit()
    {
        var registry = new TestRegistry(card => true);
        var sut = new RiftboundDeckSimulationReadinessService(registry);
        var deck = BuildValidDeck();
        deck.Cards[0].Quantity = 4;
        deck.Cards[0].Card!.Tags = ["Signature", "Champion:Chaos"];

        var result = sut.Evaluate(deck);

        Assert.False(result.IsSimulationReady);
        Assert.Contains(
            result.ValidationIssues,
            issue => issue.Contains("3 total Signature cards", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Evaluate_ReturnsIssue_WhenSignatureTagDoesNotMatchLegendChampionTag()
    {
        var registry = new TestRegistry(card => true);
        var sut = new RiftboundDeckSimulationReadinessService(registry);
        var deck = BuildValidDeck();
        deck.Cards[0].Card!.Name = "Mismatched Signature";
        deck.Cards[0].Card.Tags = ["Signature", "Champion:Order"];

        var result = sut.Evaluate(deck);

        Assert.False(result.IsSimulationReady);
        Assert.Contains(
            result.ValidationIssues,
            issue => issue.Contains("without matching champion tag", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Evaluate_ReturnsIssues_WhenChampionAndRuneColorsDoNotMatchLegend()
    {
        var registry = new TestRegistry(card => true);
        var sut = new RiftboundDeckSimulationReadinessService(registry);
        var deck = BuildValidDeck();
        deck.Champion!.Color = ["Order"];
        deck.Runes[0].Card!.Color = ["Order"];

        var result = sut.Evaluate(deck);

        Assert.False(result.IsSimulationReady);
        Assert.Contains(
            result.ValidationIssues,
            issue => issue.Contains("Champion does not match", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Contains(
            result.ValidationIssues,
            issue => issue.Contains("Rune deck contains runes outside", StringComparison.OrdinalIgnoreCase)
        );
    }

    private static RiftboundDeck BuildValidDeck()
    {
        var legend = new RiftboundCard
        {
            Id = 1,
            Name = "Chaos Legend",
            Type = "Legend",
            Color = ["Chaos"],
            Tags = ["Champion:Chaos"],
        };
        var champion = new RiftboundCard
        {
            Id = 2,
            Name = "Chaos Champion",
            Type = "Champion",
            Color = ["Chaos"],
            Tags = ["Champion:Chaos"],
        };

        var mainCards = Enumerable
            .Range(0, 40)
            .Select(i =>
                new RiftboundDeckCard
                {
                    DeckId = 1,
                    CardId = 100 + i,
                    Quantity = 1,
                    Card = new RiftboundCard
                    {
                        Id = 100 + i,
                        Name = $"Main Card {i}",
                        Type = "Unit",
                        Color = ["Chaos"],
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
                    DeckId = 1,
                    CardId = 500 + i,
                    Quantity = 2,
                    Card = new RiftboundCard
                    {
                        Id = 500 + i,
                        Name = name,
                        Type = "Rune",
                        Color = ["Chaos"],
                    },
                }
            )
            .ToList();

        var battlefields = Enumerable
            .Range(0, 3)
            .Select(i =>
                new RiftboundDeckBattlefield
                {
                    DeckId = 1,
                    CardId = 700 + i,
                    Card = new RiftboundCard
                    {
                        Id = 700 + i,
                        Name = $"Battlefield {i}",
                        Type = "Battlefield",
                        Color = ["Chaos"],
                    },
                }
            )
            .ToList();

        return new RiftboundDeck
        {
            Id = 1,
            Name = "Valid Simulation Deck",
            OwnerId = 77,
            IsPublic = false,
            Colors = ["CHAOS"],
            LegendId = legend.Id,
            ChampionId = champion.Id,
            Legend = legend,
            Champion = champion,
            Cards = mainCards,
            Runes = runes,
            Battlefields = battlefields,
            Shares = [],
            Ratings = [],
            Comments = [],
        };
    }

    private sealed class TestRegistry(Func<RiftboundCard, bool> supportPredicate)
        : IRiftboundSimulationDefinitionRegistry
    {
        public string RulesetVersion => "test-ruleset";
        public IReadOnlyCollection<string> SupportedKeywords => [];
        public IReadOnlyCollection<RiftboundRuleCorrection> RuleCorrections => [];

        public RiftboundSimulationCardDefinition? FindDefinition(RiftboundCard card) => null;

        public bool IsCardSupported(RiftboundCard card) => supportPredicate(card);
    }
}
