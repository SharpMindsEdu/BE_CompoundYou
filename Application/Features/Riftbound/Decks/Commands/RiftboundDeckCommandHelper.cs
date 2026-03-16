using Application.Features.Riftbound.Decks.DTOs;
using Application.Features.Riftbound.Simulation.Services;
using Application.Shared;
using Domain.Entities.Riftbound;
using Domain.Repositories;
using Domain.Specifications.Riftbound.Decks;

namespace Application.Features.Riftbound.Decks.Commands;

internal static class RiftboundDeckCommandHelper
{
    internal const int MainDeckCardCount = 39;
    internal const int RuneDeckCardCount = 12;
    internal const int BattlefieldCardCount = 3;
    internal const int SideboardCardCount = 8;
    internal const int MainAndSideboardCopyLimit = 3;

    public static async Task<Result<DeckValidationPayload>> ValidateDeckAsync(
        long legendId,
        long championId,
        IReadOnlyCollection<RiftboundDeckCardInput> cards,
        IReadOnlyCollection<RiftboundDeckSideboardCardInput>? sideboardCards,
        IReadOnlyCollection<RiftboundDeckRuneInput>? runeCards,
        IReadOnlyCollection<long>? battlefieldCardIds,
        IRepository<RiftboundCard> cardRepository,
        CancellationToken ct
    )
    {
        var normalizedSideboardCards = sideboardCards ?? [];
        if (cards.Count == 0 || normalizedSideboardCards.Count == 0)
        {
            return Result<DeckValidationPayload>.Failure(
                ErrorResults.InvalidDeckCardSelection,
                ResultStatus.BadRequest
            );
        }

        if (
            cards.Any(c => c.CardId == legendId || c.CardId == championId)
            || normalizedSideboardCards.Any(c => c.CardId == legendId || c.CardId == championId)
        )
        {
            return Result<DeckValidationPayload>.Failure(
                ErrorResults.InvalidDeckCardSelection,
                ResultStatus.BadRequest
            );
        }

        var uniqueCardIds = cards.Select(c => c.CardId).Distinct().ToList();
        var uniqueSideboardCardIds = normalizedSideboardCards
            .Select(c => c.CardId)
            .Distinct()
            .ToList();
        var uniqueRuneCardIds =
            runeCards?.Select(c => c.CardId).Distinct().ToList() ?? new List<long>();
        var uniqueBattlefieldCardIds = battlefieldCardIds?.Distinct().ToList() ?? new List<long>();
        var lookupIds = uniqueCardIds
            .Concat(uniqueSideboardCardIds)
            .Concat(uniqueRuneCardIds)
            .Concat(uniqueBattlefieldCardIds)
            .Concat(new[] { legendId, championId })
            .Distinct()
            .ToList();
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
        var cardById = cardEntities.ToDictionary(c => c.Id);
        var deckCards = new List<RiftboundDeckCard>();
        var deckSideboardCards = new List<RiftboundDeckSideboardCard>();
        var deckRunes = new List<RiftboundDeckRune>();
        var deckBattlefields = new List<RiftboundDeckBattlefield>();
        var mainDeckCount = 0;
        var sideboardCount = 0;
        var runeCount = 0;

        foreach (var group in cards.GroupBy(c => c.CardId))
        {
            var entity = cardById[group.Key];
            if (!IsMainDeckCard(entity))
            {
                return Result<DeckValidationPayload>.Failure(
                    ErrorResults.InvalidDeckCardSelection,
                    ResultStatus.BadRequest
                );
            }

            if (!CardMatchesColors(entity, normalizedColors))
            {
                return Result<DeckValidationPayload>.Failure(
                    ErrorResults.InvalidDeckColors,
                    ResultStatus.BadRequest
                );
            }

            var quantity = group.Sum(x => x.Quantity);
            mainDeckCount += quantity;
            deckCards.Add(
                new RiftboundDeckCard { CardId = group.Key, Quantity = quantity }
            );
        }

        foreach (var group in normalizedSideboardCards.GroupBy(c => c.CardId))
        {
            var entity = cardById[group.Key];
            if (!IsMainDeckCard(entity))
            {
                return Result<DeckValidationPayload>.Failure(
                    ErrorResults.InvalidSideboardSelection,
                    ResultStatus.BadRequest
                );
            }

            if (!CardMatchesColors(entity, normalizedColors))
            {
                return Result<DeckValidationPayload>.Failure(
                    ErrorResults.InvalidDeckColors,
                    ResultStatus.BadRequest
                );
            }

            var quantity = group.Sum(x => x.Quantity);
            sideboardCount += quantity;
            deckSideboardCards.Add(
                new RiftboundDeckSideboardCard
                {
                    CardId = group.Key,
                    Quantity = quantity,
                }
            );
        }

        if (runeCards is not null)
        {
            foreach (var group in runeCards.GroupBy(c => c.CardId))
            {
                var entity = cardById[group.Key];
                if (!IsRune(entity))
                {
                    return Result<DeckValidationPayload>.Failure(
                        ErrorResults.InvalidRuneSelection,
                        ResultStatus.BadRequest
                    );
                }

                if (!CardMatchesColors(entity, normalizedColors))
                {
                    return Result<DeckValidationPayload>.Failure(
                        ErrorResults.InvalidDeckColors,
                        ResultStatus.BadRequest
                    );
                }

                var quantity = group.Sum(x => x.Quantity);
                runeCount += quantity;
                deckRunes.Add(
                    new RiftboundDeckRune
                    {
                        CardId = group.Key,
                        Quantity = quantity,
                    }
                );
            }
        }

        if (battlefieldCardIds is not null)
        {
            foreach (var battlefieldCardId in battlefieldCardIds.Distinct())
            {
                var entity = cardById[battlefieldCardId];
                if (!IsBattlefield(entity))
                {
                    return Result<DeckValidationPayload>.Failure(
                        ErrorResults.InvalidBattlefieldSelection,
                        ResultStatus.BadRequest
                    );
                }

                if (!CardMatchesColors(entity, normalizedColors))
                {
                    return Result<DeckValidationPayload>.Failure(
                        ErrorResults.InvalidDeckColors,
                        ResultStatus.BadRequest
                    );
                }

                deckBattlefields.Add(new RiftboundDeckBattlefield { CardId = battlefieldCardId });
            }
        }

        if (mainDeckCount != MainDeckCardCount)
        {
            return Result<DeckValidationPayload>.Failure(
                ErrorResults.InvalidMainDeckCount,
                ResultStatus.BadRequest
            );
        }

        if (sideboardCount != SideboardCardCount)
        {
            return Result<DeckValidationPayload>.Failure(
                ErrorResults.InvalidSideboardCount,
                ResultStatus.BadRequest
            );
        }

        if (runeCount != RuneDeckCardCount)
        {
            return Result<DeckValidationPayload>.Failure(
                ErrorResults.InvalidRuneDeckCount,
                ResultStatus.BadRequest
            );
        }

        if (deckBattlefields.Count != BattlefieldCardCount)
        {
            return Result<DeckValidationPayload>.Failure(
                ErrorResults.InvalidBattlefieldDeckCount,
                ResultStatus.BadRequest
            );
        }

        var groupedMainAndSideboardCopies = deckCards
            .Select(entry =>
            {
                var card = cardById[entry.CardId];
                return (Key: NormalizeDeckCardName(card), entry.Quantity);
            })
            .Concat(
                deckSideboardCards.Select(entry =>
                {
                    var card = cardById[entry.CardId];
                    return (Key: NormalizeDeckCardName(card), entry.Quantity);
                })
            )
            .GroupBy(x => x.Key)
            .Select(g => g.Sum(x => x.Quantity))
            .ToList();
        if (groupedMainAndSideboardCopies.Any(q => q > MainAndSideboardCopyLimit))
        {
            return Result<DeckValidationPayload>.Failure(
                ErrorResults.InvalidDeckCopyLimit,
                ResultStatus.BadRequest
            );
        }

        return Result<DeckValidationPayload>.Success(
            new DeckValidationPayload(
                legend,
                champion,
                normalizedColors,
                deckCards,
                deckSideboardCards,
                deckRunes,
                deckBattlefields
            )
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
        IRiftboundDeckSimulationReadinessService readinessService,
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

        var readiness = readinessService.Evaluate(deck);
        return RiftboundDeckMappings.ToDto(
            deck,
            userId,
            readiness.IsSimulationReady,
            readiness.UnsupportedCards
        );
    }

    internal static List<string> NormalizeColors(IEnumerable<string>? colors) =>
        colors?.Select(NormalizeColor).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];

