using Domain.Entities;

namespace Domain.Entities.Riftbound;

public class RiftboundDeckComment : TrackedEntity
{
    public long Id { get; set; }
    public long DeckId { get; set; }
    public long UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public long? ParentCommentId { get; set; }

    public RiftboundDeck? Deck { get; set; }
    public RiftboundDeckComment? ParentComment { get; set; }
    public List<RiftboundDeckComment> Replies { get; set; } = [];
    public User? User { get; set; }
}
