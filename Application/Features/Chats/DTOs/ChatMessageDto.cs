namespace Application.Features.Chats.DTOs;

public record ChatMessageDto(
    long Id,
    long ChatRoomId,
    long UserId,
    string Content,
    string? AttachmentUrl,
    Domain.Enums.AttachmentType? AttachmentType,
    long? ReplyToMessageId,
    DateTimeOffset CreatedOn
);
