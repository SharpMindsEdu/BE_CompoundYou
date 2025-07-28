using Domain.Enums;

namespace Domain.Entities;

public class ChatMessage : TrackedEntity
{
    public long Id { get; set; }
    public long ChatRoomId { get; set; }
    public long UserId { get; set; }
    public required string Content { get; set; }
    public string? AttachmentUrl { get; set; }
    public AttachmentType? AttachmentType { get; set; }
    public long? ReplyToMessageId { get; set; }

    public ChatMessage? ReplyToMessage { get; set; }
    public List<ChatMessage> Replies { get; set; } = [];

    public ChatRoom ChatRoom { get; set; } = null!;
    public User User { get; set; } = null!;
}
