namespace Domain.Entities;

public class TrackedEntity
{
    public DateTimeOffset CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTimeOffset UpdatedOn { get; set; }= DateTime.UtcNow;
    public DateTimeOffset DeletedOn { get; set; }= DateTime.UtcNow;
}
