using Application.Features.Riftbound.Decks.Commands;
using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Services;

public sealed class RiftboundDeckSimulationReadinessService(
    IRiftboundSimulationDefinitionRegistry definitionRegistry
) : IRiftboundDeckSimulationReadinessService
{
    public RiftboundDeckSimulationReadiness Evaluate(RiftboundDeck deck)
    {
        var issues = new List<string>();
        var unsupportedCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (deck.Legend is null || !RiftboundDeckCommandHelper.IsLegend(deck.Legend))
        {
            issues.Add("Legend is missing or invalid.");
        }

        if (deck.Champion is null || !RiftboundDeckCommandHelper.IsChampion(deck.Champion))
        {
            issues.Add("Chosen champion is missing or invalid.");
        }
        else if (
            deck.Legend is not null
            && !RiftboundDeckCommandHelper.ChampionMatchesLegend(deck.Champion, deck.Legend)
        )
        {
            issues.Add("Champion does not match the selected legend.");
        }

        var legendDomains = RiftboundDeckCommandHelper.NormalizeColors(deck.Legend?.Color);
        if (legendDomains.Count == 0)
        {
            issues.Add("Legend must define at least one domain.");
        }

        var mainDeckCount = deck.Cards.Sum(c => c.Quantity);
        if (mainDeckCount != RiftboundDeckCommandHelper.MainDeckCardCount)
        {
            issues.Add(
                $"Main deck must contain exactly {RiftboundDeckCommandHelper.MainDeckCardCount} cards."
            );
        }

        var overCopyLimit = deck
            .Cards.Where(c => c.Card is not null)
            .Select(c => (Name: NormalizeName(c.Card!.Name, c.CardId), c.Quantity))
            .Concat(
                deck.SideboardCards.Where(c => c.Card is not null).Select(c =>
                    (Name: NormalizeName(c.Card!.Name, c.CardId), c.Quantity)
                )
            )
            .GroupBy(c => c.Name)
            .Where(g => g.Sum(x => x.Quantity) > RiftboundDeckCommandHelper.MainAndSideboardCopyLimit)
            .Select(g => g.Key)
            .ToList();
        if (overCopyLimit.Count > 0)
        {
            issues.Add(
                $"Copy limit exceeded for: {string.Join(", ", overCopyLimit.OrderBy(x => x))}."
            );
        }

        var signatureCards = deck
            .Cards.Where(c => c.Card is not null && IsSignatureCard(c.Card!))
            .ToList();
        var signatureCount = signatureCards.Sum(c => c.Quantity);
        if (signatureCount > 3)
        {
            issues.Add("A deck may only contain 3 total Signature cards.");
        }

        var legendChampionTags = GetChampionTags(deck.Legend).ToHashSet(
            StringComparer.OrdinalIgnoreCase
        );
        if (legendChampionTags.Count > 0)
        {
            var wrongSignatures = signatureCards
                .Where(c => !GetChampionTags(c.Card!).Any(legendChampionTags.Contains))
                .Select(c => c.Card!.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            if (wrongSignatures.Count > 0)
            {
                issues.Add(
                    $"Signature cards without matching champion tag: {string.Join(", ", wrongSignatures)}."
                );
            }
        }

        var runeCount = deck.Runes.Sum(x => x.Quantity);
        if (runeCount != RiftboundDeckCommandHelper.RuneDeckCardCount)
        {
            issues.Add(
                $"Rune deck must contain exactly {RiftboundDeckCommandHelper.RuneDeckCardCount} rune cards."
            );
        }

        foreach (var runeEntry in deck.Runes)
        {
            if (runeEntry.Card is null || !RiftboundDeckCommandHelper.IsRune(runeEntry.Card))
            {
                issues.Add("Rune deck contains non-rune cards.");
                continue;
            }

            if (!RiftboundDeckCommandHelper.CardMatchesColors(runeEntry.Card, legendDomains))
            {
                issues.Add("Rune deck contains runes outside of legend domain identity.");
            }
        }

        var uniqueBattlefields = deck
            .Battlefields.Where(x => x.CardId > 0)
            .Select(x => x.CardId)
            .Distinct()
            .Count();
        if (uniqueBattlefields != RiftboundDeckCommandHelper.BattlefieldCardCount)
        {
            issues.Add(
                $"Deck must contain exactly {RiftboundDeckCommandHelper.BattlefieldCardCount} distinct battlefields for 1v1 Duel."
            );
        }

        foreach (var battlefieldEntry in deck.Battlefields)
        {
            if (
                battlefieldEntry.Card is null
                || !RiftboundDeckCommandHelper.IsBattlefield(battlefieldEntry.Card)
            )
            {
                issues.Add("Battlefield deck contains non-battlefield cards.");
                continue;
            }

            if (!RiftboundDeckCommandHelper.CardMatchesColors(battlefieldEntry.Card, legendDomains))
            {
                issues.Add("Battlefield deck contains battlefields outside of domain identity.");
            }
        }

        var sideboardCount = deck.SideboardCards.Sum(x => x.Quantity);
        if (sideboardCount != RiftboundDeckCommandHelper.SideboardCardCount)
        {
            issues.Add(
                $"Sideboard must contain exactly {RiftboundDeckCommandHelper.SideboardCardCount} cards."
            );
        }

        foreach (var sideboardEntry in deck.SideboardCards)
        {
            if (sideboardEntry.Card is null || !RiftboundDeckCommandHelper.IsMainDeckCard(sideboardEntry.Card))
            {
                issues.Add("Sideboard contains invalid card types.");
                continue;
            }

            if (!RiftboundDeckCommandHelper.CardMatchesColors(sideboardEntry.Card, legendDomains))
            {
                issues.Add("Sideboard contains cards outside of legend domain identity.");
            }
        }

        foreach (var card in EnumerateAllSimulationCards(deck))
        {
            if (!definitionRegistry.IsCardSupported(card))
            {
                unsupportedCards.Add(card.Name);
            }
        }

        return new RiftboundDeckSimulationReadiness(
            IsSimulationReady: issues.Count == 0 && unsupportedCards.Count == 0,
            ValidationIssues: issues.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            UnsupportedCards: unsupportedCards.OrderBy(x => x).ToList()
        );
    }

    private static IEnumerable<RiftboundCard> EnumerateAllSimulationCards(RiftboundDeck deck)
    {
        if (deck.Legend is not null)
        {
            yield return deck.Legend;
        }

        if (deck.Champion is not null)
        {
            yield return deck.Champion;
        }

        foreach (var card in deck.Cards.Select(c => c.Card).Where(c => c is not null))
        {
            yield return card!;
        }

        foreach (var card in deck.SideboardCards.Select(c => c.Card).Where(c => c is not null))
        {
            yield return card!;
        }

        foreach (var card in deck.Runes.Select(c => c.Card).Where(c => c is not null))
        {
            yield return card!;
        }

        foreach (var card in deck.Battlefields.Select(c => c.Card).Where(c => c is not null))
        {
            yield return card!;
        }
    }

    private static bool IsSignatureCard(RiftboundCard card)
    {
        if (card.Tags is null)
        {
            return false;
        }

        return card.Tags.Any(tag => tag.Contains("signature", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeName(string? value, long cardId)
    {
        return string.IsNullOrWhiteSpace(value) ? $"id:{cardId}" : value.Trim();
    }

    private static IEnumerable<string> GetChampionTags(RiftboundCard? card)
    {
        if (card?.Tags is null)
        {
            return [];
        }

        return card
            .Tags.Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
