using Application.Features.Riftbound.Decks.DTOs;
using Application.Shared;
using Domain.Entities.Riftbound;
using Domain.Repositories;
using Domain.Specifications.Riftbound.Decks;

namespace Application.Features.Riftbound.Decks.Commands;

internal static class RiftboundDeckCommandHelper
{
    public static async Task<Result<DeckValidationPayload>> ValidateDeckAsync(
        long legendId,
        long championId,
        IReadOnlyCollection<RiftboundDeckCardInput> cards,
        IRepository<RiftboundCard> cardRepository,
        CancellationToken ct
    )
    {
        if (cards.Count == 0)
        {
            return Result<DeckValidationPayload>.Failure(
                ErrorResults.InvalidDeckCardSelection,
                ResultStatus.BadRequest
            );
        }

        if (cards.Any(c => c.CardId == legendId || c.CardId == championId))
        {
            return Result<DeckValidationPayload>.Failure(
                ErrorResults.InvalidDeckCardSelection,
                ResultStatus.BadRequest
            );
        }

        var uniqueCardIds = cards.Select(c => c.CardId).Distinct().ToList();
        var lookupIds = uniqueCardIds.Concat(new[] { legendId, championId }).Distinct().ToList();
        var cardEntities = await cardRepository.ListAll(c => lookupIds.Contains(c.Id), ct);

        if (cardEntities.Count != lookupIds.Count)
        {
            return Result<DeckValidationPayload>.Failure(
                ErrorResults.InvalidDeckCardSelection,
                ResultStatus.NotFound
            );
        }

        var legend = cardEntities.SingleOrDefault(c => c.Id == legendId);
        if (legend == null || !IsLegend(legend))
        {
            return Result<DeckValidationPayload>.Failure(
                ErrorResults.InvalidLegend,
                ResultStatus.BadRequest
            );
        }

        var champion = cardEntities.SingleOrDefault(c => c.Id == championId);
        if (champion == null || !ChampionMatchesLegend(champion, legend))
        {
            return Result<DeckValidationPayload>.Failure(
                ErrorResults.InvalidChampion,
                ResultStatus.BadRequest
            );
        }

        var normalizedColors = NormalizeColors(legend.Color);
        var deckCards = new List<RiftboundDeckCard>();

        foreach (var group in cards.GroupBy(c => c.CardId))
        {
            var entity = cardEntities.Single(c => c.Id == group.Key);
            if (!CardMatchesColors(entity, normalizedColors))
            {
                return Result<DeckValidationPayload>.Failure(
                    ErrorResults.InvalidDeckColors,
                    ResultStatus.BadRequest
                );
            }

            deckCards.Add(
                new RiftboundDeckCard { CardId = group.Key, Quantity = group.Sum(x => x.Quantity) }
            );
        }

        return Result<DeckValidationPayload>.Success(
            new DeckValidationPayload(legend, champion, normalizedColors, deckCards)
        );
    }

    public static List<RiftboundDeckShare> BuildShares(IEnumerable<long>? userIds, long ownerId)
    {
        if (userIds is null)
            return [];

        return userIds
            .Where(id => id > 0 && id != ownerId)
            .Distinct()
            .Select(id => new RiftboundDeckShare { UserId = id })
            .ToList();
    }

    public static async Task<RiftboundDeckDto> LoadDeckDtoAsync(
        IRiftboundDeckSpecification specification,
        long deckId,
        long userId,
        CancellationToken ct
    )
    {
        var deck = await specification
            .Reset()
            .IncludeDetails()
            .AccessibleForUser(userId)
            .ByDeckId(deckId)
            .FirstOrDefault(ct);

        if (deck == null)
            throw new InvalidOperationException("Deck konnte nicht geladen werden.");

        return RiftboundDeckMappings.ToDto(deck, userId);
    }

    private static List<string> NormalizeColors(IEnumerable<string>? colors) =>
        colors?.Select(NormalizeColor).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];

    private static string NormalizeColor(string color) => color.Trim().ToUpperInvariant();

    private static bool IsLegend(RiftboundCard card) =>
        string.Equals(card.Type, "Legend", StringComparison.OrdinalIgnoreCase);

    private static bool IsChampion(RiftboundCard card) =>
        string.Equals(card.Type, "Champion", StringComparison.OrdinalIgnoreCase);

    private static bool CardMatchesColors(
        RiftboundCard card,
        IReadOnlyCollection<string> deckColors
    )
    {
        if (card.Color is null || card.Color.Count == 0)
            return deckColors.Count == 0 || deckColors.Count > 0;

        if (deckColors.Count == 0)
            return false;

        return card
            .Color.Select(NormalizeColor)
            .All(color => deckColors.Contains(color, StringComparer.OrdinalIgnoreCase));
    }

    private static bool ChampionMatchesLegend(RiftboundCard champion, RiftboundCard legend)
    {
        if (!IsChampion(champion) || !IsLegend(legend))
            return false;

        var deckColors = NormalizeColors(legend.Color);
        if (!CardMatchesColors(champion, deckColors))
            return false;

        var explicitLink = false;
        if (!string.IsNullOrWhiteSpace(champion.Cycle) && !string.IsNullOrWhiteSpace(legend.Cycle))
        {
            explicitLink = string.Equals(
                champion.Cycle,
                legend.Cycle,
                StringComparison.OrdinalIgnoreCase
            );
            if (!explicitLink)
                return false;
        }

        if (!explicitLink && champion.Tags is not null)
        {
            var legendMatches = new[] { legend.Name, legend.Slug, legend.ReferenceId }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim().ToLowerInvariant())
                .ToList();

            if (legendMatches.Count > 0)
            {
                explicitLink = champion.Tags.Any(tag =>
                    legendMatches.Any(match =>
                        tag.Contains(match, StringComparison.OrdinalIgnoreCase)
                    )
                );
            }
        }

        if (
            !explicitLink
            && !string.IsNullOrWhiteSpace(champion.SetName)
            && !string.IsNullOrWhiteSpace(legend.SetName)
        )
        {
            explicitLink = string.Equals(
                champion.SetName,
                legend.SetName,
                StringComparison.OrdinalIgnoreCase
            );
        }

        return explicitLink || deckColors.Count > 0;
    }
}

internal record DeckValidationPayload(
    RiftboundCard Legend,
    RiftboundCard Champion,
    List<string> Colors,
    List<RiftboundDeckCard> Cards
);
