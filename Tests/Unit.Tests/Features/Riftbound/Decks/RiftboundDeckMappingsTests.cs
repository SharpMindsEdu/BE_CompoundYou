using Application.Features.Riftbound.Decks.DTOs;
using Domain.Entities;
using Domain.Entities.Riftbound;

namespace Unit.Tests.Features.Riftbound.Decks;

[Trait("category", ServiceTestCategories.UnitTests)]
public class RiftboundDeckMappingsTests
{
    [Fact]
    public void ToDto_MapsFlagsOrderingRatingsAndCommentTree()
    {
        var owner = new User { Id = 7, DisplayName = "Deck Owner" };
        var now = DateTimeOffset.UtcNow;

        var deck = new RiftboundDeck
        {
            Id = 10,
            Name = "Demo",
            OwnerId = owner.Id,
            Owner = owner,
            IsPublic = true,
            Colors = ["CHAOS", "ORDER"],
            LegendId = 100,
            ChampionId = 101,
            Legend = new RiftboundCard { Id = 100, Name = "Legend A", Type = "Legend" },
            Champion = new RiftboundCard
            {
                Id = 101,
                Name = "Champion A",
                Type = "Champion",
            },
            Cards =
            [
                new RiftboundDeckCard
                {
                    CardId = 1,
                    Quantity = 1,
                    Card = new RiftboundCard { Id = 1, Name = "Zulu Unit", Type = "Unit" },
                },
                new RiftboundDeckCard
                {
                    CardId = 2,
                    Quantity = 2,
                    Card = new RiftboundCard { Id = 2, Name = "Alpha Unit", Type = "Unit" },
                },
            ],
            Runes =
            [
                new RiftboundDeckRune
                {
                    CardId = 3,
                    Quantity = 2,
                    Card = new RiftboundCard { Id = 3, Name = "Rune B", Type = "Rune" },
                },
                new RiftboundDeckRune
                {
                    CardId = 4,
                    Quantity = 2,
                    Card = new RiftboundCard { Id = 4, Name = "Rune A", Type = "Rune" },
                },
            ],
            Battlefields =
            [
                new RiftboundDeckBattlefield
                {
                    CardId = 5,
                    Card = new RiftboundCard { Id = 5, Name = "Battlefield C", Type = "Battlefield" },
                },
                new RiftboundDeckBattlefield
                {
                    CardId = 6,
                    Card = new RiftboundCard { Id = 6, Name = "Battlefield A", Type = "Battlefield" },
                },
            ],
            Shares =
            [
                new RiftboundDeckShare { DeckId = 10, UserId = 42 },
                new RiftboundDeckShare { DeckId = 10, UserId = 42 },
                new RiftboundDeckShare { DeckId = 10, UserId = 99 },
            ],
            Ratings =
            [
                new RiftboundDeckRating { DeckId = 10, UserId = 1, Value = 5 },
                new RiftboundDeckRating { DeckId = 10, UserId = 2, Value = 4 },
            ],
            Comments =
            [
                new RiftboundDeckComment
                {
                    Id = 1,
                    DeckId = 10,
                    UserId = 1,
                    Content = "Parent",
                    CreatedOn = now,
                    User = new User { Id = 1, DisplayName = "Alice" },
                },
                new RiftboundDeckComment
                {
                    Id = 2,
                    DeckId = 10,
                    UserId = 2,
                    ParentCommentId = 1,
                    Content = "Reply",
                    CreatedOn = now.AddMinutes(1),
                    User = new User { Id = 2, DisplayName = "Bob" },
                },
                new RiftboundDeckComment
                {
                    Id = 3,
                    DeckId = 10,
                    UserId = 3,
                    Content = "Second root",
                    CreatedOn = now.AddMinutes(2),
                    User = new User { Id = 3, DisplayName = "Cara" },
                },
            ],
        };

        var dto = RiftboundDeckMappings.ToDto(
            deck,
            currentUserId: 42,
            simulationSupport: true,
            unsupportedSimulationCards: ["Unknown Card"]
        );

        Assert.Equal(4.5m, dto.AverageRating);
        Assert.Equal(2, dto.RatingsCount);
        Assert.False(dto.CanEdit);
        Assert.True(dto.IsSharedWithCurrentUser);
        Assert.Equal([42, 99], dto.SharedWithUserIds.OrderBy(x => x).ToArray());
        Assert.True(dto.SimulationSupport);
        Assert.Equal(["Unknown Card"], dto.UnsupportedSimulationCards);

        Assert.Equal(["Alpha Unit", "Zulu Unit"], dto.Cards.Select(c => c.Name).ToArray());
        Assert.Equal(["Rune A", "Rune B"], dto.RuneCards.Select(c => c.Name).ToArray());
        Assert.Equal(
            ["Battlefield A", "Battlefield C"],
            dto.Battlefields.Select(c => c.Name).ToArray()
        );

        Assert.Equal(2, dto.Comments.Count);
        var firstRoot = dto.Comments.First();
        Assert.Equal("Parent", firstRoot.Content);
        Assert.Single(firstRoot.Replies);
        Assert.Equal("Reply", firstRoot.Replies.First().Content);
    }
}
