namespace Application.Features.Chats.DTOs;

public record ChatRoomDto(long Id, string Name, bool IsPublic, bool IsDirect);
