namespace Domain.Entities;

public class UserBlock
{
    public long UserId { get; set; }
    public long BlockedUserId { get; set; }

    public User User { get; set; } = null!;
    public User BlockedUser { get; set; } = null!;
}
