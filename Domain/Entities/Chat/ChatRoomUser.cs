namespace Domain.Entities.Chat;

public class ChatRoomUser
{
    public long ChatRoomId { get; set; }
    public long UserId { get; set; }
    public bool IsAdmin { get; set; }

    public ChatRoom ChatRoom { get; set; } = null!;
    public User User { get; set; } = null!;
}
