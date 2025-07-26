namespace Application.Features.Chats.DTOs;

public record ChatMessageDto(long Id, long ChatRoomId, long UserId, string Content, DateTimeOffset CreatedOn);