    internal static string NormalizeColor(string color) => color.Trim().ToUpperInvariant();

    internal static bool IsLegend(RiftboundCard card) =>
        string.Equals(card.Type, "Legend", StringComparison.OrdinalIgnoreCase);

    internal static bool IsChampion(RiftboundCard card) =>
        string.Equals(card.Type, "Champion", StringComparison.OrdinalIgnoreCase);

    internal static bool IsRune(RiftboundCard card) =>
        string.Equals(card.Type, "Rune", StringComparison.OrdinalIgnoreCase);

    internal static bool IsBattlefield(RiftboundCard card) =>
        string.Equals(card.Type, "Battlefield", StringComparison.OrdinalIgnoreCase);

    internal static bool IsMainDeckCard(RiftboundCard card)
    {
        if (IsLegend(card) || IsChampion(card) || IsRune(card) || IsBattlefield(card))
            return false;

        return true;
    }

    internal static bool CardMatchesColors(
        RiftboundCard card,
        IReadOnlyCollection<string> deckColors
    )
    {
        if (card.Color is null || card.Color.Count == 0)
            return true;

        if (deckColors.Count == 0)
            return false;

        return card
            .Color.Select(NormalizeColor)
            .All(color => deckColors.Contains(color, StringComparer.OrdinalIgnoreCase));
    }

    internal static bool ChampionMatchesLegend(RiftboundCard champion, RiftboundCard legend)
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

    private static string NormalizeDeckCardName(RiftboundCard card)
    {
        if (string.IsNullOrWhiteSpace(card.Name))
        {
            return $"id:{card.Id}";
        }

        return card.Name.Trim().ToUpperInvariant();
    }
}

internal record DeckValidationPayload(
    RiftboundCard Legend,
    RiftboundCard Champion,
    List<string> Colors,
    List<RiftboundDeckCard> Cards,
    List<RiftboundDeckSideboardCard> SideboardCards,
    List<RiftboundDeckRune> Runes,
    List<RiftboundDeckBattlefield> Battlefields
);
