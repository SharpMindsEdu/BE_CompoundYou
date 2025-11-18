using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Decks.DTOs;

public record RiftboundDeckCardDto(
    long CardId,
    string Name,
    string? Type,
    int? Cost,
    IReadOnlyCollection<string>? Color,
    int Quantity
);

public record RiftboundDeckCommentDto(
    long Id,
    long UserId,
    string AuthorName,
    string Content,
    long? ParentCommentId,
    DateTimeOffset CreatedOn,
    IReadOnlyCollection<RiftboundDeckCommentDto> Replies
);

public record RiftboundDeckDto(
    long Id,
    string Name,
    bool IsPublic,
    IReadOnlyCollection<string> Colors,
    long OwnerId,
    string OwnerName,
    long LegendId,
    string LegendName,
    long ChampionId,
    string ChampionName,
    IReadOnlyCollection<RiftboundDeckCardDto> Cards,
    decimal AverageRating,
    int RatingsCount,
    bool CanEdit,
    bool IsSharedWithCurrentUser,
    IReadOnlyCollection<long> SharedWithUserIds,
    IReadOnlyCollection<RiftboundDeckCommentDto> Comments
);

public static class RiftboundDeckMappings
{
    public static RiftboundDeckDto ToDto(RiftboundDeck deck, long currentUserId)
    {
        var sharedWith = deck.Shares.Select(s => s.UserId).Distinct().ToList();
        var averageRating = deck.Ratings.Count == 0
            ? 0m
            : decimal.Round(
                (decimal)deck.Ratings.Average(r => r.Value),
                2,
                MidpointRounding.AwayFromZero
            );

        var orderedCards = deck
            .Cards
            .OrderBy(c => c.Card?.Name)
            .Select(c => new RiftboundDeckCardDto(
                c.CardId,
                c.Card?.Name ?? string.Empty,
                c.Card?.Type,
                c.Card?.Cost,
                c.Card?.Color,
                c.Quantity
            ))
            .ToList();

        var comments = BuildComments(deck.Comments);

        return new RiftboundDeckDto(
            deck.Id,
            deck.Name,
            deck.IsPublic,
            deck.Colors ?? [],
            deck.OwnerId,
            deck.Owner?.DisplayName ?? string.Empty,
            deck.LegendId,
            deck.Legend?.Name ?? string.Empty,
            deck.ChampionId,
            deck.Champion?.Name ?? string.Empty,
            orderedCards,
            averageRating,
            deck.Ratings.Count,
            deck.OwnerId == currentUserId,
            sharedWith.Contains(currentUserId),
            sharedWith,
            comments
        );
    }

    private static IReadOnlyCollection<RiftboundDeckCommentDto> BuildComments(
        IEnumerable<RiftboundDeckComment> comments
    )
    {
        var commentList = comments.ToList();
        return BuildCommentTree(commentList, null);
    }

    private static IReadOnlyCollection<RiftboundDeckCommentDto> BuildCommentTree(
        IReadOnlyCollection<RiftboundDeckComment> all,
        long? parentId
    )
    {
        return all
            .Where(c => c.ParentCommentId == parentId)
            .OrderBy(c => c.CreatedOn)
            .Select(c =>
                new RiftboundDeckCommentDto(
                    c.Id,
                    c.UserId,
                    c.User?.DisplayName ?? string.Empty,
                    c.Content,
                    c.ParentCommentId,
                    c.CreatedOn,
                    BuildCommentTree(all, c.Id)
                )
            )
            .ToList();
    }
}
