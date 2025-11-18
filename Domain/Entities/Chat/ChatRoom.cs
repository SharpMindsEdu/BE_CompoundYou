namespace Domain.Entities.Chat;

public class ChatRoom : TrackedEntity
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public bool IsPublic { get; set; }
    public bool IsDirect { get; set; }

    public List<ChatRoomUser> Users { get; set; } = [];
    public List<ChatMessage> Messages { get; set; } = [];
}
